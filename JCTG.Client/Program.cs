using Azure.Messaging.WebPubSub;
using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using Websocket.Client;

namespace JCTG.Client
{
    public class Program
    {
        private static string _pubsubConnectionString = "Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;";
        public static IServiceProvider? Service { get; private set; }

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting the application ... ");
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            Service = await ConfigureServicesAsync();

            // Use ServiceProvider to get services as needed
            if (Service != null)
            {
                var metatrader = Service.GetService<Metatrader>();
                if (metatrader != null)
                {
                    try
                    {
                        await metatrader.ListenToTheClientsAsync();
                        await metatrader.ListenToTheServerAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Caught exception: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }
            }

            Console.ReadLine();
        }

        private static async Task<IServiceProvider> ConfigureServicesAsync()
        {
            // Load TerminalConfig 
            TerminalConfig? config = await HttpCall.GetTerminalConfigAsync();
            if (config == null)
                throw new Exception("Can not load config file");

            // Init Dependency Injection class
            var services = new ServiceCollection();

            // Add other services
            services.AddTransient<Metatrader>();

            // Register configuration instance with DI container
            services.AddSingleton(config);

            var serviceClient = new WebPubSubServiceClient(_pubsubConnectionString, "account" + config.AccountId.ToString());
            var client = new WebsocketClient(serviceClient.GetClientAccessUri());
            services.AddSingleton(new AzurePubSubClient(client));

            return services.BuildServiceProvider();
        }

        static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Console.WriteLine($"Unhandled exception caught: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}