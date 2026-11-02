using Microsoft.Extensions.Options;

namespace AUTORESTART1.Configuration;

public sealed class BackupOptionsValidator : IValidateOptions<BackupOptions>
{
    public ValidateOptionsResult Validate(string? name, BackupOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.SourceFile))
            failures.Add("BackupSettings:SourceFile is required.");

        if (string.IsNullOrWhiteSpace(options.BackupFolder))
            failures.Add("BackupSettings:BackupFolder is required.");

        if (string.IsNullOrWhiteSpace(options.BackupFileName) ||
            !string.Equals(Path.GetFileName(options.BackupFileName), options.BackupFileName, StringComparison.Ordinal) ||
            options.BackupFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            failures.Add("BackupSettings:BackupFileName must be a file name without a directory path.");
        }

        if (options.StartTime < TimeSpan.Zero || options.StartTime >= TimeSpan.FromDays(1))
            failures.Add("BackupSettings:StartTime must be a time between 00:00:00 and 23:59:59.");

        if (options.EndTime < TimeSpan.Zero || options.EndTime >= TimeSpan.FromDays(1))
            failures.Add("BackupSettings:EndTime must be a time between 00:00:00 and 23:59:59.");

        if (options.StartTime > options.EndTime)
            failures.Add("BackupSettings:StartTime must not be later than EndTime.");

        if (options.IntervalMinutes <= 0 || options.IntervalMinutes > 24 * 60)
            failures.Add("BackupSettings:IntervalMinutes must be between 1 and 1440.");

        if (options.CommandTimeoutSeconds <= 0 || options.CommandTimeoutSeconds > 60 * 60)
            failures.Add("BackupSettings:CommandTimeoutSeconds must be between 1 and 3600.");

        if (!string.IsNullOrWhiteSpace(options.SourceFile) &&
            !string.IsNullOrWhiteSpace(options.BackupFolder) &&
            !string.IsNullOrWhiteSpace(options.BackupFileName))
        {
            try
            {
                string sourcePath = Path.GetFullPath(options.SourceFile);
                string destinationPath = Path.GetFullPath(Path.Combine(options.BackupFolder, options.BackupFileName));
                if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                    failures.Add("The backup destination must not overwrite the source database.");
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                failures.Add("BackupSettings contains an invalid source or destination path.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
