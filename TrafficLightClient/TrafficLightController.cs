
using System.IO.Ports;
using System.Diagnostics;
using System;
using System.IO;

namespace TrafficLightClient
{
    public enum TrafficLightState
    {
        Green,
        Yellow,
        Red,
        Inactive
    }

    public class TrafficLightController : IDisposable
    {
        private TrafficLightState _state = TrafficLightState.Inactive;
        private SerialPort? _port;
        private readonly object _lock = new object();

        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;

        public TrafficLightState State
        {
            get { lock (_lock) { return _state; } }
            private set { lock (_lock) { _state = value; } }
        }

        public bool IsConnected
        {
            get { lock (_lock) { return _port != null && _port.IsOpen; } }
        }

        public string PortName { get; private set; }

        public TrafficLightController()
        {
            var env = Environment.GetEnvironmentVariable("TRAFFICLIGHT_PORT");
            if (!string.IsNullOrWhiteSpace(env))
            {
                PortName = env!;
            }
            else
            {
                var ports = SerialPort.GetPortNames();
                PortName = ports.FirstOrDefault() ?? "COM3";
            }

            TryOpenPort();
        }

        public TrafficLightController(string portName)
        {
            PortName = portName;
            TryOpenPort();
        }

        private void TryOpenPort()
        {
            lock (_lock)
            {
                try
                {
                    if (_port != null)
                    {
                        if (_port.IsOpen) return;
                        try { _port.Dispose(); } catch (Exception ex) { Trace.TraceWarning("Error disposing old serial port: " + ex); }
                        _port = null;
                    }

                    _port = new SerialPort(PortName, 115200, Parity.None, 8, StopBits.One)
                    {
                        Handshake = Handshake.None,
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        NewLine = "\n"
                    };
                    _port.Open();
                    Trace.TraceInformation($"Opened serial port {PortName}");
                    // notify listeners
                    ConnectionStatusChanged?.Invoke(this, true);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.TraceWarning($"Access denied to port {PortName}: {ex}");
                    _port = null;
                    ErrorOccurred?.Invoke(this, ex);
                    ConnectionStatusChanged?.Invoke(this, false);
                }
                catch (IOException ex)
                {
                    Trace.TraceWarning($"IO error opening port {PortName}: {ex}");
                    _port = null;
                    ErrorOccurred?.Invoke(this, ex);
                    ConnectionStatusChanged?.Invoke(this, false);
                }
                catch (ArgumentException ex)
                {
                    Trace.TraceWarning($"Invalid port name {PortName}: {ex}");
                    _port = null;
                    ErrorOccurred?.Invoke(this, ex);
                    ConnectionStatusChanged?.Invoke(this, false);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed to open port {PortName}: {ex}");
                    try { _port?.Dispose(); } catch (Exception ex2) { Trace.TraceWarning($"Error disposing port after failure: {ex2}"); }
                    _port = null;
                    ErrorOccurred?.Invoke(this, ex);
                    ConnectionStatusChanged?.Invoke(this, false);
                }
            }
        }

        public void SetState(TrafficLightState state)
        {
            // ensure state change and send happen without racing another setter
            lock (_lock)
            {
                if (_state == state) return;
                _state = state;
                try
                {
                    switch (state)
                    {
                        case TrafficLightState.Red:
                            SendCommand("set r");
                            break;
                        case TrafficLightState.Yellow:
                            SendCommand("set y");
                            break;
                        case TrafficLightState.Green:
                            SendCommand("set g");
                            break;
                        case TrafficLightState.Inactive:
                            SendCommand("set");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Error sending state command: {ex}");
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private void SendCommand(string cmd)
        {
            lock (_lock)
            {
                if (_port == null || !_port.IsOpen)
                {
                    TryOpenPort();
                }

                if (_port == null || !_port.IsOpen)
                    return;

                try
                {
                    _port.Write(cmd + _port.NewLine);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed to write to port {PortName}: {ex}");
                    try { _port?.Close(); } catch (Exception ex2) { Trace.TraceWarning($"Error closing port after write failure: {ex2}"); }
                    try { _port?.Dispose(); } catch (Exception ex3) { Trace.TraceWarning($"Error disposing port after write failure: {ex3}"); }
                    _port = null;
                    ConnectionStatusChanged?.Invoke(this, false);
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try { _port?.Close(); } catch (Exception ex) { Trace.TraceWarning($"Error closing port during dispose: {ex}"); }
                try { _port?.Dispose(); } catch (Exception ex) { Trace.TraceWarning($"Error disposing port during dispose: {ex}"); }
                _port = null;
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }
    }
}