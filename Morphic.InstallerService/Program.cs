using Morphic.InstallerService;
using IoDCLI;
using JKang.IpcServiceFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Morphic.InstallerService.Contracts;
using Microsoft.AspNetCore.Hosting;

//sc create "Moprhic Installer Service" binPath="C:\Users\codan\Downloads\IoDCLI\InstallerService\bin\Debug\net5.0-windows10.0.17763\InstallerService.exe C:\Users\codan\Downloads\IoDCLI\InstallerService\bin\Debug\net5.0-windows10.0.17763"

WindowsIdentityHelper.RegDisablePredefinedCacheEx();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddScoped<IInstallerService, InstallerIpcService>();
        services.AddLogging(configure => configure.AddConsole());
        services.AddLogging(configure => configure.AddEventLog());
        services.AddTransient<PackageManagerService>();
        services.AddGrpc();
    })
    .ConfigureIpcHost(builder =>
    {
        builder.AddNamedPipeEndpoint<IInstallerService>("moprhicinstaller");
    })
    .ConfigureLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
    })
    .UseWindowsService()
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>();
    })
    .Build();

await host.RunAsync();
