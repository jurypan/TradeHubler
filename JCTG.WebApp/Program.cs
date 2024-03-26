using JCTG;
using JCTG.WebApp.Backend.Middleware;
using JCTG.WebApp.Backend.Queue;
using JCTG.WebApp.Backend.Repository;
using JCTG.WebApp.Frontend.Components.Apex;
using JCTG.WebApp.Frontend.Components.Tradingview;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Determine the base address dynamically based on the environment
var baseAddress = builder.Environment.IsDevelopment()
    ? "https://localhost:7197/" // Development base URL
    : "http://justcalltheguy.westeurope.cloudapp.azure.com/"; // Production base URL

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
       .AddNewtonsoftJson(options =>
       {
           options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
       });
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(baseAddress)
});


// Add DB Context
builder.Services.AddDbContextFactory<JCTGDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING"))
    .UseLoggerFactory(LoggerFactory.Create(builder =>
       builder.AddFilter((category, level) =>
           category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));

// Add Queue Server <-> Terminal
builder.Services.AddAzureQueueServer();

// Add Components
builder.Services.InitTradingview();
builder.Services.InitApex();

// Add services to the scope
builder.Services.AddTransient<SignalRepository>();
builder.Services.AddTransient<OrderRepository>();
builder.Services.AddTransient<LogRepository>();
builder.Services.AddTransient<ClientRepository>();
builder.Services.AddTransient<DealRepository>();
builder.Services.AddTransient<TradingviewAlertRepository>();
builder.Services.AddTransient<MarketAbstentionRepository>();

// Init the frontend pages
builder.Services.Configure<RazorPagesOptions>(options =>
{
    options.RootDirectory = "/Frontend/Pages";
});

// Init logging
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}


//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapControllers();
app.UseWebSockets();

app.Run();