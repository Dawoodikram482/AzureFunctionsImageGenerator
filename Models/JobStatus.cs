namespace WeatherImageGenerator.Models;

public class JobStatus
{
    public string? JobId { get; set; }
    public string? Status { get; set; } // "queued", "processing", "completed", "failed"
    public int TotalStations { get; set; }
    public int ProcessedStations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
