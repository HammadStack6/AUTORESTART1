

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

public class BackupWorker : BackgroundService
{
    private readonly ILogger<BackupWorker> _logger;
    private readonly IConfiguration _config;

    public BackupWorker(
        ILogger<BackupWorker> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

  
   
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BACKUP WORKER STARTED");

        string sourceFile = _config["BackupSettings:SourceFile"]!;
        string backupFolder = _config["BackupSettings:BackupFolder"]!;

        _logger.LogInformation("Source File: {SourceFile}", sourceFile);
        _logger.LogInformation("Backup Folder: {BackupFolder}", backupFolder);

        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    try
        //    {
        //        DateTime now = DateTime.Now;

        //        // Backup window: 8:00 AM to 9:00 PM
        //        TimeSpan startTime = new TimeSpan(8, 0, 0);
        //        TimeSpan endTime = new TimeSpan(21, 0, 0);

        //        DateTime nextRun;

        //        // If current time is before 8 AM
        //        if (now.TimeOfDay < startTime)
        //        {
        //            nextRun = now.Date.Add(startTime);
        //        }
        //        // If current time is between 8 AM and 9 PM
        //        else if (now.TimeOfDay <= endTime)
        //        {
        //            // Run at the next full hour
        //            nextRun = now.Date
        //                .AddHours(now.Hour + 1);

        //            // If it's exactly 8:00, next run becomes 9:00.
        //            // If service starts at 8:30, next run becomes 9:00.
        //            // If service starts at 20:30, next run becomes 21:00.
        //        }
        //        // After 9 PM → wait until 8 AM tomorrow
        //        else
        //        {
        //            nextRun = now.Date.AddDays(1).Add(startTime);
        //        }

        //        TimeSpan waitTime = nextRun - now;

        //        _logger.LogInformation(
        //            "Current time: {Now}",
        //            now);

        //        _logger.LogInformation(
        //            "Next backup scheduled at: {NextRun}",
        //            nextRun);

        //        _logger.LogInformation(
        //            "Waiting for: {WaitTime}",
        //            waitTime);

        //        await Task.Delay(waitTime, stoppingToken);

        //        if (stoppingToken.IsCancellationRequested)
        //            break;

        //        _logger.LogInformation("Starting scheduled backup...");

        //        RunBackup(sourceFile, backupFolder);

        //        _logger.LogInformation("Scheduled backup completed.");
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        break;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Scheduled backup failed.");

        //        await Task.Delay(
        //            TimeSpan.FromMinutes(5),
        //            stoppingToken);
        //    }
        //}

   
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTime now = DateTime.Now;

                // FOR TESTING: Run backup every 5 minutes
                DateTime nextRun = now.AddMinutes(5);

                TimeSpan waitTime = nextRun - now;

                _logger.LogInformation(
                    "Current time: {Now}",
                    now);

                _logger.LogInformation(
                    "TEST MODE - Next backup scheduled at: {NextRun}",
                    nextRun);

                _logger.LogInformation(
                    "Waiting for: {WaitTime}",
                    waitTime);

                await Task.Delay(waitTime, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("Starting scheduled backup...");

                RunBackup(sourceFile, backupFolder);

                _logger.LogInformation("Scheduled backup completed.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed.");

                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);
            }
        }


        _logger.LogInformation("BACKUP WORKER STOPPED");
    }



    private void RunBackup(string sourceFile, string backupFolder)
    {
        try
        {
            _logger.LogInformation(" RunBackup STARTED ");

            _logger.LogInformation("Source File: {SourceFile}", sourceFile);
            _logger.LogInformation("Backup Folder: {BackupFolder}", backupFolder);

            // CREATE BACKUP DIRECTORY
            _logger.LogInformation("Creating backup directory if not exists...");

            Directory.CreateDirectory(backupFolder);

            _logger.LogInformation("Backup directory ready.");

            // CHECK SOURCE FILE
            _logger.LogInformation("Checking source file exists...");

            bool sourceExists = File.Exists(sourceFile);

            _logger.LogInformation("Source file exists: {Exists}", sourceExists);

            if (!sourceExists)
            {
                _logger.LogError("Source file NOT found.");
                throw new Exception("Source file does not exist.");
            }

            // GET DRIVE
            string drive = Path.GetPathRoot(sourceFile)!;

            _logger.LogInformation("Detected drive: {Drive}", drive);

            // CREATE SHADOW COPY
            _logger.LogInformation("Creating shadow copy...");

            RunPowerShell(
                $"wmic shadowcopy call create Volume='{drive}'");

            _logger.LogInformation("Shadow copy created.");

            // GET SHADOW PATH
            _logger.LogInformation("Getting shadow copy path...");

            string shadowPath = RunPowerShellWithOutput(
                "wmic shadowcopy get DeviceObject /value")
                .Trim();

            _logger.LogInformation("Raw shadow output:");
            _logger.LogInformation(shadowPath);

            // SPLIT OUTPUT
            string[] lines = shadowPath.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries);

            _logger.LogInformation("Shadow output lines count: {Count}", lines.Length);

            // GET LATEST SHADOW
            string latestShadow = lines
                .Where(x => x.Contains("GLOBALROOT"))
                .LastOrDefault()?
                .Replace("DeviceObject=", "")
                .Trim() ?? "";

            _logger.LogInformation("Latest shadow path: {LatestShadow}", latestShadow);

            if (string.IsNullOrWhiteSpace(latestShadow))
            {
                _logger.LogError("Shadow copy path extraction failed.");
                throw new Exception("Shadow copy path not found.");
            }

            // RELATIVE PATH
            string relativePath = Path.GetRelativePath(drive, sourceFile);

            _logger.LogInformation("Relative path: {RelativePath}", relativePath);

            // SHADOW FILE
            string shadowFile = Path.Combine(
                latestShadow + "\\",
                relativePath);

            _logger.LogInformation("Shadow file path: {ShadowFile}", shadowFile);

            // CHECK SHADOW FILE
            bool shadowExists = File.Exists(shadowFile);

            _logger.LogInformation("Shadow file exists: {Exists}", shadowExists);

            if (!shadowExists)
            {
                _logger.LogError("Shadow file NOT found.");
                throw new Exception("Shadow file does not exist.");
            }

            // DELETE OLD BACKUPS
            _logger.LogInformation("Searching old backup files...");

            //var oldFiles = Directory.GetFiles(backupFolder, "*.mdb");

            //_logger.LogInformation("Old backup count: {Count}", oldFiles.Length);

            //foreach (var file in oldFiles)
            //{
            //    _logger.LogInformation("Deleting old backup file: {File}", file);

            //    File.Delete(file);
            //}

            //_logger.LogInformation("Old backups deleted.");

            //// CREATE NEW BACKUP FILE
            //string backupFile = Path.Combine(
            //    backupFolder,
            //    $"data_{DateTime.Now:yyyyMMdd_HHmmss}.mdb");

            // ORIGINAL FILE = att2000.mdb
            // COPY FILE = att2026.mdb

            string backupFile = Path.Combine(
                backupFolder,
                "att2026.mdb");

            _logger.LogInformation("Backup file path: {BackupFile}", backupFile);

            // CHECK IF OLD att2026 EXISTS
            if (File.Exists(backupFile))
            {
                _logger.LogInformation("Old att2026.mdb found. Deleting...");

                File.Delete(backupFile);

                _logger.LogInformation("Old att2026.mdb deleted.");
            }
            else
            {
                _logger.LogInformation("No old att2026.mdb found.");
            }

            _logger.LogInformation("New backup file path: {BackupFile}", backupFile);

            // COPY FILE
            _logger.LogInformation("Starting file copy...");
            _logger.LogWarning(" BEFORE FILE COPY");
_logger.LogWarning("Shadow File: {shadowFile}", shadowFile);
_logger.LogWarning("Backup File: {backupFile}", backupFile);

_logger.LogWarning("Checking if shadow file can be opened...");

try
{
    using FileStream fs = File.Open(
        shadowFile,
        FileMode.Open,
        FileAccess.Read,
        FileShare.None);

    _logger.LogWarning("Shadow file OPEN SUCCESS (not locked)");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Shadow file OPEN FAILED (locked OR blocked by Defender)");
}
            //File.Copy(shadowFile, backupFile, true);

            try
            {
                _logger.LogWarning("STARTING FILE COPY NOW...");

                File.Copy(shadowFile, backupFile, true);
                File.SetLastWriteTime(backupFile, DateTime.Now);

                _logger.LogWarning("FILE COPY SUCCESS");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FILE COPY FAILED (Defender or permission issue likely)");
                throw;
            }

            // CHECK IF MDB FILE IS LOCKED

            string ldbFile = Path.ChangeExtension(sourceFile, ".ldb");

            _logger.LogInformation("Checking for LDB lock file...");
            _logger.LogInformation("LDB path: {LdbFile}", ldbFile);

            bool ldbExists = File.Exists(ldbFile);

            _logger.LogInformation("LDB file exists: {Exists}", ldbExists);

            if (ldbExists)
            {
                _logger.LogWarning("Database appears to be OPEN because .ldb file exists.");
            }
            else
            {
                _logger.LogInformation("No .ldb file found.");
            }

            // TEST DIRECT FILE ACCESS
            try
            {
                _logger.LogInformation("Testing direct read access to source MDB file...");

                using FileStream fs = File.Open(
                    sourceFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                _logger.LogInformation("Direct MDB read access SUCCESS.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct MDB read access FAILED.");
            }

            // TEST SHADOW FILE ACCESS
            try
            {
                _logger.LogInformation("Testing shadow file access...");

                using FileStream fs = File.Open(
                    shadowFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                _logger.LogInformation("Shadow MDB read access SUCCESS.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shadow MDB read access FAILED.");
            }

            _logger.LogInformation("File copy completed.");
            _logger.LogWarning("Verifying backup file exists...");
            _logger.LogWarning("Backup path: {backupFile}", backupFile);
            // VERIFY COPY
            bool backupExists = File.Exists(backupFile);

            _logger.LogInformation("Backup exists after copy: {Exists}", backupExists);

            if (!backupExists)
            {
                _logger.LogError("Backup file NOT created.");
                throw new Exception("Backup file copy failed.");
            }

            FileInfo info = new FileInfo(backupFile);

            _logger.LogInformation("Backup file size: {Size} bytes", info.Length);

            // DELETE SHADOW COPIES
            _logger.LogInformation("Deleting shadow copies...");

            RunPowerShell(
                "wmic shadowcopy delete /nointeractive");

            _logger.LogInformation("Shadow copies deleted.");

            _logger.LogInformation("BACKUP SUCCESS ");
            _logger.LogInformation("Backup created successfully: {BackupFile}", backupFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BACKUP FAILED");
            throw;
        }
    }

    private void RunPowerShell(string command)
    {
        try
        {
            _logger.LogInformation("Executing PowerShell command:");
            _logger.LogInformation(command);

            using Process process = new Process();

            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $"-Command \"{command}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            _logger.LogInformation("PowerShell Exit Code: {ExitCode}", process.ExitCode);

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("PowerShell Output:");
                _logger.LogInformation(output);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogError("PowerShell Error:");
                _logger.LogError(error);

                throw new Exception(error);
            }

            _logger.LogInformation("PowerShell command completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerShell execution failed.");
            throw;
        }
    }

    private string RunPowerShellWithOutput(string command)
    {
        try
        {
            _logger.LogInformation("Executing PowerShell command with output:");
            _logger.LogInformation(command);

            using Process process = new Process();

            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $"-Command \"{command}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            _logger.LogInformation("PowerShell Exit Code: {ExitCode}", process.ExitCode);

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("PowerShell Output:");
                _logger.LogInformation(output);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogError("PowerShell Error:");
                _logger.LogError(error);

                throw new Exception(error);
            }

            _logger.LogInformation("PowerShell command completed successfully.");

            return output; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerShell command failed: {Command}", command);
            throw;
        }
    }
}
