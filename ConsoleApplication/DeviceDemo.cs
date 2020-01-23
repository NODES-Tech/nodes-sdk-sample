using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ConsoleApplication.IoTHub;
using Nodes.API.Models;
using Nodes.API.Queries;
using static System.Console;

// ReSharper disable PossibleInvalidOperationException

namespace ConsoleApplication
{
    public class DeviceDemo
    {
        public readonly List<Device> Devices = new List<Device>();
        public SearchResult<Order> ActivatedOrders;

        public void Start()
        {
            LoadConfiguration();

            while (true)
            {
                FetchOrders();
                Devices.ForEach(UpdateDeviceLoad);
                
                if( !ComPorts.IsConnected() ) {
                    ComPorts.CreateConnectionIfNotOpen();
                    if (ComPorts.IsConnected())
                    {
                        if (ComPorts.IsReady())
                        {
                            Devices.ForEach(dev => dev.SendMaxLoad());
                        }
                        else
                        {
                            WriteLine("  COM port device did not send READY! signal");
                        }
                    }
                }
                
                if (ComPorts.IsConnected())
                {
                    Devices.ForEach(AdjustLocalDevice);
                }

                
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
                IoTDeviceId = "windturbine01",
                IoTDevicePrimaryKey = "+KdG3tZu/WJiVYVNUQ6NGTgo8qE4rfI7Xpryqs5PtBE=",
                AssetPortfolioName = "ap1",
                ComPortIndex = 1,
                InitialLoad = 10,
            });
            Devices.Add(new Device
            {
                Name = "Factory 42",
                IoTDeviceId = "factory01",
                IoTDevicePrimaryKey = "YrBRZLGwyDyu5ehWDRdyESnB0lWI8opmUwPwMsToHTQ=",
                AssetPortfolioName = "ap2",
                ComPortIndex = 2,
                InitialLoad = 10,
            });
            Devices.ForEach(dev => dev.CurrentLoad = dev.InitialLoad);
            WriteLine($"Loaded {Devices.Count} devices(s): ");
            Devices.ForEach(dev => WriteLine($"    {dev.Name} with id {dev.IoTDeviceId}, initial load {dev.InitialLoad}"));
            WriteLine();
        }

        public void FetchOrders()
        {
            ActivatedOrders = new FSP(UserRole.CreateDefaultClient()).GetCurrentActiveOrders().GetAwaiter().GetResult();
            // ActivatedOrders.Clear();
            // ActivatedOrders.AddRange(orders);
            WriteLine($"- Done fetching orders from NODES - {ActivatedOrders.Items.Count} active order(s) found. ");
            WriteLine();
        }

        public void UpdateDeviceLoad(Device dev)
        {
            dev.CurrentLoad = dev.InitialLoad;
            ActivatedOrders.Items
                .Where(FSP.IsActive)
                .Select(o => (o, (AssetPortfolio)ActivatedOrders.Embedded.Single(ap => ap.Id == o.AssetPortfolioId)))
                .Where(o => o.Item2.Name == dev.AssetPortfolioName)
                .Select(o =>o.o)
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