using JCTG;
using JCTG.WebApp;
using JCTG.WebApp.Helpers;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();

// Add DB Context
builder.Services.AddDbContext<JCTGDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING"))
    .UseLoggerFactory(LoggerFactory.Create(builder =>
       builder.AddFilter((category, level) =>
           category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));

// Add Websocket Server <-> Terminal
builder.Services.AddAzurePubSubClient(builder.Configuration.GetConnectionString("AZURE_PUBSUB_CONNECTIONSTRING"));
builder.Services.AddAzurePubSubServer();

// Init logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

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
    await serviceScope.ServiceProvider.GetRequiredService<WebsocketServer>().RunAsync();
    app.Run();
}
