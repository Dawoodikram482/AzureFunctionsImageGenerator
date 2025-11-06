using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public class GetJobStatusFunction
{
    private readonly ILogger<GetJobStatusFunction> _logger;
    private readonly IStorageService _storageService;

    public GetJobStatusFunction(
        ILogger<GetJobStatusFunction> logger,
        IStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    [Function("GetJobStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}")] HttpRequestData req,
        string jobId)
    {
        _logger.LogInformation($"Getting status for job: {jobId}");

        try
        {
            var jobStatus = await _storageService.GetJobStatusAsync(jobId);

            if (jobStatus == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Job {jobId} not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            
            var result = new JobStatusResponse
            {
                JobId = jobStatus.JobId,
                Status = jobStatus.Status,
                TotalStations = jobStatus.TotalStations,
                ProcessedStations = jobStatus.ProcessedStations,
                ProgressPercentage = jobStatus.TotalStations > 0 
                    ? (jobStatus.ProcessedStations * 100) / jobStatus.TotalStations 
                    : 0,
                CreatedAt = jobStatus.CreatedAt,
                CompletedAt = jobStatus.CompletedAt,
                ImageUrls = jobStatus.ImageUrls,
                ErrorMessage = jobStatus.ErrorMessage
            };

            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting job status for {jobId}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
