// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

namespace Bruce965.NodeDevServer;

/// <summary>
/// Options to configure the Node development server.
/// </summary>
public class NodeDevServerOptions
{
    /// <summary>
    /// Local address of the Node development server.
    /// </summary>
    public string HostUri { get; set;} = "http://localhost:8080";

    /// <summary>
    /// Local path of the Node package.
    /// </summary>
    public string Path { get; set; } = "../frontend";

    /// <summary>
    /// Name of the launch script, as defined in the "package.json" file.
    /// </summary>
    public string LaunchScript { get; set; } = "start";

    /// <summary>
    /// Supported Node package managers.
    /// <para>
    /// Valid options: <c>npm</c>, <c>yarn</c>.
    /// </para>
    /// </summary>
    public List<string> PackageManagers { get; set; } = ["npm"];
}
