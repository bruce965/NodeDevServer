// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using Bruce965.NodeDevServer;
using Bruce965.NodeDevServer.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up a Node development server in an <see cref="IServiceCollection"/>.
/// </summary>
public static class NodeDevServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required to manage a Node development server.
    /// </summary>
    /// <param name="services">Collection of service descriptors.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddNodeDevServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The proxy middleware forwards incoming requests to the Node development server.
        services.AddHttpClient<NodeDevServerMiddleware>();
        services.TryAddSingleton<NodeDevServerMiddleware>();

        // The Node development server management service handles the lifetime of the Node development server's process.
        services.TryAddSingleton<NodeDevServerManager>();
        services.AddHostedService<NodeDevServerService>();

        return services;
    }

    /// <summary>
    /// Registers services required to manage a Node development server and configures <see cref="NodeDevServerOptions"/>.
    /// </summary>
    /// <param name="services">Collection of service descriptors.</param>
    /// <param name="setupAction">A delegate to configure <see cref="NodeDevServerOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddNodeDevServer(this IServiceCollection services, Action<NodeDevServerOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddNodeDevServer();
        services.Configure(setupAction);

        return services;
    }
}
