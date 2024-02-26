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
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Color
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            open = x.Open,
            high = x.High,
            low = x.Low,
            close = x.Close,
            color = x.Color
        }).ToList();

        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addCandleStickSeries", eleRef, eleRef.Id, jsData, options);
    }


    public async Task UpdateCandleStickSeriesAsync(ElementReference eleRef, List<BarData> data)
    {
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Color
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            open = x.Open,
            high = x.High,
            low = x.Low,
            close = x.Close,
            color = x.Color
        }).ToList();

        // Call updateChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateCandleStickSeries", eleRef, eleRef.Id, jsData);
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

        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                Position = getPosition(x.Position),
                Shape = getShape(x.Shape),
                x.Color,
                x.Id,
                x.Text,
                x.Size,
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            position = x.Position,
            shape = x.Shape,
            color = x.Color,
            id = x.Id,
            text = x.Text,
            size = x.Size,
        }).ToList();

        // Call setMarkers JS function
        await module.InvokeVoidAsync("setMarkersToCandlestickSeriesAsync", eleRef, eleRef.Id, jsData);
    }

    public async Task AddAreaSeriesAsync(ElementReference eleRef, List<AreaPoint> data, ChartOptions options)
    {
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.DisplayPrice,
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            value = x.DisplayPrice,
        }).ToList();

        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addAreaSeries", eleRef, eleRef.Id, jsData, options);
    }


    public async Task UpdateAreaSeriesAsync(ElementReference eleRef, List<AreaPoint> data)
    {
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.DisplayPrice,
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            value = x.DisplayPrice,
        }).ToList();

        // Call updateChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateAreaSeries", eleRef, eleRef.Id, jsData);
    }

    public async Task AddLineSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.Price,
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            value = x.Price,
        }).ToList();

        // Call loadChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addLineSeries", eleRef, eleRef.Id, jsData, options);
    }

    public async Task UpdateLineSeriesAsync(ElementReference eleRef, List<PricePoint> data)
    {
        // Remove duplicates: Keep the first occurrence of each time and discard the others
        var filteredData = data
            // Convert to intermediate object with Unix time to ease comparison and sorting
            .Select(x => new
            {
                Time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                x.Price,
            })
            // Group by the Unix time to find duplicates
            .GroupBy(x => x.Time)
            // Select the first occurrence from each group
            .Select(g => g.First())
            // Order by time to ensure the series is chronological
            .OrderBy(x => x.Time)
            .ToList();

        // Prepare the data for the JavaScript function
        var jsData = filteredData.Select(x => new
        {
            time = x.Time,
            value = x.Price,
        }).ToList();

        // Call updateChart JS function
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateLineSeries", eleRef, eleRef.Id, jsData);
    }


    public async Task AddVolumeSeriesAsync(ElementReference eleRef, List<BarData> data, ChartOptions options)
    {
        // Initialize lastPrice outside the selection to maintain state across the data
        decimal lastPrice = 0;

        // Deduplicate and order data first, assuming color logic can adapt based on deduplicated data
        var orderedData = data
            .GroupBy(x => new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds())
            .Select(g => g.First()) // Take the first occurrence in each group
            .OrderBy(x => x.Time)
            .ToList();

        // Now apply color logic on ordered, deduplicated data
        var volumeData = orderedData.Select(x =>
        {
            var color = lastPrice < x.Close ? options.VolumeColorUp : options.VolumeColorDown;
            lastPrice = x.Close; // Update lastPrice for the next iteration
            return new
            {
                time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                value = x.Volume,
                color = color,
            };
        }).ToList();

        // Call loadChart JS function with deduplicated, color-coded data
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addVolumeSeries", eleRef, eleRef.Id, volumeData, options);
    }

    public async Task UpdateVolumeSeriesAsync(ElementReference eleRef, List<BarData> data, ChartOptions options)
    {
        // Initialize lastPrice for color determination
        decimal lastPrice = 0;

        // Deduplicate and order data
        var orderedData = data
            .GroupBy(x => new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds())
            .Select(g => g.First()) // Take the first occurrence in each group
            .OrderBy(x => x.Time)
            .ToList();

        // Apply color logic
        var volumeData = orderedData.Select(x =>
        {
            var color = lastPrice < x.Close ? options.VolumeColorUp : options.VolumeColorDown;
            lastPrice = x.Close;
            return new
            {
                time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                value = x.Volume,
                color = color,
            };
        }).ToList();

        // Invoke update
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateVolumeSeries", eleRef, eleRef.Id, volumeData);
    }


    public async Task AddVolumeSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        var filteredData = data
                            .GroupBy(x => new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds())
                            .Select(g => g.First())
                            .OrderBy(x => x.Time)
                            .ToList();

        // Extract volume data
        decimal lastPrice = 0;
        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        // Invoke
        var volumeData = filteredData.Select(x => {
            return new
            {
                time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                value = x.Volume,
                color = getVolumeColor(x.DisplayPrice), // Apply color logic based on the DisplayPrice
        };
        }).ToList();

        // Call loadChart JS function with processed data
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addVolumeSeries", eleRef, eleRef.Id, volumeData, options);
    }

    public async Task UpdateVolumeSeriesAsync(ElementReference eleRef, List<PricePoint> data, ChartOptions options)
    {
        decimal lastPrice = 0;

        var filteredData = data
            .GroupBy(x => new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds())
            .Select(g => g.First())
            .OrderBy(x => x.Time)
            .ToList();

        Func<decimal, string> getVolumeColor = (nextPrice) =>
        {
            bool isLower = lastPrice < nextPrice;
            lastPrice = nextPrice;
            return isLower ? options.VolumeColorUp : options.VolumeColorDown;
        };

        var volumeData = filteredData.Select(x => {
            return new
            {
                time = new DateTimeOffset(x.Time, TimeSpan.Zero).ToUnixTimeSeconds(),
                value = x.Volume,
                color = getVolumeColor(x.DisplayPrice),
            };
        }).ToList();

        // Invoke update with processed data
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("updateVolumeSeries", eleRef, eleRef.Id, volumeData);
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