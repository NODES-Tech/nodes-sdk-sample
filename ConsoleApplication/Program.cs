using System;
using System.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using static System.Console;
using static System.String;

// ReSharper disable ConvertToConstant.Local

namespace ConsoleApplication
{
    public static class Program
    {
        // public const string APIUrl = "https://api.test.nodesmarket.com/";
        public const string APIUrl = "https://nodes-demo.azurewebsites.net/";
        // public const string APIUrl = "https://localhost:5001/";

        public static readonly (string name, Action action)[] Operations =
        {
            ("help", ShowHelp),
            ("dso-grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp-assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp-portfolios", () => new FSP().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp-order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso-order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("devices-demo", () => new DeviceDemo().Start()),
        };

        public static void Main(params string[] args)
        {
            ComPorts.GetOrCreateConnection();
            ComPorts.SendBytes("READY?");
            var res = ComPorts.ReadBytes();
            
            WriteLine($"RES: {res}");



            // return; 
            
            
            WriteLine("Welcome to Nodes Client Example!");

            var arg = Join(" ", args.Where(s => !s.StartsWith("-")));
            var pauseAtEnd = !args.Any(s => s.Equals("-pause=off"));

            var (name, oper) = Operations.SingleOrDefault(n => n.name == arg);
            if (oper == null)
            {
                ShowHelp();
            }
            else
            {
                WriteLine($"   Your command: {name}. Run with argument 'help' to see list of options. ");
                oper();
            }

            // TODO: Remove, just for running locally
            if (args.Contains("all"))
            {
                foreach (var operation in Operations.Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
                {
                    operation.action();
                }
            }

            if (pauseAtEnd)
            {
                WriteLine("--- DONE - Press enter to close program");
                ReadLine();
            }
        }

        public static void ShowHelp()
        {
            WriteLine("Commands: ");
            Operations
                .Select(n => "   " + n.name).ToList()
                .ForEach(WriteLine);
            WriteLine("Options: ");
            WriteLine("   -pause=off: Don't wait for readline when program ends");
        }
    }
}