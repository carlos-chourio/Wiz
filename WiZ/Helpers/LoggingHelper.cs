using System;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WiZ.Helpers
{
    public static class LoggingHelper
    {
        public static void LogInput(this Microsoft.Extensions.Logging.ILogger logger, string text, IPAddress localip, IPAddress remoteip)
        {
            logger.FormatLog(text, true, localip, remoteip);
        }

        public static void LogOutput(this Microsoft.Extensions.Logging.ILogger logger, string text, IPAddress localip, IPAddress remoteip)
        {
            logger.FormatLog(text, false, localip, remoteip);
        }

        private static void FormatLog(this Microsoft.Extensions.Logging.ILogger logger, string text, bool input, IPAddress localip, IPAddress remoteip)
        {
            logger.FormatLog(text, input, localip?.ToString(), remoteip?.ToString());
        }

        private static void FormatLog(this Microsoft.Extensions.Logging.ILogger logger, string text, bool input, string localip, string remoteip)
        {
            var arrow = input ? "<=" : "=>";
            var timestamp = DateTime.Now.ToString("G");
            
            var networkInfo = $"LOCAL: {localip} {arrow} REMOTE: {remoteip}";
            var logMessage = text;

            if (input)
            {
                logger.LogInformation("[{Timestamp}] {NetworkInfo} - Received: {LogMessage}", 
                    timestamp, networkInfo, logMessage);
            }
            else
            {
                logger.LogInformation("[{Timestamp}] {NetworkInfo} - Sent: {LogMessage}", 
                    timestamp, networkInfo, logMessage);
            }
        }
    }
}