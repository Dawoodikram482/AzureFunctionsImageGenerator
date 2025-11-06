using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public class ProcessJobQueueFunction
{
    private readonly ILogger<ProcessJobQueueFunction> _logger;
    private readonly IBuienradarService _buienradarService;
    private readonly IStorageService _storageService;
    private readonly QueueClient _stationQueueClient;

    public ProcessJobQueueFunction(
        ILogger<ProcessJobQueueFunction> logger,
        IBuienradarService buienradarService,
        IStorageService storageService,
        IConfiguration configuration)
    {
        _logger = logger;
        _buienradarService = buienradarService;
        _storageService = storageService;
        
        var connectionString = configuration["AzureWebJobsStorage"];
        var queueOptions = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };
        _stationQueueClient = new QueueClient(connectionString, "process-station-queue", queueOptions);
    }

    [Function("ProcessJobQueue")]
    public async Task Run(
        [QueueTrigger("process-job-queue", Connection = "AzureWebJobsStorage")] string message)
    {
        _logger.LogInformation("=== QUEUE TRIGGER FIRED ===");
        _logger.LogInformation($"Processing job queue message: {message}");

        try
        {
            var jobMessage = JsonSerializer.Deserialize<ProcessJobMessage>(message);
            if (jobMessage?.JobId == null)
            {
                _logger.LogError("Invalid job message");
                return;
            }

            _logger.LogInformation($"Deserialized job message for JobId: {jobMessage.JobId}");

            // Update job status to processing
            await _storageService.UpdateJobStatusAsync(jobMessage.JobId, "processing");
            _logger.LogInformation("Updated job status to processing");

            // Fetch weather stations
            _logger.LogInformation("Fetching weather stations from Buienradar");
            var stations = await _buienradarService.GetWeatherStationsAsync();
            
            _logger.LogInformation($"Fetched {stations.Count} weather stations");

            // Ensure queue exists
            await _stationQueueClient.CreateIfNotExistsAsync();
            _logger.LogInformation("Station queue created/verified");

            // Fan-out: Queue a message for each station
            foreach (var station in stations)
            {
                var stationMessage = new ProcessStationMessage
                {
                    JobId = jobMessage.JobId,
                    Station = station
                };
                
                var stationMessageJson = JsonSerializer.Serialize(stationMessage);
                await _stationQueueClient.SendMessageAsync(stationMessageJson);
            }

            _logger.LogInformation($"Queued {stations.Count} station processing jobs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== EXCEPTION IN ProcessJobQueue ===");
            _logger.LogError($"Exception Type: {ex.GetType().Name}");
            _logger.LogError($"Exception Message: {ex.Message}");
            _logger.LogError($"Stack Trace: {ex.StackTrace}");
            throw;
        }
    }
}
