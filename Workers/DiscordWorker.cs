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
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using ccrallybot.Models;
using System.Text.Json;

namespace ccrallybot.Services
{
    public class DiscordWorker : BackgroundService
    {
        private readonly ILogger<DiscordWorker> _logger;
        private readonly IAppSettings _settings;
        private readonly IHostEnvironment _environment;
        private HttpClient _httpClient;
        private Random _random;

        private readonly GoogleSheetService _sheetService;
        private DiscordSocketClient _client;

        private string hellyeahpath = Path.Combine("Images", "hellyeah.jpg");

        public DiscordWorker(ILogger<DiscordWorker> logger,
                            IAppSettings appSettings,
                            IHostEnvironment environment,
                            IHttpClientFactory httpClientFactory,
                            GoogleSheetService gSheetService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _httpClient = httpClientFactory.CreateClient(AppConstants.DiscordHttpClient);
            _sheetService = gSheetService;

            _random = new Random();
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

                    try
                    {
                        var coinPrice = await GetCurrentPrice();
                        foreach (var guild in _client.Guilds)
                        {
                            var user = guild.GetUser(_client.CurrentUser.Id);
                            await user.ModifyAsync(userProps =>
                            {
                                userProps.Nickname = $"CRIME {coinPrice.priceInUSD:C2}:RLY {coinPrice.priceInRLY:N2}";
                            });
                        }
                    }
                    catch (Exception exc)
                    {
                        _logger.LogError($"Exception trying to update nickname: {exc.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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

            if( IsHellYeah(message) )
            {
                await message.Channel.SendFileAsync(hellyeahpath);
            }

            if (!(message.Content.StartsWith(".") || message.Content.StartsWith("!"))) return;

            var dmchannelID = await message.Author.GetOrCreateDMChannelAsync();
            bool isDM = (message.Channel.Id == dmchannelID.Id);

            if (message.Content.StartsWith("!price"))
            {
                var coinPrice = await GetCurrentPrice();
                await message.Channel.SendMessageAsync($"$CRIME is worth { coinPrice.priceInUSD:C2} (or { coinPrice.priceInRLY:N2} RLY if you prefer)");
            }
            else if(message.Content.StartsWith("!referral"))
            {
                await HandleReferralRequest(message);
            }
        }

        private async Task HandleReferralRequest(SocketMessage message)
        {
            var args = message.Content.Split(' ');
            if(args.Length >= 2)
            {
                var request = args[1];
                if (request.ToLower() == "add")
                {
                    string brand = string.Empty;
                    string code = string.Empty;
                    string link = string.Empty;
                    if (args.Length >= 3) brand = args[2].ToString();
                    if (args.Length >= 4) code = args[3].ToString();
                    if (args.Length >= 5) link = args[4].ToString();

                    ReferralInfo newReferral = new ReferralInfo()
                    {
                        ReferralDiscordName = message.Author.Username,
                        ReferralBrand = brand,
                        ReferralCode = code,
                        ReferralLink = link
                    };

                    var response = await _sheetService.AddReferral(newReferral);
                    await message.Channel.SendMessageAsync(response);
                    return;
                }
                else if (request.ToLower() == "get")
                {
                    if (args.Length >= 3)
                    {
                        var brandRequested = args[2].ToLower();
                        var referrals = await _sheetService.GetAllReferrals();
                        var brandReferrals = referrals.Where(r => r.ReferralBrand.ToLower() == brandRequested).ToList();

                        var index = _random.Next(0, brandReferrals.Count);
                        var winner = brandReferrals[index];

                        await message.Channel.SendMessageAsync($"Referral for {winner.ReferralBrand}:{Environment.NewLine}{winner.ReferralCode}");
                    }
                }
                else if (request.ToLower() == "list")
                {
                    var referrals = await _sheetService.GetAllReferrals();
                    var codeList = referrals.Select(code =>
                      new { ReferralBrand = code.ReferralBrand.ToLower() }).ToList().Distinct();
                    StringBuilder codeResponse = new StringBuilder();
                    codeResponse.AppendLine($"Here is the list of available referrals:");
                    foreach(var code in codeList) { codeResponse.AppendLine(code.ReferralBrand);  }
                    await message.Channel.SendMessageAsync(codeResponse.ToString());
                }
            }

        }

        private bool IsHellYeah(SocketMessage message)
        {
            // probably should change this to a string list compare
            if( message.Content.ToLower().Contains("hell yeah", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (message.Content.ToLower().Contains("hellz yeah", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (message.Content.ToLower().Contains("peephole", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private async Task<CreatorCoinPrice> GetCurrentPrice()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "crime/price");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<CreatorCoinPrice>(await response.Content.ReadAsStringAsync());
        }

        private Task Log(LogMessage msg)
        {
            _logger.LogDebug(msg.ToString());
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
