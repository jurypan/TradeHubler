using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JCTG.WebApp.Frontend.Components.Apex;

public class JSInteropt(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./js/JSApexInteropt.js?v=" + new Random().NextInt64()).AsTask());


    public async Task AreaChartInitAsync(ElementReference eleRef, string name, List<AreaPoint> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexAreaChartInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }).ToList());
    }

    public async Task AreaChartMiniInitAsync(ElementReference eleRef, string name, List<AreaPoint> data, string color)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexAreaChartMiniInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }).ToList(), color);
    }

    public async Task LineChartInitAsync(ElementReference eleRef, string name1, List<LinePoint> data1, string name2, List<LinePoint> data2)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        await module.InvokeVoidAsync("apexLineChartInit", eleRef, eleRef.Id, name1, data1.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }), name2, data2.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }).ToList());
    }

    public async Task LineChartUpdateAsync(ElementReference eleRef, string name1, List<LinePoint> data1, string name2, List<LinePoint> data2)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        await module.InvokeVoidAsync("apexLineChartUpdate", eleRef, eleRef.Id, name1, data1.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }), name2, data2.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            y = x.Price,
        }).ToList());
    }

    public async Task CandleChartInitAsync(ElementReference eleRef, string name, List<BarData> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("apexCandleChartInit", eleRef, eleRef.Id, name, data.Select(x => new
        {
            x = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeMilliseconds(),
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