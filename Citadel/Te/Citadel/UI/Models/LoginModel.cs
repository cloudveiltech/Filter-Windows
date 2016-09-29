using GalaSoft.MvvmLight;
using System;
using System.Diagnostics;
using System.Security;
using Te.Citadel.Extensions;

namespace Te.Citadel.UI.Models
{
    internal class LoginModel : ObservableObject
    {
        
        private volatile bool m_currentlyAuthenticating = false;

        private string m_serviceProvider;

        private string m_userName;

        private SecureString m_userPassword;

        public bool CurrentlyAuthenticating
        {
            get
            {
                return m_currentlyAuthenticating;
            }
        }

        public string ServiceProvider
        {
            get
            {
                return m_serviceProvider;
            }

            set
            {
                m_serviceProvider = value;
            }
        }

        public string UserName
        {
            get
            {
                return m_userName;
            }

            set
            {
                m_userName = value;
            }
        }

        public SecureString UserPassword
        {
            get
            {
                return m_userPassword;
            }

            set
            {
                m_userPassword = value;
            }
        }

        /// <summary>
        /// Determines whether or not the current state permits initiating an authentication request.
        /// </summary>
        /// <returns>
        /// True if the current state is valid for an authentication request, false otherwise.
        /// </returns>
        public bool CanAttemptAuthentication()
        {
            // Can't auth when we're in the middle of it.
            if (m_currentlyAuthenticating)
            {
                return false;
            }
            
            // Check to see if the service provider string represents a HTTP or HTTPS URI. If
            // not, then it's not valid.
            if (StringExtensions.Valid(ServiceProvider))
            {
                Uri result;
                if (!Uri.TryCreate(ServiceProvider, UriKind.Absolute, out result))
                {
                    return false;
                }

                if (!result.Scheme.OIEquals(Uri.UriSchemeHttp) && !result.Scheme.OIEquals(Uri.UriSchemeHttps))
                {
                    return false;
                }
            }

            // Ensure some sort of username has been supplied.
            if (!StringExtensions.Valid(UserName))
            {
                return false;
            }

            // Ensure some sort of password has been supplied.
            if (UserPassword == null || UserPassword.Length <= 0)
            {
                return false;
            }

            return true;
        }

        public void Authenticate()
        {
            Debug.WriteLine("Authenticate");
        }

        public LoginModel()
        {
            ServiceProvider = string.Empty;
            UserName = string.Empty;
            UserPassword = new SecureString();
        }
    }
}