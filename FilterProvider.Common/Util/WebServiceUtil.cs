/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CloudVeil;
using Filter.Platform.Common;
using Filter.Platform.Common.Data.Models;
using Filter.Platform.Common.Extensions;
using Filter.Platform.Common.Net;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;
using NLog;
using NodaTime;
using NodaTime.Text;

namespace FilterProvider.Common.Util
{
    internal enum ServiceResource
    {
        UserConfigSumCheck,
        UserConfigRequest,
        RuleDataSumCheck,
        RuleDataRequest,
        UserDataRequest,
        UserDataSumCheck,
        DeactivationRequest,
        UserTerms,
        GetToken,
        RevokeToken,
        RetrieveToken,
        ActiveateByEmail,
        BypassRequest,
        AccountabilityNotify,
        AddSelfModerationEntry,
        ServerTime,
        Custom
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
        private DateTime tokenRejectedFirstDateTime = DateTime.MinValue;
        private TimeSpan REJECTED_TIMEOUT = TimeSpan.FromHours(1);

        public event GenericWebServiceUtilDelegate AuthTokenRejected;

        private static readonly Dictionary<ServiceResource, string> m_namedResourceMap = new Dictionary<ServiceResource, string>
        {
            { ServiceResource.UserConfigRequest, "/api/v2/me/config/get" },
            { ServiceResource.UserConfigSumCheck, "/api/v2/me/config/check" },
            { ServiceResource.RuleDataRequest, "/api/v2/rules/get" },
            { ServiceResource.RuleDataSumCheck, "/api/v2/rules/check" },
            { ServiceResource.UserDataRequest, "/api/v2/me/data/get" },
            { ServiceResource.UserDataSumCheck, "/api/v2/me/data/check" },
            { ServiceResource.DeactivationRequest, "/api/v2/me/deactivate" },
            { ServiceResource.UserTerms, "/api/v2/me/terms" },
            { ServiceResource.GetToken, "/api/v2/user/gettoken" },
            { ServiceResource.RevokeToken, "/api/v2/me/revoketoken" },
            { ServiceResource.RetrieveToken, "/api/v2/user/retrievetoken" },
            { ServiceResource.ActiveateByEmail, "/api/v2/user/activation/email" },
            { ServiceResource.BypassRequest, "/api/v2/me/bypass" },
            { ServiceResource.AccountabilityNotify, "/api/v2/me/accountability" },
            { ServiceResource.AddSelfModerationEntry, "/api/v2/me/self_moderation/add" },
            { ServiceResource.ServerTime, "/api/v2/time" }
        };

        private readonly Logger m_logger;

        private IAuthenticationStorage m_authStorage;

        public string AuthToken
        {
            get
            {
                return m_authStorage.AuthToken;
            }

            set
            {
                m_authStorage.AuthToken = value;
            }
        }

        public string UserEmail
        {
            get
            {
                return m_authStorage.UserEmail;
            }

            set
            {
                m_authStorage.UserEmail = value;
            }
        }

        public string ServiceProviderApiPath
            => CloudVeil.CompileSecrets.ServiceProviderApiPath;

        public string ServiceProviderUnblockRequestPath
            => CloudVeil.CompileSecrets.ServiceProviderUnblockRequestPath;

        public bool HasStoredCredentials
        {
            get
            {
                return StringExtensions.Valid(WebServiceUtil.Default.AuthToken);
            }
        }

        private static WebServiceUtil s_instance;
        private static NLog.Logger s_logger;

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
            m_authStorage = PlatformTypes.New<IAuthenticationStorage>();
        }

        public AuthenticationResultObject AuthenticateByPassword(string username, byte[] unencryptedPassword)
        {
            m_logger.Error(nameof(AuthenticateByPassword));

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

            HttpWebRequest authRequest = null;
            try
            {
                authRequest = GetApiBaseRequest(m_namedResourceMap[ServiceResource.GetToken], new ResourceOptions());

                // Build out username and password as post form data. We need to ensure that we mop
                // up any decrypted forms of our password when we're done, and ASAP.                
                formData = System.Text.Encoding.UTF8.GetBytes(string.Format("email={0}&identifier={1}&device_id={2}&device_id_2={3}&identifier_2={4}", username, FingerprintService.Default.Value, deviceName, m_authStorage.DeviceId, m_authStorage.AuthId));

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
                            WebServiceUtil.Default.AuthToken = tr.ReadToEnd();
                            WebServiceUtil.Default.UserEmail = username;
                        }

                        response.Close();
                        authRequest.Abort();
                        ret.AuthenticationResult = AuthenticationResult.Success;

                        m_authStorage.DeviceId = deviceName;
                        m_authStorage.AuthId = FingerprintService.Default.Value;
                        return ret;
                    }
                    else
                    {
                        if(code == 401 || code == 403)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            WebServiceUtil.Default.AuthToken = string.Empty;
                            ret.AuthenticationResult = AuthenticationResult.Failure;
                            return ret;
                        }
                        else if(code > 399 && code < 499)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
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

                        if (code == 401 || code == 403)
                        {
                            AuthToken = string.Empty;
                        }

                        if (code > 399 && code < 499)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            m_logger.Info("Athentication failure text: {0}", errorText);
                            ret.AuthenticationMessage = errorText;
                            ret.AuthenticationResult = AuthenticationResult.Failure;
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

                m_logger.Info("Authentication failed due to a failure to process the request and response. Attempted URL {0}", authRequest?.RequestUri);
                WebServiceUtil.Default.AuthToken = string.Empty;
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

            m_logger.Info("Authentication failed due to a complete failure to process the request and response. Past-catch attempted url {0}", authRequest?.RequestUri);

            // If we had success, we should/would have returned by now.
            if(!connectionFailure)
            {
                WebServiceUtil.Default.AuthToken = string.Empty;
            }

            ret.AuthenticationResult = connectionFailure ? AuthenticationResult.ConnectionFailed : AuthenticationResult.Failure;
            return ret;

        }
        public AuthenticationResultObject AuthenticateByEmail(string email)
        {
            m_logger.Error(nameof(AuthenticateByEmail));

            AuthenticationResultObject ret = new AuthenticationResultObject();

            // Enforce parameters are valid.
            Debug.Assert(StringExtensions.Valid(email));

            if (!StringExtensions.Valid(email))
            {
                throw new ArgumentException("Supplied username cannot be null, empty or whitespace.", nameof(email));
            }

            // Don't bother if we don't have internet.
            var hasInternet = NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection;
            if (hasInternet == false)
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

            HttpWebRequest authRequest = null;
            try
            {
                authRequest = GetApiBaseRequest(m_namedResourceMap[ServiceResource.ActiveateByEmail], new ResourceOptions());

                // Build out username and password as post form data. We need to ensure that we mop
                // up any decrypted forms of our password when we're done, and ASAP.                
                formData = System.Text.Encoding.UTF8.GetBytes(string.Format("email={0}&identifier={1}&device_id={2}&device_id_2={3}&identifier_2={4}", email, FingerprintService.Default.Value, deviceName, m_authStorage.DeviceId, m_authStorage.AuthId));

                // Don't forget to the set the content length to the total length of our form POST data!
                authRequest.ContentLength = formData.Length;

                authRequest.ContentType = "application/x-www-form-urlencoded";

                // Grab the request stream so we can POST our login form data to it.
                using (var requestStream = authRequest.GetRequestStream())
                {
                    // Write and close.
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using (var response = (HttpWebResponse)authRequest.GetResponse())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    // Check if the response status code is outside the "success" range of codes
                    // defined in HTTP. If so, we failed. We include redirect codes (3XX) as success,
                    // since our server side will just redirect us if we're already authed.
                    if (code > 199 && code < 299)
                    {
                        response.Close();
                        authRequest.Abort();
                        ret.AuthenticationResult = AuthenticationResult.Success;

                        m_authStorage.DeviceId = deviceName;
                        m_authStorage.AuthId = FingerprintService.Default.Value;
                        return ret;
                    }
                    else
                    {
                        if (code == 401 || code == 403)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            WebServiceUtil.Default.AuthToken = string.Empty;
                            ret.AuthenticationResult = AuthenticationResult.Failure;
                            return ret;
                        }
                        else if (code > 399 && code < 499)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            ret.AuthenticationResult = AuthenticationResult.Failure;
                            return ret;
                        }
                    }
                }
            }
            catch (WebException e)
            {
                // XXX TODO - Is this sufficient?
                if (e.Status == WebExceptionStatus.Timeout)
                {
                    m_logger.Info("Authentication failed due to timeout.");
                    connectionFailure = true;
                }

                try
                {
                    using (WebResponse response = e.Response)
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

                        if (code == 401 || code == 403)
                        {
                            AuthToken = string.Empty;
                        }

                        if (code > 399 && code < 499)
                        {
                            m_logger.Info("Authentication failed with code: {0}.", code);
                            m_logger.Info("Athentication failure text: {0}", errorText);
                            ret.AuthenticationMessage = errorText;
                            ret.AuthenticationResult = AuthenticationResult.Failure;
                            return ret;
                        }


                    }
                }
                catch (Exception iex)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, iex);
                }

                // Log the exception.
                m_logger.Error(e.Message);
                m_logger.Error(e.StackTrace);
            }
            catch (Exception e)
            {
                while (e != null)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                    e = e.InnerException;
                }

                m_logger.Info("Authentication failed due to a failure to process the request and response. Attempted URL {0}", authRequest?.RequestUri);
                WebServiceUtil.Default.AuthToken = string.Empty;
                ret.AuthenticationResult = AuthenticationResult.Failure;
                return ret;
            }
            finally
            {
                if (formData != null && formData.Length > 0)
                {
                    Array.Clear(formData, 0, formData.Length);
                }
            }

            m_logger.Info("Authentication failed due to a complete failure to process the request and response. Past-catch attempted url {0}", authRequest?.RequestUri);

            // If we had success, we should/would have returned by now.
            if (!connectionFailure)
            {
                WebServiceUtil.Default.AuthToken = string.Empty;
            }

            ret.AuthenticationResult = connectionFailure ? AuthenticationResult.ConnectionFailed : AuthenticationResult.Failure;
            return ret;
        }


        public static bool ValidiateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {            
            if (sslpolicyerrors != SslPolicyErrors.None)
            {
                return false;
            }
            if(CompileSecrets.PinnedCertKey.Length == 0)
            {
                LoggerUtil.GetAppWideLogger().Info("Cert pinning is disabled");
                return true;
            }


            LoggerUtil.GetAppWideLogger().Info("Cert " + certificate.Subject + " " + certificate.GetExpirationDateString());
            string publicKey = Convert.ToBase64String(certificate.GetPublicKey());
            LoggerUtil.GetAppWideLogger().Info("Key is " + publicKey);
            if (publicKey.Equals(CompileSecrets.PinnedCertKey))
            {
                LoggerUtil.GetAppWideLogger().Info("Keys match");
                return true;
            }
            else
            {
                LoggerUtil.GetAppWideLogger().Info("Keys don't match. Skipping request");
            }
                           
            return false;
        }

        /// <summary>
        /// Builds out an HttpWebRequest specifically configured for use with the service provider API. н
        /// </summary>
        /// <param name="route">
        /// The route for the request. 
        /// </param>
        /// <returns>
        /// The configured request. 
        /// </returns>
        private HttpWebRequest GetApiBaseRequest(string route, ResourceOptions options, string baseRoute = null)
        {
            baseRoute = baseRoute == null ? ServiceProviderApiPath : baseRoute;
            var requestString = baseRoute + route;
            var requestRoute = new Uri(requestString);
            
            
            // Create a new request. We don't want auto redirect, we don't want the subsystem trying
            // to look up proxy information to configure on our request, we want a 5 second timeout
            // on any and all operations and we want to look like Firefox in a generic way. Here we
            // also set the cookie container, so we can capture session cookies if we're successful.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestRoute);
            
            //Pin public key to avoid MITM attack
            request.ServerCertificateValidationCallback = ValidiateCertificate;

            request.Method = options.Method; //"POST";
            request.Proxy = null;
            request.AllowAutoRedirect = false;
            request.UseDefaultCredentials = false;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.ContentType = options.ContentType; // "application/x-www-form-urlencoded";
            request.UserAgent = "Mozilla/5.0 (Windows NT x.y; rv:10.0) Gecko/20100101 Firefox/10.0";
            request.Accept = "application/json,text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

   //	request.Proxy = new WebProxy("127.0.0.1:8888", false);       
            
            if (options.ETag != null)
            {
                request.Headers.Add("ETag", options.ETag);
            }

            return request;
        }

        /// <summary>
        /// Request a generic resource from the service server(s).
        /// This does not include the responseReceived out variable in the parameter list.
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="code"></param>
        /// <param name="noLogging"></param>
        /// <returns></returns>
        public byte[] RequestResource(ServiceResource resource, out HttpStatusCode code, Dictionary<string, object> parameters = null, bool noLogging = false)
        {
            bool responseReceived = false;

            ResourceOptions options = new ResourceOptions();
            options.Parameters = parameters;
            options.NoLogging = noLogging;

            return RequestResource(resource, out code, out responseReceived, options);
        }

        public Dictionary<string, bool?> VerifyLists(Dictionary<string, string> hashes)
        {
            Dictionary<string, object> hashesDict = new Dictionary<string, object>();
            foreach(var hash in hashes)
            {
                hashesDict.Add(hash.Key, hash.Value);
            }

            ResourceOptions options = new ResourceOptions()
            {
                ContentType = "application/json",
                Parameters = hashesDict
            };

            HttpStatusCode code;
            bool responseReceived;

            byte[] ret = RequestResource(ServiceResource.RuleDataSumCheck, out code, out responseReceived, options);

            if(ret == null)
            {
                m_logger.Warn("No response text returned from {0}. Status code = {1}, Response received = {2}", m_namedResourceMap[ServiceResource.RuleDataSumCheck], code, responseReceived);
                return null;
            }

            Dictionary<string, bool?> responseDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, bool?>>(Encoding.UTF8.GetString(ret));

            return responseDict;
        }

        public byte[] GetFilterLists(List<FilteringPlainTextListModel> toFetch, out HttpStatusCode code, out bool responseReceived)
        {
            List<string> paths = toFetch.Select(t => t.RelativeListPath).ToList();
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("lists", paths);

            byte[] ret = RequestResource($"/api/v2/rules/get", out code, out responseReceived, new ResourceOptions()
            {
                Method = "POST",
                ContentType = "application/json",

                Parameters = parameters
            });

            if (!responseReceived) { return null; }
            if ((int)code < 200 || (int)code > 399) { return null; }

            return ret;
        }

        public byte[] GetFilterList(string @namespace, string category, string type, out HttpStatusCode code, out bool responseReceived, string sha1 = null)
        {
            byte[] ret = RequestResource($"/api/v2/rules/{@namespace}/{category}/{type}.txt", out code, out responseReceived, new ResourceOptions()
            {
                Method = "GET",
                ETag = sha1
            });

            // FIXME: First adult_abortion request_resource isn't working.
            // TODO: Output to console and see if ret is null or not null.

            if(!responseReceived)
            {
                return null;
            }

            if((int) code < 200 || (int)code > 399)
            {
                return null;
            }

            return ret;
        }

        public class ResourceOptions
        {
            public string Method { get; set; } = "POST";

            public string ETag { get; set; } = null;

            public Dictionary<string, object> Parameters { get; set; } = null;

            public string ContentType { get; set; } = "application/x-www-form-urlencoded";

            public bool NoLogging { get; set; } = false;
        }

        public byte[] RequestResource(ServiceResource resource, out HttpStatusCode code, out bool responseReceived, ResourceOptions options = null)
        {
            return RequestResource(m_namedResourceMap[resource], out code, out responseReceived, options, resource);
        }

        /// <summary>
        /// Request a generic resource from the service server(s). 
        /// </summary>
        /// <param name="route">
        /// The API route to make the request to. 
        /// </param>
        /// <param name="responseReceived">
        /// Gets set to false if no response was received, otherwise false.
        /// </param>
        /// <param name="noLogging">
        /// Whether or not to log errors. Since HttpWebRequest brilliant throws exceptions for
        /// non-success HTTP status codes, it's nice to be able to control whether or not your
        /// request should have errors logged.
        /// </param>
        /// <returns>
        /// A non-null byte array on success. Null byte array on failure. 
        /// </returns>
        public byte[] RequestResource(string resourceUri, out HttpStatusCode code, out bool responseReceived, ResourceOptions options = null, ServiceResource resource = ServiceResource.Custom)
        {
            if(options == null)
            {
                options = new ResourceOptions(); // Instantiate a resource options object with default options.
            }

            responseReceived = true;
            Dictionary<string, object> parameters = new Dictionary<string, object>();

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

                var accessToken = WebServiceUtil.Default.AuthToken;

                //m_logger.Info("RequestResource1: accessToken=" + accessToken);
                IVersionProvider versionProvider = PlatformTypes.New<IVersionProvider>();
                string version = versionProvider.GetApplicationVersion().ToString(3);

                // Build out post data with username and identifier.
                parameters.Add("identifier", FingerprintService.Default.Value);
                parameters.Add("device_id", deviceName);
                parameters.Add("identifier_2", m_authStorage.AuthId);
                parameters.Add("device_id_2", m_authStorage.DeviceId);
                parameters.Add("os", "WIN");                

                string postString = null;
                //string postString = string.Format("&identifier={0}&device_id={1}", FingerprintService.Default.Value, Uri.EscapeDataString(deviceName));

                if (options.Parameters != null)
                {
                    foreach (var parameter in options.Parameters)
                    {
                        parameters.Add(parameter.Key, parameter.Value);
                    }
                }

                if (resource == ServiceResource.UserDataSumCheck || resource == ServiceResource.UserConfigSumCheck)
                {
                    m_logger.Info("Sending version {0} to server", version);
                    parameters.Add("app_version", version);
                }

                switch(options.ContentType)
                {
                    case "application/x-www-form-urlencoded":
                        postString = string.Join("&", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
                        break;

                    case "application/json":
                        postString = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                        break;
                }

                if (options.Method == "GET" || options.Method == "DELETE")
                {
                    resourceUri += "?" + postString;

                    if(postString.Contains("app_version"))
                    {
                        m_logger.Info("Sending postString as {0}", resourceUri);
                    }
                }

                var request = GetApiBaseRequest(resourceUri, options);

                m_logger.Debug("WebServiceUtil.Request {0}", request.RequestUri);

                if (StringExtensions.Valid(accessToken))
                {
                    request.Headers.Add("Authorization", string.Format("Bearer {0}", accessToken));
                }
                else if (resource != ServiceResource.RetrieveToken)
                {
                    m_logger.Info("RequestResource1: Authorization failed.");

                    reportTokenRejected();
                    code = HttpStatusCode.Unauthorized;
                    return null;
                }

                if (options.Method != "GET" && options.Method != "DELETE")
                {
                    if (postString.Contains("app_version"))
                    {
                        m_logger.Info("Sending {0} to server as {1}", postString, options.Method);
                    }

                    var formData = System.Text.Encoding.UTF8.GetBytes(postString);
                    request.ContentLength = formData.Length;

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(formData, 0, formData.Length);
                        requestStream.Close();
                    }
                }

                m_logger.Info("RequestResource: uri={0}", request.RequestUri);
                
                // Now that our login form data has been POST'ed, get a response.
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // Get the response code as an int so we can range check it.
                    var intCode = (int)response.StatusCode;

                    code = (HttpStatusCode)intCode;

                    try
                    {
                        // Check if response code is considered a success code.
                        if (intCode >= 200 && intCode <= 299)
                        {
                            m_authStorage.DeviceId = deviceName;
                            m_authStorage.AuthId = FingerprintService.Default.Value;

                            using (var memoryStream = new MemoryStream())
                            {
                                response.GetResponseStream().CopyTo(memoryStream);

                                // We do this just in case we get something like a 204. The idea here
                                // is that if we return a non-null, the call was a success.
                                var responseBody = memoryStream.ToArray();
                                if (responseBody == null || intCode == 204)
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

                reportTokenAccepted();
            }
            catch(WebException e)
            {
                // KF - Set this to 0 for default. 0's a pretty good indicator of no internet.
                code = 0;

                try
                {
                    using(WebResponse response = e.Response)
                    {
                        if(response == null)
                        {
                            responseReceived = false;
                            return null;
                        }
                        HttpWebResponse httpResponse = (HttpWebResponse)response;
                        m_logger.Error("Error code: {0}", httpResponse.StatusCode);

                        int intCode = (int)httpResponse.StatusCode;

                        code = (HttpStatusCode)intCode;

                        // Auth failure means re-log EXCEPT when requesting deactivation.
                        if((intCode == 401 || intCode == 403) && resource != ServiceResource.DeactivationRequest)
                        {
                            WebServiceUtil.Default.AuthToken = string.Empty;
                            m_logger.Info("RequestResource2: Authorization failed.");
                            reportTokenRejected();
                        }
                        else if(intCode > 399 && intCode <= 499 && resource != ServiceResource.DeactivationRequest)
                        {
                            reportTokenAccepted();
                            m_logger.Info("Error occurred in RequestResource: {0}", intCode);
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

                if(!options.NoLogging)
                {
                    m_logger.Error(e.Message);
                    m_logger.Error(e.StackTrace);
                }
            }
            catch(Exception e)
            {
                // XXX TODO - Good default?
                code = HttpStatusCode.InternalServerError;

                if(!options.NoLogging)
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

        public ZonedDateTime? GetServerTime()
        {
            HttpStatusCode statusCode;
            bool responseReceived;

            byte[] response = RequestResource(ServiceResource.ServerTime, out statusCode, out responseReceived, new ResourceOptions()
            {
                Method = "GET"
            });

            if(!responseReceived || statusCode != HttpStatusCode.OK)
            {
                return null;
            }

            string timeString = Encoding.UTF8.GetString(response);

            ParseResult<ZonedDateTime> result = ZonedDateTimePattern.GeneralFormatOnlyIso.Parse(timeString);

            if (result.Success)
            {
                return result.Value;
            }
            else
            {
                return null;
            }
        }

        private void reportTokenRejected()
        {
            if (tokenRejectedFirstDateTime == DateTime.MinValue)
            {
                tokenRejectedFirstDateTime = DateTime.Now;
            } 
            else if(DateTime.Now - tokenRejectedFirstDateTime > REJECTED_TIMEOUT)
            {
                AuthTokenRejected?.Invoke();
            }
        }

        private void reportTokenAccepted()
        {
            tokenRejectedFirstDateTime = DateTime.MinValue;
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

                var request = GetApiBaseRequest(m_namedResourceMap[resource], new ResourceOptions());

                var accessToken = WebServiceUtil.Default.AuthToken;

                if(StringExtensions.Valid(accessToken))
                {
                    request.Headers.Add("Authorization", string.Format("Bearer {0}", accessToken));
                }
                else
                {
                    m_logger.Info("SendResource1: Authorization failed.");
                    reportTokenRejected();
                    code = HttpStatusCode.Unauthorized;
                    return false;
                }

                // Build out post data with username and identifier.
                var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("identifier={0}&device_id={1}", FingerprintService.Default.Value, Uri.EscapeDataString(deviceName)));

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

                reportTokenAccepted();
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

                        if(intCode == 401 || intCode == 403)
                        {
                            WebServiceUtil.Default.AuthToken = string.Empty;
                            m_logger.Info("SendResource2: Authorization failed.");
                            reportTokenRejected();
                        }
                        else if(intCode > 399 && intCode < 499)
                        {
                            reportTokenAccepted();
                            m_logger.Info("SendResource2: Failed with client code {0}", intCode);
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