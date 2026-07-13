using System;
using System.Windows;
using System.Threading;

namespace TrafficLightClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private TrayManager? _tray;
        private MainWindow? _mainWindow;
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mutex = new Mutex(false, "Global\\TrafficLightClientMutex");

            if (!_mutex.WaitOne(TimeSpan.FromSeconds(3), false))
            {
                MessageBox.Show("Application is already running.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            bool startInTray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)
                                               || a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)
                                               || a.Equals("--start-in-tray", StringComparison.OrdinalIgnoreCase));

            bool cpuEnabled = e.Args.Any(a => a.Equals("--cpu", StringComparison.OrdinalIgnoreCase)
                                             || a.Equals("--enable-cpu", StringComparison.OrdinalIgnoreCase));

            _tray = new TrayManager();

            _mainWindow = new MainWindow(_tray);
            _mainWindow.EnableCpuMonitorFromArgs = cpuEnabled;

            // Ensure startup options (like CPU monitor) are applied even if the window is not shown
            _mainWindow.ApplyStartupOptions();

            if (startInTray)
            {
                // start without showing the main window
                _mainWindow.Hide();
                _tray.ShowBalloon("TrafficLight", "Application started in tray.");
            }
            else
            {
                _mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _tray?.Dispose();
            base.OnExit(e);
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            // Ensure we set the device to inactive when Windows is shutting down or user is logging off.
            try
            {
                if (_mainWindow != null)
                {
                    // marshal to UI thread to perform UI-bound cleanup
                    _mainWindow.Dispatcher.Invoke(() => _mainWindow.EnsureInactiveStateForShutdown());
                }
            }
            catch { }

            base.OnSessionEnding(e);
        }
    }
}