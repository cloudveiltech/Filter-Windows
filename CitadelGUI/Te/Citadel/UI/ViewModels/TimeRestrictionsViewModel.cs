using Filter.Platform.Common.Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Te.Citadel.UI.ViewModels
{
    public class TimeRestrictionsViewModel : BaseCitadelViewModel
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
    }
}
