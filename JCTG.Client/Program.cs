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
            ArgumentNullException.ThrowIfNull(args);

            Console.WriteLine("Starting the application ... ");
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalExceptionHandler);

            try
            {
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
            catch (Exception ex)
            {
                LogException(ex);
                ShowException(ex);
            }
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

        static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception ex = (Exception)args.ExceptionObject;
            LogException(ex);
            ShowException(ex);
        }

        static void LogException(Exception ex)
        {
            string logFilePath = "crash.log";
            using StreamWriter writer = new(logFilePath, true);
            writer.WriteLine($"[{DateTime.Now}] Exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                writer.WriteLine($"[{DateTime.Now}] Inner Exception: {ex.Message}");
            }
            writer.WriteLine($"Stack Trace: {ex.StackTrace}");
            writer.WriteLine();
        }

        static void ShowException(Exception ex)
        {
            Console.WriteLine("An unexpected error occurred:");
            Console.WriteLine(ex.Message);
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}