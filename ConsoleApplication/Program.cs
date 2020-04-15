using System;
using System.Linq;
using System.Net.Http;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nodes.API.Http.Client.Support;
using static System.Console;
using static System.String;

// ReSharper disable ConvertToConstant.Local

namespace ConsoleApplication
{
    public class Program
    {
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

        private bool _pauseAtEnd = true; // Should be true in non-CI scenarios so that double-clicking a batch file keeps the result visible
        private readonly ServiceProvider _services;

        private void RunDsoFspDemo()
        {
            foreach (var operation in Operations().Where(o => o.name.StartsWith("fsp") || o.name.StartsWith("dso")))
            {
                operation.action();
            }
        }

        public static void Main(params string[] args)
        {
            WriteLine("Welcome to Nodes Client Example!");
            new Program().Run(args);
        }

        public Program()
        {
            var configRoot = BuildConfigurationRoot();
            _services = new ServiceCollection()
                .AddSingleton(configRoot)
                .AddSingleton<HttpClient, HttpClient>()
                .AddSingleton(CreateDefaultClient(configRoot))
                .AddSingleton<DSO>()
                .AddSingleton<FSP>()
                .AddSingleton<DeviceDemo>()
                .BuildServiceProvider();
        }

        public NodesClient CreateDefaultClient(IConfigurationRoot configRoot)
        {
            var apiUrl = configRoot.GetValue<string>("APIUrl");
            WriteLine($"Connecting to {apiUrl}");
            var client = new NodesClient(apiUrl)
            {
                AuthorizationTokenProvider = x =>
                {
                    var section = configRoot.GetSection("Authentication");
                    var req = new ClientCredentialsTokenRequest
                    {
                        Address = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.Address)),
                        Scope = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.Scope)),
                        ClientId = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.ClientId)),
                        ClientSecret = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.ClientSecret)),
                    };
                    TokenResponse response = x.RequestClientCredentialsTokenAsync(req).GetAwaiter().GetResult();
                    if (IsNullOrEmpty(response.AccessToken) || response.Error != null)
                        throw new Exception(response.Error + "; " + response.ErrorDescription);
                    return response.AccessToken;
                }
            };


            return client;
        }

        public void Run(string[] args)
        {
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

        private IConfigurationRoot BuildConfigurationRoot() =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional:true)
                .Build();

        public void ShowHelp()
        {
            WriteLine("Commands: ");
            Operations()
                .Select(n => "   " + n.name).ToList()
                .ForEach(WriteLine);
        }
    }
}