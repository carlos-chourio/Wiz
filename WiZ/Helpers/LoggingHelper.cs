using System;
using System.Net;
using System.Runtime.InteropServices;
using Serilog;

namespace WiZ.Helpers
{
    public static class LoggingHelper
    {
        public static void LogInput(string text, string localip, string remoteip)
        {
            FormatLog(text, true, localip, remoteip);
        }
        public static void LogInput(string text, IPAddress localip, IPAddress remoteip)
        {
            FormatLog(text, true, localip, remoteip);
        }

        public static void LogOutput(string text, string localip, string remoteip)
        {
            FormatLog(text, false, localip, remoteip);
        }

        public static void LogOutput(string text, IPAddress localip, IPAddress remoteip)
        {
            FormatLog(text, false, localip, remoteip);
        }

        public static void FormatLog(string text, bool inp, IPAddress localip, IPAddress remoteip)
        {
            FormatLog(text, inp, localip?.ToString(), remoteip?.ToString());
        }

        public static void FormatLog(string text, bool inp, string localip, string remoteip)
        {
            var arrow = inp ? "<=" : "=>";
            var timestamp = DateTime.Now.ToString("G");
            
            var networkInfo = $"LOCAL: {localip} {arrow} REMOTE: {remoteip}";
            var logMessage = text;

            if (inp)
            {
                Log.Information("[{Timestamp}] {NetworkInfo} - Received: {LogMessage}", 
                    timestamp, networkInfo, logMessage);
            }
            else
            {
                Log.Information("[{Timestamp}] {NetworkInfo} - Sent: {LogMessage}", 
                    timestamp, networkInfo, logMessage);
            }
        }
    }
}