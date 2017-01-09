using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class MainWindowModel : ObservableObject
    {
        private volatile bool m_internetIsConnected = false;

        private Timer m_timer;

        public MainWindowModel()
        {
            // Start out with a 5 second delay for the first check.
            m_timer = new Timer(new TimerCallback(OnUpdateTimer), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        }

        private async void OnUpdateTimer(object state)
        {
            m_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            var result = await WebServiceUtil.GetHasInternetServiceAsync();

            this.InternetIsConnected = result;

            if(result == false)
            {
                // If we're disconnected, we're going to check every 10 seconds if we're connected.
                // if we're connected, we'll set it a little higher.
                m_timer.Change(TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
            }
            else
            {
                m_timer.Change(TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
            }
        }

        public bool InternetIsConnected
        {
            get
            {
                return m_internetIsConnected;
            }

            private set
            {
                m_internetIsConnected = value;
                RaisePropertyChanged(nameof(InternetIsConnected));
            }
        }
    }
}
