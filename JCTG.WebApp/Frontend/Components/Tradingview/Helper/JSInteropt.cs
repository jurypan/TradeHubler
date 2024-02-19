using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static JCTG.WebApp.Frontend.Components.Tradingview.Marker;

namespace JCTG.WebApp.Frontend.Components.Tradingview;

public class JSInteropt(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./js/JSInteropt.js?v=2").AsTask());

    public async Task InitAsync(ElementReference eleRef, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("initTradingView", eleRef, eleRef.Id, options);
    }

    public async Task AddCandleStickSeriesAsync(ElementReference eleRef, List<BarData> data, ChartOptions options)
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
            color = x.Color
        }), options);
    }

    public async Task UpdateCandleStickSeriesAsync(ElementReference eleRef, List<BarData> data)
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
            color = x.Color
        }));
    }

    public async Task UpdateCandleStickAsync(ElementReference eleRef, Tick data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateCandleStick", eleRef, eleRef.Id, new
        {
            time = new DateTimeOffset(data.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = data.Price,
        });
    }

    public async Task SetMarkersToCandlestickSeriesAsync(ElementReference eleRef, List<Marker> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;

        // Inline method
        Func<MarkerShape, string> getShape = (shape) =>
        {
            if (shape == MarkerShape.Square)
                return "square";
            else if (shape == MarkerShape.Circle)
                return "circle";
            else if (shape == MarkerShape.ArrowUp)
                return "arrowUp";
            else if (shape == MarkerShape.ArrowDown)
                return "arrowDown";
            else
                return "arrowDown";
        };

        // Inline method
        Func<MarkerPosition, string> getPosition = (position) =>
        {
            if (position == MarkerPosition.AboveBar)
                return "aboveBar";
            else if (position == MarkerPosition.BelowBar)
                return "belowBar";
            else if (position == MarkerPosition.InBar)
                return "inBar";
            else
                return "belowBar";
        };

        // Invoke javascript
        await module.InvokeVoidAsync("setMarkersToCandlestickSeriesAsync", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            position = getPosition(x.Position),
            shape = getShape(x.Shape),
            color = x.Color,
            id = x.Id,
            text = x.Text,
            size = x.Size,
        }));
    }

    public async Task AddAreaSeriesAsync(ElementReference eleRef, List<AreaPoint> data, ChartOptions options)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addAreaSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.DisplayPrice,
        }), options);
    }

    public async Task UpdateAreaSeriesAsync(ElementReference eleRef, List<AreaPoint> data)
    {
        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateAreaSeries", eleRef, eleRef.Id, data.Select(x => new
        {
            time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
            value = x.DisplayPrice,
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

    public async Task AddVolumeSeriesAsync(ElementReference eleRef, List<BarData> data, ChartOptions options)
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
            color = getVolumeColor(x.Close),
        }), options);
    }

    public async Task UpdateVolumeSeriesAsync(ElementReference eleRef, List<BarData> data, ChartOptions options)
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
            color = getVolumeColor(x.Close),
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