using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ccrallybot.Properties
{
    public class AppSettings : IAppSettings
    {
        private const string _BotName = "Rally $CRIME bot";

        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly string _DiscordToken;
        private readonly string _GoogleToken;

        private readonly string _CrimeSheetId;

        public AppSettings(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<AppSettings>(); ;
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _DiscordToken = _config["CCDiscordToken"];
            _GoogleToken = _config["GoogleCredentials"];

            _CrimeSheetId = _config["CrimeSheetId"];
        }

        public string GetDiscordToken()
        {
            return _DiscordToken;
        }
        public string GetGoogleToken()
        {
            return _GoogleToken;
        }

        public string GetBotHelpString()
        {
            StringBuilder helpString = new StringBuilder();
            var nl = Environment.NewLine;
            helpString.Append($"{_BotName} currently supports the following commands:");
            return helpString.ToString();
        }

        public string GetBotName()
        {
            return _BotName;
        }

        public string GetColumnSpan()
        {
            return "A:E";
        }

        public string GetCrimeSheetId()
        {
            return _CrimeSheetId;
        }

        public string GetHackExceptionUser()
        {
            return _config["OneOffTODiscordID"];
        }
    }
}
