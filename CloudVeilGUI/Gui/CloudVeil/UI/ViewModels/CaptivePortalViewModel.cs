using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class CaptivePortalViewModel : BaseCloudVeilViewModel
    {
        private RelayCommand helpMeConnect;

        /*
         * public RelayCommand AuthenticateCommand
        {
            get
            {
                if(authenticateCommand == null)
                {
                    authenticateCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            RequestViewChange(typeof(ProgressWait)); 
                            await model.Authenticate();

                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }), model.CanAttemptAuthentication);
                }

                return authenticateCommand;
            }
        }
        */

        public RelayCommand HelpMeConnect
        {
            get
            {
                if(helpMeConnect == null)
                {
                    helpMeConnect = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("http://connectivitycheck.cloudveil.org");
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }));
                }

                return helpMeConnect;
            }
        }
    }
}
