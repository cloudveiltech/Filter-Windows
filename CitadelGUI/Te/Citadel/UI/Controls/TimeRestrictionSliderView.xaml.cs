using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace Te.Citadel.UI.Controls
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

        public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register("UpperValue", typeof(double), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register("LowerValue", typeof(double), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty IndicatorValueProperty = DependencyProperty.Register("IndicatorValue", typeof(double), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty IndicatorVisibleProperty = DependencyProperty.Register("IndicatorVisible", typeof(bool), typeof(TimeRestrictionSliderView));
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register("Caption", typeof(string), typeof(TimeRestrictionSliderView));

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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
            }
        }

        public double LowerValue
        {
            get => (double)GetValue(LowerValueProperty);
            set
            {
                SetValue(LowerValueProperty, value);
                OnPropertyChanged(nameof(LowerValue));
            }
        }

        public double UpperValue
        {
            get => (double)GetValue(UpperValueProperty);
            set
            {
                SetValue(UpperValueProperty, value);
                OnPropertyChanged(nameof(UpperValue));
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
    }
}
