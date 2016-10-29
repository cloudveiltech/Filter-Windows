using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.UI.Models;

namespace Te.Citadel.Util
{
    internal class WebServiceUtil
    {
        /// <summary>
        /// Request a generic resource from the service server(s).
        /// </summary>
        /// <param name="route">
        /// The API route to make the request to.
        /// </param>
        /// <param name="noLogging">
        /// Whether or not to log errors. Since HttpWebRequest brilliant throws exceptions for non-success HTTP status codes,
        /// it's nice to be able to control whether or not your request should have errors logged.
        /// </param>
        /// <returns>
        /// A non-null byte array on success. Null byte array on failure.
        /// </returns>
        public static async Task<byte[]> RequestResource(string route, bool noLogging = false)
        {
            try
            {
                var serviceProviderApiBasePath = (string)Application.Current.Properties["ServiceProviderApi"];
                var requestString = serviceProviderApiBasePath + route;                
                var requestRoute = new Uri(requestString);

                // Create a new request. We don't want auto redirect, we don't want the subsystem
                // trying to look up proxy information to configure on our request, we want a 5
                // second timeout on any and all operations and we want to look like Firefox in a
                // generic way. Here we also set the cookie container, so we can capture session
                // cookies if we're successful.
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

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}", AuthenticatedUserModel.Instance.Username, FingerPrint.Value));

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
                        // Check if response code is considered a success code. If it is, then we've
                        // been given permission to shut down.
                        if(code >= 200 && code <= 299)
                        {
                            using(var memoryStream = new MemoryStream())
                            {
                                response.GetResponseStream().CopyTo(memoryStream);
                                return memoryStream.ToArray();
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
                var logger = LogManager.GetLogger("Citadel");

                LoggerUtil.RecursivelyLogException(logger, webException);
            }

            return null;        
        }
    }
}
