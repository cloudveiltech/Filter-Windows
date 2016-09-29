using Microsoft.Win32;
using NLog;
using System;
using System.ComponentModel;
using System.Windows;
using Te.Citadel.UI.Views;
using Te.Citadel.UI.Windows;

namespace Te.Citadel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class CitadelApp : Application
    {
        /// <summary>
        /// Logger.
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Shown when the program is minimized to the tray. The app is always minimized to the tray
        /// on close.
        /// </summary>
        private System.Windows.Forms.NotifyIcon m_trayIcon;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// Primary and only window we use.
        /// </summary>
        private MainWindow m_mainWindow;

        #region Views

        /// <summary>
        /// This view is shown whenever a valid auth or re-auth request cannot be performed, or has
        /// never been performed.
        /// </summary>
        private LoginView m_viewLogin;

        /// <summary>
        /// This view is shown when auth with a certain provider is a success, and the conditions are
        /// to be laid out for user acceptance.
        /// </summary>
        private ProviderConditionsView m_viewProviderConditions;

        #endregion

        /// <summary>
        /// Default ctor.
        /// </summary>
        public CitadelApp()
        {
            m_logger = LogManager.GetLogger("Citadel");
            this.Startup += CitadelOnStartup;
        }

        private void CitadelOnStartup(object sender, StartupEventArgs e)
        {
            // Do stuff that must be done on the UI thread first.
            InitViews();

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync(e);
        }

        /// <summary>
        /// Called to initialize the various application views on startup.
        /// </summary>
        private void InitViews()
        {
            m_viewLogin = new LoginView();            
        }

        /// <summary>
        /// Runs initialization off the UI thread.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void DoBackgroundInit(object sender, DoWorkEventArgs e)
        {
            // Hook the shutdown/logoff event.
            SystemEvents.SessionEnded += OnOsShutdownOrLogoff;
        }

        /// <summary>
        /// Called whenever the user is logging off or shutting down the system. Here we simply react
        /// to the event by safely terminating the program.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void OnOsShutdownOrLogoff(object sender, SessionEndedEventArgs e)
        {
            // Unhook first.
            SystemEvents.SessionEnded -= OnOsShutdownOrLogoff;


        }

        /// <summary>
        /// Called when the background initialization function has returned.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void OnBackgroundInitComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                m_logger.Error("Error during initialization.");
                if (e.Error != null && m_logger != null)
                {
                    m_logger.Error(e.Error.Message);
                    m_logger.Error(e.Error.StackTrace);
                }

                Current.Shutdown(-1);
                return;
            }

            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    m_mainWindow = new Citadel.UI.Windows.MainWindow();
                    m_mainWindow.CurrentView.Content = new LoginView();
                    m_mainWindow.Show();
                }
            );

            // Check for updates, always.
            //WinSparkle.CheckUpdateWithoutUI();
        }

        /// <summary>
        /// Initializes the m_trayIcon member, loading the icon graphic and hooking appropriate
        /// handlers to respond to user iteraction requesting to bring the application back out of
        /// the tray.
        /// </summary>
        private void InitTrayIcon()
        {
            m_trayIcon = new System.Windows.Forms.NotifyIcon();

            m_trayIcon.Icon = new System.Drawing.Icon(AppDomain.CurrentDomain.BaseDirectory + "stahpit.ico");

            m_trayIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    BringAppToFocus();
                };
        }

        /// <summary>
        /// Brings the main application window into focus for the user and removes it from the tray
        /// if the application icon is in the tray.
        /// </summary>
        public void BringAppToFocus()
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if (this.MainWindow != null)
                    {
                        this.MainWindow.Show();
                        this.MainWindow.WindowState = WindowState.Normal;
                    }

                    if (m_trayIcon != null)
                    {
                        m_trayIcon.Visible = false;
                    }
                }
            );
        }
    }
}