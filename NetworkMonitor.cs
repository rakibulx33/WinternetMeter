using System;
using System.Linq;
using System.Net.NetworkInformation;
using WinternetMeter;
using WinternetMeter.Properties;

namespace WinternetMeter
{
    public class NetworkMonitor
    {
        private NetworkInterface? selectedInterface;
        private long lastBytesReceived = 0;
        private long lastBytesSent = 0;
        private DateTime lastSampleTime = DateTime.MinValue;

        public NetworkMonitor(string adapterName)
        {
            selectedInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.Name == adapterName && nic.OperationalStatus == OperationalStatus.Up);
            Reset();
        }

        public void Reset()
        {
            if (selectedInterface != null)
            {
                var stats = selectedInterface.GetIPv4Statistics();
                lastBytesReceived = stats.BytesReceived;
                lastBytesSent = stats.BytesSent;
                lastSampleTime = DateTime.UtcNow;
            }
        }

        public (double downloadBps, double uploadBps) GetSpeed()
        {
            if (selectedInterface == null)
                return (0, 0);
            var stats = selectedInterface.GetIPv4Statistics();
            var now = DateTime.UtcNow;
            double seconds = (now - lastSampleTime).TotalSeconds;
            if (lastSampleTime == DateTime.MinValue || seconds <= 0)
            {
                lastBytesReceived = stats.BytesReceived;
                lastBytesSent = stats.BytesSent;
                lastSampleTime = now;
                return (0, 0);
            }
            double dl = (stats.BytesReceived - lastBytesReceived) / seconds;
            double ul = (stats.BytesSent - lastBytesSent) / seconds;
            lastBytesReceived = stats.BytesReceived;
            lastBytesSent = stats.BytesSent;
            lastSampleTime = now;
            return (dl, ul);
        }

        public static string[] GetAdapterNames()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Select(nic => nic.Name)
                .ToArray();
        }
    }
}
