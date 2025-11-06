using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public class ProcessStationQueueFunction
{
    private readonly ILogger<ProcessStationQueueFunction> _logger;
    private readonly IPixabayService _pixabayService;
    private readonly IImageService _imageService;
    private readonly IStorageService _storageService;

    public ProcessStationQueueFunction(
        ILogger<ProcessStationQueueFunction> logger,
        IPixabayService pixabayService,
        IImageService imageService,
        IStorageService storageService)
    {
        _logger = logger;
        _pixabayService = pixabayService;
        _imageService = imageService;
        _storageService = storageService;
    }

    [Function("ProcessStationQueue")]
    public async Task Run(
        [QueueTrigger("process-station-queue", Connection = "AzureWebJobsStorage")] string message)
    {
        _logger.LogInformation($"Processing station queue message");

        try
        {
            var stationMessage = JsonSerializer.Deserialize<ProcessStationMessage>(message);
            if (stationMessage?.JobId == null || stationMessage.Station == null)
            {
                _logger.LogError("Invalid station message");
                return;
            }

            var station = stationMessage.Station;
            _logger.LogInformation($"Processing station: {station.StationName} (ID: {station.StationId})");

            // 1. Fetch random image from Pixabay
            _logger.LogInformation("Fetching random image from Pixabay");
            var imageBytes = await _pixabayService.GetRandomImageAsync();

            // 2. Add weather data to image
            _logger.LogInformation("Adding weather data to image");
            var processedImageBytes = await _imageService.AddWeatherDataToImageAsync(imageBytes, station);

            // 3. Upload to blob storage
            _logger.LogInformation("Uploading image to blob storage");
            var imageUrl = await _storageService.UploadImageAsync(
                stationMessage.JobId, 
                station.StationId, 
                processedImageBytes);

            // 4. Update job status
            _logger.LogInformation("Updating job status");
            await _storageService.AddImageToJobAsync(stationMessage.JobId, imageUrl);
            await _storageService.IncrementProcessedStationsAsync(stationMessage.JobId);

            _logger.LogInformation($"Successfully processed station {station.StationId}. Image URL: {imageUrl}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing station queue");
            // Don't rethrow - we want to continue processing other stations
        }
    }
}
