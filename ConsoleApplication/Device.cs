using System;
using Nodes.API.Enums;
using Nodes.API.Models;
using static System.Console;

namespace ConsoleApplication
{
    public class Device
    {
        public string AssetPortfolioId { get; set; }
        public float InitialLoad { get; set; }
        public float CurrentLoad { get; set; }
        public string Name { get; set; }

        public int ComPortIndex { get; set; }

        public string IoTDeviceId { get; set; }
        public string IoTDevicePrimaryKey { get; set; }

        public string IoTGlobalDeviceEndpoint { get; } = "global.azure-devices-provisioning.net";

        public string IoTDeviceScopeId { get; } = "0ne000AC6E1";

        public override string ToString() => $"{Name} ({IoTDeviceId})";

        public void SendLocalLoad()
        {
            ComPorts.SendBytes($"{ComPortIndex}VAL{ToCString(CurrentLoad)}");
            WriteLine($"    {this}: Value set to {ToCString(CurrentLoad)}");
        }

        public void SendMaxLoad() => ComPorts.SendBytes($"{ComPortIndex}MAX{ToCString(InitialLoad)}");

        private static string ToCString(float f)
        {
            var s = f.ToString("0") + ".0";
            // WriteLine($"FLOAT: {f} => {s}");
            return s;
        }

        public void UpdateDeviceLoad(Order o)
        {
            if (o.RegulationType == null)
            {
                WriteLine($"  {this}: Malformed order - ignored: {o}");
                return;
            }

            if (o.RegulationType == RegulationType.Up)
            {
                WriteLine($"  {this}: Production/consumption increased by {o.Quantity:F0} due to order {o}");
                CurrentLoad += (float) o.Quantity.Value;
                return;
            }

            if (o.RegulationType == RegulationType.Down)
            {
                WriteLine($"  {this}: Production/consumption reduced by {o.Quantity:F0} due to order {o}");
                CurrentLoad -= (float) o.Quantity.Value;
                return;
            }

            throw new ArgumentException("WTF");
        }
    }
}