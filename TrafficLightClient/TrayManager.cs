using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TrafficLightClient
{
    internal static class NativeMethods
    {
        public const int WM_USER = 0x0400;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;

        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            // The struct is larger in modern Windows; we don't need other fields for basic use
        }
    }

    public class TrayManager : IDisposable
    {
        private readonly HwndSource _hwndSource;
        private IntPtr _iconHandle = IntPtr.Zero;
        private readonly uint _id = 1;
        private bool _added = false;
        private TrafficLightState _state = TrafficLightState.Inactive;
        private readonly DispatcherTimer _blinkTimer = new DispatcherTimer();
        private bool _blinkOn = false;

        private const int WM_TRAYICON = NativeMethods.WM_USER + 1;

        public event EventHandler? IconDoubleClick;
        public event EventHandler? ExitRequested;

        public TrayManager()
        {
            var parameters = new HwndSourceParameters("TrayIconHiddenWindow");
            parameters.WindowStyle = 0;
            parameters.Width = 0;
            parameters.Height = 0;
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            _blinkTimer.Interval = TimeSpan.FromMilliseconds(500);
            _blinkTimer.Tick += (s, e) => { _blinkOn = !_blinkOn; UpdateIcon(); };

            UpdateIcon();
            AddIcon();
        }

        public void ShowBalloon(string title, string text)
        {
            SetToolTip(text);
        }

        public void SetState(TrafficLightState state)
        {
            _state = state;
            if (state == TrafficLightState.Inactive)
            {
                if (!_blinkTimer.IsEnabled) _blinkTimer.Start();
            }
            else
            {
                if (_blinkTimer.IsEnabled) _blinkTimer.Stop();
                _blinkOn = false;
            }

            UpdateIcon();
        }

        private void SetToolTip(string tip)
        {
            var data = new NativeMethods.NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA));
            data.hWnd = _hwndSource.Handle;
            data.uID = _id;
            data.uFlags = NativeMethods.NIF_TIP;
            data.uCallbackMessage = WM_TRAYICON;
            data.hIcon = _iconHandle;
            data.szTip = tip ?? string.Empty;
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
        }

        private void AddIcon()
        {
            var data = new NativeMethods.NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA));
            data.hWnd = _hwndSource.Handle;
            data.uID = _id;
            data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
            data.uCallbackMessage = WM_TRAYICON;
            data.hIcon = _iconHandle;
            data.szTip = "TrafficLightClient";
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
            _added = true;
        }

        private void UpdateIcon()
        {
            // draw a small bitmap representing the traffic light
            IntPtr newIcon = IntPtr.Zero;
            try
            {
                using (var bmp = DrawIconBitmap(_state, _blinkOn))
                {
                    newIcon = bmp.GetHicon();
                }

                // replace icon
                if (_iconHandle != IntPtr.Zero)
                {
                    try { NativeMethods.DestroyIcon(_iconHandle); } catch { }
                }
                _iconHandle = newIcon;

                var data = new NativeMethods.NOTIFYICONDATA();
                data.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA));
                data.hWnd = _hwndSource.Handle;
                data.uID = _id;
                data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
                data.uCallbackMessage = WM_TRAYICON;
                data.hIcon = _iconHandle;
                data.szTip = "TrafficLightClient";

                bool result;
                if (_added)
                    result = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
                else
                    result = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);

                if (!result)
                {
                    Trace.TraceWarning("Shell_NotifyIcon returned false when updating/adding icon");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Error updating tray icon: {ex}");
                if (newIcon != IntPtr.Zero) NativeMethods.DestroyIcon(newIcon);
            }
        }

        private Bitmap DrawIconBitmap(TrafficLightState state, bool blinkOn)
        {
            int size = 32;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    int cx = size / 2;
                    int top = 5;
                    int r = 4;

                    DrawCircle(g, cx, top + 0 * (r * 2), r, state == TrafficLightState.Red ? Color.Red : Color.FromArgb(20, Color.Red), state == TrafficLightState.Red);
                    DrawCircle(g, cx, top + 1 * (r * 2), r, state == TrafficLightState.Yellow ? Color.Yellow : Color.FromArgb(20, Color.Yellow), state == TrafficLightState.Yellow);
                    DrawCircle(g, cx, top + 2 * (r * 2), r, state == TrafficLightState.Green ? Color.LimeGreen : Color.FromArgb(20, Color.LimeGreen), state == TrafficLightState.Green);

                    if (state == TrafficLightState.Inactive)
                    {
                        var c = blinkOn ? Color.Yellow : Color.Black;
                        DrawCircle(g, cx, top + 1 * (r * 2), r, c, true);
                    }
                }

                return bmp;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Error drawing tray icon bitmap: {ex}");
                bmp.Dispose();
                // fallback: return a minimal empty bitmap
                return new Bitmap(size, size, PixelFormat.Format32bppArgb);
            }
        }

        private void DrawCircle(Graphics g, int cx, int cy, int r, Color fill, bool on)
        {
            var rect = new Rectangle(cx - r, cy - r, r * 2, r * 2);
            using (var brush = new SolidBrush(on ? fill : Color.FromArgb(60, fill)))
            {
                g.FillEllipse(brush, rect);
            }
            using (var pen = new Pen(Color.Black, 1))
            {
                g.DrawEllipse(pen, rect);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_LBUTTONDBLCLK = 0x0203;
            const int WM_RBUTTONUP = 0x0205;

            if (msg == WM_TRAYICON)
            {
                int m = lParam.ToInt32();
                if (m == WM_LBUTTONDBLCLK)
                {
                    IconDoubleClick?.Invoke(this, EventArgs.Empty);
                }
                else if (m == WM_RBUTTONUP)
                {
                    // show simple WPF context menu
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var menu = new ContextMenu();
                        var miOpen = new MenuItem { Header = "Open" };
                        miOpen.Click += (s, e) => IconDoubleClick?.Invoke(this, EventArgs.Empty);
                        var miExit = new MenuItem { Header = "Exit" };
                        miExit.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
                        menu.Items.Add(miOpen);
                        menu.Items.Add(miExit);
                        menu.Placement = PlacementMode.MousePoint;
                        menu.IsOpen = true;
                    }));
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            try
            {
                if (_added)
                {
                    var data = new NativeMethods.NOTIFYICONDATA();
                    data.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA));
                    data.hWnd = _hwndSource.Handle;
                    data.uID = _id;
                    NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
                }

                if (_iconHandle != IntPtr.Zero)
                {
                    NativeMethods.DestroyIcon(_iconHandle);
                    _iconHandle = IntPtr.Zero;
                }
            }
            catch { }
            try { _hwndSource.RemoveHook(WndProc); _hwndSource.Dispose(); } catch { }
        }
    }
}