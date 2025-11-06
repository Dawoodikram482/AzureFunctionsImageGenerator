namespace WeatherImageGenerator.Models;

public class WeatherStation
{
    public int StationId { get; set; }
    public string? StationName { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? RegionName { get; set; }
    public double? Temperature { get; set; }
    public string? WeatherDescription { get; set; }
    public int? Humidity { get; set; }
    public double? WindSpeed { get; set; }
    public string? WindDirection { get; set; }
}
