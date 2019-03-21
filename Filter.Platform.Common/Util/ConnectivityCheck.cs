using CloudVeil;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Filter.Platform.Common.Util
{
    public static class ConnectivityCheck
    {
        public enum Accessible
        {
            Yes,
            No,
            UnexpectedResponse
        }

        private static NLog.Logger logger;

        static ConnectivityCheck()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public static Accessible IsAccessible()
        {
            WebClient client = new WebClient();
            string captivePortalCheck = null;
            try
            {
                captivePortalCheck = client.DownloadString(CompileSecrets.ConnectivityCheck + "/ncsi.txt");

                if (captivePortalCheck.Trim(' ', '\r', '\n', '\t') != CompileSecrets.NCSIString)
                {
                    return Accessible.UnexpectedResponse;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                {
                    return Accessible.No;
                }

                logger.Info("Got an error response from captive portal check. {0}", ex.Status);
                return Accessible.UnexpectedResponse;
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                return Accessible.Yes;
            }

            return Accessible.Yes;
        }
    }
}
