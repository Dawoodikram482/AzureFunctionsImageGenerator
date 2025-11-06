using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using WeatherImageGenerator.Models;

namespace WeatherImageGenerator.Services;

public interface IStorageService
{
    Task<string> UploadImageAsync(string jobId, int stationId, byte[] imageData);
    Task<JobStatus?> GetJobStatusAsync(string jobId);
    Task CreateJobAsync(string jobId, int totalStations);
    Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null);
    Task AddImageToJobAsync(string jobId, string imageUrl);
    Task IncrementProcessedStationsAsync(string jobId);
    Task<string> GenerateSasTokenForBlob(string blobUrl);
}

public class StorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableClient _tableClient;
    private const string ContainerName = "weather-images";
    private const string TableName = "jobstatus";

    public StorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureWebJobsStorage"];
        _blobServiceClient = new BlobServiceClient(connectionString);
        
        var tableServiceClient = new TableServiceClient(connectionString);
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<string> UploadImageAsync(string jobId, int stationId, byte[] imageData)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
        
        var blobName = $"{jobId}/{stationId}.jpg";
        var blobClient = containerClient.GetBlobClient(blobName);
        
        using var stream = new MemoryStream(imageData);
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "image/jpeg" });
        
        return blobClient.Uri.ToString();
    }

    public async Task<JobStatus?> GetJobStatusAsync(string jobId)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<TableEntity>("job", jobId);
            return MapToJobStatus(entity.Value);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task CreateJobAsync(string jobId, int totalStations)
    {
        var entity = new TableEntity("job", jobId)
        {
            { "Status", "queued" },
            { "TotalStations", totalStations },
            { "ProcessedStations", 0 },
            { "CreatedAt", DateTime.UtcNow },
            { "ImageUrls", "" }
        };
        
        await _tableClient.AddEntityAsync(entity);
    }

    public async Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null)
    {
        await UpdateJobEntityAsync(jobId, entity =>
        {
            entity["Status"] = status;

            if (status == "completed" || status == "failed")
            {
                entity["CompletedAt"] = DateTime.UtcNow;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                entity["ErrorMessage"] = errorMessage;
            }
        });
    }

    public async Task AddImageToJobAsync(string jobId, string imageUrl)
    {
        await UpdateJobEntityAsync(jobId, entity =>
        {
            var currentUrls = entity.GetString("ImageUrls") ?? string.Empty;
            var urls = string.IsNullOrEmpty(currentUrls)
                ? new List<string>()
                : currentUrls.Split(',').ToList();

            urls.Add(imageUrl);
            entity["ImageUrls"] = string.Join(',', urls);
        });
    }

    public async Task IncrementProcessedStationsAsync(string jobId)
    {
        await UpdateJobEntityAsync(jobId, entity =>
        {
            var processed = entity.GetInt32("ProcessedStations") ?? 0;
            var total = entity.GetInt32("TotalStations") ?? 0;

            processed += 1;
            entity["ProcessedStations"] = processed;

            if (processed >= total && total > 0)
            {
                entity["Status"] = "completed";
                entity["CompletedAt"] = DateTime.UtcNow;
            }
        });
    }

    private async Task UpdateJobEntityAsync(string jobId, Action<TableEntity> updateAction)
    {
        const int maxAttempts = 20;
        var random = new Random();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>("job", jobId);
            var entity = response.Value;

            updateAction(entity);

            try
            {
                await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // Entity was modified concurrently; retry with exponential backoff + jitter
                var baseDelay = Math.Min(100 * Math.Pow(2, attempt - 1), 2000);
                var jitter = random.Next(0, (int)(baseDelay * 0.3));
                await Task.Delay(TimeSpan.FromMilliseconds(baseDelay + jitter));
            }
        }

        throw new RequestFailedException(412, $"Failed to update job '{jobId}' after multiple attempts due to concurrent updates.");
    }

    public Task<string> GenerateSasTokenForBlob(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var blobClient = new BlobClient(uri);
        
        if (blobClient.CanGenerateSasUri)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            };
            
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return Task.FromResult(sasUri.ToString());
        }
        
        return Task.FromResult(blobUrl);
    }

    private JobStatus MapToJobStatus(TableEntity entity)
    {
        var imageUrlsString = entity.GetString("ImageUrls") ?? "";
        var imageUrls = string.IsNullOrEmpty(imageUrlsString) 
            ? new List<string>() 
            : imageUrlsString.Split(',').ToList();

        return new JobStatus
        {
            JobId = entity.RowKey,
            Status = entity.GetString("Status"),
            TotalStations = entity.GetInt32("TotalStations") ?? 0,
            ProcessedStations = entity.GetInt32("ProcessedStations") ?? 0,
            CreatedAt = entity.GetDateTime("CreatedAt") ?? DateTime.MinValue,
            CompletedAt = entity.GetDateTime("CompletedAt"),
            ImageUrls = imageUrls,
            ErrorMessage = entity.GetString("ErrorMessage")
        };
    }
}
