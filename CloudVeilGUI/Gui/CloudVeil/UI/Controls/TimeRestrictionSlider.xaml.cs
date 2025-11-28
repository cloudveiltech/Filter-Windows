using Filter.Platform.Common.Data.Models;
using FilterProvider.Common.Util;
using GalaSoft.MvvmLight;
using Newtonsoft.Json.Linq;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Gui.CloudVeil.UI.Controls
{
    public class PercentageTimeConverter : IMultiValueConverter
    {
        public const double MINUTES_IN_DAY= 1440;

        public object Convert(
        object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (System.Convert.ToDouble(values[0]) / MINUTES_IN_DAY) * System.Convert.ToDouble(values[1]);
        }

        public object[] ConvertBack(
            object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class AllowedDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var intervals = value as TimeRestrictionSlider.Interval[];
            if (intervals.Length >= 1 && intervals[0].Width == PercentageTimeConverter.MINUTES_IN_DAY)
            {
                return "Internet allowed all day";
            }
            if (intervals.Length >= 1 && intervals[0].Width == 0)
            {
                return "Internet blocked all day";
            }
            if(intervals.Length == 0)
            {
                return "Internet blocked all day";
            }

            var description = "Internet allowed: ";
            var isEnabled = false;
            foreach (var interval in intervals)
            {
                if (interval.Enabled)
                {
                    var from = TimeDetection.FormatMinutes(interval.Start);
                    var to = TimeDetection.FormatMinutes(interval.Start + interval.Width);
                    description += $"{from} - {to}; ";
                    if(interval.Width > 0)
                    {
                        isEnabled = true;
                    }
                }
            }

            if(!isEnabled)
            {
                description = "Internet blocked all day";
            }
            return description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaction logic for TimeRestrictionSlider.xaml
    /// </summary>
    public partial class TimeRestrictionSlider : UserControl, INotifyPropertyChanged
    {
        static SolidColorBrush FILLED_BRUSH = new SolidColorBrush(Color.FromArgb(255, 65, 177, 227));
        static SolidColorBrush TRANSPARENT_BRUSH = new SolidColorBrush(Colors.Transparent);
        
        DispatcherTimer timer = new DispatcherTimer();
        public TimeRestrictionSlider()
        {
            InitializeComponent();
            timer.Tick += Timer_Tick;
            timer.Interval = TimeSpan.FromSeconds(30);
            timer.Stop();
            ArrowPosition = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ArrowPosition = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        }

        public static readonly DependencyProperty IntervalsSliderProperty = DependencyProperty.Register("Intervals", typeof(Interval[]), typeof(TimeRestrictionSlider), new UIPropertyMetadata(new Interval[] { }, new PropertyChangedCallback(OnIntervalPropertyChanged)));

        public static readonly DependencyProperty TimeRestrictionModelSliderProperty = DependencyProperty.Register("TimeRestriction", typeof(TimeRestrictionModel), typeof(TimeRestrictionSlider), new UIPropertyMetadata(new TimeRestrictionModel(), new PropertyChangedCallback(OnTimeRestrictionPropertyChanged)));

        public static readonly DependencyProperty TimerEnabledSliderProperty = DependencyProperty.Register("TimerEnabled", typeof(Boolean), typeof(TimeRestrictionSlider), new UIPropertyMetadata(true, new PropertyChangedCallback(OnTimerEnabledSliderPropertyChanged)));

        public static readonly DependencyProperty ArrowPositionSliderProperty = DependencyProperty.Register("ArrowPosition", typeof(int), typeof(TimeRestrictionSlider), new UIPropertyMetadata(0, new PropertyChangedCallback(OnArrowPositionSliderPropertyChanged)));

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        static void OnIntervalPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var obj = o as TimeRestrictionSlider;
            if (obj == null)
                return;
            TimeRestrictionSlider timeRestrictionSlider = (TimeRestrictionSlider)o;
            timeRestrictionSlider.ListBox.ItemsSource = (Interval[])e.NewValue;
        }

        static void OnTimeRestrictionPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var obj = o as TimeRestrictionSlider;
            if (obj == null)
                return;
            TimeRestrictionSlider timeRestrictionSlider = (TimeRestrictionSlider)o;
            var model = (TimeRestrictionModel)e.NewValue;
            Interval[] intervals = convertTimeRestrictionsToIntervals(model);
            timeRestrictionSlider.ListBox.ItemsSource = intervals;
            timeRestrictionSlider.Intervals = intervals;
        }
        
        static void OnArrowPositionSliderPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var obj = o as TimeRestrictionSlider;
            if (obj == null)
                return;
            TimeRestrictionSlider timeRestrictionSlider = (TimeRestrictionSlider)o;
            timeRestrictionSlider.ArrowPosition = (int)e.NewValue;
        }

        private static Interval[] convertTimeRestrictionsToIntervals(TimeRestrictionModel model)
        {
            if(model == null)
            {
                return new Interval[0];
            }
            List<Interval> intervals = new List<Interval>();
            for (int i = 0; i < model.EnabledThrough.Length-1; i++)
            {
                var start = TimeDetection.GetTimeSpanFromDecimal(model.EnabledThrough[i]);
                var end = TimeDetection.GetTimeSpanFromDecimal(model.EnabledThrough[i + 1]);
                var length = end.Subtract(start);

                intervals.Add(new Interval
                {
                    Start = (int)start.TotalMinutes,
                    Color = i % 2 == 0 ? FILLED_BRUSH : TRANSPARENT_BRUSH,
                    Width = (int)length.TotalMinutes,
                    Enabled = i%2 == 0,
                    ToolTipText = (i % 2 == 0 ? "Enabed: " : "Disabled: ") + TimeDetection.FormatMinutes((int)start.TotalMinutes) + " - " + TimeDetection.FormatMinutes((int)end.TotalMinutes)
                });
            }

            if (intervals[0].Start > 0)
            {
                intervals.Insert(0, new Interval
                {
                    Start = 0,
                    Color = TRANSPARENT_BRUSH,
                    Enabled = false,
                    Width = (int)intervals[0].Start,
                    ToolTipText = "Disabled until " + TimeDetection.FormatMinutes(intervals[0].Start)
                });
            }
            if (intervals[intervals.Count - 1].Width + intervals[intervals.Count - 1].Start < PercentageTimeConverter.MINUTES_IN_DAY)
            {
                var start = intervals[intervals.Count - 1].Width + intervals[intervals.Count - 1].Start;
                intervals.Add(new Interval
                {
                    Start = start,
                    Color = TRANSPARENT_BRUSH,
                    Enabled = false,
                    Width = (int)PercentageTimeConverter.MINUTES_IN_DAY-start,
                    ToolTipText = "Disabled until next day"
                });
            }
            return intervals.ToArray();
        }
        private static void OnTimerEnabledSliderPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var obj = o as TimeRestrictionSlider;
            if (obj == null)
                return;
            TimeRestrictionSlider timeRestrictionSlider = (TimeRestrictionSlider)o;
            var visible = (Boolean)e.NewValue;
            timeRestrictionSlider.Arrows.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
            if (visible)
            {
                timeRestrictionSlider.timer.Start();
            } 
            else
            {
                timeRestrictionSlider.timer.Stop();
            }
        }

        public struct Interval
        {
            public int Start { get; set; }
            public int Width { get; set; }
            public SolidColorBrush Color { get; set; }
            public string ToolTipText { get; set; }
            public bool Enabled { get; set; }
        }

        public Interval[] Intervals
        {
            get => (Interval[])GetValue(IntervalsSliderProperty);
            set => SetValue(IntervalsSliderProperty, value);
        }

        public bool TimerEnabled
        {
            get => (bool)GetValue(TimerEnabledSliderProperty);
            set => SetValue(TimerEnabledSliderProperty, value);
        }

        public TimeRestrictionModel TimeRestriction
        {
            get => (TimeRestrictionModel)GetValue(TimeRestrictionModelSliderProperty);
            set => SetValue(TimeRestrictionModelSliderProperty, value);
        }

        public int ArrowPosition
        {
            get => (int)GetValue(ArrowPositionSliderProperty);
            set => SetValue(ArrowPositionSliderProperty, value);
        }
    }
}
