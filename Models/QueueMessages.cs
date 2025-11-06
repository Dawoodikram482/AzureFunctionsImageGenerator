namespace WeatherImageGenerator.Models;

public class ProcessJobMessage
{
    public string? JobId { get; set; }
}

public class ProcessStationMessage
{
    public string? JobId { get; set; }
    public WeatherStation? Station { get; set; }
}
