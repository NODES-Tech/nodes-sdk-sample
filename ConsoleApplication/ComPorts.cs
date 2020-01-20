using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Nodes.API.Support;
using static System.Console;

namespace ConsoleApplication
{
    public class ComPorts
    {
        public static SerialPort Port; 
        public static void GetOrCreateConnection()
        {
            if (Port == null || !Port.IsOpen)
            {
                var portId = FindPortId();
                Port = NewPort(portId);

                Port.Open();
                WriteLine($"  Connection opened on COM port {portId}");
            }
        }

        private static SerialPort NewPort(string portId)
        {
            return new SerialPort
            {
                PortName = portId,
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
        }

        public static string FindPortId()
        {
            var candidates = SerialPort.GetPortNames();
            WriteLine($"      {candidates.Length} available port names: {candidates.JoinToString()}");
            var found = candidates.Where(HasInputDevice);
            return found.SingleOrDefault();
        }

        public static bool HasInputDevice(string portId)
        {
            try
            {
                var port = NewPort(portId);
                port.Open();
                port.Close();
                Thread.Sleep(100);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string ReadBytes()
        {
            // return Encoding.ASCII.GetBytes(Port.ReadLine());
            return Port.ReadLine();
            // throw new NotImplementedException();
        }
        

        public static void SendBytes(string s)
        {
            // WriteLine( "Writing as bytes: " + s);
            var msg = s + "\n\0";
            var bytes = Encoding.ASCII.GetBytes(msg);
            Port.Write(bytes, 0, bytes.Length);
            Thread.Sleep(1000);
        }        
    }
}