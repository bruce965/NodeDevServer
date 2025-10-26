// SPDX-FileCopyrightText: 2024-2025 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using Bruce965.NodeDevServer;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to manage a Node development server from an HTTP application pipeline.
/// </summary>
public static class NodeDevServerAppBuilderExtensions
{
    /// <summary>
    /// Adds a middleware to the request pipeline, forwarding all incoming requests to the Node development server.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="trap"><see langword="true"/> to proxy all requests; <see langword="false"/> to only proxy requests
    /// that would otherwise remain unhandled.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseNodeDevServer(
        this IApplicationBuilder app,
        bool trap = false
    )
    {
        ArgumentNullException.ThrowIfNull(app);

        if (trap)
            app.UseMiddleware<NodeDevServerTrapMiddleware>();
        else
            app.UseMiddleware<NodeDevServerMiddleware>();

        return app;
    }

    #region Legacy

    /// <inheritdoc cref="UseNodeDevServer(IApplicationBuilder, bool)"/>
    public static IApplicationBuilder UseNodeDevServer(this IApplicationBuilder app) =>
        UseNodeDevServer(app, false);

    #endregion
}
