namespace AUTORESTART1.Backup;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandExecutionException : Exception
{
    public CommandExecutionException(string fileName, int exitCode, string standardError)
        : base($"Command '{fileName}' failed with exit code {exitCode}. {standardError}".Trim())
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
