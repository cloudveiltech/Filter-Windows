using Filter.Platform.Common.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Gui.CloudVeil.UI.Controls
{
    /// <summary>
    /// Interaction logic for NumberCodeInput.xaml
    /// </summary>
    public partial class NumberCodeInput : UserControl
    {
        const int DEFAULT_FIELD_COUNT = 6;
        public NumberCodeInput()
        {
            InitializeComponent();
            initFields(DEFAULT_FIELD_COUNT);
        }

        public static readonly DependencyProperty NumberCountProperty = DependencyProperty.Register("NumberCount",
            typeof(int),
            typeof(NumberCodeInput),
            new UIPropertyMetadata(DEFAULT_FIELD_COUNT, new PropertyChangedCallback(OnFieldCountChanged)));


        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value",
            typeof(SecureString),
            typeof(NumberCodeInput),
            new UIPropertyMetadata(new SecureString(), new PropertyChangedCallback(OnValueChanged)));


        private static void OnFieldCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var obj = o as NumberCodeInput;
            if (obj == null)
                return;
            NumberCodeInput numberCodeInput = (NumberCodeInput)o;
            var fieldCount = (int)e.NewValue;
            numberCodeInput.initFields(fieldCount);
        }

        private void initFields(int fieldCount)
        {
            stackPanel.Children.Clear();
            for (int i = 0; i < fieldCount; i++)
            {
                var textBox = new TextBox()
                {
                    Margin = new Thickness(5, 0, 0, 0),
                    Width = 24,
                    Height = 30,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = "",
                    MaxLength = 1
                };
                textBox.PreviewTextInput += (sender, args) =>
                {
                    args.Handled = !IsTextAllowed(args.Text);
                };
                textBox.TextChanged += (sender, args) =>
                {
                    TextBox textBoxSender = sender as TextBox;
                    if (textBoxSender != null && textBoxSender.Text.Length == 1)
                    {
                        textBoxSender.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        updateValue();
                    }
                };
                textBox.GotFocus += (sender, args) => {
                    textBox.SelectAll();
                };
              
                stackPanel.Children.Add(textBox);
            }
        }

        private static void OnValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
           
        }
        
        private void updateValue()
        {
            var value = new SecureString();
            foreach (var child in stackPanel.Children)
            {
                if (child is TextBox)
                {
                    var text = (child as TextBox).Text;
                    if (text != null && text.Length > 0)
                    {
                        value.AppendChar(text[0]);
                    }
                }
            }
            Value = value;
        }

        static System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("^[0-9]$");
        private static bool IsTextAllowed(string text)
        {
            return regex.IsMatch(text);
        }

        public int NumberCount
        {
            get => (int)GetValue(NumberCountProperty);
            set => SetValue(NumberCountProperty, value);
        }

        public SecureString Value
        {
            get => (SecureString)GetValue(ValueProperty);
            set
            {
                SetValue(ValueProperty, value);

                var charArray = Encoding.ASCII.GetString(value.SecureStringBytes()).ToCharArray();

                int i = 0;
                foreach (var child in stackPanel.Children)
                {
                    if (child is TextBox && i < charArray.Length)
                    {
                        (child as TextBox).Text = charArray[i].ToString();
                        i++;
                    }
                }
            }
        }
    }
}
