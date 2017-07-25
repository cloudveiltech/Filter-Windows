/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using AngleSharp.Parser.Html;
using Citadel.Core.Data.Serialization;
using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Citadel.Core.Windows.Util
{
    public enum ServiceResource
    {
        UserDataRequest,
        UserDataSumCheck,
        DeactivationRequest,
        UserTerms
    };

    /// <summary>
    /// Enumeration for summarizing the result of an authentication request. 
    /// </summary>
    public enum AuthenticationResult
    {
        /// <summary>
        /// Indicates that the auth request was a failure. 
        /// </summary>
        Failure,

        /// <summary>
        /// Indicates that the auth request was a success. 
        /// </summary>
        Success,

        /// <summary>
        /// Indicates that, during the auth request, a connection using the provided URI could not be established.
        /// </summary>
        ConnectionFailed
    }

    /// <summary>
    /// This class facilitates the communication of the application with an upstream service
    /// provider. This class static, maintaining a single state only that persists throughout the
    /// application lifetime, because this application was only ever designed to support, by
    /// requirement, a single validated/authenticated user.
    /// </summary>
    public class WebServiceUtil
    {
        /// <summary>
        /// This class is used to make persisting the state of the last authenticated user. 
        /// </summary>
        private class SerializableAuthenticatedUserModel
        {
            [JsonProperty]
            public string Username
            {
                get;
                set;
            }

            [JsonProperty]
            public byte[] EncryptedPassword
            {
                get;
                set;
            }

            [JsonProperty]
            [JsonConverter(typeof(CookieListConverter))]
            public List<Cookie> Cookies
            {
                get;
                set;
            }

            [JsonProperty]
            public string CSRFTokenString
            {
                get;
                set;
            }

            [JsonIgnore]
            public bool IsValid
            {
                get
                {
                    if(!StringExtensions.Valid(Username))
                    {
                        return false;
                    }

                    if(EncryptedPassword == null || EncryptedPassword.Length <= 0)
                    {
                        return false;
                    }

                    if(Cookies == null || Cookies.Count == 0)
                    {
                        return false;
                    }

                    return true;
                }
            }
        }

        private static readonly Dictionary<ServiceResource, string> m_namedResourceMap = new Dictionary<ServiceResource, string>
        {
            { ServiceResource.UserDataRequest, "/api/user/me/data/get" },
            { ServiceResource.UserDataSumCheck, "/api/user/me/data/check" },
            { ServiceResource.DeactivationRequest, "/api/user/me/deactivate" },
            { ServiceResource.UserTerms, "/api/user/me/terms" }
        };

        /// <summary>
        /// Logger. 
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Path where we'll persist this object on the file system. 
        /// </summary>
        private readonly string m_savePath;

        /// <summary>
        /// Used in the Entropy property getter to enforce synchronization. 
        /// </summary>
        private object m_entropyLockObject;

        /// <summary>
        /// Holds an encrypted version of the last password used in a successful auth request. 
        /// </summary>
        private byte[] m_passwordEncrypted;

        /// <summary>
        /// Holds the last username used in a successful auth request. 
        /// </summary>
		public string Username
        {
            get;
            set;
        } = string.Empty;

        /// <summary>
        /// Holds the last password used in a successful auth request. 
        /// </summary>
		private byte[] Password
        {
            get
            {
                // Create a decrypted copy and return it. Should be destroyed ASAP.
                byte[] plaintext = ProtectedData.Unprotect(m_passwordEncrypted, Entropy, DataProtectionScope.LocalMachine);
                return plaintext;
            }

            set
            {
                Debug.Assert(value != null && value.Length > 0);

                if(value == null)
                {
                    throw new ArgumentException("Expected valid password byte array.", nameof(Password));
                }

                if(value.Length <= 0)
                {
                    throw new ArgumentException("Got empty password byte array. Expect a length greater than zero.", nameof(Password));
                }

                m_passwordEncrypted = ProtectedData.Protect(value, Entropy, DataProtectionScope.LocalMachine);

                // Clear out the unencrypted password bytes ASAP.
                Array.Clear(value, 0, value.Length);
            }
        }

        /// <summary>
        /// Gets the Entropy bytes we supply during the protection/unprotection of the password member. 
        /// </summary>
        private byte[] Entropy
        {
            get
            {
                // Enforce rigid synchronization.
                lock(m_entropyLockObject)
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

                    // Open the CURRENT_USER\SYSTEM sub key for read/write.
                    RegistryKey key = Registry.LocalMachine.OpenSubKey("Software", true);

                    // Create or open our application's key.
                    key.CreateSubKey(applicationNiceName);
                    key = key.OpenSubKey(applicationNiceName, true);

                    // Come up with a default value to supply to the registry value fetch function.
                    byte[] doesntExist = new byte[] { (byte)'n', (byte)'o', (byte)'p', (byte)'e' };
                    var entropy = (byte[])key.GetValue(keyName, doesntExist);

                    // If our entropy member is equal to our doesntExist value, then the key has not
                    // yet been set and we need to generate some new entropy, store it, and then
                    // return it.
                    if(entropy == null || entropy.Length == 0 || (entropy.Length == doesntExist.Length && entropy == doesntExist))
                    {
                        // Doesn't exist, so create it.
                        entropy = new byte[20];

                        using(RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                        {
                            rng.GetBytes(entropy);
                        }

                        // Update the registry key, so get this value back later.
                        key.SetValue(keyName, entropy, RegistryValueKind.Binary);
                    }

                    return entropy;
                }
            }
        }

        private CookieContainer UserSessionCookies
        {
            get;
            set;
        }

        private string CSRFToken
        {
            get;
            set;
        }

        public string ServiceProviderApiAuthPath
        {
            get
            {
                return ServiceProviderApiPath + "/login";
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
                return StringExtensions.Valid(Username) && m_passwordEncrypted != null && m_passwordEncrypted.Length > 0;
            }
        }

        public bool IsSessionExpired
        {
            get
            {
                if(UserSessionCookies == null || UserSessionCookies.Count <= 0)
                {
                    return true;
                }

                bool retVal = false;

                var allCookies = UserSessionCookies.GetCookies(new Uri(ServiceProviderApiAuthPath)).OfType<Cookie>().ToList();
                foreach(var cookie in allCookies)
                {
                    if(cookie.Expired)
                    {
                        retVal = true;
                        break;
                    }
                }

                if(retVal)
                {
                    // Ensure we remove old junk cookies.
                    UserSessionCookies = new CookieContainer();
                }

                return retVal;
            }
        }

        private static WebServiceUtil s_instance;

        static WebServiceUtil()
        {
            s_instance = new WebServiceUtil();
        }

        public static void Destroy()
        {
            s_instance = new WebServiceUtil();

            if(File.Exists(s_instance.m_savePath))
            {
                File.Delete(s_instance.m_savePath);
            }
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
            m_savePath = AppDomain.CurrentDomain.BaseDirectory + "u.dat";
            m_logger = LoggerUtil.GetAppWideLogger();
            m_entropyLockObject = new object();
        }

        /// <summary>
        /// Checks for internet connectivity by pinging google's pub DNS in an asynchronous fashion. 
        /// </summary>
        /// <returns>
        /// True if a response to the ping was received, false otherwise. 
        /// </returns>
        public static async Task<bool> GetHasInternetServiceAsync()        {
            
            try
            {
                // We'll ping google's public DNS servers to avoid getting flagged as some sort of bot.
                Ping googleDnsPing = new Ping();
                byte[] buffer = new byte[32];
                PingReply reply = await googleDnsPing.SendPingAsync(IPAddress.Parse("8.8.4.4"), 1000, buffer, new PingOptions());
                return (reply.Status == IPStatus.Success);
            }
            catch { }

            return false;
        }

        public async Task<AuthenticationResult> Authenticate(string username, byte[] unencryptedPassword)
        {
            m_logger.Info(nameof(Authenticate));

            string csrfDataStr = CSRFToken;

            var apiAuthData = await GetCsfrData();

            csrfDataStr = apiAuthData.Item1;
            var cookies = apiAuthData.Item2;

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
            var hasInternet = await GetHasInternetServiceAsync();
            if(hasInternet == false)
            {
                return AuthenticationResult.ConnectionFailed;
            }

            // Will be set if we get any sort of web exception.
            bool connectionFailure = false;

            // Where the post variables will be written as bytes. This also needs to be cleaned up
            // and ASAP, since it will contain the user's password in a decrypted state.
            byte[] finalPostPayload = null;

            // Try to send the device name as well. Helps distinguish between clients under the same account.
            string deviceName = string.Empty;

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
                var authRequest = GetApiBaseRequest("/login");

                // Update our CSRF data etc.
                if(authRequest.Headers["X-CSRF-TOKEN"] == null)
                {
                    authRequest.Headers.Add("X-CSRF-TOKEN", csrfDataStr);
                }
                else
                {
                    authRequest.Headers["X-CSRF-TOKEN"] = csrfDataStr;
                }

                authRequest.CookieContainer = cookies;

                // Build out username and password as post form data. We need to ensure that we mop
                // up any decrypted forms of our password when we're done, and ASAP.
                var formDataStart = System.Text.Encoding.UTF8.GetBytes(string.Format("email={0}&identifier={1}&device_id={2}&password=", username, FingerPrint.Value, deviceName));
                finalPostPayload = new byte[formDataStart.Length + unencryptedPassword.Length];

                // Here we copy the byte range of the unencrypted password, in order to avoid having
                // this value held in a String object, which will linger around in memory
                // indefinitely, exposing our secrets to the whole world.
                Array.Copy(formDataStart, finalPostPayload, formDataStart.Length);
                Array.Copy(unencryptedPassword, 0, finalPostPayload, formDataStart.Length, unencryptedPassword.Length);

                // Don't forget to the set the content length to the total length of our form POST data!
                authRequest.ContentLength = finalPostPayload.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = await authRequest.GetRequestStreamAsync())
                {
                    // Write and close.
                    await requestStream.WriteAsync(finalPostPayload, 0, finalPostPayload.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)await authRequest.GetResponseAsync())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    // Check if the response status code is outside the "success" range of codes
                    // defined in HTTP. If so, we failed. We include redirect codes (3XX) as success,
                    // since our server side will just redirect us if we're already authed.
                    if(code > 199 && code < 399)
                    {
                        if(code > 299 && code < 399)
                        {
                            // Make sure we weren't redirected back to login. If we were, then our
                            // auth failed.
                            if(response.Headers["Location"] != null)
                            {
                                if(response.Headers["Location"].ToLower().IndexOf("login") != -1)
                                {
                                    response.Close();
                                    authRequest.Abort();
                                    return AuthenticationResult.Failure;
                                }
                            }
                        }

                        Username = username;
                        Password = unencryptedPassword;

                        UserSessionCookies = response.Headers.GetResponseCookiesFromService();
                        CSRFToken = csrfDataStr;

                        // Just save these credentials automatically any time that we have a
                        // successfull auth.
                        Save();

                        response.Close();
                        authRequest.Abort();
                        return AuthenticationResult.Success;
                    }
                }
            }
            catch(Exception e)
            {
                connectionFailure = true;

                if(e is WebException)
                {
                    var ewx = e as WebException;

                    if(ewx != null)
                    {
                        using(WebResponse response = ewx.Response)
                        {
                            HttpWebResponse httpResponse = (HttpWebResponse)response;
                            m_logger.Info("Error code: {0}", httpResponse.StatusCode);
                            using(Stream data = response.GetResponseStream())
                            using(var reader = new StreamReader(data))
                            {
                                string text = reader.ReadToEnd();

                                if(text.IndexOf("csrf", StringComparison.OrdinalIgnoreCase) != -1)
                                {
                                    // We've somehow become out of sync with the server. It could be
                                    // that the session was manually destroyed on the server side.
                                    // Our token is no longer being accepted.
                                    //
                                    // XXX TODO This is pretty dirty, maybe find a cleaner way.
                                    // There's just not much else we can do given current server
                                    // side design.
                                    //
                                    // If we destroy our cookies, then the next API call will detect
                                    // that we have no session, and re-auth will be issued.
                                    // This process will refresh our server auth state, including
                                    // our mismatched CSRF token.
                                    UserSessionCookies = new CookieContainer();
                                }

                                m_logger.Info(text);
                            }
                        }
                    }
                }

                // Log the exception.
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                // This finally block is guaranteed TM to be run, so this is where we're going to
                // clean up any decrypted versions of the user's password held in memory.
                if(unencryptedPassword != null && unencryptedPassword.Length > 0)
                {
                    Array.Clear(unencryptedPassword, 0, unencryptedPassword.Length);
                }

                if(finalPostPayload != null && finalPostPayload.Length > 0)
                {
                    Array.Clear(finalPostPayload, 0, finalPostPayload.Length);
                }
            }

            // If we had success, we should/would have returned by now.
            return connectionFailure ? AuthenticationResult.ConnectionFailed : AuthenticationResult.Failure;
        }

        public async Task<AuthenticationResult> ReAuthenticate()
        {
            m_logger.Info(nameof(ReAuthenticate));
            var user = Username;
            var password = Password;

            try
            {
                var result = await Authenticate(user, password);
                return result;
            }
            catch(WebException e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return AuthenticationResult.ConnectionFailed;
            }
            finally
            {
                if(password != null)
                {
                    Array.Clear(password, 0, password.Length);
                }
            }
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
        private HttpWebRequest GetApiBaseRequest(string route)
        {
            var requestString = ServiceProviderApiPath + route;
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
            request.CookieContainer = UserSessionCookies;

            if(StringExtensions.Valid(CSRFToken))
            {
                request.Headers.Add("X-CSRF-TOKEN", CSRFToken);
            }

            return request;
        }

        private async Task<Tuple<string, CookieContainer>> GetCsfrData()
        {
            if(!IsSessionExpired)
            {
                m_logger.Info("Using persisted user state.");
                return new Tuple<string, CookieContainer>(CSRFToken, UserSessionCookies);
            }

            var request = GetApiBaseRequest("/login");
            request.Method = "GET";

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

                            var responseBody = memoryStream.ToArray();
                            if(responseBody != null && responseBody.Length > 0)
                            {
                                var asHtmlString = System.Text.Encoding.UTF8.GetString(responseBody);

                                var parser = new HtmlParser();
                                var document = parser.Parse(asHtmlString);

                                var csfrMetaTag = document.QuerySelector("[name=csrf-token]");
                                var csrfToken = csfrMetaTag.GetAttribute("content");

                                m_logger.Info("Using new CSRF token: {0}", csrfToken);

                                return new Tuple<string, CookieContainer>(csrfToken, response.Headers.GetResponseCookiesFromService());
                            }
                        }
                    }
                    else if(code >= 300 && code <= 399)
                    {
                        // Don't create a new session. We were redirected, which means these session
                        // values are still valid.
                        m_logger.Info("Using old token data. How? If we weren't expired, we shouldn't have arrived here.");
                        return new Tuple<string, CookieContainer>(CSRFToken, UserSessionCookies);
                    }
                }
                catch(Exception webException)
                {
                    var logger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(logger, webException);
                }
            }

            return null;
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
        public async Task<byte[]> RequestResource(ServiceResource resource, bool noLogging = false)
        {
            try
            {
                if(IsSessionExpired)
                {
                    var reAuthResult = await ReAuthenticate();
                    if(reAuthResult != AuthenticationResult.Success)
                    {
                        m_logger.Error("In {0}, user session was expired and could not re-auth.", nameof(RequestResource));
                        return null;
                    }
                }

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

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}&device_id={2}", Username, FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Don't forget to the set the content length to the total length of our form POST data!
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
                                if(responseBody == null || code == 204)
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
            catch(Exception e)
            {
                if(noLogging == false)
                {
                    var logger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(logger, e);
                }

                if(e is WebException)
                {
                    var ewx = e as WebException;

                    if(ewx != null)
                    {
                        using(WebResponse response = ewx.Response)
                        {
                            HttpWebResponse httpResponse = (HttpWebResponse)response;
                            m_logger.Info("Error code: {0}", httpResponse.StatusCode);
                            using(Stream data = response.GetResponseStream())
                            using(var reader = new StreamReader(data))
                            {
                                string text = reader.ReadToEnd();

                                if(StringExtensions.Valid(text))
                                {
                                    if(text.IndexOf("csrf", StringComparison.OrdinalIgnoreCase) != -1)
                                    {
                                        // We've somehow become out of sync with the server. It could be
                                        // that the session was manually destroyed on the server side.
                                        // Our token is no longer being accepted.
                                        //
                                        // XXX TODO This is pretty dirty, maybe find a cleaner way.
                                        // There's just not much else we can do given current server
                                        // side design.
                                        //
                                        // If we destroy our cookies, then the next API call will detect
                                        // that we have no session, and re-auth will be issued.
                                        // This process will refresh our server auth state, including
                                        // our mismatched CSRF token.
                                        UserSessionCookies = new CookieContainer();
                                    }
                                }

                                m_logger.Info(text);
                            }
                        }
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
        public async Task<bool> SendResource(ServiceResource resource, byte[] formEncodedData, bool noLogging = true)
        {
            try
            {
                if(IsSessionExpired)
                {
                    var reAuthResult = await ReAuthenticate();
                    if(reAuthResult != AuthenticationResult.Success)
                    {
                        m_logger.Error("In {0}, user session was expired and could not re-auth.", nameof(SendResource));
                        return false;
                    }
                }

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

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}&device_id={2}&", Username, FingerPrint.Value, Uri.EscapeDataString(deviceName)));

                // Merge all data.
                var finalData = new byte[formData.Length + formEncodedData.Length];
                Array.Copy(formData, finalData, formData.Length);
                Array.Copy(formEncodedData, 0, finalData, formData.Length, formEncodedData.Length);
                formData = finalData;

                // Don't forget to the set the content length to the total length of our form POST data!
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
                    var logger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(logger, webException);
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to serialize this instance, writing the result to a predetermined file. 
        /// </summary>
        /// <returns>
        /// True if this instance with required members was successfully serialized and the result
        /// was written to the file system. False otherwise.
        /// </returns>
        private bool Save()
        {
            // User isn't authenticated if we have no cookies. No cookies == no auth cookies.
            if(UserSessionCookies == null || UserSessionCookies.Count <= 0)
            {
                Debug.WriteLine("While saving, user session cookies was null or had zero entries.");
                return false;
            }

            if(!StringExtensions.Valid(Username))
            {
                Debug.WriteLine("While saving, username was null, empty or whitespace.");
                return false;
            }

            if(Password == null || Password.Length <= 0)
            {
                Debug.WriteLine("While saving, password was null or empty.");
                return false;
            }

            // Will contain decrypted password. Must be mopped up ASAP!
            byte[] tempDecryptedPassword = null;

            try
            {
                // Grab a decrypted copy of our encrypted password.
                tempDecryptedPassword = Password;

                // Create instance of internal serializable.
                var internalSerializable = new SerializableAuthenticatedUserModel();

                // Set props equal to this, but, encrypt password again. Just want to ensure entropy
                // etc is matched up. XXX TODO - Can probably go without
                internalSerializable.Username = Username;
                internalSerializable.EncryptedPassword = ProtectedData.Protect(tempDecryptedPassword, Entropy, DataProtectionScope.LocalMachine);
                internalSerializable.Cookies = UserSessionCookies.GetCookies(new Uri(ServiceProviderApiAuthPath)).OfType<Cookie>().ToList();
                internalSerializable.CSRFTokenString = CSRFToken;

                // Serialize and write to output stream.
                var serialized = JsonConvert.SerializeObject(internalSerializable, Formatting.Indented);

                var randomFileName = Directory.GetParent(m_savePath).FullName + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                File.WriteAllText(randomFileName, serialized);

                if(File.Exists(m_savePath))
                {
                    File.Replace(randomFileName, m_savePath, m_savePath + ".bak", true);
                }
                else
                {
                    File.Move(randomFileName, m_savePath);
                }

                return true;
            }
            catch(Exception err)
            {
                // Had an error. Attempt to log it.
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
            finally
            {
                // Ensure that we zero our local copy of the decrypted password, if it exists.
                if(tempDecryptedPassword != null && tempDecryptedPassword.Length > 0)
                {
                    Array.Clear(tempDecryptedPassword, 0, tempDecryptedPassword.Length);
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to read a serialized version of this model from the file system, and then
        /// populate the members of this instance with the values within the deserialized instance.
        /// </summary>
        /// <returns>
        /// True if we were able to restore state from the filesystem and required members in this
        /// instance were populated correctly. False otherwise.
        /// </returns>
        public bool LoadFromSave()
        {
            if(!File.Exists(m_savePath))
            {
                return false;
            }

            byte[] plaintext = null;

            try
            {
                var savedData = File.ReadAllText(m_savePath);
                var deserialized = JsonConvert.DeserializeObject<SerializableAuthenticatedUserModel>(savedData);

                if(deserialized == null || !deserialized.IsValid)
                {
                    return false;
                }

                plaintext = ProtectedData.Unprotect(deserialized.EncryptedPassword, Entropy, DataProtectionScope.LocalMachine);

                this.Username = deserialized.Username;
                this.Password = plaintext;

                this.CSRFToken = deserialized.CSRFTokenString;

                // Load up our cookies.
                var cookieCollection = new CookieContainer();
                var allCookies = deserialized.Cookies;
                foreach(var cookie in allCookies)
                {
                    cookieCollection.Add(cookie);
                }

                this.UserSessionCookies = cookieCollection;

                return true;
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
            finally
            {
                // Always clear our pword!!!!
                if(plaintext != null && plaintext.Length > 0)
                {
                    Array.Clear(plaintext, 0, plaintext.Length);
                }
            }

            return false;
        }
    }
}