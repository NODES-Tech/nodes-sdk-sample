using System;
using System.Linq;
using static System.Console;
using static System.String;

// ReSharper disable ConvertToConstant.Local

namespace ConsoleApplication
{
    public static class Program
    {
        public const string APIUrl = "https://api-test-mock.nodesmarket.com/";
        public static bool PauseAtEnd = true; // Should be true in non-CI scenarios so that double-clicking a batch file keeps the result visible

        public static readonly (string name, Action action)[] Operations =
        {
            ("help", ShowHelp),
            ("skip-pause-at-end", () => PauseAtEnd = false ), // Required for CI/CD pipeline! 
            ("demo", RunDsoFspDemo ),
            ("dso-grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp-show", () => FSP.GetInfo().GetAwaiter().GetResult()),
            ("fsp-assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp-portfolios", () => new FSP().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp-order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso-order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("orders-clear", () => new FSP(UserRole.CreateDefaultClient()).ClearOrders().GetAwaiter().GetResult()),
            ("devices-demo", () => new DeviceDemo().Start()),
        };

        private static void RunDsoFspDemo()
        {
            foreach (var operation in Operations.Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
            {
                operation.action();
            }
        }

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");

            var todo = Operations.Where(p => args.Contains(p.name)).ToList();
            if (!todo.Any() || args.Any(a => Operations.All(p => p.name != a)))
            {
                ShowHelp();
                return;
            }

            WriteLine($"   Your commands: {Join(" ", args)}. Run with argument 'help' to see list of options. ");
            foreach (var oper in todo)
            {
                oper.action();
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