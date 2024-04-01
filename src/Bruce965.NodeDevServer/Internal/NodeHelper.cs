// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Bruce965.NodeDevServer.Internal;

static class NodeHelper
{
    public static async Task<bool> TryInstallAsync(
        ILogger logger,
        string nodeProjectDirectory,
        IEnumerable<string> packageManagers,
        CancellationToken gracefulCancellationToken,
        CancellationToken forcefulCancellationToken)
    {
        foreach (string packageManager in packageManagers)
        {
            Task<bool> task = packageManager.ToUpperInvariant() switch
            {
                "NPM" => TryNpmInstallAsync(logger, nodeProjectDirectory, gracefulCancellationToken, forcefulCancellationToken),
                "YARN" => TryYarnInstallAsync(logger, nodeProjectDirectory, gracefulCancellationToken, forcefulCancellationToken),
                _ => throw new ArgumentException($"Unknown or unsupported package manager: {packageManager}", nameof(packageManagers))
            };

            if (await task)
                return true;
        }

        return false;
    }

    public static Task<int> RunScriptAsync(
        ILogger logger,
        string nodeProjectDirectory,
        string script,
        IEnumerable<string> args,
        CancellationToken gracefulCancellationToken,
        CancellationToken forcefulCancellationToken)
    {
        return ProcessHelper.RunAsync(nodeProjectDirectory, "npm", ["run", script, ..args], logger, gracefulCancellationToken, forcefulCancellationToken);
    }

    static async Task<bool> TryNpmInstallAsync(ILogger logger, string nodeProjectDirectory, CancellationToken gracefulCancellationToken, CancellationToken forcefulCancellationToken)
    {
        int exitCode = await ProcessHelper.RunAsync(nodeProjectDirectory, "npm", ["install", "--package-lock-only", "--no-audit"], logger, gracefulCancellationToken, forcefulCancellationToken);

        return exitCode == 0;
    }

    static async Task<bool> TryYarnInstallAsync(ILogger logger, string nodeProjectDirectory, CancellationToken gracefulCancellationToken, CancellationToken forcefulCancellationToken)
    {
        try
        {
            int exitCode = await ProcessHelper.RunAsync(nodeProjectDirectory, "yarn", ["install", "--frozen-lockfile", "--no-progress", "--non-interactive"], logger, gracefulCancellationToken, forcefulCancellationToken);

            return exitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 /* "ERROR_FILE_NOT_FOUND" */)
        {
            return false;
        }
    }
}
