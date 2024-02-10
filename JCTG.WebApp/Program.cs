using JCTG;
using JCTG.WebApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Websocket.Client;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<JCTGDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING"))
    .UseLoggerFactory(LoggerFactory.Create(builder =>
       builder.AddFilter((category, level) =>
           category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));

builder.Services.AddAzurePubSubClient(builder.Configuration.GetConnectionString("AZURE_PUBSUB_CONNECTIONSTRING"));
builder.Services.AddAzurePubSubServer();

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapControllers();
app.UseWebSockets();



app.Run();
