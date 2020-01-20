using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Nodes.API.Support;
using static System.Console;

namespace ConsoleApplication
{
    public class Device
    {
        public bool LightsOn { get; set; }

        
        public string AssetPortfolioId { get; set; }
        public float InitialLoad { get; set; }
        public float CurrentLoad { get; set; }
        public string Name { get; set; }

        public SerialPort Port { get; set; }

        public string DeviceId { get; set; }
        public string DevicePrimaryKey { get; set; }

        public string GlobalDeviceEndpoint { get; set; } = "global.azure-devices-provisioning.net";
        public const string scopeId = "0ne000AC6E1";



        public override string ToString() => $"{Name} ({DeviceId})";

        public void SendLocalLoad()
        {
            GetOrCreateConnection();
            if (!IsReady())
            {
                throw new Exception("Did not receive ready response from device. Check connection and power");
            }

            var valAsString = LightsOn ? "20000": "-1";
            SendBytes($"1VAL{valAsString}");

            var onOff = LightsOn ? "1" : "0";
            SendBytes($"2VAL{onOff}");
            LightsOn = !LightsOn;
            WriteLine($"    {this}: Value set to {valAsString} / {onOff}");
        }

        public void SendMaxLoad() => SendBytes($"MAX{CurrentLoad:0000}");

        public bool IsReady()
        {
            try
            {
                Port.DiscardInBuffer();
                SendBytes("READY?");
                Thread.Sleep(100);
                var bytes = ReadBytes();
                var response = Encoding.ASCII.GetString(bytes);
                return response.Contains("READY!");
            }
            catch (Exception)
            {
                return false;
            }
        }


        private void SendBytes(string s)
        {
            var msg = s + "\n\0";
            var bytes = Encoding.ASCII.GetBytes(msg);
            Port.Write(bytes, 0, bytes.Length);
            Thread.Sleep(1000);
            // Port.WriteLine(s);
            // throw new NotImplementedException();
        }


        public void GetOrCreateConnection()
        {
            if (Port == null || !Port.IsOpen)
            {
                // Find com port number
                var portId = FindPortId();
                // create connection
                Port = new SerialPort
                {
                    PortName = portId,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                Port.Open();
                WriteLine($"  {this}: Connection opened on port {portId}");
            }
        }

        private string FindPortId()
        {
            var candidates = SerialPort.GetPortNames();
            WriteLine($"      {candidates.Length} available port names: {candidates.JoinToString()}");
            var found = candidates.Where(HasInputDevice);
            return found.SingleOrDefault();
        }

        private bool HasInputDevice(string s)
        {
            try
            {
                var port = new SerialPort
                {
                    PortName = s,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                port.Open();
                // return port.IsOpen;
                port.Close();
                Thread.Sleep(100);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private byte[] ReadBytes()
        {
            return Encoding.ASCII.GetBytes(Port.ReadLine());
            // throw new NotImplementedException();
        }
    }
}