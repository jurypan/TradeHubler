using Azure.Messaging.WebPubSub;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
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

            Service = ConfigureServices();

            // Use ServiceProvider to get services as needed
            if(Service != null)
            {
                var metatrader = Service.GetService<Metatrader>();
                if(metatrader != null)
                {
                    await metatrader.ListToTheClientsAsync();
                    await metatrader.ListenToTheServerAsync();
                }
            }
           
            Console.ReadLine();
        }

        private static IServiceProvider ConfigureServices()
        {
            // Load AppConfig 
            string json = File.ReadAllText("settings.json");
            AppConfig? config = JsonConvert.DeserializeObject<AppConfig>(json);
            if (config == null)
                throw new Exception("Can not load config file");

            // Init Dependency Injection class
            var services = new ServiceCollection();

            // Add other services
            services.AddTransient<Metatrader>();

            // Register configuration instance with DI container
            services.AddSingleton(config);

            var serviceClient = new WebPubSubServiceClient(_pubsubConnectionString, "a" + config.AccountId.ToString());
            var url = serviceClient.GetClientAccessUri();
            services.AddSingleton(new AzurePubSub(new WebsocketClient(url)));

            return services.BuildServiceProvider();
        }
    }
}