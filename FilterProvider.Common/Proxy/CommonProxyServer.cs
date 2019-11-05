using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.Text;
using GoproxyWrapper;
using Filter.Platform.Common.Util;
using GoProxyWrapper;

namespace FilterProvider.Common.Proxy
{
    public class CommonProxyServer : IProxyServer, IDisposable
    {
        public bool IsRunning => GoProxy.Instance.IsRunning; 

        public CommonProxyServer()
        {
            GoProxy.Instance.BeforeRequest += OnBeforeRequest;
            GoProxy.Instance.BeforeResponse += OnBeforeResponse;

            GoProxy.Instance.Blacklisted += OnBlacklisted;
            GoProxy.Instance.Whitelisted += OnWhitelisted;
        }

        public void Init(int httpPortNumber, int httpsPortNumber, string certFile, string keyFile)
        {
            GoProxy.Instance.Init((short)httpPortNumber, (short)httpsPortNumber, certFile, keyFile);
        }

        public void Start()
        {
            GoProxy.Instance.Start();
        }

        public void Stop()
        {
            GoProxy.Instance.Stop();
        }

        public event GoProxy.OnBeforeRequest BeforeRequest;
        public event GoProxy.OnBeforeResponse BeforeResponse;

        public event AdBlockMatcherApi.AdBlockCallbackDelegate Whitelisted;
        public event AdBlockMatcherApi.AdBlockCallbackDelegate Blacklisted;
        
        public int OnBeforeRequest(Session session)
        {
            Int32? blocked = BeforeRequest?.Invoke(session);

            if (blocked.HasValue)
            {
                return blocked.Value;
            }
            else
            {
                return 0;
            }

        }

        public void OnBeforeResponse(Session session)
        {
            BeforeResponse?.Invoke(session);
        }

        public int OnWhitelisted(Session session, string url, int[] categories)
        {
            return Whitelisted?.Invoke(session, url, categories) ?? 0;
        }

        public int OnBlacklisted(Session session, string url, int[] categories)
        {
            return Blacklisted?.Invoke(session, url, categories) ?? 0;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Stop();
                    GoProxy.Instance.BeforeRequest -= OnBeforeRequest;
                    GoProxy.Instance.BeforeResponse -= OnBeforeResponse;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CommonProxyServer() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
