using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<BackupWorker>();
    })
    .Build()
    .Run();