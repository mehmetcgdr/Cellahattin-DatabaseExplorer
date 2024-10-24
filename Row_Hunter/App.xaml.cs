using System.Windows;
using Cellahattin.Configuration;
namespace Cellahattin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

        public partial class App : Application
        {
            private void Application_Startup(object sender, StartupEventArgs e)
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

                var config = SecurityConfig.Instance;

                var mainWindow = new MainWindow();
                mainWindow.Show();
            }

            private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                if (e.Exception is ConfigurationException configEx)
                {
                    MessageBox.Show($"Configuration Error: {configEx.Message}",
                        "Configuration Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                e.Handled = true;
            }
        }
    

}
