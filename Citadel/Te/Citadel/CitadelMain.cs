/*
* Copyright (c) 2016 Jesse Nicholson.
*
* This file is part of Citadel.
*
* Citadel is free software: you can redistribute it and/or
* modify it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 3 of the License, or (at
* your option) any later version.
*
* In addition, as a special exception, the copyright holders give
* permission to link the code of portions of this program with the OpenSSL
* library.
*
* You must obey the GNU General Public License in all respects for all of
* the code used other than OpenSSL. If you modify file(s) with this
* exception, you may extend this exception to your version of the file(s),
* but you are not obligated to do so. If you do not wish to do so, delete
* this exception statement from your version. If you delete this exception
* statement from all source files in the program, then also delete it
* here.
*
* Citadel is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
* Public License for more details.
*
* You should have received a copy of the GNU General Public License along
* with Citadel. If not, see <http://www.gnu.org/licenses/>.
*/

using Microsoft.VisualBasic.ApplicationServices;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel
{

    /// <summary>
    /// Enforces that only a single instance of this application can be run at any given time.
    /// </summary>
    public class SingleAppInstanceManager : WindowsFormsApplicationBase
    {
        private CitadelApp m_app;

        /// <summary>
        /// 
        /// </summary>
        public SingleAppInstanceManager()
        {
            IsSingleInstance = true;            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <returns></returns>
        protected override bool OnStartup(StartupEventArgs eventArgs)
        {
            m_app = new CitadelApp();
            m_app.InitializeComponent();
            m_app.Run();
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            base.OnStartupNextInstance(eventArgs);
            m_app.BringAppToFocus();
        }
    }

    public static class CitadelMain
    {

        public static Logger MainLogger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        public static void Main(string[] args)
        {            

            try
            {
                var nlogCfgPath = AppDomain.CurrentDomain.BaseDirectory + @"Nlog.config";
                // Nlog config is gone. Let's put it back.
                // XXX TODO - Remove this once we switch to programatically setting it.
                if(!File.Exists(nlogCfgPath))
                {
                    var nlogCfgUri = new Uri("pack://application:,,,/Resources/NLog.config");
                    var resourceStream = System.Windows.Application.GetResourceStream(nlogCfgUri);
                    TextReader tsr = new StreamReader(resourceStream.Stream);
                    var nlogConfigText = tsr.ReadToEnd();
                    resourceStream.Stream.Close();
                    resourceStream.Stream.Dispose();
                    File.WriteAllText(nlogCfgPath, nlogConfigText);
                }

                MainLogger = LogManager.GetLogger("Citadel");
            }
            catch
            {
                // What can be done?   
            }
            
            try
            {
                MainLogger = LogManager.GetLogger("Citadel");

                SingleAppInstanceManager appManager = new SingleAppInstanceManager();
                appManager.Run(args);
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(MainLogger, e);
            }

            // No matter what, always ensure that critical flags are removed from our process before
            // exiting.
            ProcessProtection.Unprotect();
        }
    }
}
