using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WeatherImageGenerator.Models;

namespace WeatherImageGenerator.Services;

public interface IBuienradarService
{
    Task<List<WeatherStation>> GetWeatherStationsAsync();
}

public class BuienradarService : IBuienradarService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public BuienradarService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiUrl = configuration["BuienradarApiUrl"] ?? "https://data.buienradar.nl/2.0/feed/json";
    }

    public async Task<List<WeatherStation>> GetWeatherStationsAsync()
    {
        var response = await _httpClient.GetStringAsync(_apiUrl);
        var jsonDoc = JsonDocument.Parse(response);
        
        var stations = new List<WeatherStation>();
        
        if (jsonDoc.RootElement.TryGetProperty("actual", out var actual) &&
            actual.TryGetProperty("stationmeasurements", out var measurements))
        {
            foreach (var station in measurements.EnumerateArray().Take(50))
            {
                var stationId = station.TryGetProperty("stationid", out var idElement)
                    ? ParseInt(idElement) ?? 0
                    : 0;

                stations.Add(new WeatherStation
                {
                    StationId = stationId,
                    StationName = station.TryGetProperty("stationname", out var name) ? name.GetString() : null,
                    Lat = station.TryGetProperty("lat", out var lat) ? ParseDouble(lat) : null,
                    Lon = station.TryGetProperty("lon", out var lon) ? ParseDouble(lon) : null,
                    RegionName = station.TryGetProperty("regio", out var region) ? region.GetString() : null,
                    Temperature = station.TryGetProperty("temperature", out var temp) ? ParseDouble(temp) : null,
                    WeatherDescription = station.TryGetProperty("weatherdescription", out var desc) ? desc.GetString() : null,
                    Humidity = station.TryGetProperty("humidity", out var humidity) ? ParseInt(humidity) : null,
                    WindSpeed = station.TryGetProperty("windspeed", out var windSpeed) ? ParseDouble(windSpeed) : null,
                    WindDirection = station.TryGetProperty("winddirection", out var windDir) ? windDir.GetString() : null
                });
            }
        }
        
        return stations;
    }

    private static int? ParseInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var numericValue) => numericValue,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static double? ParseDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}
