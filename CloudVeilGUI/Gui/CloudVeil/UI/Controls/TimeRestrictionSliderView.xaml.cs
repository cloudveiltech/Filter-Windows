using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Gui.CloudVeil.UI.Controls
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TimeRestrictionSliderView : UserControl, INotifyPropertyChanged
    {
        public TimeRestrictionSliderView()
        {
            InitializeComponent();

            LayoutRoot.DataContext = this;
        }

        public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register("UpperValue", typeof(double), typeof(TimeRestrictionSliderView),
            new PropertyMetadata(24.0, new PropertyChangedCallback(OnUpperValueChanged)));

        public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register("LowerValue", typeof(double), typeof(TimeRestrictionSliderView),
            new PropertyMetadata(0.0, new PropertyChangedCallback(OnLowerValueChanged)));

        public static readonly DependencyProperty IndicatorValueProperty = DependencyProperty.Register("IndicatorValue", typeof(double), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty IndicatorVisibleProperty = DependencyProperty.Register("IndicatorVisible", typeof(bool), typeof(TimeRestrictionSliderView), new PropertyMetadata(false, OnIndicatorVisibleChanged));

        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register("Caption", typeof(string), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty ToolTipPlacementProperty = DependencyProperty.Register("ToolTipPlacement", typeof(AutoToolTipPlacement), typeof(TimeRestrictionSliderView));

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private static void OnUpperValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeRestrictionSliderView v = d as TimeRestrictionSliderView;
            v.OnPropertyChanged(nameof(UpperValue));
            v.OnPropertyChanged(nameof(AllowedDescription));
        }

        private static void OnLowerValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeRestrictionSliderView v = d as TimeRestrictionSliderView;
            v.OnPropertyChanged(nameof(LowerValue));
            v.OnPropertyChanged(nameof(AllowedDescription));
        }

        public double IndicatorValue
        {
            get => (double)GetValue(IndicatorValueProperty);
            set
            {
                SetValue(IndicatorValueProperty, value);
                OnPropertyChanged(nameof(IndicatorValue));
            }
        }

        public bool IndicatorVisible
        {
            get => (bool)GetValue(IndicatorVisibleProperty);
            set
            {
                SetValue(IndicatorVisibleProperty, value);
                OnPropertyChanged(nameof(IndicatorVisible));

                ToolTipPlacement = value ? AutoToolTipPlacement.TopLeft : AutoToolTipPlacement.None;
            }

        }

    public AutoToolTipPlacement ToolTipPlacement
        {
            get => (AutoToolTipPlacement)GetValue(ToolTipPlacementProperty);
            set
            {
                SetValue(ToolTipPlacementProperty, value);
                OnPropertyChanged(nameof(ToolTipPlacement));
            }
        }

        public double LowerValue
        {
            get => (double)GetValue(LowerValueProperty);
            set
            {
                SetValue(LowerValueProperty, value);
                OnPropertyChanged(nameof(LowerValue));
                OnPropertyChanged(nameof(AllowedDescription));
            }
        }

        public double UpperValue
        {
            get => (double)GetValue(UpperValueProperty);
            set
            {
                SetValue(UpperValueProperty, value);
                OnPropertyChanged(nameof(UpperValue));
                OnPropertyChanged(nameof(AllowedDescription));
            }
        }

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set
            {
                SetValue(CaptionProperty, value);
                OnPropertyChanged(nameof(Caption));
            }
        }

        private static void OnIndicatorVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.SetValue(ToolTipPlacementProperty, (bool)e.NewValue ? AutoToolTipPlacement.TopLeft : AutoToolTipPlacement.None);
        }


        private static TimeSpan getTimeSpan(decimal time)
        {
            int hours, minutes;

            hours = (int)Math.Truncate(time);
            minutes = (int)((time - hours) * 60.0m);

            return new TimeSpan(hours, minutes, 0);
        }

        private static string formatTimeSpanAsTime(TimeSpan t)
        {
            int hours = t.Hours;

            string ampm = "AM";
            if(hours >= 12)
            {
                hours -= 12;
                ampm = "PM";
            }

            // This catches both midnight and noon (see above subtraction)
            if(hours == 0)
            {
                hours = 12;
            }

            string minutesStr = t.Minutes.ToString();
            if(t.Minutes < 10)
            {
                minutesStr = "0" + minutesStr;
            }

            return $"{hours}:{minutesStr} {ampm}";
        }

        public string AllowedDescription
        {
            get
            {
                decimal lower = Math.Round((decimal)LowerValue, 4);
                decimal upper = Math.Round((decimal)UpperValue, 4);

                if(lower == 0.0m && upper == 24.0m)
                {
                    return "Internet allowed all day";
                }
                else if(lower == upper)
                {
                    return "Internet blocked all day";
                }
                else
                {
                    TimeSpan lowerTime = getTimeSpan(lower);
                    TimeSpan upperTime = getTimeSpan(upper);

                    return $"Internet allowed from {formatTimeSpanAsTime(lowerTime)} - {formatTimeSpanAsTime(upperTime)}";
                }
            }
        }
    }
}
