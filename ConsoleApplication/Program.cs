using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Nodes.API.Http.Client.Support;
using static System.Console;
using static System.String;

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
                
                // In cases where the server provides invalid/selfsigned SSL certificates, e.g. localhost, 
                // add a dummy validator: 
                .AddSingleton<HttpMessageHandler>(
                    new HttpClientHandler  
                    {
                        ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true
                    })
                .AddSingleton<HttpClient, HttpClient>()
                
                // This token provider will get a token from the B2C server, using clientid/secret found in appsettings*.json
                // We recommend creating a new file appsettings.local.json and adding your values there. 
                // See TokenProvider for details. 
                .AddSingleton<ITokenProvider, TokenProvider>() 
                .AddSingleton<HttpUtils>()
                
                // The APIUrl is specified in appsettings.json or appsettings.local.json
                // The rest of the parameters to NodesClient will be fetched from the service collection on instantiation. 
                .AddSingleton(x => ActivatorUtilities.CreateInstance<NodesClient>(x, x.GetRequiredService<IConfiguration>().GetSection("APIUrl").Value))
                .AddSingleton<DSO>()
                .AddSingleton<FSP>()
                .AddSingleton<DeviceDemo>()
                .BuildServiceProvider();

        private bool _pauseAtEnd = true; // Should be default true, in non-CI scenarios, so that double-clicking a batch file keeps the result visible
        private readonly ServiceProvider _services;

        public (string name, Action action)[] Operations() => new (string name, Action action)[]
        {
            ("help", ShowHelp),
            ("skip-pause-at-end", () => _pauseAtEnd = false), // Required for CI/CD pipeline! 
            ("demo", RunDsoFspDemo),
            ("dso-grid", () => _services.GetRequiredService<DSO>().CreateGridNodes().GetAwaiter().GetResult()),
            ("fsp-show", () => _services.GetRequiredService<FSP>().GetInfo().GetAwaiter().GetResult()),
            ("fsp-assets", () => _services.GetRequiredService<FSP>().CreateAssets().GetAwaiter().GetResult()),
            ("fsp-portfolios", () => _services.GetRequiredService<FSP>().CreatePortfolio().GetAwaiter().GetResult()),
            ("fsp-order", () => _services.GetRequiredService<FSP>().PlaceSellOrder().GetAwaiter().GetResult()),
            ("dso-order", () => _services.GetRequiredService<DSO>().PlaceBuyOrder().GetAwaiter().GetResult()),
            ("orders-clear", () => _services.GetRequiredService<FSP>().ClearOrders().GetAwaiter().GetResult()),
            ("devices-demo", () => _services.GetRequiredService<DeviceDemo>().Start()),
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
                return;
            }

            WriteLine($"   Your commands: {Join(" ", args)}. Run with argument 'help' to see list of options. ");
            foreach (var oper in todo)
            {
                oper.action();
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