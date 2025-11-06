using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WeatherImageGenerator.Services;

public interface IPixabayService
{
    Task<byte[]> GetRandomImageAsync();
}

public class PixabayService : IPixabayService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private static readonly string[] DefaultQueries =
    {
        "weather",
        "landscape",
        "sky",
        "nature",
        "cityscape"
    };

    public PixabayService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["PixabayApiKey"] ?? throw new InvalidOperationException("PixabayApiKey not configured");
        _apiUrl = configuration["PixabayApiUrl"] ?? "https://pixabay.com/api/";
    }

    public async Task<byte[]> GetRandomImageAsync()
    {
        var query = DefaultQueries[Random.Shared.Next(DefaultQueries.Length)];
        var requestUri = $"{_apiUrl}?key={_apiKey}&q={Uri.EscapeDataString(query)}&image_type=photo&orientation=horizontal&per_page=100";

        using var metadataResponse = await _httpClient.GetAsync(requestUri);
        if (!metadataResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Pixabay request failed with status {(int)metadataResponse.StatusCode} {metadataResponse.StatusCode}.");
        }

        await using var contentStream = await metadataResponse.Content.ReadAsStreamAsync();
        using var jsonDoc = await JsonDocument.ParseAsync(contentStream);

        if (!jsonDoc.RootElement.TryGetProperty("hits", out var hits) || hits.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Pixabay returned no images for the current query.");
        }

        var selected = hits[Random.Shared.Next(hits.GetArrayLength())];
        var imageUrl = selected.TryGetProperty("largeImageURL", out var largeUrlElement)
            ? largeUrlElement.GetString()
            : selected.TryGetProperty("webformatURL", out var webFormatElement)
                ? webFormatElement.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new InvalidOperationException("Pixabay response did not include a usable image URL.");
        }

        return await _httpClient.GetByteArrayAsync(imageUrl);
    }
}
