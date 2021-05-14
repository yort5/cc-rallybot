using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ccrallybot.Properties;
using ccrallybot.Services;
using Microsoft.Extensions.Hosting;

namespace ccrallybot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureAppConfiguration((context, config) =>
                {
                    var settings = config.Build();
                    config.AddAzureAppConfiguration(settings["AzureAppConfigurationEndpoint"]);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IAppSettings, AppSettings>();
                    services.AddSingleton<GoogleSheetService>();
                    services.AddHttpClient();
                    services.AddHttpClient(AppConstants.RallyClient, client =>
                    {
                        client.BaseAddress = new Uri(@"https://api.rally.io/v1/creator_coins/");
                    });
                    //services.AddHostedService<DiscordWorker>();
                    services.AddHostedService<RallyTrackingWorker>();
                    //services.AddHostedService<TwitchBot>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                // Only required if the service responds to requests.
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
