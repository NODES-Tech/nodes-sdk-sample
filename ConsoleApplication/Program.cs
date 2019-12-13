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
        public const string APIUrl = "https://nodes-demo.azurewebsites.net/";
//        public const string APIUrl = "https://localhost:5001/";

        public static readonly (string n, Action a)[] Operations =
        {
            ("dso grid", () => new DSO().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp assets", () => new FSP().CreateAssets().GetAwaiter().GetResult()),
            ("fsp assets", () => new FSP().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp order", () => new FSP().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso order", () => new DSO().PlaceBuyOrder().GetAwaiter().GetResult()),
        };

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");
            
            var arg = Join(" ", args);
            var (_, c) = Operations.SingleOrDefault(n => n.n == arg);
            (c ?? ShowHelp)();

            // TODO: Remove
            foreach (var operation in Operations)
            {
                operation.a();
            }
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