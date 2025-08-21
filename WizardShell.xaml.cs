using System.Windows;
using System.Windows.Controls;

namespace RCLayoutPreview
{
    public partial class WizardShell : Window
    {
        private int _currentStep = 0;
        private UserControl[] _steps;

        public WizardShell()
        {
            InitializeComponent();

            _steps = new UserControl[]
            {
                new Step1(),
                new Step2(),
                new Step3()
            };

            StepHost.Content = _steps[_currentStep];
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < _steps.Length - 1)
            {
                _currentStep++;
                StepHost.Content = _steps[_currentStep];
                MessageBox.Show($"Switched to step {_currentStep}");
            }
            else
            {
                // Close wizard when last step is reached
                this.DialogResult = true;
                this.Close();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                StepHost.Content = _steps[_currentStep];
            }
        }
    }
}
