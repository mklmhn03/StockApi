using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Register a named httpclient to manage headers centrally
builder.Services.AddHttpClient("YahooFinance", client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com");

    // Yahoo blocks requests without user agent so this simulates browser
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
});

var app = builder.Build();

app.MapGet("/stock/{symbol}", async (string symbol, IHttpClientFactory clientFactory) =>
{
    // Fetch data from Yahoo
    var client = clientFactory.CreateClient("YahooFinance");
    var url = $"/v8/finance/chart/{symbol}?range=1mo&interval=15m";
    
    try 
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        
        // Deserialize with case insensitive and number handling options
        var yahooData = JsonSerializer.Deserialize<YahooResponse>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString 
        });

        // Prevent crashes on empty response
        var result = yahooData?.Chart?.Result?.FirstOrDefault();
        if (result?.Timestamp is null || result.Indicators?.Quote is null)
        {
            return Results.NotFound(new { message = "no data found for symbol" });
        }

        var quotes = result.Indicators.Quote.FirstOrDefault();
        if (quotes is null) return Results.NotFound("no quotes available");

        var dataPoints = new List<IntradayPoint>();

        // Yahoo splits time and price into arrays so loop by index to zip into single object
        for (int i = 0; i < result.Timestamp.Count; i++)
        {
            // Skip nulls if market closed or glitch
            if (quotes.Low[i] is null || quotes.High[i] is null || quotes.Volume[i] is null)
                continue;

            var date = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp[i]).DateTime;

            // Positional record for concise syntax
            dataPoints.Add(new IntradayPoint(
                date.ToString("yyyy-MM-dd"), 
                quotes.Low[i]!.Value, 
                quotes.High[i]!.Value, 
                quotes.Volume[i]!.Value
            ));
        }

        // Group by day string and aggregate data
        var groupedData = dataPoints
            .GroupBy(x => x.Day)
            .Select(g => new 
            {
                day = g.Key,
                lowAverage = Math.Round(g.Average(x => x.Low), 4),
                highAverage = Math.Round(g.Average(x => x.High), 4),
                volume = g.Sum(x => x.Volume)
            })
            .OrderBy(x => x.day)
            .ToList();

        return Results.Ok(groupedData);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"error fetching yahoo data {ex.Message}");
    }
});

app.Run();

// Positional record for internal immutable processing
record IntradayPoint(string Day, double Low, double High, long Volume);

// Standard records for external API mapping; init ensures immutability after creation
record YahooResponse
{
    public ChartData? Chart { get; init; }
}

record ChartData
{
    public List<ChartResult>? Result { get; init; }
}

record ChartResult
{
    public List<long>? Timestamp { get; init; }
    public Indicators? Indicators { get; init; }
}

record Indicators
{
    public List<QuoteData>? Quote { get; init; }
}

record QuoteData
{
    // nullable types handle gaps in trading data
    public List<double?>? Low { get; init; }
    public List<double?>? High { get; init; }
    public List<long?>? Volume { get; init; }
}