using GalaSoft.MvvmLight;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class DashboardModel : ObservableObject
    {
        private readonly Logger m_logger;

        public DashboardModel()
        {
            m_logger = LogManager.GetLogger("Citadel");
        }

        public async Task<bool> RequestAppDeactivation()
        {
            try
            {
                // Look at that great evil singletons create!
                var authUri = AuthenticatedUserModel.Instance.AuthRoute;
                var deactivateUriString = authUri.Scheme + "://" + authUri.Host + "/capi/deactivate.php";
                Debug.WriteLine(deactivateUriString);
                var deactivateRoute = new Uri(deactivateUriString);

                // Create a new request. We don't want auto redirect, we don't want the subsystem
                // trying to look up proxy information to configure on our request, we want a 5
                // second timeout on any and all operations and we want to look like Firefox in a
                // generic way. Here we also set the cookie container, so we can capture session
                // cookies if we're successful.
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(deactivateRoute);
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

                    Debug.WriteLine(code);

                    response.Close();
                    request.Abort();

                    // Check if response code is considered a success code. If it is, then we've
                    // been given permission to shut down.
                    if(code >= 200 && code <= 299)
                    {
                        return true;
                    }
                }
            }
            catch(Exception webException)
            {
                // Why don't we log this stuff? Because .NET is a bit of a silly
                // bird and takes non-success status codes like 403 to be an exception
                // and 403 is the expected response from this request.

                /*
                Debug.WriteLine(webException.Message);
                Debug.WriteLine(webException.StackTrace);

                // We had an error. Attempt to log it.
                if(m_logger != null)
                {
                    m_logger.Error(webException.Message);
                    m_logger.Error(webException.StackTrace);

                    if(webException.InnerException != null)
                    {
                        m_logger.Error(webException.InnerException.Message);
                        m_logger.Error(webException.InnerException.StackTrace);

                        Debug.WriteLine(webException.InnerException.Message);
                        Debug.WriteLine(webException.InnerException.StackTrace);
                    }
                }
                */
            }

            return false;
        }
    }
}
