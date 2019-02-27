using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace FilterProvider.Common.ControlServer
{
    public class Server : IDisposable
    {
        private WebServer server = null;

        public Server(int port, X509Certificate2 cert)
        {
            server = new WebServer(new WebServerOptions($"https://localhost:{port}/")
            {
                AutoRegisterCertificate = true,
                Certificate = cert
            });
        }

        public List<Tuple<Type, Func<IHttpContext, object>>> ControllerFactories { get; set; } = new List<Tuple<Type, Func<IHttpContext, object>>>();

        public void RegisterController(Type controllerType, Func<IHttpContext, object> fn)
        {
            ControllerFactories.Add(new Tuple<Type, Func<IHttpContext, object>>(controllerType, fn));
        }

        public void Start()
        {
            server.EnableCors()
                .WithLocalSession()
                .RegisterModule(new WebApiModule());

            var module = server.Module<WebApiModule>();

            foreach(var factory in ControllerFactories)
            {
                module.RegisterController(factory.Item1, factory.Item2);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    server.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Server() {
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
