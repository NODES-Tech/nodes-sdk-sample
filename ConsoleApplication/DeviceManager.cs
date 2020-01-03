using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nodes.API.Models;
using static System.Console;
// ReSharper disable PossibleInvalidOperationException

namespace ConsoleApplication
{
    public class DeviceManager
    {
        public readonly List<Device> Devices = new List<Device>();
        public readonly List<Order> ActivatedOrders = new List<Order>();
        public readonly FSP FSP = new FSP();

        public void Start()
        {
            LoadConfiguration();

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
                Id = "el-lampo-numero-uno",
                Name = "Lamp",
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
            var orders = FSP.GetCurrentActiveOrders().GetAwaiter().GetResult();
            ActivatedOrders.Clear();
            ActivatedOrders.AddRange(orders);
            WriteLine( $" done fetching {ActivatedOrders.Count} active order(s) ");
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
            WriteLine($"  {dev}: Load reduced by {o.Quantity}");
            dev.CurrentLoad -= (float) o.Quantity.Value;
        }
        
        public void UploadLoadData(Device dev)
        {
            WriteLine($"  {dev}: Uploading current load {dev.CurrentLoad} to IOT-Hub: ");
            WriteLine("   (not yet implemented)");
        }

        public void AdjustLocalDevice(Device dev)
        {
            WriteLine($"  {dev}: Setting actual physical load to {dev.CurrentLoad}: ");
            WriteLine("   (not yet implemented)");
        }

    }
}