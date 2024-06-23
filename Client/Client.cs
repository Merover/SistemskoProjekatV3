using Newtonsoft.Json;
using System;
using System.Net.Http;
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

public class WeatherClient
{
    private readonly HttpClient _client;

    public WeatherClient()
    {
        _client = new HttpClient();
    }

    public async Task Start()
    {
        Console.WriteLine("Enter city name (or 'exit' to quit):");
        string input;
        while ((input = Console.ReadLine()) != "exit")
        {
            await FetchWeather(input);
            Console.WriteLine("\nEnter another city name (or 'exit' to quit):");
        }
    }

    private async Task FetchWeather(string cityName)
    {
        string url = $"http://localhost:5000/weather?city={cityName}";

        try
        {
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            if (json.Contains("not found"))
            {
                Console.WriteLine($"City '{cityName}' does not exist");
            }
            else
            {
                var weatherData = JsonConvert.DeserializeObject<WeatherData>(json);
                DisplayWeatherInfo(weatherData);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }


    private void DisplayWeatherInfo(WeatherData data)
    {
        Console.WriteLine($"Average Humidity: {data.AverageHumidity}");
        Console.WriteLine($"Min Humidity: {data.MinHumidity}");
        Console.WriteLine($"Max Humidity: {data.MaxHumidity}");
        Console.WriteLine($"Average Visibility: {data.AverageVisibility}");
        Console.WriteLine($"Min Visibility: {data.MinVisibility}");
        Console.WriteLine($"Max Visibility: {data.MaxVisibility}");
        Console.WriteLine($"Average UV Index: {data.AverageUVIndex}");
        Console.WriteLine($"Min UV Index: {data.MinUVIndex}");
        Console.WriteLine($"Max UV Index: {data.MaxUVIndex}");
    }
}

class Program
{
    public static async Task Main(string[] args)
    {
        var client = new WeatherClient();
        await client.Start();
    }
}