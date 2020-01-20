using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleApplication.IoTHub;
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

        public void Start()
        {
            LoadConfiguration();
            Devices.ForEach(UpdateDeviceLoad);
            ComPorts.GetOrCreateConnection();
            Devices.ForEach(dev => dev.IsReady());
            Devices.ForEach(dev => dev.SendMaxLoad());

            while (true)
            {
                FetchOrders();

                Devices.ForEach(UpdateDeviceLoad);
                Devices.ForEach(AdjustLocalDevice);
                Devices.ForEach(IoTHubUtil.UploadLoadData);

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
                Name = "Wind mill 007",
                IoTDeviceId = "DemandResponsDevice01",
                IoTDevicePrimaryKey = "keeuh9t/NPB5PjIxodOLDJZJIRS5Pm4ReaNkrC8Jex4=",
                AssetPortfolioId = "ap1",
                ComPortIndex = 1,
                InitialLoad = 10,
            });
            Devices.Add(new Device
            {
                Name = "Factory 42",
                // IoTDeviceId = "DemandResponsDevice01",
                // IoTDevicePrimaryKey = "keeuh9t/NPB5PjIxodOLDJZJIRS5Pm4ReaNkrC8Jex4=",
                AssetPortfolioId = "ap2",
                ComPortIndex = 2,
                InitialLoad = 10,
            });
            WriteLine($"Loaded {Devices.Count} devices(s): ");
            Devices.ForEach(dev => WriteLine($"    {dev.Name} with id {dev.IoTDeviceId}, initial load {dev.InitialLoad}"));
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
                .ForEach(dev.UpdateDeviceLoad);
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