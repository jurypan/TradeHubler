using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace JCTG.WebApp.Frontend.Components.Tradingview;

public class JSInteropt(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./js/JSInteropt.js").AsTask());

    public async Task InitAsync(ElementReference eleRef, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("initTradingView", eleRef, eleRef.Id, options);
    }

    public async Task AddCandleStickSeriesAsync(ElementReference eleRef, List<CandlePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addCandleStickSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            open = x.Open,
            high = x.High,
            low = x.Low,
            close = x.Close,
        }), options);
    }

    public async Task UpdateCandleStickSeriesAsync(ElementReference eleRef, List<CandlePoint> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateCandleStickSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            open = x.Open,
            high = x.High,
            low = x.Low,
            close = x.Close,
        }));
    }

    public async Task AddLineSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addLineSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Price,
        }), options);
    }

    public async Task UpdateLineSeriesAsync(ElementReference eleRef, List<PricePoint> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateLineSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Price,
        }));
    }

    public async Task AddVolumeSeriesAsync(ElementReference eleRef, List<CandlePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        // Extract volume data
        decimal lastPrice = 0;
        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        await module.InvokeVoidAsync("addVolumeSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Volume,
            color = getVolumeColor(x.DisplayPrice),
        }), options);
    }

    public async Task UpdateVolumeSeriesAsync(ElementReference eleRef, List<CandlePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        // Extract volume data
        decimal lastPrice = 0;
        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        // Invoke
        await module.InvokeVoidAsync("updateVolumeSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Volume,
            color = getVolumeColor(x.DisplayPrice),
        }));
    }

    public async Task AddVolumeSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        // Extract volume data
        decimal lastPrice = 0;
        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        // Invoke
        await module.InvokeVoidAsync("addVolumeSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Volume,
            color = getVolumeColor(x.DisplayPrice),
        }), options);
    }

    public async Task UpdateVolumeSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        // Extract volume data
        decimal lastPrice = 0;
        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        // Invoke
        await module.InvokeVoidAsync("updateVolumeSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.Volume,
            color = getVolumeColor(x.DisplayPrice),
        }));
    }

    public async Task AddMarkersToCandleStickSeriesAsync(ElementReference eleRef, List<Marker> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addMarkersToCandleStickSeries", eleRef, eleRef.Id, data);
    }

    public async Task UpdateMarkersToCandleStickSeriesAsync(ElementReference eleRef, List<Marker> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateMarkersToCandleStickSeries", eleRef, eleRef.Id, data);
    }

    public async Task AddMarkersToLineSeriesAsync(ElementReference eleRef, List<Marker> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addMarkersToLineSeries", eleRef, eleRef.Id, data);
    }

    public async Task UpdateMarkersToLineSeriesAsync(ElementReference eleRef, List<Marker> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateMarkersToLineSeries", eleRef, eleRef.Id, data);
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