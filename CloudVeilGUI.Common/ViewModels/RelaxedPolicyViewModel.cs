/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

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
