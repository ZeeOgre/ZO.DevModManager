using System.Windows;

namespace ZO.DMM.AppNF
{
    public partial class MissingConfigDialog : Window
    {
        public MissingConfigDialog()
        {
            InitializeComponent();
        }

        private void CopySample_Click(object sender, RoutedEventArgs e)
        {
            // Logic to copy the sample configuration file
            _ = MessageBox.Show("Sample configuration file copied.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsWindow_Click(object sender, RoutedEventArgs e)
        {
            // Logic to open the settings window
            var settingsWindow = new SettingsWindow(SettingsLaunchSource.MissingConfigDialog);
            settingsWindow.Show();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Logic to exit the application
            Application.Current.Shutdown();
        }
    }
}
