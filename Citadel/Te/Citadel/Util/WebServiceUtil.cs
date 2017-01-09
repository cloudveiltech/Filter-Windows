using NLog;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.UI.Models;

namespace Te.Citadel.Util
{
    internal class WebServiceUtil
    {
        /// <summary>
        /// Gets whether or not internet connectivity is available by pinging google's pub DNS in a
        /// synchronous fashion.
        /// </summary>
        public static bool HasInternetService
        {
            get
            {
                try
                {
                    // We'll ping google's public DNS servers to avoid getting flagged as some sort
                    // of bot.
                    Ping googleDnsPing = new Ping();
                    byte[] buffer = new byte[32];
                    PingReply reply = googleDnsPing.Send(IPAddress.Parse("8.8.4.4"), 1000, buffer, new PingOptions());
                    return (reply.Status == IPStatus.Success);
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// Checks for internet connectivity by pinging google's pub DNS in an asynchronous fashion.
        /// </summary>
        /// <returns>
        /// True if a response to the ping was received, false otherwise.
        /// </returns>
        public static async Task<bool> GetHasInternetServiceAsync()
        {
            try
            {
                // We'll ping google's public DNS servers to avoid getting flagged as some sort of
                // bot.
                Ping googleDnsPing = new Ping();
                byte[] buffer = new byte[32];
                PingReply reply = await googleDnsPing.SendPingAsync(IPAddress.Parse("8.8.4.4"), 1000, buffer, new PingOptions());
                return (reply.Status == IPStatus.Success);
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Builds out an HttpWebRequest specifically configured for use with the service provider
        /// API.
        /// </summary>
        /// <param name="route">
        /// The route for the request.
        /// </param>
        /// <returns>
        /// The configured request.
        /// </returns>
        private static HttpWebRequest GetApiBaseRequest(string route)
        {
            var serviceProviderApiBasePath = (string)Application.Current.Properties["ServiceProviderApi"];
            var requestString = serviceProviderApiBasePath + route;
            var requestRoute = new Uri(requestString);

            // Create a new request. We don't want auto redirect, we don't want the subsystem trying
            // to look up proxy information to configure on our request, we want a 5 second timeout
            // on any and all operations and we want to look like Firefox in a generic way. Here we
            // also set the cookie container, so we can capture session cookies if we're successful.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestRoute);
            request.Method = "POST";
            request.Proxy = null;
            request.AllowAutoRedirect = false;
            request.UseDefaultCredentials = false;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "Mozilla/5.0 (Windows NT x.y; rv:10.0) Gecko/20100101 Firefox/10.0";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.CookieContainer = AuthenticatedUserModel.Instance.UserSessionCookies;

            return request;
        }

        /// <summary>
        /// Request a generic resource from the service server(s).
        /// </summary>
        /// <param name="route">
        /// The API route to make the request to.
        /// </param>
        /// <param name="noLogging">
        /// Whether or not to log errors. Since HttpWebRequest brilliant throws exceptions for
        /// non-success HTTP status codes, it's nice to be able to control whether or not your
        /// request should have errors logged.
        /// </param>
        /// <returns>
        /// A non-null byte array on success. Null byte array on failure.
        /// </returns>
        public static async Task<byte[]> RequestResource(string route, bool noLogging = false)
        {
            try
            {
                // Try to send the device name as well. Helps distinguish between clients under the
                // same account.
                string deviceName = string.Empty;

                try
                {
                    deviceName = Environment.MachineName;
                }
                catch
                {
                    deviceName = "Unknown";
                }

                var request = GetApiBaseRequest(route);

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}&device_name={2}", AuthenticatedUserModel.Instance.Username, FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Don't forget to the set the content length to the total length of our form POST
                // data!
                request.ContentLength = formData.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = await request.GetRequestStreamAsync())
                {
                    // Write and close.
                    await requestStream.WriteAsync(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    try
                    {
                        // Check if response code is considered a success code.
                        if(code >= 200 && code <= 299)
                        {
                            using(var memoryStream = new MemoryStream())
                            {
                                response.GetResponseStream().CopyTo(memoryStream);

                                // We do this just in case we get something like a 204. The idea here
                                // is that if we return a non-null, the call was a success.
                                var responseBody = memoryStream.ToArray();
                                if(responseBody == null)
                                {
                                    responseBody = new byte[0];
                                }

                                return responseBody;
                            }
                        }
                    }
                    finally
                    {
                        response.Close();
                        request.Abort();
                    }
                }
            }
            catch(Exception webException)
            {
                if(noLogging == false)
                {
                    var logger = LogManager.GetLogger("Citadel");
                    LoggerUtil.RecursivelyLogException(logger, webException);
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to post the given form-encoded data to the service API.
        /// </summary>
        /// <param name="route">
        /// The API route to make the request to.
        /// </param>
        /// <param name="formEncodedData">
        /// The form encoded data to post.
        /// </param>
        /// <param name="noLogging">
        /// Whether or not to log errors. Since HttpWebRequest brilliant throws exceptions for
        /// non-success HTTP status codes, it's nice to be able to control whether or not your
        /// request should have errors logged.
        /// </param>
        /// <returns>
        /// True if the web service gave a success response, false otherwise.
        /// </returns>
        public static async Task<bool> SendResource(string route, byte[] formEncodedData, bool noLogging = true)
        {
            try
            {
                // Try to send the device name as well. Helps distinguish between clients under the
                // same account.
                string deviceName = string.Empty;

                try
                {
                    deviceName = Environment.MachineName;
                }
                catch
                {
                    deviceName = "Unknown";
                }

                var request = GetApiBaseRequest(route);

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}&device_name={2}&", AuthenticatedUserModel.Instance.Username, FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Merge all data.
                var finalData = new byte[formData.Length + formEncodedData.Length];
                Array.Copy(formData, finalData, formData.Length);
                Array.Copy(formEncodedData, 0, finalData, formData.Length, formEncodedData.Length);
                formData = finalData;

                // Don't forget to the set the content length to the total length of our form POST
                // data!
                request.ContentLength = formData.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = await request.GetRequestStreamAsync())
                {
                    // Write and close.
                    await requestStream.WriteAsync(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    try
                    {
                        // Check if response code is considered a success code.
                        if(code >= 200 && code <= 299)
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        response.Close();
                        request.Abort();
                    }
                }
            }
            catch(Exception webException)
            {
                if(noLogging == false)
                {
                    var logger = LogManager.GetLogger("Citadel");
                    LoggerUtil.RecursivelyLogException(logger, webException);
                }
            }

            return false;
        }
    }
}