using JCTG;
using JCTG.WebApp.Backend.Middleware;
using JCTG.WebApp.Backend.Repository;
using JCTG.WebApp.Backend.Websocket;
using JCTG.WebApp.Frontend.Components.Tradingview;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7197/")
});
builder.Services.AddTransient<ChartService>();


// Add DB Context
builder.Services.AddDbContextFactory<JCTGDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING"))
    .UseLoggerFactory(LoggerFactory.Create(builder =>
       builder.AddFilter((category, level) =>
           category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));

// Add Websocket Server <-> Terminal
builder.Services.AddAzurePubSubClient(builder.Configuration.GetConnectionString("AZURE_PUBSUB_CONNECTIONSTRING"));
builder.Services.AddAzurePubSubServer();

// Add services to the scope
builder.Services.AddTransient<SignalRepository>();
builder.Services.AddTransient<OrderRepository>();
builder.Services.AddTransient<LogRepository>();
builder.Services.AddTransient<ClientRepository>();
builder.Services.AddTransient<DealRepository>();

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

using (var serviceScope = app.Services.CreateScope())
{
    app.Logger.LogInformation("Starting the app");
    if (!app.Environment.IsDevelopment())
        await serviceScope.ServiceProvider.GetRequiredService<WebsocketServer>().RunAsync();
    app.Run();
}
