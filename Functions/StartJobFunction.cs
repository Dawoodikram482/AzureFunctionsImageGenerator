using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public class StartJobFunction
{
    private readonly ILogger<StartJobFunction> _logger;
    private readonly IStorageService _storageService;
    private readonly QueueClient _queueClient;

    public StartJobFunction(
        ILogger<StartJobFunction> logger,
        IStorageService storageService,
        IConfiguration configuration)
    {
        _logger = logger;
        _storageService = storageService;
        
        var connectionString = configuration["AzureWebJobsStorage"];
        var queueOptions = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };
        _queueClient = new QueueClient(connectionString, "process-job-queue", queueOptions);
    }

    [Function("StartJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs")] HttpRequestData req)
    {
        _logger.LogInformation("Starting new weather image generation job");

        try
        {
            // Ensure queue exists
            await _queueClient.CreateIfNotExistsAsync();
            
            // Generate unique job ID
            var jobId = Guid.NewGuid().ToString();

            // Create job in table storage (we'll set total stations as 50)
            await _storageService.CreateJobAsync(jobId, 50);

            // Queue the job for processing
            var message = new ProcessJobMessage { JobId = jobId };
            var messageJson = JsonSerializer.Serialize(message);
            await _queueClient.SendMessageAsync(messageJson);

            // Create response
            var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
            
            var result = new StartJobResponse
            {
                JobId = jobId,
                Message = "Job created successfully",
                StatusUrl = $"{req.Url.Scheme}://{req.Url.Host}/api/jobs/{jobId}"
            };

            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting job");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
