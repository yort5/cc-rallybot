using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Google.Apis.Sheets.v4.Data;
using System.Text;
using System.IO;
using System.Threading;
using Google.Apis.Util.Store;
using System.Linq;
using System.Globalization;
using ccrallybot.Properties;
using ccrallybot.Models;
using System.Threading.Tasks;

namespace ccrallybot.Services
{
    public class GoogleSheetService
    {
        private readonly ILogger _logger;
        private readonly IAppSettings _settings;
        private readonly SheetsService _sheetService;

        private const string _LastCheckedString = "Last Checked";
        private const string _LastRecordedString = "Last Recorded";

        public GoogleSheetService(ILoggerFactory loggerFactory, IAppSettings appSettings)
        {
            _logger = loggerFactory.CreateLogger<GoogleSheetService>(); ;
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

            _sheetService = AuthorizeGoogleSheets();
        }

        private SheetsService AuthorizeGoogleSheets()
        {
            try
            {
                string[] Scopes = { SheetsService.Scope.Spreadsheets };
                string ApplicationName = _settings.GetBotName();
                string googleCredentialJson = _settings.GetGoogleToken();

                GoogleCredential credential;
                credential = GoogleCredential.FromJson(googleCredentialJson).CreateScoped(Scopes);
                //Reading Credentials File...
                //using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
                //{
                //    credential = GoogleCredential.FromStream(stream)
                //        .CreateScoped(Scopes);
                //}


                var service = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                return service;
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return null;
            }
        }

        internal async Task<MasterInfo> LoadMasterInfo()
        {
            MasterInfo masterInfo = new MasterInfo();
            try
            {
                //var sheetsService = AuthorizeGoogleSheets();
                // Define request parameters.
                var range = $"Master!A:D";

                var sheetRequest = _sheetService.Spreadsheets.Values.Get(_settings.GetCrimeSheetId(), range);
                var sheetResponse = await sheetRequest.ExecuteAsync();
                var values = sheetResponse.Values;

                if(values.Count() >= 1)
                {
                    var record = values[0];
                    // grab the last update from the header
                    if (record.Count >= 2 && record[1] != null)
                    {
                        DateTime.TryParse(record[1].ToString(), out masterInfo.LastChecked);
                    }
                    // grab the last recorded from the header
                    if (record.Count >= 4 && record[3] != null)
                    {
                        DateTime.TryParse(record[3].ToString(), out masterInfo.LastRecorded);
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                _logger.LogError($"Exception loading LoadMasterInfo: {exc.Message}");
            }
            return masterInfo;
        }

        public async Task<bool> UpdateMasterInfo(MasterInfo masterInfo)
        {
            try
            {
                var sheetsService = AuthorizeGoogleSheets();
                // Define request parameters.
                var range = $"Master!A1:D1";

                // first load existing data
                var checkExistingRequest = sheetsService.Spreadsheets.Values.Get(_settings.GetCrimeSheetId(), range);
                var existingRecords = await checkExistingRequest.ExecuteAsync();

                if(existingRecords.Values.Count() >= 1)
                {
                    var record = existingRecords.Values[0];

                }

                var oblist = new List<object>()
                    { _LastCheckedString, masterInfo.LastChecked, _LastRecordedString, masterInfo.LastRecorded };
                var valueRange = new ValueRange();
                valueRange.Values = new List<IList<object>> { oblist };

                // Performing Update Operation...
                var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, _settings.GetCrimeSheetId(), range);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                var appendReponse = await updateRequest.ExecuteAsync();
                return true;
                
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return false;
            }
        }

        internal void RecordCoinPrice(CreatorCoinPrice coinPrice)
        {
            try
            {
                //var sheetsService = AuthorizeGoogleSheets();
                // Define request parameters.
                var range = $"{coinPrice.symbol.ToUpper()}!A:D";

                var loadExistingRequest = _sheetService.Spreadsheets.Values.Get(_settings.GetCrimeSheetId(), range);

                var oblist = new List<object>()
                    { coinPrice.priceInUSD, coinPrice.priceInRLY, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) };
                var valueRange = new ValueRange();
                valueRange.Values = new List<IList<object>> { oblist };

                // Append the above record...
                var appendRequest = _sheetService.Spreadsheets.Values.Append(valueRange, _settings.GetCrimeSheetId(), range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                var appendReponse = appendRequest.Execute();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                _logger.LogError($"Exceptin saving YouTube info: {exc.Message}");
            }
        }
    }
}
