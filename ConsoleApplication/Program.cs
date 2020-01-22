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

        public static bool PauseAtEnd = true; // Should be true so that double-clicking a batch file keeps the result visible..."

        public static readonly (string name, Action action)[] Operations =
        {
            ("help", ShowHelp),
            ("skip-pause-at-end", () => PauseAtEnd = false ), // Required for CI/CD pipeline! 
            ("dso-grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp-assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp-portfolios", () => new FSP().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp-order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso-order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("orders-clear", () => new FSP(UserRole.CreateDefaultClient()).ClearOrders().GetAwaiter().GetResult()),
            ("devices-demo", () => new DeviceDemo().Start()),
        };

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");

            var todo = Operations.Where(p => args.Contains(p.name)).ToList();
            if (!todo.Any() || args.Any(a => Operations.All(p => p.name != a)))
            {
                ShowHelp();
                return;
            }

            WriteLine($"   Your commands: {string.Join(" ", args)}. Run with argument 'help' to see list of options. ");
            foreach (var oper in todo)
            {
                oper.action();
            }

            // TODO: Remove, just for running locally
            if (args.Contains("all"))
            {
                foreach (var operation in Operations.Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
                {
                    operation.action();
                }
            }

            if (PauseAtEnd)
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
        }
    }
}