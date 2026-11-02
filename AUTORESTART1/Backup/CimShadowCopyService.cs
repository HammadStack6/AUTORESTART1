using System.Text.RegularExpressions;
using AUTORESTART1.Configuration;

namespace AUTORESTART1.Backup;

public sealed partial class CimShadowCopyService : IShadowCopyService
{
    private readonly ICommandRunner _commandRunner;
    private readonly TimeSpan _timeout;
    private readonly ILogger<CimShadowCopyService> _logger;

    public CimShadowCopyService(
        ICommandRunner commandRunner,
        BackupOptions options,
        ILogger<CimShadowCopyService> logger)
    {
        _commandRunner = commandRunner;
        _timeout = TimeSpan.FromSeconds(options.CommandTimeoutSeconds);
        _logger = logger;
    }

    public async Task<ShadowCopy> CreateAsync(string sourceFile, CancellationToken cancellationToken)
    {
        string? root = Path.GetPathRoot(sourceFile);
        if (string.IsNullOrWhiteSpace(root) || !DriveRootPattern().IsMatch(root))
            throw new InvalidOperationException("The source database must be on a local drive for VSS backup.");

        string createScript =
            $"$result = Invoke-CimMethod -ClassName Win32_ShadowCopy -MethodName Create -Arguments @{{ Volume = '{root}' }}; " +
            "Write-Output ('ReturnValue=' + $result.ReturnValue); " +
            "Write-Output ('ShadowID=' + $result.ShadowID)";

        CommandResult creation = await RunPowerShellAsync(createScript, cancellationToken);
        Match returnValueMatch = ReturnValuePattern().Match(creation.StandardOutput);
        if (!returnValueMatch.Success || returnValueMatch.Groups[1].Value != "0")
            throw new InvalidOperationException("Windows failed to create a volume shadow copy.");

        Match idMatch = ShadowIdPattern().Match(creation.StandardOutput);
        if (!idMatch.Success)
            throw new InvalidOperationException("Windows did not return the created shadow-copy ID.");

        string shadowId = idMatch.Groups[1].Value;

        try
        {
            string lookupScript =
                $"$shadow = Get-CimInstance -ClassName Win32_ShadowCopy -Filter \"ID = '{shadowId}'\"; " +
                "if ($null -eq $shadow) { throw 'Created shadow copy was not found.' }; " +
                "Write-Output ('DeviceObject=' + $shadow.DeviceObject)";

            CommandResult lookup = await RunPowerShellAsync(lookupScript, cancellationToken);
            Match deviceMatch = DeviceObjectPattern().Match(lookup.StandardOutput);
            if (!deviceMatch.Success)
                throw new InvalidOperationException("Windows did not return the created shadow-copy device path.");

            return new ShadowCopy(shadowId, deviceMatch.Groups[1].Value.Trim());
        }
        catch
        {
            await TryDeleteAfterCreateFailureAsync(shadowId);
            throw;
        }
    }

    public Task DeleteAsync(ShadowCopy shadowCopy, CancellationToken cancellationToken) =>
        DeleteByIdAsync(shadowCopy.Id, cancellationToken);

    private async Task DeleteByIdAsync(string shadowId, CancellationToken cancellationToken)
    {
        string deleteScript =
            $"$shadow = Get-CimInstance -ClassName Win32_ShadowCopy -Filter \"ID = '{shadowId}'\"; " +
            "if ($null -ne $shadow) { $shadow | Remove-CimInstance }";

        await RunPowerShellAsync(deleteScript, cancellationToken);
    }

    private Task<CommandResult> RunPowerShellAsync(string script, CancellationToken cancellationToken) =>
        _commandRunner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", script],
            _timeout,
            cancellationToken);

    private async Task TryDeleteAfterCreateFailureAsync(string shadowId)
    {
        try
        {
            await DeleteByIdAsync(shadowId, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            _logger.LogError(cleanupException, "Failed to clean up shadow copy {ShadowId} after creation failed.", shadowId);
        }
    }

    [GeneratedRegex(@"^[A-Za-z]:\\$")]
    private static partial Regex DriveRootPattern();

    [GeneratedRegex(@"ReturnValue\s*=\s*(\d+)\s*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ReturnValuePattern();

    [GeneratedRegex("ShadowID\\s*=\\s*\"?(\\{[0-9A-Fa-f-]+\\})\"?\\s*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ShadowIdPattern();

    [GeneratedRegex(@"DeviceObject\s*=\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceObjectPattern();
}
