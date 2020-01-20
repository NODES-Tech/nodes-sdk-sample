using System;

namespace ConsoleApplication.IoTHub
{
    public class PowerTelemetry : Telemetry
    {
        public DateTime Timestamp { get; set; }

        public double? PowerAmountKW { get; set; }

        public ElectricityUsageMethod UsageMethod { get; set; }

        /// <summary>
        /// Used for identification of the message type for Azure Stream Analytics
        /// </summary>
        public string IoTHubTypeIdentifier { get; set; } = "PowerTelemetry";
    }
}