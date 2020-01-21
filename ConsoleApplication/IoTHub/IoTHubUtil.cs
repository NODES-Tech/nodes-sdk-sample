using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace ConsoleApplication.IoTHub
{
    public class IoTHubUtil
    {
        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static IEnumerable<Message> ConvertToMessages(IEnumerable<Telemetry> events)
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

        public static async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security, Device dev)
        {
            // Console.WriteLine("Register device...");
            using var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly);
            var provClient =
                ProvisioningDeviceClient.Create(dev.IoTGlobalDeviceEndpoint, dev.IoTDeviceScopeId, security, transport);
            // Console.WriteLine($"RegistrationID = {security.GetRegistrationID()}");
            // Console.Write("ProvisioningClient RegisterAsync...");
            var result = await provClient.RegisterAsync();
            // Console.WriteLine($"ProvisioningClient Stauts: {result.Status}, AssignedHub: {result.AssignedHub}; DeviceID: {result.DeviceId}");
            return result;
        }

        public static void UploadLoadData(Device dev)
        {
            if (string.IsNullOrEmpty(dev.IoTDeviceId))
            {
                Console.WriteLine($"  {dev}: No iot device id - skipped");
                return;
            }
            Console.WriteLine($"  {dev}: Uploading current load {dev.CurrentLoad:F0} to IOT-Hub: ");
            DeviceClient deviceClient;
            using (var security = new SecurityProviderSymmetricKey(dev.IoTDeviceId, dev.IoTDevicePrimaryKey, null))
            {
                var result = RegisterDeviceAsync(security, dev).GetAwaiter().GetResult();
                if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                {
                    Console.WriteLine("Failed to register device");
                    return;
                }
                IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, security.GetPrimaryKey());
                deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
            }
            // Console.WriteLine("   Got device-client, proceeding to upload");
            var powerTelemetry = new List<PowerTelemetry>
            {
                new PowerTelemetry
                {
                    CreationTimeUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour,
                        DateTime.UtcNow.Minute, 0, 0, DateTimeKind.Utc),
                    PowerAmountKW = dev.CurrentLoad,
                    UsageMethod = ElectricityUsageMethod.Consumption
                },
                new PowerTelemetry
                {
                    CreationTimeUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour,
                        DateTime.UtcNow.Minute, 0, 0, DateTimeKind.Utc),
                    PowerAmountKW = dev.InitialLoad - dev.CurrentLoad,
                    UsageMethod = ElectricityUsageMethod.OptimizedConsumptionDecrease
                },
            };
            var message = IoTHubUtil.ConvertToMessages(powerTelemetry);
            // Send the message to SmartUtility
            deviceClient.SendEventBatchAsync(message, new CancellationToken()).Wait();
            Console.WriteLine("    (done)");
        }
    }
}