/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util.Net;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Citadel.Core.Windows.Util
{
    internal enum ServiceResource
    {
        UserDataRequest,
        UserDataSumCheck,
        DeactivationRequest,
        UserTerms,
        GetToken,
        RevokeToken
    };

    public delegate void GenericWebServiceUtilDelegate();

    /// <summary>
    /// This class facilitates the communication of the application with an upstream service
    /// provider. This class static, maintaining a single state only that persists throughout the
    /// application lifetime, because this application was only ever designed to support, by
    /// requirement, a single validated/authenticated user.
    /// </summary>
    internal class WebServiceUtil
    {
        public event GenericWebServiceUtilDelegate AuthTokenRejected;

        private static readonly Dictionary<ServiceResource, string> m_namedResourceMap = new Dictionary<ServiceResource, string>
        {
            { ServiceResource.UserDataRequest, "/api/v2/me/data/get" },
            { ServiceResource.UserDataSumCheck, "/api/v2/me/data/check" },
            { ServiceResource.DeactivationRequest, "/api/v2/me/deactivate" },
            { ServiceResource.UserTerms, "/api/v2/me/terms" },
            { ServiceResource.GetToken, "/api/v2/user/gettoken" },
            { ServiceResource.RevokeToken, "/api/v2/me/revoketoken" },
        };

        private object m_authenticationLock = new object();
        private object m_emailLock = new object();

        private readonly Logger m_logger;

        /// <summary>
        /// Abstracts the creation of our app's registry key away from the two properties.
        /// </summary>
        /// <param name="writeable">Should we get writeable permission?</param>
        /// <param name="createKey">Should we create the key if it doesn't exist?</param>
        /// <returns>registry key on success, or null otherwise</returns>
        private RegistryKey getAppRegistryKey(bool writeable = false, bool createKey = false)
        {
            // Get the name of our process, aka the Executable name.
            var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);


            // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software", writeable))
            {
                // Create or open our application's key.

                RegistryKey sub = null;

                if (!createKey)
                {
                    sub = key.OpenSubKey(applicationNiceName, writeable);
                }
                else
                {
                    try
                    {
                        sub = key.OpenSubKey(applicationNiceName, writeable);
                    }
                    catch
                    {
                    
                    }

                    if(sub == null)
                    {
                        try
                        {
                            key.DeleteSubKey(applicationNiceName, false);
                            sub = key.CreateSubKey(applicationNiceName);
                        }
                        catch
                        {
                            sub = null;
                        }
                    }
                }

                return sub;

            }
        }

        /// <summary>
        /// Stores the email that was granted the auth token.
        /// </summary>
        public string UserEmail
        {
            get
            {
                lock (m_emailLock)
                {
                    string machineName = string.Empty;

                    try
                    {
                        machineName = System.Environment.MachineName;
                    }
                    catch
                    {
                        machineName = "Unknown";
                    }

                    // This key will have the entropy written to it in the registry.
                    string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName + @"\email-address").ToString();

                    using (var sub = getAppRegistryKey(createKey: false))
                    {
                        string emailAddress = null;

                        if (sub != null)
                        {
                            emailAddress = sub.GetValue(keyName) as string;

                            if (emailAddress == null || emailAddress.Length == 0)
                            {
                                return null;
                            }
                        }

                        return emailAddress;
                    }
                }
            }

            set
            {
                Debug.Assert(value != null && value.Length > 0);

                lock (m_emailLock)
                {
                    string machineName = string.Empty;

                    try
                    {
                        machineName = System.Environment.MachineName;
                    }
                    catch
                    {
                        machineName = "Unknown";
                    }

                    // This key will have the entropy written to it in the registry.
                    string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName + @"\email-address").ToString();

                    // Get the name of our process, aka the Executable name.
                    var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                    // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                    using (RegistryKey sub = getAppRegistryKey(true, true))
                    {
                        // Create or open our application's key.
                        
                        if (sub != null)
                        {
                            try
                            {
                                sub.SetValue(keyName, value, RegistryValueKind.String);
                            }
                            catch(Exception e)
                            {
                                System.Diagnostics.Debug.WriteLine("sub.SetValue threw exception {0}", e.ToString());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Holds the auth token returned from the last successful auth request. 
        /// </summary>
		public string AuthToken
        {
            get
            {
                lock(m_authenticationLock)
                {
                    string machineName = string.Empty;

                    try
                    {
                        machineName = System.Environment.MachineName;
                    }
                    catch
                    {
                        machineName = "Unknown";
                    }

                    // This key will have the entropy written to it in the registry.
                    string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName).ToString();

                    // Get the name of our process, aka the Executable name.
                    var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                    // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                    using(RegistryKey sub = getAppRegistryKey())
                    {
                        // Create or open our application's key.

                        string authToken = null;

                        if(sub != null)
                        {
                            authToken = sub.GetValue(keyName) as string;

                            if(authToken == null || authToken.Length == 0)
                            {
                                return null;
                            }
                        }

                        return authToken;
                    }
                }
            }

            set
            {
                Debug.Assert(value != null && value.Length > 0);

                lock (m_authenticationLock)
                {
                    string machineName = string.Empty;

                    try
                    {
                        machineName = System.Environment.MachineName;
                    }
                    catch
                    {
                        machineName = "Unknown";
                    }

                    // This key will have the entropy written to it in the registry.
                    string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName).ToString();

                    // Get the name of our process, aka the Executable name.
                    var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                    // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                    using (RegistryKey sub = getAppRegistryKey(true, true))
                    {
                        if (sub != null)
                        {
                            try
                            {
                                sub.SetValue(keyName, value, RegistryValueKind.String);
                            }
                            catch (Exception e)
                            {
                                System.Diagnostics.Debug.WriteLine("sub.SetValue threw exception {0}", e.ToString());
                            }
                        }
                    }
                }
            }
        }

        public string LookupUriApiPath
        {
            get
            {
                return "https://manage.cloudveil.org";
            }
        }

        public string ServiceProviderApiPath
        {
            get
            {
                return "https://manage.cloudveil.org/citadel";
            }
        }

        public string ServiceProviderUnblockRequestPath
        {
            get
            {
                return "https://manage.cloudveil.org/unblock_request/new_request";
            }
        }

        public bool HasStoredCredentials
        {
            get
            {
                return StringExtensions.Valid(AuthToken);
            }
        }

        private static WebServiceUtil s_instance;

        static WebServiceUtil()
        {
            s_instance = new WebServiceUtil();
        }

        public static WebServiceUtil Default
        {
            get
            {
                return s_instance;
            }
        }

        private WebServiceUtil()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        public AuthenticationResultObject Authenticate(string username, byte[] unencryptedPassword)
        {
            m_logger.Error(nameof(Authenticate));

            AuthenticationResultObject ret = new AuthenticationResultObject();

            // Enforce parameters are valid.
            Debug.Assert(StringExtensions.Valid(username));

            if(!StringExtensions.Valid(username))
            {
                throw new ArgumentException("Supplied username cannot be null, empty or whitespace.", nameof(username));
            }

            Debug.Assert(unencryptedPassword != null && unencryptedPassword.Length > 0);

            if(unencryptedPassword == null || unencryptedPassword.Length <= 0)
            {
                throw new ArgumentException("Supplied password byte array cannot be null and must have a length greater than zero.", nameof(unencryptedPassword));
            }
            //

            // Don't bother if we don't have internet.
            var hasInternet = NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection;
            if(hasInternet == false)
            {
                m_logger.Info("Aborting authentication attempt because no internet connection could be detected.");
                ret.AuthenticationResult = AuthenticationResult.ConnectionFailed;
                ret.AuthenticationMessage = "Aborting authentication attempt because no internet connection could be detected.";
                return ret;
            }

            // Will be set if we get any sort of web exception.
            bool connectionFailure = false;

            // Try to send the device name as well. Helps distinguish between clients under the same account.
            string deviceName = string.Empty;

            byte[] formData = null;

            try
            {
                deviceName = Environment.MachineName;
            }
            catch
            {
                deviceName = "Unknown";
            }

            try
            {
                var authRequest = GetApiBaseRequest(m_namedResourceMap[ServiceResource.GetToken]);

                // Build out username and password as post form data. We need to ensure that we mop
                // up any decrypted forms of our password when we're done, and ASAP.
                formData = System.Text.Encoding.UTF8.GetBytes(string.Format("email={0}&identifier={1}&device_id={2}", username, FingerPrint.Value, deviceName));

                // Don't forget to the set the content length to the total length of our form POST data!
                authRequest.ContentLength = formData.Length;

                authRequest.ContentType = "application/x-www-form-urlencoded";

                // XXX TODO - This is naughty, because we're putting our password into a string, so
                // it will linger in memory. However, it appears that the provided API doesn't give
                // us any choice.
                // KF NOTE: Why System.String is insecure. https://stackoverflow.com/questions/1166952/net-secure-memory-structures
                var encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + Encoding.UTF8.GetString(unencryptedPassword)));
                authRequest.Headers.Add("Authorization", "Basic " + encoded);

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = authRequest.GetRequestStream())
                {
                    // Write and close.
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)authRequest.GetResponse())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    // Check if the response status code is outside the "success" range of codes
                    // defined in HTTP. If so, we failed. We include redirect codes (3XX) as success,
                    // since our server side will just redirect us if we're already authed.
                    if(code > 199 && code < 299)
                    {
                        using(var tr = new StreamReader(response.GetResponseStream()))
                        {
                            AuthToken = tr.ReadToEnd();
                            UserEmail = username;
                        }

                        response.Close();
                        authRequest.Abort();
                        ret.AuthenticationResult = AuthenticationResult.Success;
                        return ret;
                    }
                    else
                    {
                        if(code > 399 && code < 499)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            AuthToken = string.Empty;
                            ret.AuthenticationResult = AuthenticationResult.Failure;
                            return ret;
                        }
                    }
                }
            }
            catch(WebException e)
            {
                // XXX TODO - Is this sufficient?
                if(e.Status == WebExceptionStatus.Timeout)
                {
                    m_logger.Info("Authentication failed due to timeout.");
                    connectionFailure = true;
                }

                try
                {
                    using(WebResponse response = e.Response)
                    {
                        HttpWebResponse httpResponse = (HttpWebResponse)response;
                        m_logger.Error("Error code: {0}", httpResponse.StatusCode);

                        string errorText = string.Empty;

                        using (Stream data = response.GetResponseStream())
                        using (var reader = new StreamReader(data))
                        {
                            errorText = reader.ReadToEnd();
                            
                            // GS Just cleans up the punctuation at the end of string
                            string excpList = "$@*!.";
                            var chRemoved = errorText
                                .Select(ch => excpList.Contains(ch) ? (char?)null : ch);
                            errorText = string.Concat(chRemoved.ToArray()) + "!";

                            m_logger.Error("Stream errorText: " + errorText);
                        }

                        int code = (int)httpResponse.StatusCode;

                        if(code > 399 && code < 499)
                        {
                            AuthToken = string.Empty;
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            m_logger.Info("Athentication failure text: {0}", errorText);
                            ret.AuthenticationMessage = errorText;
                            ret.AuthenticationResult =  AuthenticationResult.Failure;
                            return ret;
                        }


                    }
                }
                catch(Exception iex)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, iex);
                }

                // Log the exception.
                m_logger.Error(e.Message);
                m_logger.Error(e.StackTrace);
            }
            catch(Exception e)
            {
                while(e != null)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                    e = e.InnerException;
                }

                m_logger.Info("Authentication failed due to a failure to process the request and response.");
                AuthToken = string.Empty;
                ret.AuthenticationResult = AuthenticationResult.Failure;
                return ret;
            }
            finally
            {
                // This finally block is guaranteed TM to be run, so this is where we're going to
                // clean up any decrypted versions of the user's password held in memory.
                if(unencryptedPassword != null && unencryptedPassword.Length > 0)
                {
                    Array.Clear(unencryptedPassword, 0, unencryptedPassword.Length);
                }

                if(formData != null && formData.Length > 0)
                {
                    Array.Clear(formData, 0, formData.Length);
                }
            }

            m_logger.Info("Authentication failed due to a complete failure to process the request and response.");

            // If we had success, we should/would have returned by now.
            if(!connectionFailure)
            {
                AuthToken = string.Empty;
            }

            ret.AuthenticationResult = connectionFailure ? AuthenticationResult.ConnectionFailed : AuthenticationResult.Failure;
            return ret;

        }

        /// <summary>
        /// Builds out an HttpWebRequest specifically configured for use with the service provider API. 
        /// </summary>
        /// <param name="route">
        /// The route for the request. 
        /// </param>
        /// <returns>
        /// The configured request. 
        /// </returns>
        private HttpWebRequest GetApiBaseRequest(string route, string baseRoute = null)
        {
            baseRoute = baseRoute == null ? ServiceProviderApiPath : baseRoute;
            var requestString = baseRoute + route;
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
            request.Accept = "application/json,text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            return request;
        }

        /// <summary>
        /// Used to look up a specific URL to return information about it.
        /// Used primarily to determine what the block page category is going to be.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="noLogging"></param>
        /// <returns></returns>
        public UriInfo LookupUri(Uri uri, bool noLogging = false)
        {
            HttpStatusCode code;
            UriInfo uriInfo = null;
            string url = uri.ToString();

            try
            {
                HttpWebRequest request = GetApiBaseRequest(string.Format("/api/uri/lookup/existing?uri={0}", Uri.EscapeDataString(url)), LookupUriApiPath);
                request.Method = "GET";
                
                string uriInfoText = null;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        uriInfoText = reader.ReadToEnd();

                        try
                        {
                            uriInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<UriInfo>(uriInfoText);
                        }
                        catch
                        {
                            uriInfo = null;
                        }
                    }
                }

                return uriInfo;
            }
            catch (WebException e)
            {
                // KF - Set this to 0 for default. 0's a pretty good indicator of no internet.
                code = 0;

                try
                {
                    using (WebResponse response = e.Response)
                    {
                        HttpWebResponse httpResponse = (HttpWebResponse)response;
                        m_logger.Error("Error code: {0}", httpResponse.StatusCode);

                        int intCode = (int)httpResponse.StatusCode;

                        code = (HttpStatusCode)intCode;

                        // Auth failure means re-log EXCEPT when requesting deactivation.
                        if (intCode > 399 && intCode < 499)
                        {
                            AuthToken = string.Empty;
                            m_logger.Info("Client error occurred while trying to lookup site.");
                            //AuthTokenRejected?.Invoke();
                        }

                        using (Stream data = response.GetResponseStream())
                        using (var reader = new StreamReader(data))
                        {
                            string text = reader.ReadToEnd();
                            m_logger.Error(text);
                        }
                    }
                }
                catch { }

                if (noLogging == false)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                }
            }
            catch (Exception e)
            {
                // XXX TODO - Good default?
                code = 0;

                if (noLogging == false)
                {
                    while (e != null)
                    {
                        m_logger.Error(e.Message);
                        m_logger.Error(e.StackTrace);
                        e = e.InnerException;
                    }
                }
            }

            return uriInfo;
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
        public byte[] RequestResource(ServiceResource resource, out HttpStatusCode code, bool noLogging = false)
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

                var request = GetApiBaseRequest(m_namedResourceMap[resource]);

                var accessToken = AuthToken;

                m_logger.Info("RequestResource1: accessToken=" + accessToken);

                if(StringExtensions.Valid(accessToken))
                {
                    request.Headers.Add("Authorization", string.Format("Bearer {0}", accessToken));
                }
                else
                {
                    m_logger.Info("RequestResource1: Authorization failed.");
                    AuthTokenRejected?.Invoke();
                    code = HttpStatusCode.Unauthorized;
                    return null;
                }

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("&identifier={0}&device_id={1}", FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Don't forget to the set the content length to the total length of our form POST data!
                request.ContentLength = formData.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = request.GetRequestStream())
                {
                    // Write and close.
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)request.GetResponse())
                {
                    // Get the response code as an int so we can range check it.
                    var intCode = (int)response.StatusCode;

                    code = (HttpStatusCode)intCode;

                    try
                    {
                        // Check if response code is considered a success code.
                        if(intCode >= 200 && intCode <= 299)
                        {
                            using(var memoryStream = new MemoryStream())
                            {
                                response.GetResponseStream().CopyTo(memoryStream);                                

                                // We do this just in case we get something like a 204. The idea here
                                // is that if we return a non-null, the call was a success.
                                var responseBody = memoryStream.ToArray();
                                if(responseBody == null || intCode == 204)
                                {                                    
                                    return null;
                                }

                                return responseBody;
                            }
                        }
                        else
                        {
                            m_logger.Info("When requesting resource, got unexpected response code of {0}.", code);
                        }
                    }
                    finally
                    {
                        response.Close();
                        request.Abort();
                    }
                }
            }
            catch(WebException e)
            {
                // KF - Set this to 0 for default. 0's a pretty good indicator of no internet.
                code = 0;

                try
                {
                    using(WebResponse response = e.Response)
                    {
                        HttpWebResponse httpResponse = (HttpWebResponse)response;
                        m_logger.Error("Error code: {0}", httpResponse.StatusCode);

                        int intCode = (int)httpResponse.StatusCode;

                        code = (HttpStatusCode)intCode;

                        // Auth failure means re-log EXCEPT when requesting deactivation.
                        if(intCode > 399 && intCode < 499 && resource != ServiceResource.DeactivationRequest)
                        {
                            AuthToken = string.Empty;
                            m_logger.Info("RequestResource2: Authorization failed.");
                            AuthTokenRejected?.Invoke();
                        }

                        using(Stream data = response.GetResponseStream())
                        using(var reader = new StreamReader(data))
                        {
                            string text = reader.ReadToEnd();
                            m_logger.Error(text);
                        }
                    }
                }
                catch { }

                if(noLogging == false)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                }
            }
            catch(Exception e)
            {
                // XXX TODO - Good default?
                code = HttpStatusCode.InternalServerError;

                if(noLogging == false)
                {
                    while(e != null)
                    {
                        m_logger.Error(e.Message);
                        m_logger.Error(e.StackTrace);
                        e = e.InnerException;
                    }
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
        public bool SendResource(ServiceResource resource, byte[] formEncodedData, out HttpStatusCode code,  bool noLogging = true)
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

                var request = GetApiBaseRequest(m_namedResourceMap[resource]);

                var accessToken = AuthToken;

                if(StringExtensions.Valid(accessToken))
                {
                    request.Headers.Add("Authorization", string.Format("Bearer {0}", accessToken));
                }
                else
                {
                    m_logger.Info("SendResource1: Authorization failed.");
                    AuthTokenRejected?.Invoke();
                    code = HttpStatusCode.Unauthorized;
                    return false;
                }

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("identifier={0}&device_id={1}&", FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Merge all data.
                var finalData = new byte[formData.Length + formEncodedData.Length];
                Array.Copy(formData, finalData, formData.Length);
                Array.Copy(formEncodedData, 0, finalData, formData.Length, formEncodedData.Length);
                formData = finalData;

                // Don't forget to the set the content length to the total length of our form POST data!
                request.ContentLength = formData.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = request.GetRequestStream())
                {
                    // Write and close.
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)request.GetResponse())
                {
                    // Get the response code as an int so we can range check it.
                    var intCode = (int)response.StatusCode;

                    code = (HttpStatusCode)intCode;

                    try
                    {
                        // Check if response code is considered a success code.
                        if(intCode >= 200 && intCode <= 299)
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
            catch(WebException e)
            {
                // XXX TODO - Good default?
                code = HttpStatusCode.InternalServerError;

                try
                {
                    using(WebResponse response = e.Response)
                    {
                        HttpWebResponse httpResponse = (HttpWebResponse)response;
                        m_logger.Error("Error code: {0}", httpResponse.StatusCode);

                        int intCode = (int)httpResponse.StatusCode;

                        if(intCode > 399 && intCode < 499)
                        {
                            AuthToken = string.Empty;
                            m_logger.Info("SendResource2: Authorization failed.");
                            AuthTokenRejected?.Invoke();
                        }

                        using(Stream data = response.GetResponseStream())
                        using(var reader = new StreamReader(data))
                        {
                            string text = reader.ReadToEnd();
                            m_logger.Error(text);
                        }
                    }
                }
                catch { }

                if(noLogging == false)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                }
            }
            catch(Exception e)
            {
                // XXX TODO - Good default?
                code = HttpStatusCode.InternalServerError;

                if(noLogging == false)
                {
                    while(e != null)
                    {
                        m_logger.Error(e.Message);
                        m_logger.Error(e.StackTrace);
                        e = e.InnerException;
                    }
                }
            }

            return false;
        }
    }
}