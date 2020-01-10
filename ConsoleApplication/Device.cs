using System;
using System.IO.Ports;
using System.Text;
using static System.Console;

namespace ConsoleApplication
{
    public class Device
    {
        public string Id { get; set; }
        public string AssetPortfolioId { get; set; }
        public float InitialLoad { get; set; }
        public float CurrentLoad { get; set; }
        public string Name { get; set; }

        public SerialPort Port { get; set; }


        public override string ToString() => $"{Name} ({Id})";

        public void SendLocalLoad()
        {
            GetOrCreateConnection();
            if (!IsReady())
            {
                throw new Exception("Did not receive ready response from device. Check connection and power");
            }

            SendBytes($"VAL{CurrentLoad:0000}");
            WriteLine($"    {this}: Value set to {CurrentLoad:0000}");
        }

        public void SendMaxLoad() => SendBytes($"MAX{CurrentLoad:0000}");

        public bool IsReady()
        {
            try
            {
                SendBytes("READY?");
                var bytes = ReadBytes();
                var response = Encoding.ASCII.GetString(bytes);
                return response.Equals("READY!");
            }
            catch (Exception)
            {
                return false;
            }
        }


        private void SendBytes(string s)
        {
            // var bytes = Encoding.ASCII.GetBytes(s);
            Port.WriteLine(s);
            // throw new NotImplementedException();
        }


        public void GetOrCreateConnection()
        {
            if (Port == null || !Port.IsOpen)
            {
                // Find com port number
                var portId = FindPortId(); 
                // create connection
                Port = new SerialPort(portId, 9600, Parity.None);
                Port.Open();
                WriteLine($"  {this}: Connection opened on port {Port}");
            }
        }

        private string FindPortId()
        {
            return "COM3";
        }

        private byte[] ReadBytes()
        {
            return Encoding.ASCII.GetBytes(Port.ReadLine());
            // throw new NotImplementedException();
        }
    }
}