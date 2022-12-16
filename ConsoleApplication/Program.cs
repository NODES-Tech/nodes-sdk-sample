using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using static System.Console;
using static System.String;

// ReSharper disable InvertIf
// ReSharper disable ConvertToConstant.Local

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(params string[] args) => new Program().Run(args);

        public static IConfigurationRoot BuildConfigurationRoot() =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();

        public Program() =>
            _services = new ServiceCollection()

                // Read appsettings and appsettings. Customize if needed.
                .AddSingleton<IConfiguration>(BuildConfigurationRoot())

                // In cases where the server provides invalid/self-signed SSL certificates, e.g. localhost 
                // or certain corporate / educational environments, add a dummy validator: 
                .AddSingleton<HttpMessageHandler>(
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    })
                .AddSingleton<HttpClient, HttpClient>()

                // This token provider will get a token from the B2C server, using clientid/secret found in appsettings*.json
                // We recommend creating a new file appsettings.local.json and adding your values there. 
                // See TokenProvider for details. 
                .AddSingleton<ITokenProvider, TokenProvider>()
                .AddSingleton<HttpUtils>()

                // The APIUrl is specified in appsettings.json or appsettings.local.json
                // The rest of the parameters to NodesClient will be fetched from the service collection on instantiation. 
                .AddSingleton(x =>
                    ActivatorUtilities.CreateInstance<NodesClient>(x,
                        x.GetRequiredService<IConfiguration>().GetSection("APIUrl").Value))
                .AddSingleton<DSO>()
                .AddSingleton<FSP>()
                .BuildServiceProvider();

        // This setting should be default true, in non-CI scenarios, so that double-clicking a batch file keeps the result visible
        private bool _pauseAtEnd = true;

        private readonly ServiceProvider _services;

        public (string name, Action action)[] Operations() => new (string name, Action action)[]
        {
            ("help", ShowHelp),
            ("skip-pause-at-end", () => _pauseAtEnd = false), // Required for CI/CD pipeline! 
            ("demo", RunDsoFspDemo),
            ("show-grid", () => _services.GetRequiredService<DSO>().DisplayGridNodeTree().GetAwaiter().GetResult()),
            ("dso-grid", () => _services.GetRequiredService<DSO>().CreateGridNodesIfNeeded().GetAwaiter().GetResult()),
            ("fsp-show", () => _services.GetRequiredService<FSP>().GetInfo().GetAwaiter().GetResult()),
            ("fsp-assets", () => _services.GetRequiredService<FSP>().CreateAssets().GetAwaiter().GetResult()),
            ("fsp-gridassignments",
                () => _services.GetRequiredService<FSP>().AssignAssetsToGrid().GetAwaiter().GetResult()),
            ("dso-approve-assets", () => _services.GetRequiredService<DSO>().ApproveAssets().GetAwaiter().GetResult()),
            ("fsp-portfolios", () => _services.GetRequiredService<FSP>().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp-order", () => _services.GetRequiredService<FSP>().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso-order", () => _services.GetRequiredService<DSO>().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("orders-clear", () => _services.GetRequiredService<FSP>().ClearOrders().GetAwaiter().GetResult()),
        };

        public void RunDsoFspDemo()
        {
            foreach (var operation in Operations().Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
            {
                operation.action();
            }
        }

        public void Run(string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");

            var todo = Operations().Where(p => args.Contains(p.name)).ToList();
            if (!todo.Any() || args.Any(a => Operations().All(p => p.name != a)))
            {
                ShowHelp();
            }
            else
            {
                WriteLine($"   Your commands: {Join(" ", args)}. Run with argument 'help' to see list of options. ");
                foreach (var oper in todo)
                {
                    oper.action();
                }
            }


            if (_pauseAtEnd)
            {
                WriteLine("--- DONE - Press enter to close program");
                ReadLine();
            }
        }

        public void ShowHelp()
        {
            WriteLine("Commands: ");
            Operations()
                .Select(n => "   " + n.name).ToList()
                .ForEach(WriteLine);
        }
    }
}