using JCTG;
using JCTG.WebApp.Data;
using Microsoft.EntityFrameworkCore;

var sqlConnectionString = "Server=tcp:justcalltheguy.database.windows.net,1433;Initial Catalog=justcalltheguy;Persist Security Info=False;User ID=joeri.pansaerts;Password=*4EJQPCuksV&!BG8;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddDbContext<JCTGDbContext>(
    options => options.UseSqlServer(sqlConnectionString)
    .UseLoggerFactory(LoggerFactory.Create(builder =>
       builder.AddFilter((category, level) =>
           category != DbLoggerCategory.Database.Command.Name || level > LogLevel.Information))));
builder.Services.AddSingleton<PairService>();

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

app.Run();
