// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
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
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseNodeDevServer(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<NodeDevServerMiddleware>();

        return app;
    }
}
