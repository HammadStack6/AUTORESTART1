using AUTORESTART1.Backup;
using AUTORESTART1.Configuration;
using AUTORESTART1.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        services
            .AddOptions<BackupOptions>()
            .Bind(context.Configuration.GetSection(BackupOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<BackupOptions>, BackupOptionsValidator>();
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<BackupOptions>>().Value);
        services.AddSingleton<HourlyBackupSchedule>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IBackupDelay, SystemBackupDelay>();
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IShadowCopyService, CimShadowCopyService>();
        services.AddSingleton<IBackupFileCopier, AtomicBackupFileCopier>();
        services.AddSingleton<IBackupExecutor, BackupExecutor>();
        services.AddHostedService<BackupWorker>();
    })
    .Build()
    .Run();
