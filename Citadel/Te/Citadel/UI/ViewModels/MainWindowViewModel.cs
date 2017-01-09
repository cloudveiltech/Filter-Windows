using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Models;

namespace Te.Citadel.UI.ViewModels
{
    public class MainWindowViewModel : BaseCitadelViewModel
    {
        private MainWindowModel m_model;

        public bool InternetIsConnected
        {
            get
            {
                return m_model.InternetIsConnected;
            }
        }

        public MainWindowViewModel()
        {
            m_model = new MainWindowModel();
            m_model.PropertyChanged += OnModelChange;
        }

        private void OnModelChange(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(InternetIsConnected):
                    {
                        RaisePropertyChanged(nameof(InternetIsConnected));
                    }
                    break;
            }
        }
    }
}
