using System;
using Newtonsoft.Json;

namespace ConsoleApplication.IoTHub
{
    public class Telemetry
    {
        [JsonProperty(PropertyName = "iothub-creation-time-utc")]
        public DateTime CreationTimeUtc { get; set; }
    }
}