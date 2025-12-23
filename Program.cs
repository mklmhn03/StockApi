using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

app.MapGet("/stock/{symbol}", async (string symbol, IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient();
    // simulate a browser request
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    
    var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=1mo&interval=15m";
    var response = await client.GetAsync(url);
    var json = await response.Content.ReadAsStringAsync();

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    
    var yahooData = JsonSerializer.Deserialize<YahooResponse>(json, options);
    var result = yahooData.Chart.Result[0];

    var points = new List<StockPoint>();
    var quote = result.Indicators.Quote[0];

    for (int i = 0; i < result.Timestamp.Count; i++)
    {
        if (quote.Low[i] != null && quote.High[i] != null && quote.Volume[i] != null)
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp[i]).DateTime;
            var p = new StockPoint();
            p.Date = date.ToString("yyyy-MM-dd");
            p.Low = quote.Low[i].Value;
            p.High = quote.High[i].Value;
            p.Volume = quote.Volume[i].Value;
            points.Add(p);
        }
    }

    var grouped = points.GroupBy(p => p.Date).Select(g => new
    {
        day = g.Key,
        lowAverage = Math.Round(g.Average(x => x.Low), 4),
        highAverage = Math.Round(g.Average(x => x.High), 4),
        volume = g.Sum(x => x.Volume)
    }).OrderBy(x => x.day);

    return Results.Ok(grouped);
});

app.Run();

public class StockPoint
{
    public string Date { get; set; }
    public double Low { get; set; }
    public double High { get; set; }
    public long Volume { get; set; }
}

public class YahooResponse
{
    public ChartData Chart { get; set; }
}

public class ChartData
{
    public List<ChartResult> Result { get; set; }
}

public class ChartResult
{
    public List<long> Timestamp { get; set; }
    public Indicators Indicators { get; set; }
}

public class Indicators
{
    public List<QuoteData> Quote { get; set; }
}

public class QuoteData
{
    public List<double?> Low { get; set; }
    public List<double?> High { get; set; }
    public List<long?> Volume { get; set; }
}