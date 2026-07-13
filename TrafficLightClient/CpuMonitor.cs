using System.Diagnostics;

namespace TrafficLightClient
{
    public class CpuLoadEventArgs(double load) : EventArgs
    {
        public double Load { get; } = load;
    }

    public class CpuMonitor
    {
        private System.Threading.Timer? _timer;
        private readonly PerformanceCounter? _pc;

        public event EventHandler<CpuLoadEventArgs>? CpuUpdated;

        public CpuMonitor()
        {
            try
            {
                _pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // first call returns 0, so call once
                _ = _pc.NextValue();
            }
            catch
            {
                _pc = null;
            }
        }

        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(OnTick, null, 500, 1000);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void OnTick(object? state)
        {
            double load = 0;
            try
            {
                if (_pc != null)
                {
                    load = Math.Round(_pc.NextValue());
                }
            }
            catch
            {
                load = 0;
            }

            CpuUpdated?.Invoke(this, new CpuLoadEventArgs(load));
        }
    }
}