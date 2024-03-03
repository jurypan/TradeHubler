using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JCTG.WebApp.Frontend.Components.Apex;

public class JSInteropt(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./js/JSApexInteropt.js?v=4").AsTask());


    public async Task AreaChartInitAsync(ElementReference eleRef, string name, List<AreaPoint> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexAreaChartInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            y = x.Price,
        }).ToList());
    }

    public async Task AreaChartMiniInitAsync(ElementReference eleRef, string name, List<AreaPoint> data, string color)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexAreaChartMiniInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            y = x.Price,
        }).ToList(), color);
    }


    public async Task CandleChartInitAsync(ElementReference eleRef, string name, List<BarData> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexCandleChartInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            y = new object[] { x.Open, x.High, x.Low, x.Close }
        }).ToList());
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask != null && moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}