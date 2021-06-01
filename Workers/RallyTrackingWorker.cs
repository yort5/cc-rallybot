using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ccrallybot.Models;
using ccrallybot.Services;
using Discord;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ccrallybot.Properties
{
    public class RallyTrackingWorker : BackgroundService
    {
        private readonly ILogger<RallyTrackingWorker> _logger;
        private readonly IAppSettings _settings;
        private HttpClient _httpClient;
        private readonly GoogleSheetService _sheetService;
        private MasterInfo _masterInfo = new MasterInfo();

        public RallyTrackingWorker(ILogger<RallyTrackingWorker> logger,
                                    IAppSettings appSettings,
                                    IHttpClientFactory httpClientFactory,
                                    GoogleSheetService gSheetService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _sheetService = gSheetService;
            _httpClient = httpClientFactory.CreateClient(AppConstants.RallyHttpClient);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RallyTracking Service is starting.");

            stoppingToken.Register(() => _logger.LogInformation("RallyTracking Service is stopping."));

            try
            {
                _masterInfo = await _sheetService.LoadMasterInfo();
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("RallyMonitorService is doing background work.");

                    var coinList = new string[] { "CRIME", "WALLS" };
                    await CheckTrackedPrices(coinList);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (Exception exc)
            {
                _logger.LogError(exc.Message);
            }

            _logger.LogInformation("RallyMonitorService has stopped.");
        }

        private async Task<bool> CheckTrackedPrices(string[] coinList)
        {
            if (IsTimeToRecord())
            {
                foreach (var coin in coinList)
                {
                    var coinPrice = await CheckCoinPrice(coin);
                    _sheetService.RecordCoinPrice(coinPrice);
                }
                _masterInfo.LastRecorded = DateTime.UtcNow;
            }
            _masterInfo.LastChecked = DateTime.UtcNow;
            await _sheetService.UpdateMasterInfo(_masterInfo);
            return true;
        }

        private async Task<CreatorCoinPrice> CheckCoinPrice(string symbol)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{symbol}/price");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<CreatorCoinPrice>(await response.Content.ReadAsStringAsync());
        }


        private bool IsTimeToRecord()
        {
            DateTime now = DateTime.UtcNow;
            if (now.Hour == 0 || now.Hour == 12) // if it's in the noon or midnight hour
            {
                var timeSinceLastRecorded = now - _masterInfo.LastRecorded;
                // if the time since we last recorded is more than an hour, record!
                if (timeSinceLastRecorded.TotalHours > 1)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
