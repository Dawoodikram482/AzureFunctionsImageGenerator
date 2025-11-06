using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using WeatherImageGenerator.Models;

namespace WeatherImageGenerator.Services;

public interface IImageService
{
    Task<byte[]> AddWeatherDataToImageAsync(byte[] imageBytes, WeatherStation station);
}

public class ImageService : IImageService
{
    public async Task<byte[]> AddWeatherDataToImageAsync(byte[] imageBytes, WeatherStation station)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            
            // Create weather text
            var weatherText = $@"
Station: {station.StationName ?? "Unknown"}
Region: {station.RegionName ?? "N/A"}
Temperature: {station.Temperature?.ToString("F1") ?? "N/A"}°C
Humidity: {station.Humidity?.ToString() ?? "N/A"}%
Wind: {station.WindSpeed?.ToString("F1") ?? "N/A"} m/s {station.WindDirection ?? ""}
{station.WeatherDescription ?? ""}
            ".Trim();
            
            // Try to load a system font, fallback to defaults if not available
            Font font;
            try
            {
                var fontFamily = SystemFonts.Get("Arial");
                font = fontFamily.CreateFont(24, FontStyle.Bold);
            }
            catch
            {
                // Use a fallback if Arial is not available
                if (!SystemFonts.Families.Any())
                {
                    throw new Exception("No system fonts available");
                }
                var fontFamily = SystemFonts.Families.First();
                font = fontFamily.CreateFont(24, FontStyle.Bold);
            }
            
            // Draw semi-transparent background
            var textOptions = new RichTextOptions(font)
            {
                Origin = new PointF(20, 20),
                WrappingLength = image.Width - 40,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            
            image.Mutate(ctx =>
            {
                // Add semi-transparent black background for text
                var textBounds = TextMeasurer.MeasureBounds(weatherText, textOptions);
                var padding = 15;
                ctx.Fill(
                    new Color(new Rgba32(0, 0, 0, 180)),
                    new RectangleF(
                        textBounds.X - padding,
                        textBounds.Y - padding,
                        textBounds.Width + (padding * 2),
                        textBounds.Height + (padding * 2)
                    )
                );
                
                // Draw white text
                ctx.DrawText(textOptions, weatherText, Color.White);
            });
            
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            return ms.ToArray();
        });
    }
}
