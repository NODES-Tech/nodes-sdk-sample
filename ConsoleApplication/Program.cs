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
        // public const string APIUrl = "https://nodes-demo.azurewebsites.net/";
        public const string APIUrl = "https://localhost:5001/";

        public static readonly (string name, Action action)[] Operations =
        {
            ("help", ShowHelp),
            ("dso grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp portfolios", () => new FSP().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("devices", () => new DeviceManager().Start()),
        };

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");

            var arg = Join(" ", args);
            var (_, c) = Operations.SingleOrDefault(n => n.name == arg);
            if (c == null)
            {
                ShowHelp();
            }
            else
            {
                WriteLine("   Run with argument 'help' to see list of options. ");
                c();
            }

            // TODO: Remove
            if (args.Contains("all"))
            {
                foreach (var operation in Operations.Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
                {
                    operation.action();
                }
            }

            WriteLine("--- DONE - Press enter to close program");
            ReadLine();
        }

        public static void ShowHelp()
        {
            WriteLine("Your options are: ");
            Operations
                .Select(n => "   " + n.name).ToList()
                .ForEach(WriteLine);
        }
    }
}