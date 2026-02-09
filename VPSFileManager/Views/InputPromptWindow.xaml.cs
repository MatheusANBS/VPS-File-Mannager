using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public partial class InputPromptWindow : FluentWindow
    {
        public string InputValue { get; private set; } = string.Empty;

        public InputPromptWindow(string title = "Input", string message = "Please enter a value", string label = "Value", string defaultValue = "")
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            LabelText.Text = label;
            InputTextBox.Text = defaultValue;
            
            Title = title;
            
            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            InputValue = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}
