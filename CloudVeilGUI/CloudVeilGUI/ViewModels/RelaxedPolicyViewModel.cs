using CloudVeilGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Xamarin.Forms;

namespace CloudVeilGUI.ViewModels
{
    public class RelaxedPolicyViewModel : INotifyPropertyChanged
    {

        int availableRelaxedRequests;
        public int AvailableRelaxedRequests
        {
            get
            {
                return availableRelaxedRequests;
            }

            set
            {
                availableRelaxedRequests = value;
                RaisePropertyChanged(nameof(AvailableRelaxedRequests));
            }
        }

        string relaxedDuration;
        public string RelaxedDuration
        {
            get
            {
                return relaxedDuration;
            }

            set
            {
                relaxedDuration = value;
                RaisePropertyChanged(nameof(RelaxedDuration));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public delegate void RelaxedPolicyRequestDelegate();

        public event RelaxedPolicyRequestDelegate RelaxedPolicyRequested;

        public event RelaxedPolicyRequestDelegate RelinquishRelaxedPolicyRequested;

        private Command useRelaxedPolicyCommand;
        public Command UseRelaxedPolicyCommand
        {
            get
            {
                if(useRelaxedPolicyCommand == null)
                {
                    useRelaxedPolicyCommand = new Command(() =>
                    {
                        RelaxedPolicyRequested?.Invoke();
                    });
                }

                return useRelaxedPolicyCommand;
            }
        }

        private Command relinquishRelaxedPolicyCommand;
        public Command RelinquishRelaxedPolicyCommand
        {
            get
            {
                if(relinquishRelaxedPolicyCommand == null)
                {
                    relinquishRelaxedPolicyCommand = new Command(() =>
                    {
                        RelinquishRelaxedPolicyRequested?.Invoke();
                    });
                }

                return relinquishRelaxedPolicyCommand;
            }
        }
    }
}
