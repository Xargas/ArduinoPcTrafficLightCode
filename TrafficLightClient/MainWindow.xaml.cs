using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace TrafficLightClient
{
    public partial class MainWindow : Window
    {
        private readonly TrayManager? _tray;
        private bool _allowClose = false;
        private readonly TrafficLightController _controller;
        private readonly CpuMonitor _cpuMonitor;

        public bool EnableCpuMonitorFromArgs { get; set; }

        public MainWindow(TrayManager? tray = null)
        {
            InitializeComponent();
            _tray = tray;
            _controller = new TrafficLightController();
            _cpuMonitor = new CpuMonitor();
            _cpuMonitor.CpuUpdated += CpuMonitor_CpuUpdated;

            if (_tray != null)
            {
                _tray.SetState(TrafficLightState.Inactive);
                _tray.IconDoubleClick += Tray_IconDoubleClick;
                _tray.ExitRequested += Tray_ExitRequested;
            }

            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            UpdateStatus("Ready");
            Loaded += MainWindow_Loaded;
        }

        // Apply startup options set by the launcher (for example when starting minimized to tray)
        // This ensures features like the CPU monitor are started even if the window is never shown.
        public void ApplyStartupOptions()
        {
            if (EnableCpuMonitorFromArgs)
            {
                // Set the checkbox which will trigger the Checked handler and start the monitor
                if (!ChkCpu.IsChecked.GetValueOrDefault(false))
                    ChkCpu.IsChecked = true;
            }
        }

        // Ensures the device is set to Inactive and resources are cleaned up.
        // This is used during system shutdown where Closing may not be triggered
        // in the same way as a user-requested Exit.
        public void EnsureInactiveStateForShutdown()
        {
            try
            {
                SetState(TrafficLightState.Inactive);
            }
            catch { }

            try { _cpuMonitor.Stop(); } catch { }
            try { _controller?.Dispose(); } catch { }
            try { _tray?.Dispose(); } catch { }
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            if (EnableCpuMonitorFromArgs)
            {
                ChkCpu.IsChecked = true;
            }
        }

        private void Tray_IconDoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_allowClose)
            {
                // cancel close and send to tray
                e.Cancel = true;
                Hide();
                _tray?.ShowBalloon("TrafficLight", "Application minimized to tray.");
                return;
            }

            // allow close: set device to inactive and cleanup
            try
            {
                SetState(TrafficLightState.Inactive);
            }
            catch { }

            _cpuMonitor.Stop();
            try { _controller?.Dispose(); } catch { }
            _tray?.Dispose();
        }

        private void Tray_ExitRequested(object? sender, EventArgs e)
        {
            // request real exit: allow closing and then shut down
            _allowClose = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Close the main window which will now proceed to close
                Close();
            }));
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // minimize to tray
                Hide();
                _tray?.ShowBalloon("TrafficLight", "Application minimized to tray.");
            }
        }

        private void CpuMonitor_CpuUpdated(object? sender, CpuLoadEventArgs e)
        {
            // map cpu to states
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => CpuMonitor_CpuUpdated(sender, e)));
                return;
            }

            if (!ChkCpu.IsChecked.GetValueOrDefault(false))
                return;

            if (e.Load >= 80)
                SetState(TrafficLightState.Red);
            else if (e.Load >= 50)
                SetState(TrafficLightState.Yellow);
            else
                SetState(TrafficLightState.Green);
        }

        private void UpdateStatus(string text)
        {
            TxtStatus.Text = text;
        }

        private void SetState(TrafficLightState state)
        {
            _controller.SetState(state);
            _tray?.SetState(state);
            UpdateStatus($"State: {state}");
        }

        private void BtnGreen_Click(object sender, RoutedEventArgs e)
        {
            SetState(TrafficLightState.Green);
        }

        private void BtnYellow_Click(object sender, RoutedEventArgs e)
        {
            SetState(TrafficLightState.Yellow);
        }

        private void BtnRed_Click(object sender, RoutedEventArgs e)
        {
            SetState(TrafficLightState.Red);
        }

        private void BtnInactive_Click(object sender, RoutedEventArgs e)
        {
            SetState(TrafficLightState.Inactive);
        }

        private void ChkCpu_Checked(object sender, RoutedEventArgs e)
        {
            _cpuMonitor.Start();
            UpdateStatus("CPU monitor enabled");
        }

        private void ChkCpu_Unchecked(object sender, RoutedEventArgs e)
        {
            _cpuMonitor.Stop();
            UpdateStatus("CPU monitor disabled");
        }
    }
}