// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using Bruce965.NodeDevServer.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bruce965.NodeDevServer;

class NodeDevServerManager(IOptions<NodeDevServerOptions> options, ILogger<NodeDevServerManager> logger) : IDisposable
{
    readonly NodeDevServerOptions _options = options.Value;
    readonly ILogger<NodeDevServerManager> _logger = logger;

    readonly SemaphoreSlim _semaphore = new(1);
    readonly CancellationTokenSource _gracefulShutdown = new();
    readonly CancellationTokenSource _forcefulShutdown = new();

    readonly object _serverTaskGuard = new();
    Task? _serverTask;

    public bool IsRunning
    {
        get
        {
            lock (_serverTaskGuard)
                return _serverTask is not null && !_serverTask.IsCompleted;
        }
    }

    /// <summary>
    /// Start a Node development server if it's not running already.
    /// <para>
    /// This method returns after all dependencies have been installed,
    /// but before the server has actually started handling requests.
    /// </para>
    /// <para>
    /// If the Node development server crashes, invoking this method again will
    /// start a new instance.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Token to abort the start operation.</param>
    /// <returns>Task completed when the Node development server is about to start.</returns>
    public async Task StartNodeDevServerAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _gracefulShutdown.Token);

        // Avoid accidentally starting a server after stop is requested.
        cancel.Token.ThrowIfCancellationRequested();

        await _semaphore.WaitAsync(cancel.Token);
        try
        {
            // Is the Node development server active?
            if (!IsRunning)
            {
                // Install dependencies.
                await NodeHelper.TryInstallAsync(_logger, _options.Path, _options.PackageManagers, cancel.Token, _forcefulShutdown.Token);

                // Start Node development server.
                Task serverTask = NodeHelper.RunScriptAsync(_logger, _options.Path, _options.LaunchScript, [], _gracefulShutdown.Token, _forcefulShutdown.Token);

                lock (_serverTaskGuard)
                    _serverTask = serverTask;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Stop the Node development server if it's running.
    /// <para>
    /// Once stopped through this method, the server can no longer be restarted.</para>
    /// </summary>
    /// <param name="forceStopToken">Forcefully stop the Node development server as soon as possible, in an ungraceful way.</param>
    /// <returns>A task completed when the Node development server is stopped.</returns>
    public async Task StopNodeDevServerAsync(CancellationToken forceStopToken)
    {
        // Forward request to forcefully terminate the Node development server (if any).
        forceStopToken.Register(() => _forcefulShutdown.Cancel());

        // Request graceful termination of the Node development server (if any).
        await _gracefulShutdown.CancelAsync();

        Task? serverTask;

        // Wait for any in-progress operation (such as installing dependencies or starting Node development server).
        await _semaphore.WaitAsync(CancellationToken.None);
        try
        {
            lock (_serverTaskGuard)
                serverTask = _serverTask;
        }
        finally
        {
            _semaphore.Release();
        }

        // Wait for the Node development server's process to actually terminate.
        if (serverTask is not null)
            await serverTask.ContinueWith(static _ => {}, CancellationToken.None);
    }

    #region Disposing

    bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore.Dispose();
                _gracefulShutdown.Dispose();
                _forcefulShutdown.Dispose();
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
