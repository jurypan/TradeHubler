using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;

namespace JCTG.Client
{
    public class Program
    {
        private static string _queueConnectionString = "DefaultEndpointsProtocol=https;AccountName=justcalltheguy;AccountKey=nGb/y75LMEqV1hGgvZn77Lm94fBkaVLU0wFZoQpxPbDpc0VVQnG8NTX3+EWyPg1L1196N4JKxTtq+AStKOTpAg==;EndpointSuffix=core.windows.net";
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

            // Queue
            services.AddSingleton(new AzureQueueClient(_queueConnectionString, config.AccountId));

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