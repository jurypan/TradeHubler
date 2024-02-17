using System.Globalization;
using System.Text;

namespace JCTG.WebApp.Frontend.Components.Tradingview;

public class ChartService
{
    public ChartService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    HttpClient _httpClient;

    public async Task<List<Candle>> GetSampleData()
        => (await ReadCsvAsync<Candle>("/sample-data.csv")).ToList();

    public async Task<List<Marker>> GetSampleMarkers()
        => await ReadCsvAsync<Marker>("/sample-markers.csv");
    public async Task<List<PricePoint>> GetSampleLineData()
        => (await GetSampleData())
        .Select(x => new PricePoint()
        {
            Time = x.Time,
            Price = x.Close,
            Volume = x.Volume
        }).ToList();

    private async Task<List<T>> ReadCsvAsync<T>(string url)
    {
        using (Stream receiveStream = await _httpClient.GetStreamAsync(url))
        using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
        using (var csv = new CsvHelper.CsvReader(readStream, CultureInfo.InvariantCulture))
            return csv.GetRecords<T>().ToList();
    }
}