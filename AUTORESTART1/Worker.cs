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
        string sourceFile = _config["BackupSettings:SourceFile"]!;
        string backupFolder = _config["BackupSettings:BackupFolder"]!;
        string runTime = _config["BackupSettings:RunTime"]!;

        // FIRST RUN IMMEDIATELY
        try
        {
            _logger.LogInformation("Running first backup immediately...");

            RunBackup(sourceFile, backupFolder);

            _logger.LogInformation("First backup completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "First backup failed.");
        }

        // THEN RUN DAILY
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TimeSpan target = TimeSpan.Parse(runTime);
                DateTime now = DateTime.Now;

                DateTime nextRun = now.Date.Add(target);

                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                TimeSpan wait = nextRun - now;

                _logger.LogInformation($"Next backup at {nextRun}");

                await Task.Delay(wait, stoppingToken);

                RunBackup(sourceFile, backupFolder);

                _logger.LogInformation("Scheduled backup completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }





    private void RunBackup(string sourceFile, string backupFolder)
    {
        try
        {
            Directory.CreateDirectory(backupFolder);

            string drive = Path.GetPathRoot(sourceFile)!;

            // CREATE SHADOW COPY
            RunPowerShell(
                $"wmic shadowcopy call create Volume='{drive}'");


            //string drive = Path.GetPathRoot(sourceFile)!;
            //drive = drive.Replace("\\", "");

            //RunPowerShell(
            //    $"wmic shadowcopy call create Volume='{drive}\\\\'");

            // GET LATEST SHADOW COPY PATH
            string shadowPath = RunPowerShellWithOutput(
                "wmic shadowcopy get DeviceObject /value")
                .Trim();

            // EXTRACT LAST DEVICE OBJECT
            string[] lines = shadowPath.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries);

            string latestShadow = lines
                .Where(x => x.Contains("GLOBALROOT"))
                .Last()
                .Replace("DeviceObject=", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(latestShadow))
                throw new Exception("Shadow copy path not found.");

            string relativePath = Path.GetRelativePath(drive, sourceFile);

            string shadowFile = Path.Combine(
                latestShadow + "\\",
                relativePath);

            // DELETE OLD BACKUPS
            var oldFiles = Directory.GetFiles(backupFolder, "*.mdb");

            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }

            string backupFile = Path.Combine(
                backupFolder,
                $"data_{DateTime.Now:yyyyMMdd_HHmmss}.mdb");

            File.Copy(shadowFile, backupFile, true);

            // DELETE SHADOW COPIES
            RunPowerShell(
                "wmic shadowcopy delete /nointeractive");

            _logger.LogInformation($"Backup created: {backupFile}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup process failed.");
            throw;
        }
    }


    private void RunPowerShell(string command)
    {
        try
        {


            using Process process = new Process();

            process.StartInfo.FileName = "powershell.exe";
            //process.StartInfo.Arguments = command;
            process.StartInfo.Arguments = $"-Command \"{command}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            process.WaitForExit();

            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception(error);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }




    private string RunPowerShellWithOutput(string command)
    {
        try
        {


            using Process process = new Process();

            process.StartInfo.FileName = "powershell.exe";
            //process.StartInfo.Arguments = command;
            process.StartInfo.Arguments = $"-Command \"{command}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception(error);

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerShell command failed: {Command}", command);
            throw;
        }
    }


}