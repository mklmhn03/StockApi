using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

app.MapGet("/", () => "Stock API is running");

app.MapGet("/stock/{symbol}", async (string symbol, IHttpClientFactory clientFactory) => 
{
    return new 
    { 
        Status = "Online", 
        ServerTime = DateTime.UtcNow, 
        Version = "0.1",
        Symbol = symbol
    };
});

app.Run();