using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Tasks;

public class WeatherData
{
    public double AverageHumidity { get; set; }
    public double MinHumidity { get; set; }
    public double MaxHumidity { get; set; }
    public double AverageVisibility { get; set; }
    public double MinVisibility { get; set; }
    public double MaxVisibility { get; set; }
    public double AverageUVIndex { get; set; }
    public double MinUVIndex { get; set; }
    public double MaxUVIndex { get; set; }
}

public class OpenMeteoService
{
    private readonly HttpClient _client;

    public OpenMeteoService()
    {
        _client = new HttpClient();
    }

    public async Task<(float, float)> GeocodeCity(string cityName)
    {
        string apiUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={cityName}";

        try
        {
            var response = await _client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode(); // Ensure HTTP 200 OK status

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(json).RootElement;

            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || !results.EnumerateArray().Any())
            {
                throw new Exception($"City '{cityName}' not found.");
            }

            var result = results.EnumerateArray().First();
            var latitude = result.GetProperty("latitude").GetSingle();
            var longitude = result.GetProperty("longitude").GetSingle();

            return (latitude, longitude);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"City not found: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetWeatherForecast(float latitude, float longitude)
    {
        string apiUrl = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=relative_humidity_2m,visibility&daily=uv_index_max";

        try
        {
            var response = await _client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode(); // Ensure HTTP 200 OK status

            var json = await response.Content.ReadAsStringAsync();
            return json;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }
}

public class WebServer
{
    private readonly HttpListener _listener;
    private readonly Subject<HttpListenerContext> _subject;

    public WebServer(string[] prefixes)
    {
        _listener = new HttpListener();
        foreach (string prefix in prefixes)
        {
            _listener.Prefixes.Add(prefix);
        }
        _subject = new Subject<HttpListenerContext>();
    }

    public void Start()
    {
        _listener.Start();
        Console.WriteLine("Server started...");

        Observable.FromAsync(_listener.GetContextAsync)
            .Repeat()
            .Subscribe(
                context => _subject.OnNext(context),
                ex => _subject.OnError(ex),
                () => _subject.OnCompleted()
            );

        _subject.Subscribe(
            async context =>
            {
                try
                {
                    string responseString = await HandleRequestAsync(context.Request);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                    context.Response.ContentLength64 = buffer.Length;
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();

                    Console.WriteLine($"Request processed: {context.Request.Url}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing request: {ex.Message}");
                }
            },
            ex => Console.WriteLine($"Stream error: {ex.Message}")
        );
    }

    private async Task<string> HandleRequestAsync(HttpListenerRequest request)
    {
        if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/weather")
        {
            string cityName = request.QueryString["city"];
            if (string.IsNullOrEmpty(cityName))
            {
                return "City name is required";
            }

            var service = new OpenMeteoService();
            try
            {
                var (latitude, longitude) = await service.GeocodeCity(cityName);
                var weatherDataJson = await service.GetWeatherForecast(latitude, longitude);
                var weatherData = ParseWeatherData(weatherDataJson);

                return JsonSerializer.Serialize(weatherData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request for city '{cityName}': {ex.Message}");
                return $"City '{cityName}' not found";
            }
        }
        return "Invalid request";
    }

    private WeatherData ParseWeatherData(string json)
    {
        var weatherData = new WeatherData();

        try
        {
            var jsonObject = JsonDocument.Parse(json).RootElement;

            // Accessing hourly data
            if (jsonObject.TryGetProperty("hourly", out var hourlyProperty) && hourlyProperty.ValueKind == JsonValueKind.Object)
            {
                // Getting arrays of humidity and visibility
                var humidityValues = hourlyProperty.GetProperty("relative_humidity_2m").EnumerateArray().Select(h => h.GetDouble());
                var visibilityValues = hourlyProperty.GetProperty("visibility").EnumerateArray().Select(v => v.GetDouble());

                // Setting average, min, and max values
                weatherData.AverageHumidity = humidityValues.Average();
                weatherData.MinHumidity = humidityValues.Min();
                weatherData.MaxHumidity = humidityValues.Max();
                weatherData.AverageVisibility = visibilityValues.Average();
                weatherData.MinVisibility = visibilityValues.Min();
                weatherData.MaxVisibility = visibilityValues.Max();
            }
            else
            {
                throw new Exception("Invalid JSON format or missing 'hourly' property.");
            }

            // Accessing daily data
            if (jsonObject.TryGetProperty("daily", out var dailyProperty) && dailyProperty.ValueKind == JsonValueKind.Object)
            {
                // Getting array of max UV indices
                var uvIndexValues = dailyProperty.GetProperty("uv_index_max").EnumerateArray().Select(u => u.GetDouble());

                // Setting average, min, and max values
                weatherData.AverageUVIndex = uvIndexValues.Average();
                weatherData.MinUVIndex = uvIndexValues.Min();
                weatherData.MaxUVIndex = uvIndexValues.Max();
            }
            else
            {
                throw new Exception("Invalid JSON format or missing 'daily' property.");
            }
        }
        catch (JsonException ex)
        {
            throw new Exception("Error parsing JSON response.", ex);
        }

        return weatherData;
    }

    public void Stop()
    {
        _listener.Stop();
        _listener.Close();
    }
}

class Program
{
    public static void Main(string[] args)
    {
        string[] prefixes = { "http://localhost:5000/" };
        var server = new WebServer(prefixes);

        server.Start();

        Console.WriteLine("Press Enter to stop the server...");
        Console.ReadLine();

        server.Stop();
    }
}