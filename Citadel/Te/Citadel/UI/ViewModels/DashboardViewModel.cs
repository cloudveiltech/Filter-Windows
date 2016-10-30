using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    public class DashboardViewModel : BaseCitadelViewModel
    {
        /// <summary>
        /// The model.
        /// </summary>
        private DashboardModel m_model = new DashboardModel();

        public DashboardViewModel()
        {

        }
        /// <summary>
        /// Private data member for the public DeactivateCommand property.
        /// </summary>
        private RelayCommand m_deactivationCommand;

        /// <summary>
        /// Command to run a deactivation request for the current authenticated user.
        /// </summary>
        public RelayCommand RequestDeactivateCommand
        {
            get
            {
                if(m_deactivationCommand == null)
                {
                    m_deactivationCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            RequestViewChangeCommand.Execute(typeof(ProgressWait));
                            var authSuccess = await m_model.RequestAppDeactivation();

                            if(!authSuccess)
                            {
                                RequestViewChangeCommand.Execute(typeof(DashboardView));
                            }
                            else
                            {
                                if(ProcessProtection.IsProtected)
                                {
                                    ProcessProtection.Unprotect();
                                }
                                
                                // Init the shutdown of this application.
                                Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                                return;
                            }
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }

                    }));
                }

                return m_deactivationCommand;
            }
        }
    }
}
