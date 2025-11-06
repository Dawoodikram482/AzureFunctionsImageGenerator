namespace WeatherImageGenerator.Models;

public class StartJobRequest
{
    public string? JobId { get; set; }
}

public class StartJobResponse
{
    public string? JobId { get; set; }
    public string? Message { get; set; }
    public string? StatusUrl { get; set; }
}

public class JobStatusResponse
{
    public string? JobId { get; set; }
    public string? Status { get; set; }
    public int TotalStations { get; set; }
    public int ProcessedStations { get; set; }
    public int ProgressPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
