// SPDX-FileCopyrightText: 2024 Fabio Iotti <info@fabioiotti.com>
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bruce965.NodeDevServer.Internal;

static class ProcessHelper
{
    public static async Task<int> RunAsync(
        string workingDirectory,
        string command,
        IEnumerable<string> args,
        ILogger logger,
        CancellationToken gracefulCancellationToken,
        CancellationToken forcefulCancellationToken)
    {
        logger.LogInformation("Starting process '{Command}' with arguments: {Arguments}.", command, args);

        ProcessStartInfo startInfo = new()
        {
            WorkingDirectory = workingDirectory,
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        using Process? proc = Process.Start(startInfo) ?? throw new InvalidOperationException("Process not started.");
        try
        {
            void ForwardData(object? sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data) && logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("{Data}", e.Data.Trim());
            }

            proc.OutputDataReceived += ForwardData;
            proc.ErrorDataReceived += ForwardData;

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            TaskCompletionSource procExited = new();
            proc.Exited += (_, _) => procExited.TrySetResult();
            proc.EnableRaisingEvents = true;

            TaskCompletionSource abortRequested = new();
            gracefulCancellationToken.Register(() => abortRequested.TrySetResult());
            forcefulCancellationToken.Register(() => abortRequested.TrySetResult());

            // Wait for abortion or process exit, whichever happens first.
            await Task.WhenAny(procExited.Task, abortRequested.Task);

            if (!proc.HasExited)
            {
                // This is quite similar to a CTRL + C form the terminal.
                proc.StandardInput.Close();

                // Give it a bit of time to finish up any already-running operations.
                await procExited.Task.WaitAsync(forcefulCancellationToken)
                    .ContinueWith(static _ => {});
            }
        }
        finally
        {
            // If the process is still running, kill it without mercy.
            if (!proc.HasExited)
            {
                logger.LogInformation("Forcefully terminating process...");

                proc.Kill(true);
            }
        }

        logger.LogInformation("Process exited with code: {ExitCode}", proc.ExitCode);

        return proc.ExitCode;
    }
}
