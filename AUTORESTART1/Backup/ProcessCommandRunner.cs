using System.Diagnostics;

namespace AUTORESTART1.Backup;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Terminate(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw new TimeoutException($"Command '{fileName}' exceeded its {timeout} timeout.");
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        string output = await outputTask;
        string error = await errorTask;
        CommandResult result = new(process.ExitCode, output, error);

        if (result.ExitCode != 0)
            throw new CommandExecutionException(fileName, result.ExitCode, result.StandardError);

        return result;
    }

    private static void Terminate(Process process)
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }
}
