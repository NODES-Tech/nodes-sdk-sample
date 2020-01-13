using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Azure.Devices.Client;
using Nodes.API.Models;
using static System.Console;

// ReSharper disable PossibleInvalidOperationException

namespace ConsoleApplication
{
    public class DeviceDemo
    {
        public readonly List<Device> Devices = new List<Device>();
        public readonly List<Order> ActivatedOrders = new List<Order>();

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
                Id = "TestDeviceForAssetHub",
                Name = "Fan",
                AssetPortfolioId = "ap1",
                InitialLoad = 10000,
            });
            WriteLine($"Loaded {Devices.Count} devices(s): ");
            Devices.ForEach(dev => WriteLine($"    {dev.Name} with id {dev.Id}, initial load {dev.InitialLoad}"));
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

            var connectionString = $"HostName=iot-mvp-prd.azure-devices.net;DeviceId={dev.Id};SharedAccessKey=BvZC7bA+A9k0TBrNJHRAqZLjJgz5EJHpu601hAM+X2Y=";
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Amqp);

            var data = $"{{\"messageId\":{Guid.NewGuid().ToString()},\"load\":{dev.CurrentLoad}}}";
            var eventMessage = new Message(Encoding.UTF8.GetBytes(data));
            deviceClient.SendEventAsync(eventMessage).ConfigureAwait(false).GetAwaiter().GetResult();
            WriteLine("    (done)");

            // WriteLine("   (not yet implemented)");
        }

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
    }
}