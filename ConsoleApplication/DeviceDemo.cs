using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Nodes.API.Models;
using static System.Console;

// ReSharper disable PossibleInvalidOperationException

namespace ConsoleApplication
{
    public class DeviceDemo
    {
        public readonly List<Device> Devices = new List<Device>();
        public readonly List<Order> ActivatedOrders = new List<Order>();
        
        public const string globalDeviceEndpoint = "global.azure-devices-provisioning.net";
        public const string scopeId = "0ne000AC6E1";

        
        public static readonly JsonSerializerSettings SerializerSettings= new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public void Start()
        {
            LoadConfiguration();
            Devices.ForEach(UpdateDeviceLoad);

            while (true)
            {
                FetchOrders();

                Devices.ForEach(UpdateDeviceLoad);
                Devices.ForEach(AdjustLocalDevice);
                Devices.ForEach(UploadLoadData);

                WriteLine();
                WriteLine("... waiting (press CTRL-C to terminate)");
                Thread.Sleep(10000);
                WriteLine();
                WriteLine();
            }

            // ReSharper disable once FunctionNeverReturns
        }


        public void LoadConfiguration()
        {
            Devices.Clear();
            Devices.Add(new Device
            {
                // Id = "el-lampo-numero-uno",
                // Id = "TestDeviceForAssetHub",
                DeviceId = "DemandResponsDevice01",
                DevicePrimaryKey =  "keeuh9t/NPB5PjIxodOLDJZJIRS5Pm4ReaNkrC8Jex4=",
                Name = "Usb device",
                AssetPortfolioId = "ap1",
                InitialLoad = 10,
                
            });
            WriteLine($"Loaded {Devices.Count} devices(s): ");
            Devices.ForEach(dev => WriteLine($"    {dev.Name} with id {dev.DeviceId}, initial load {dev.InitialLoad}"));
            Devices.ForEach(dev => dev.CurrentLoad = dev.InitialLoad);
            WriteLine();
        }

        public void FetchOrders()
        {
            var client = UserRole.CreateDefaultClient();
            var orders = new FSP(client).GetCurrentActiveOrders().GetAwaiter().GetResult();
            ActivatedOrders.Clear();
            ActivatedOrders.AddRange(orders);
            WriteLine($" done fetching {ActivatedOrders.Count} active order(s) ");
            WriteLine();
        }

        public void UpdateDeviceLoad(Device dev)
        {
            dev.CurrentLoad = dev.InitialLoad;
            ActivatedOrders
                .Where(o => o.AssetPortfolioId == dev.AssetPortfolioId)
                .ToList()
                .ForEach(o => UpdateDeviceLoad(dev, o));
        }

        public void UpdateDeviceLoad(Device dev, Order o)
        {
            WriteLine($"  {dev}: Load reduced by {o.Quantity:F0} due to order {o}");
            dev.CurrentLoad -= (float) o.Quantity.Value;
        }

        public void UploadLoadData(Device dev)
        {
            WriteLine($"  {dev}: Uploading current load {dev.CurrentLoad:F0} to IOT-Hub: ");

            // var connectionString = $"HostName=iot-mvp-prd.azure-devices.net;DeviceId={dev.Id};SharedAccessKey=BvZC7bA+A9k0TBrNJHRAqZLjJgz5EJHpu601hAM+X2Y=";


            DeviceClient deviceClient; 
            
            using (var security = new SecurityProviderSymmetricKey(deviceId, pkey, null))
            {
                var result =  RegisterDeviceAsync(security).GetAwaiter().GetResult();
                
                if (result.Status != ProvisioningRegistrationStatusType.Assigned) {
                    WriteLine("Failed to register device");
                    return;
                }
                IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, security.GetPrimaryKey());
                deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
            }

            WriteLine( "   Got device-client, proceeding to upload");
            
            var powerTelemetry = new List<PowerTelemetry>
            {
                new PowerTelemetry
                {
                    CreationTimeUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, 0, DateTimeKind.Utc),
                    PowerAmountKW = dev.CurrentLoad,
                    UsageMethod = ElectricityUsageMethod.Consumption
                },
                new PowerTelemetry
                {
                    CreationTimeUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, 0, DateTimeKind.Utc),
                    PowerAmountKW = dev.InitialLoad-dev.CurrentLoad,
                    UsageMethod = ElectricityUsageMethod.OptimizedConsumptionDecrease
                },
            };

            
            var message = ConvertToMessages<PowerTelemetry>(powerTelemetry);

            // Send the message to SmartUtility
            deviceClient.SendEventBatchAsync(message, new CancellationToken()).Wait();

            
            
            
            WriteLine("    (done)");

            // WriteLine("   (not yet implemented)");
        }
        
        
        public class Telemetry
        {
            [JsonProperty(PropertyName = "iothub-creation-time-utc")]
            public DateTime CreationTimeUtc { get; set; }
        }
        
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
        
        private static IEnumerable<Message> ConvertToMessages<T>(IEnumerable<Telemetry> events)
        {
            foreach (var eventObject in events)
            {
                var messageString = JsonConvert.SerializeObject(eventObject, SerializerSettings);
                var message = new Message(Encoding.UTF8.GetBytes(messageString))
                {
                    CreationTimeUtc = eventObject.CreationTimeUtc
                    
                };
                // Yield message with UTF-8 encoded payload.
                yield return message;
            }
        }

        //
        // public class PowerTelemetry : Telemetry
        // {/
        //     public DateTime Timestamp { get; set; }
        //
        //     public double? PowerAmountKW { get; set; }
        //
        //     public ElectricityUsageMethod UsageMethod { get; set; }
        //
        //     /// <summary>
        //     /// Used for identification of the message type for Azure Stream Analytics
        //     /// </summary>
        //     public string IoTHubTypeIdentifier { get; set; } = "PowerTelemetry";
        // }


        public void AdjustLocalDevice(Device dev)
        {
            WriteLine($"  {dev}: Setting actual physical load to {dev.CurrentLoad:F0}: ");
            try
            {
                dev.SendLocalLoad();
            }
            catch (Exception e)
            {
                WriteLine($"      failed: {e.Message}");
            }
        }
        
        
        public static async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security)
        {
            WriteLine("Register device...");

            using (var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly))
            {
                var provClient =
                    ProvisioningDeviceClient.Create(globalDeviceEndpoint, scopeId, security, transport);

                WriteLine($"RegistrationID = {security.GetRegistrationID()}");


                Write("ProvisioningClient RegisterAsync...");
                DeviceRegistrationResult result = await provClient.RegisterAsync();

                WriteLine($"{result.Status}");
                WriteLine($"ProvisioningClient AssignedHub: {result.AssignedHub}; DeviceID: {result.DeviceId}");

                return result;
            }
        }
        
        
    }
}