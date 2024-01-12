using JCTG;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;



var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults()
        .ConfigureAppConfiguration(builder =>
        {

        })
        .ConfigureServices(services =>
        {
            var connectionString = "Server=tcp:justcalltheguy.database.windows.net,1433;Initial Catalog=justcalltheguy;Persist Security Info=False;User ID=joeri.pansaerts;Password=*4EJQPCuksV&!BG8;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30";
            services.AddDbContext<JCTGDbContext>(
                options => options.UseSqlServer(connectionString)
                .UseLoggerFactory(LoggerFactory.Create(builder =>
                   builder.AddFilter((category, level) =>
                       category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));
        })
        //.ConfigureFunctionsWorkerDefaults(app =>
        //{

        //})
        .Build();

host.Run();

