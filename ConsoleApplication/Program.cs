using System;
using System.Linq;
using static System.Console;
using static System.String;

namespace ConsoleApplication
{
    public static class Program
    {
        public const string APIUrl = "https://nodes-demo.azurewebsites.net/";
//        public const string APIUrl = "https://localhost:5001/";

        public static readonly (string n, Action a)[] Operations =
        {
            ("dso grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
        };

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");
            var arg = Join(" ", args);
            var (o, c) = Operations.SingleOrDefault(n => n.n == arg);
            (c ?? ShowHelp)();

            // TODO: Remove
            Operations[0].a();
            Operations[1].a();
            Operations[2].a();
            Operations[3].a();
        }

        public static void ShowHelp()
        {
            WriteLine("Your options are: ");
            Operations
                .Select((n, _) => "   " + n).ToList()
                .ForEach(WriteLine);
        }
    }
}