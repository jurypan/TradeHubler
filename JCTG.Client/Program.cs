using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace JCTG.Client
{
    public class Program
    {
        // Public static property for IServiceProvider
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
                    await metatrader.ListenToAzureWebPubSubAsync();
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

            // Init Depdency Injection class
            var services = new ServiceCollection();

            // Add other services
            services.AddTransient<Metatrader>();
            services.AddTransient<AzureFunctionApiClient>();

            // Register configuration instance with DI container
            services.AddSingleton(config);

            return services.BuildServiceProvider();
        }
    }
}