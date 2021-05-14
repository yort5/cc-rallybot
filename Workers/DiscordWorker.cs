using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ccrallybot.Properties;
using Discord;
using Discord.Commands;
using Discord.Net.Rest;
using Discord.Rest;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;


namespace ccrallybot.Services
{
    public class DiscordWorker : BackgroundService
    {
        private readonly ILogger<DiscordWorker> _logger;
        private readonly IAppSettings _settings;
        private readonly IHostEnvironment _environment;

        private readonly GoogleSheetService _sheetService;
        private DiscordSocketClient _client;

        public DiscordWorker(ILogger<DiscordWorker> logger,
                            IAppSettings appSettings,
                            IHostEnvironment environment,
                            GoogleSheetService gSheetService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _sheetService = gSheetService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DiscordBot Service is starting.");

            stoppingToken.Register(() => _logger.LogInformation("DiscordBot Service is stopping."));

            try
            {
                _client = new DiscordSocketClient();

                _client.Log += Log;

                //Initialize command handling.
                _client.MessageReceived += DiscordMessageReceived;
                //await InstallCommands();      

                // Connect the bot to Discord
                string token = _settings.GetDiscordToken();
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();

                // Block this task until the program is closed.
                //await Task.Delay(-1, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("DiscordBot is doing background work.");

                    //LoadCurrentEvents();
                    //CheckYouTube();

                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }
            catch (Exception exc)
            {
                _logger.LogError(exc.Message);
            }

            _logger.LogInformation("DiscordService has stopped.");
        }

        private async Task DiscordMessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            if (!(message.Content.StartsWith(".") || message.Content.StartsWith("!"))) return;

            var dmchannelID = await message.Author.GetOrCreateDMChannelAsync();
            bool isDM = (message.Channel.Id == dmchannelID.Id);

            if (message.Content == "!ping")
            {
                await message.Channel.SendMessageAsync("Pong!");
            }
        }


        private Task Log(LogMessage msg)
        {
            _logger.LogDebug(msg.ToString());
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
