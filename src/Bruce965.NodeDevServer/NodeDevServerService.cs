// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Hosting;

namespace Bruce965.NodeDevServer.Services;

class NodeDevServerService(NodeDevServerManager manager) : IHostedService
{
    readonly NodeDevServerManager _manager = manager;

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask; // Do not start the server immediately, it will be started with the first request.

    public Task StopAsync(CancellationToken cancellationToken) =>
        _manager.StopNodeDevServerAsync(cancellationToken);
}
