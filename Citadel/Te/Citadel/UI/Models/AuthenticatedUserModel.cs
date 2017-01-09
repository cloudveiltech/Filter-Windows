using GalaSoft.MvvmLight;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Te.Citadel.Extensions;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class AuthenticatedUserModel : ObservableObject
    {
        /// <summary>
        /// This class is used to make persisting the state of the larger and more complex
        /// AuthenticatedUserModel class easier.
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
            public string CookieString
            {
                get;
                set;
            }

            [JsonProperty]
            public bool Accepted
            {
                get;
                set;
            }

            [JsonProperty]
            public Uri AuthRoute
            {
                get;
                set;
            }
        }

        /// <summary>
        /// Singleton because we love anti-patterns. Seriously though there should only ever be one
        /// authenticated user at any given time, it needs to be accessible from many places and this
        /// is the easiest way to accomplish that.
        /// </summary>
        private static AuthenticatedUserModel s_instance;

        /// <summary>
        /// Gets the single auth user instance.
        /// </summary>
        public static AuthenticatedUserModel Instance
        {
            get
            {
                if(s_instance == null)
                {
                    s_instance = new AuthenticatedUserModel();
                }
                return s_instance;
            }
        }

        /// <summary>
        /// Destroys the current saved user, which will lead to forced re-authentication.
        /// </summary>
        public static void Destroy()
        {
            if(s_instance != null && File.Exists(s_instance.m_savePath))
            {
                File.Delete(s_instance.m_savePath);
            }

            s_instance = new AuthenticatedUserModel();
        }

        /// <summary>
        /// Remember if the user has accepted the terms after authenticating.
        /// </summary>
        private volatile bool m_termsAccepted;

        /// <summary>
        /// Path where we'll persist this object on the file system.
        /// </summary>
        private readonly string m_savePath;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Holds the entropy used to encrypt the last password used in a successful auth request.
        /// </summary>
        private byte[] m_entropy;

        /// <summary>
        /// Holds an encrypted version of the last password used in a successful auth request.
        /// </summary>
        private byte[] m_passwordEncrypted;

        /// <summary>
        /// Holds the last username used in a successful auth request.
        /// </summary>
        private string m_username;

        /// <summary>
        /// Holds the URI of the last successful authentication request.
        /// </summary>
        private Uri m_lastAuthRoute;

        /// <summary>
        /// Container supplied to HttpRequest and friends during auth requests. This container should
        /// be populated with session cookies that we'll store and persist for the purpose of future
        /// requests to restricted material.
        /// </summary>
        private CookieContainer m_userSessionCookies;

        /// <summary>
        /// Used in the Entropy property getter to enforce synchronization.
        /// </summary>
        private object m_entropyLockObject;

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
            /// Indicates that, during the auth request, a connection using the provided URI could
            /// not be established.
            /// </summary>
            ConnectionFailed
        }

        /// <summary>
        /// Indicates whether or not the user has an active session as the result of a successful
        /// authentication request. This internally counts the saved set cookies.
        /// </summary>
        public bool HasStoredSession
        {
            get
            {
                var cookies = UserSessionCookies;

                return (cookies != null && cookies.Count > 0);
            }
        }

        /// <summary>
        /// Gets or sets whether or not the user has accepted the terms. Since the user is presented
        /// with terms AFTER authenticating, we shouldn't just assume that successful auth means 100%
        /// authenticated. The terms need to be accepted, so we store that information as well.
        /// </summary>
        public bool HasAcceptedTerms
        {
            get
            {
                return m_termsAccepted;
            }

            set
            {
                m_termsAccepted = value;

                if(m_termsAccepted)
                {
                    // Just save again whenever terms are accepted.
                    Save();
                }
            }
        }

        /// <summary>
        /// Gets or sets the URI used for the last successful authentication request. Entirely
        /// private, used internally. A property is used just to bind some logic to access.
        /// </summary>
        public Uri AuthRoute
        {
            get
            {
                return m_lastAuthRoute;
            }

            private set
            {
                Debug.Assert(value != null);

                if(value == null)
                {
                    throw new ArgumentException("Argument cannot be null.", nameof(AuthRoute));
                }

                var isHttp = value.Scheme.OIEquals(Uri.UriSchemeHttp);
                var isHttps = value.Scheme.OIEquals(Uri.UriSchemeHttps);

                // Enforce that we're encrypting when not in debug.
#if !CITADEL_DEBUG
                Debug.Assert(isHttps == true);
#else
                Debug.Assert(isHttp == true || isHttps == true);
#endif

                // Enforce that we're encrypting when not in debug.
#if !CITADEL_DEBUG
                if (!isHttps)
#else
                if(!isHttp && !isHttps)
#endif
                {
                    throw new ArgumentException("URI scheme not supported.", nameof(AuthRoute));
                }

                m_lastAuthRoute = value;
            }
        }

        /// <summary>
        /// Gets and privately the last username used in a successful auth request.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// When the setter is called, the string must not be null, empty or whitespace. If it is,
        /// the setter will throw.
        /// </exception>
        public string Username
        {
            get
            {
                return m_username;
            }

            private set
            {
                // Ensure this is a valid string.
                Debug.Assert(StringExtensions.Valid(value));

                if(!StringExtensions.Valid(value))
                {
                    throw new ArgumentException("Expected valid, non-empty, non-whitespace string.", nameof(Username));
                }

                m_username = value;
            }
        }

        /// <summary>
        /// Gets a decrypted copy of the password bytes on-demand. The returned byte array should be
        /// zeroed ASAP.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// In the private setter, the supplied byte array cannot be null and must have a length
        /// greater then zero. In the event that these conditions are not met, the setter will throw.
        /// </exception>
        /// <remarks>
        /// When the private setter is called, the data is immediately encrypted and the supplied
        /// unencrypted array is zeroed.
        /// </remarks>
        public byte[] Password
        {
            get
            {
                // Create a decrypted copy and return it. Should be destroyed ASAP.
                byte[] plaintext = ProtectedData.Unprotect(m_passwordEncrypted, Entropy, DataProtectionScope.CurrentUser);
                return plaintext;
            }

            private set
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

                m_passwordEncrypted = ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser);

                // Clear out the unencrypted password bytes ASAP.
                Array.Clear(value, 0, value.Length);
            }
        }

        /// <summary>
        /// Get and privately sets the cookie container used in a successful auth request. Such a
        /// cookie container would/should contain any and all cookies required to persist the session
        /// across multiple requests.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// When being set, if the supplied container is null, the setter will throw. If the supplied
        /// container has no cookie entries, the setter will throw.
        /// </exception>
        public CookieContainer UserSessionCookies
        {
            get
            {
                return m_userSessionCookies;
            }

            private set
            {
                Debug.Assert(value != null && value.Count > 0);

                if(value == null)
                {
                    throw new ArgumentException("Expected valid HttpCookieCollection instance.", nameof(UserSessionCookies));
                }

                if(value.Count <= 0)
                {
                    throw new ArgumentException("Supplied collection contains no entries.", nameof(UserSessionCookies));
                }

                m_userSessionCookies = value;
            }
        }

        /// <summary>
        /// Gets the Entropy bytes we supply during the protection/unprotection of the password
        /// member.
        /// </summary>
        private byte[] Entropy
        {
            get
            {
                // Enforce rigid synchronization.
                lock(m_entropyLockObject)
                {
                    // This is a random key name we're going to use. This key will have the entropy
                    // written to it in the registry.
                    const string keyName = "9e5f71f5-d68d-4349-8683-9910a54bc066";

                    // Get the name of our process, aka the Executable name.
                    var applicationNiceName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

                    // Open the CURRENT_USER\SYSTEM sub key for read/write.
                    RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);

                    // Create or open our application's key.
                    key.CreateSubKey(applicationNiceName);
                    key = key.OpenSubKey(applicationNiceName, true);

                    // Come up with a default value to supply to the registry value fetch function.
                    byte[] doesntExist = new byte[] { (byte)'n', (byte)'o', (byte)'p', (byte)'e' };
                    m_entropy = (byte[])key.GetValue(keyName, doesntExist);

                    // If our entropy member is equal to our doesntExist value, then the key has not
                    // yet been set and we need to generate some new entropy, store it, and then
                    // return it.
                    if(m_entropy.Length == 0 || (m_entropy.Length == doesntExist.Length && m_entropy == doesntExist))
                    {
                        // Doesn't exist, so create it.
                        m_entropy = new byte[20];

                        using(RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                        {
                            rng.GetBytes(m_entropy);
                        }

                        // Update the registry key, so get this value back later.
                        key.SetValue(keyName, m_entropy, RegistryValueKind.Binary);
                    }

                    return m_entropy;
                }
            }
        }

        /// <summary>
        /// Default ctor.
        /// </summary>
        private AuthenticatedUserModel()
        {
            m_savePath = AppDomain.CurrentDomain.BaseDirectory + "u.dat";
            m_logger = LogManager.GetLogger("Citadel");
            m_entropyLockObject = new object();
            m_username = string.Empty;
        }

        /// <summary>
        /// </summary>
        /// <param name="username">
        /// </param>
        /// <param name="unencryptedPassword">
        /// </param>
        /// <param name="authRoute">
        /// </param>
        /// <returns>
        /// </returns>
        public async Task<AuthenticationResult> Authenticate(string username, byte[] unencryptedPassword, Uri authRoute)
        {
            // Disable forcing endpoint to be HTTPS when beta testing.
#if !CITADEL_DEBUG
            Debug.Assert(authRoute.Scheme.OIEquals(Uri.UriSchemeHttps));

            if (!authRoute.Scheme.OIEquals(Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Supplied auth route URI does not use the HTTPS scheme. Cannot authenticate over non-HTTPS connection.", nameof(authRoute));
            }
#endif
            
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
            var hasInternet = await WebServiceUtil.GetHasInternetServiceAsync();
            if(hasInternet == false)
            {
                return AuthenticationResult.ConnectionFailed;
            }

            // Will be saved if we get a success result.
            var sessionCookieContainer = new CookieContainer();

            // Will be set if we get any sort of web exception.
            bool connectionFailure = false;

            // Where the post variables will be written as bytes. This also needs to be cleaned up
            // and ASAP, since it will contain the user's password in a decrypted state.
            byte[] finalPostPayload = null;

            try
            {
                // Create a new request. We don't want auto redirect, we don't want the subsystem
                // trying to look up proxy information to configure on our request, we want a 5
                // second timeout on any and all operations and we want to look like Firefox in a
                // generic way. Here we also set the cookie container, so we can capture session
                // cookies if we're successful.
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(authRoute);
                request.Method = "POST";
                request.Proxy = null;
                request.AllowAutoRedirect = false;
                request.UseDefaultCredentials = false;
                request.Timeout = 5000;
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
                request.ReadWriteTimeout = 5000;
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (Windows NT x.y; rv:10.0) Gecko/20100101 Firefox/10.0";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                request.CookieContainer = sessionCookieContainer;

                // Build out username and password as post form data. We need to ensure that we mop
                // up any decrypted forms of our password when we're done, and ASAP.
                var formDataStart = System.Text.Encoding.UTF8.GetBytes(string.Format("user_id={0}&identifier={1}&user_password=", username, FingerPrint.Value));
                finalPostPayload = new byte[formDataStart.Length + unencryptedPassword.Length];

                // Here we copy the byte range of the unencrypted password, in order to avoid having
                // this value held in a String object, which will linger around in memory
                // indefinitely, exposing our secrets to the whole world.
                Array.Copy(formDataStart, finalPostPayload, formDataStart.Length);
                Array.Copy(unencryptedPassword, 0, finalPostPayload, formDataStart.Length, unencryptedPassword.Length);

                // Don't forget to the set the content length to the total length of our form POST
                // data!
                request.ContentLength = finalPostPayload.Length;

                // Grab the request stream so we can POST our login form data to it.
                using(var requestStream = await request.GetRequestStreamAsync())
                {
                    // Write and close.
                    await requestStream.WriteAsync(finalPostPayload, 0, finalPostPayload.Length);
                    requestStream.Close();
                }

                // Now that our login form data has been POST'ed, get a response.
                using(var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    // Get the response code as an int so we can range check it.
                    var code = (int)response.StatusCode;

                    response.Close();
                    request.Abort();

                    // Check if the response status code is outside the "success" range of codes
                    // defined in HTTP. If so, we failed.
                    if(code >= 200 && code <= 299)
                    {
                        this.Username = username;
                        this.Password = unencryptedPassword;

                        // We have to do this crazy nonsense with cookies, otherwise
                        // .NET will assume a WHOLE BUNCH of about the paths and such
                        // of cookies and will not send them correctly.
                        var newCookieContainer = new CookieContainer();
                        foreach(Cookie cookie in response.Cookies)
                        {
                            cookie.Domain = authRoute.Host;
                            cookie.Path = string.Empty;
                            newCookieContainer.Add(cookie);
                        }

                        this.UserSessionCookies = newCookieContainer;
                        this.AuthRoute = authRoute;
                    }

                    // Ensure that we actually have a code that is within the "success" range of
                    // codes define in HTTP before we call this a true success.
                    if(code >= 200 && code <= 299)
                    {
                        // Just save these credentials automatically any time that we have a
                        // successfull auth.
                        Save();
                        return AuthenticationResult.Success;
                    }
                }
            }
            catch(Exception e)
            {
                connectionFailure = true;

                // Log the exception.
                LoggerUtil.RecursivelyLogException(m_logger, e);

                if(e is WebException)
                {
                    var ewx = e as WebException;

                    if(ewx != null)
                    {
                        if(ewx.Status == WebExceptionStatus.ProtocolError)
                        {
                            var response = ewx.Response as HttpWebResponse;

                            if(response != null)
                            {
                                var statusAsInt = (int)response.StatusCode;
                                if(statusAsInt > 399 && statusAsInt < 499)
                                {
                                    // Refused auth.
                                    return AuthenticationResult.Failure;
                                }
                            }

                            return AuthenticationResult.ConnectionFailed;
                        }
                    }
                }   
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

        /// <summary>
        /// Attempts to re-authenticate with the saved route, username and password.
        /// </summary>
        /// <returns>
        /// An AuthenticationResult enum value indicated the result of the operation.
        /// </returns>
        public async Task<AuthenticationResult> ReAuthenticate()
        {
            var route = this.AuthRoute;

            if(route != null)
            {
                return await ReAuthenticate(route);
            }

            return AuthenticationResult.Failure;
        }

        /// <summary>
        /// Attempts to ReAuthenticate with the existing credentials.
        /// </summary>
        /// <param name="authRoute">
        /// The URI to direct the authentication request to.
        /// </param>
        /// <returns>
        /// True if re-authentication was a success, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// In the event that the supplied Uri is not HTTPS, this method will throw.
        /// </exception>
        public async Task<AuthenticationResult> ReAuthenticate(Uri authRoute)
        {
            var user = Username;
            var password = Password;

            try
            {
                var result = await Authenticate(user, password, authRoute);

                return result;
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
                plaintext = ProtectedData.Unprotect(deserialized.EncryptedPassword, Entropy, DataProtectionScope.CurrentUser);
                this.Username = deserialized.Username;
                this.Password = plaintext;
                this.AuthRoute = deserialized.AuthRoute;

                // Set using private member, because public member calls
                // save again.
                this.m_termsAccepted = deserialized.Accepted;

                // We have to do this crazy nonsense with cookies, otherwise
                // .NET will assume a WHOLE BUNCH of about the paths and such
                // of cookies and will not send them correctly.
                var cookieCollection = new CookieContainer();
                var allCookies = deserialized.CookieString.Split(',');
                foreach(var cookieString in allCookies)
                {
                    var split = cookieString.Split('=');

                    if(split.Length == 2)
                    {
                        var newCookie = new Cookie(split[0], split[1]);
                        newCookie.Domain = deserialized.AuthRoute.Host;
                        newCookie.Path = string.Empty;
                        cookieCollection.Add(newCookie);
                    }
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

        /// <summary>
        /// Attempts to serialize this instance, writing the result to a predetermined file.
        /// </summary>
        /// <returns>
        /// True if this instance with required members was successfully serialized and the result
        /// was written to the file system. False otherwise.
        /// </returns>
        public bool Save()
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
                tempDecryptedPassword = this.Password;

                // Create instance of internal serializable.
                var internalSerializable = new SerializableAuthenticatedUserModel();

                // Set props equal to this, but, encrypt password again. Just want to ensure entropy
                // etc is matched up. XXX TODO - Can probably go without this.
                internalSerializable.Username = this.Username;
                internalSerializable.Accepted = this.HasAcceptedTerms;
                internalSerializable.EncryptedPassword = ProtectedData.Protect(tempDecryptedPassword, Entropy, DataProtectionScope.CurrentUser);
                internalSerializable.CookieString = this.UserSessionCookies.GetCookieHeader(this.AuthRoute);
                internalSerializable.AuthRoute = this.AuthRoute;

                // Serialize and write to output stream.
                var serialized = JsonConvert.SerializeObject(internalSerializable, Formatting.Indented);
                File.WriteAllText(m_savePath, serialized);

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
    }
}