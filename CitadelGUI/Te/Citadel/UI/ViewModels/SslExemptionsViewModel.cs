using Citadel.Core.Windows.Util;
using Citadel.IPC;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    public class SslExemptionInfo : ObservableObject
    {
        public string Host { get; set; }
        public string CertificateHash { get; set; }
    }

    public class SslExemptionsViewModel : BaseCitadelViewModel
    {
        public ObservableCollection<SslExemptionInfo> SslCertificateExemptions { get; set; }

        private RelayCommand m_backToDashboardCommand;

        private RelayCommand<SslExemptionInfo> m_trustCertificateCommand;

        public RelayCommand BackToDashboardCommand
        {
            get
            {
                if(m_backToDashboardCommand == null)
                {
                    m_backToDashboardCommand = new RelayCommand((Action)(() =>
                    {
                        ViewChangeRequest(typeof(DashboardView));
                    }));
                }

                return m_backToDashboardCommand;
            }
        }

        public RelayCommand<SslExemptionInfo> TrustCertificateCommand
        {
            get
            {
                if(m_trustCertificateCommand == null)
                {
                    m_trustCertificateCommand = new RelayCommand<SslExemptionInfo>((Action<SslExemptionInfo>)((info) =>
                    {
                        try
                        {

                            SslCertificateExemptions.Remove(info);

                            Task.Run(() =>
                            {
                                using (var ipcClient = new IPCClient())
                                {
                                    ipcClient.ConnectedToServer = () =>
                                    {
                                        ipcClient.TrustCertificate(info.Host, info.CertificateHash);
                                    };

                                    ipcClient.WaitForConnection();
                                    Task.Delay(3000).Wait();
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_trustCertificateCommand;
            }
        }

        public SslExemptionsViewModel()
        {
            SslCertificateExemptions = new ObservableCollection<SslExemptionInfo>();
        }

        public void AddSslCertificateExemptionRequest(CertificateExemptionMessage msg)
        {
            foreach(var certExemption in SslCertificateExemptions)
            {
                if(certExemption.Host == msg.Host && certExemption.CertificateHash == msg.CertificateHash)
                {
                    return;
                }
            }

            SslCertificateExemptions.Add(new SslExemptionInfo()
            {
                CertificateHash = msg.CertificateHash,
                Host = msg.Host
            });
        }
        public void InitSslCertificateExemptions(Dictionary<string, CertificateExemptionMessage> certificateExemptionRequests)
        {
            SslCertificateExemptions = new ObservableCollection<SslExemptionInfo>();

            foreach (var request in certificateExemptionRequests)
            {
                SslCertificateExemptions.Add(new SslExemptionInfo()
                {
                    Host = request.Value.Host,
                    CertificateHash = request.Value.CertificateHash
                });
            }
        }

        // TODO: Implement command.
    }
}
