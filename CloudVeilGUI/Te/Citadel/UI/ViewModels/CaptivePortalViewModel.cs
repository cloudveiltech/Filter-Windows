using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.UI.ViewModels
{
    public class CaptivePortalViewModel : BaseCitadelViewModel
    {
        private RelayCommand m_helpMeConnect;

        /*
         * public RelayCommand AuthenticateCommand
        {
            get
            {
                if(m_authenticateCommand == null)
                {
                    m_authenticateCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            RequestViewChange(typeof(ProgressWait)); 
                            await m_model.Authenticate();

                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }), m_model.CanAttemptAuthentication);
                }

                return m_authenticateCommand;
            }
        }
        */

        public RelayCommand HelpMeConnect
        {
            get
            {
                if(m_helpMeConnect == null)
                {
                    m_helpMeConnect = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("http://connectivitycheck.cloudveil.org");
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_helpMeConnect;
            }
        }
    }
}
