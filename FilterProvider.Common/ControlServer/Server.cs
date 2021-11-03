using EmbedIO;
using EmbedIO.WebApi;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FilterProvider.Common.ControlServer
{
    public class Server : IDisposable
    {
        private WebServer server = null;
        private Task serverTask = null;

        private NLog.Logger logger = null;

        CancellationTokenSource cts = null;

        private Process GetNetsh(string verb, int port, string options = "") => new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                Arguments = $"http {verb} sslcert ipport=0.0.0.0:{port} {options}"
            }
        };

        public Server(int port, X509Certificate2 cert)
        {
            logger = LoggerUtil.GetAppWideLogger();

            cts = new CancellationTokenSource();

            try
            {
                Process netsh = GetNetsh("delete", port);
                netsh.Start();
                netsh.ErrorDataReceived += (s, e) =>
                {
                    logger.Error("netsh error while deleting: {0}", e.Data);
                };

                netsh.OutputDataReceived += (s, e) =>
                {
                    logger.Info("netsh output: {0}", e.Data);
                };

                netsh.WaitForExit();
            }
            catch(Exception ex)
            {
                logger.Error(ex);
            }


            server = new WebServer(o => o
                  .WithUrlPrefix($"https://127.0.0.1:{port}")
                  .WithMode(HttpListenerMode.EmbedIO)
                  .WithCertificate(cert)
                  .WithAutoRegisterCertificate());
        }

        public List<Tuple<Type, Func<WebApiController>>> ControllerFactories { get; set; } = new List<Tuple<Type, Func<WebApiController>>>();

        public void RegisterController(Type controllerType, Func<WebApiController> fn)
        {
            ControllerFactories.Add(new Tuple<Type, Func<WebApiController>>(controllerType, fn));
        }

        public void Start()
        {
            server
                .WithCors()
                .WithLocalSessionManager()
                .WithWebApi("/api", m => {

                        foreach (var factory in ControllerFactories)
                        {
                            m.RegisterController(factory.Item1, factory.Item2);
                        }
                    });

            //server.RegisterModule(new CorsModule("*", "*", "GET,POST"));
            
            serverTask = server.RunAsync(cts.Token);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    serverTask.Wait();

                    serverTask.Dispose();
                    server.Dispose();
                    cts.Dispose();
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
