/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Util;
using GalaSoft.MvvmLight.Command;
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
