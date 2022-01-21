using Filter.Platform.Common.Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class TimeRestrictionsViewModel : BaseCloudVeilViewModel
    {
        public TimeRestrictionsViewModel() : this(null)
        {

        }

        public TimeRestrictionsViewModel(Dictionary<string, TimeRestrictionModel> restrictions)
        {
            Visibilities = new bool[7]; // One for each day of the week.

            UpdateRestrictions(restrictions);
            currentTimeTimer = new Timer((state) =>
            {
                try
                {
                    UpdateCurrentTime();
                }
                catch(Exception ex)
                {
                    m_logger.Error(ex, "Error occurred while attempting to UpdateCurrentTime()");
                }
            }, null, 0, 1000);

            PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(AreTimeRestrictionsActive))
                {
                    UpdateTimeRestrictionsDescription();
                }
            };
        }

        public void UpdateRestrictions(Dictionary<string, TimeRestrictionModel> restrictions)
        {
            if (restrictions == null) return;

            var restrictionsArray = new TimeRestrictionModel[7];
            TimeRestrictionModel model;

            for(int i = 0; i < 7; i++)
            {
                model = null;
                restrictions.TryGetValue(((DayOfWeek)i).ToString().ToLowerInvariant(), out model);

                if(model != null && !model.RestrictionsEnabled)
                {
                    model.EnabledThrough[0] = 0;
                    model.EnabledThrough[1] = 24;
                }

                restrictionsArray[i] = model;
            }

            this.TimeRestrictions = restrictionsArray;
        }

        public void UpdateCurrentTime()
        {
            DateTime now = DateTime.Now;
            
            double currentTime = now.Hour + (now.Minute / 60.0) + (now.Second / 3600.0);
            CurrentTime = currentTime;

            for(int i = 0; i < Visibilities.Length; i++)
            {
                Visibilities[i] = false;
            }

            Visibilities[(int)now.DayOfWeek] = true;
            RaisePropertyChanged(nameof(Visibilities));
        }

        public void UpdateTimeRestrictionsDescription()
        {
            if(AreTimeRestrictionsActive == null)
            {
                TimeRestrictionsDescription = "Time Restrictions not enabled. Click \"Edit\" below to set them up.";
            }
            else if(AreTimeRestrictionsActive.Value)
            {
                TimeRestrictionsDescription = "Time Restrictions currently active. Internet is blocked.";
            }
            else
            {
                TimeRestrictionsDescription = "Time Restrictions currently inactive. You may access the internet.";
            }
        }

        private Timer currentTimeTimer = null;

        private TimeRestrictionModel[] timeRestrictions;
        public TimeRestrictionModel[] TimeRestrictions
        {
            get => timeRestrictions;
            set
            {
                timeRestrictions = value;
                RaisePropertyChanged(nameof(TimeRestrictions));
            }
        }

        private double currentTime;
        public double CurrentTime
        {
            get => currentTime;
            set
            {
                currentTime = value;
                RaisePropertyChanged(nameof(CurrentTime));
            }
        }

        private string timeRestrictionsDescription;
        public string TimeRestrictionsDescription
        {
            get => timeRestrictionsDescription;
            set
            {
                timeRestrictionsDescription = value;
                RaisePropertyChanged(nameof(TimeRestrictionsDescription));
            }
        }

        private bool? areTimeRestrictionsActive;
        public bool? AreTimeRestrictionsActive
        {
            get => areTimeRestrictionsActive;
            set
            {
                areTimeRestrictionsActive = value;
                RaisePropertyChanged(nameof(AreTimeRestrictionsActive));
            }
        }

        private bool[] visibilities;
        public bool[] Visibilities
        {
            get => visibilities;
            set
            {
                visibilities = value;
                RaisePropertyChanged(nameof(Visibilities));
            }
        }

        public string TimeRestrictionsUri => global::CloudVeil.CompileSecrets.ServiceProviderUserTimeRestrictionsPath;
    }
}
