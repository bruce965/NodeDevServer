// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Bruce965.NodeDevServer;

class NodeDevServerMiddleware : IMiddleware, IDisposable
{
    readonly HttpClient _client;
    readonly NodeDevServerManager _server;

    readonly string _scheme;
    readonly string _webSocketScheme;
    readonly string _host;
    readonly int _port;

    readonly SemaphoreSlim _semaphore = new(1);

    public NodeDevServerMiddleware(
        IOptions<NodeDevServerOptions> options,
        HttpClient client,
        NodeDevServerManager server)
    {
        _client = client;
        _server = server;

        Uri hostUri = new(options.Value.HostUri, UriKind.Absolute);
        (_scheme, _host, _port) = (hostUri.Scheme, hostUri.Host, hostUri.Port);

        _webSocketScheme = string.Equals(_scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        bool accepted = await TryForwardToNodeDevServerAsync(context);

        // If failed to connect to a Node development server, and the local Node
        // development server has not been started or is no longer running...
        if (!accepted && !_server.IsRunning)
        {
            // Ensure only one operation at a time...
            await _semaphore.WaitAsync(context.RequestAborted);
            try
            {
                // Start a Node development server.
                await _server.StartNodeDevServerAsync(context.RequestAborted);

                // Wait for the Node development server to either start accepting
                // requests, or to die because it failed to start.
                while (!accepted && _server.IsRunning)
                {
                    accepted = await TryForwardToNodeDevServerAsync(context);

                    if (!accepted)
                        await Task.Delay(500, context.RequestAborted);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        if (!accepted)
            throw new InvalidOperationException("Unable to start Node development server.");
    }

    Uri BuildRequestUri(HttpRequest request, bool webSocket = false) =>
        new UriBuilder(webSocket ? _webSocketScheme : _scheme, _host, _port, request.Path, request.QueryString.ToUriComponent()).Uri;

    /// <summary>
    /// Forward the request to the Node development server.
    /// </summary>
    /// <param name="context">Request context.</param>
    /// <returns><see langword="true"/> if forwarded; <see langword="false"/> if the Node development server is not running (does not consume request).</returns>
    async Task<bool> TryForwardToNodeDevServerAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
            return await TryForwardWebSocketToNodeDevServerAsync(context);

        using HttpRequestMessage request = new()
        {
            Method = HttpMethod.Parse(context.Request.Method),
            RequestUri = BuildRequestUri(context.Request),
        };

        bool hasBody = request.Method != HttpMethod.Trace && (
            context.Request.Headers.ContainsKey(HeaderNames.ContentLength) ||
            context.Request.Headers.ContainsKey(HeaderNames.TransferEncoding)
        );

        if (hasBody)
            request.Content = new StreamContent(context.Request.Body);

        foreach ((string key, StringValues values) in context.Request.Headers)
        {
            bool contentHeaderAdded = false;

            if (request.Content is not null)
            {
                if (values.Count is 0)
                    contentHeaderAdded = request.Content.Headers.TryAddWithoutValidation(key, values.ToString());
                else
                    contentHeaderAdded = request.Content.Headers.TryAddWithoutValidation(key, values.ToArray());
            }
            
            if (!contentHeaderAdded)
            {
                if (values.Count is 0)
                    request.Headers.Add(key, values.ToString());
                else
                    request.Headers.Add(key, values.ToArray());
            }
        }

        HttpResponseMessage? response = null;
        try
        {
            try
            {
                response = await _client.SendAsync(request, context.RequestAborted);
            }
            catch (HttpRequestException ex) when (ex is {
                InnerException: SocketException {
                    SocketErrorCode: SocketError.ConnectionRefused,
                },
            })
            {
                return false;
            }

            context.Response.StatusCode = (int)response.StatusCode;

            foreach ((string key, IEnumerable<string> values) in response.Content.Headers)
            {
                context.Response.Headers.Append(key, values.ToArray());
            }

            foreach ((string key, IEnumerable<string> values) in response.Headers)
            {
                context.Response.Headers.Append(key, values.ToArray());
            }

            if ((int)response.StatusCode is not (100 or 101 or 204 or 205 or 304))
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        finally
        {
            response?.Dispose();
        }

        return true;
    }

    async Task<bool> TryForwardWebSocketToNodeDevServerAsync(HttpContext context)
    {
        Debug.Assert(context.WebSockets.IsWebSocketRequest);

        using ClientWebSocket socketWithDevServer = new();

        Uri uri = BuildRequestUri(context.Request, webSocket: true);
        
        if (context.Request.Headers.TryGetValue("Sec-WebSocket-Protocol", out StringValues protocols))
            foreach (string? protocol in protocols)
                if (protocol is not null)
                    socketWithDevServer.Options.AddSubProtocol(protocol);

        foreach ((string key, StringValues values) in context.Request.Headers)
        {
            if (key.ToUpperInvariant() is "UPGRADE" or "SEC-WEBSOCKET-KEY" or "SEC-WEBSOCKET-PROTOCOL" or "SEC-WEBSOCKET-VERSION")
                continue;

            socketWithDevServer.Options.SetRequestHeader(key, values);
        }

        try
        {
            await socketWithDevServer.ConnectAsync(uri, _client, context.RequestAborted).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex is {
            InnerException: HttpRequestException {
                InnerException: SocketException {
                    SocketErrorCode: SocketError.ConnectionRefused,
                },
            },
        })
        {
            return false;
        }

        using WebSocket socketWithClient = await context.WebSockets.AcceptWebSocketAsync();

        async Task ForwardMessages(WebSocket a, WebSocket b)
        {
            byte[] buff = ArrayPool<byte>.Shared.Rent(8 * 1024);
            try
            {
                while (
                    a.State is WebSocketState.Open or WebSocketState.CloseSent or WebSocketState.CloseReceived &&
                    b.State is WebSocketState.Open or WebSocketState.CloseSent or WebSocketState.CloseReceived)
                {
                    WebSocketReceiveResult incoming = await a.ReceiveAsync(new(buff), context.RequestAborted);

                    if (incoming.CloseStatus is not null)
                    {
                        await b.CloseAsync(incoming.CloseStatus.Value, incoming.CloseStatusDescription, context.RequestAborted);
                        break;
                    }

                    await b.SendAsync(new ArraySegment<byte>(buff, 0, incoming.Count), incoming.MessageType, incoming.EndOfMessage, context.RequestAborted);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buff);
            }
        }

        await Task.WhenAll(
            ForwardMessages(socketWithClient, socketWithDevServer),
            ForwardMessages(socketWithDevServer, socketWithClient)
        );
        
        return true;
    }

    #region Disposable

    bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
