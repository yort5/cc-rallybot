using System;
using System.Collections.Generic;

namespace ccrallybot.Properties
{
    public interface IAppSettings
    {

        public string GetDiscordToken();
        public string GetGoogleToken();

        public string GetCrimeSheetId();

        public string GetColumnSpan();
        public string GetBotName();
        public string GetBotHelpString();

        public string GetHackExceptionUser();

    }
}
