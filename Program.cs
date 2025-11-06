using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherImageGenerator.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register services
    services.AddHttpClient<IBuienradarService, BuienradarService>();
    services.AddHttpClient<IPixabayService, PixabayService>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IStorageService, StorageService>();
    })
    .Build();

host.Run();
