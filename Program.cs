using GeocoderSolution.Configuration;
using GeocoderSolution.Data;
using GeocoderSolution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var cacheConnectionString = builder.Configuration.GetConnectionString("GeocodingCache")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:GeocodingCache is required.");

builder.Services.AddDbContext<GeocodingCacheDbContext>(options =>
    options.UseSqlite(cacheConnectionString));
builder.Services.AddScoped<GeocodingCacheStore>();

builder.Services
    .AddOptions<NominatimOptions>()
    .Bind(builder.Configuration.GetSection(NominatimOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Nominatim:BaseUrl must be an absolute URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.UserAgent),
        "Nominatim:UserAgent is required.")
    .Validate(options => options.MinimumRequestIntervalMilliseconds > 1000,
        "Nominatim:MinimumRequestIntervalMilliseconds must be greater than 1000.")
    .ValidateOnStart();

builder.Services.AddHttpClient(NominatimClient.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<NominatimOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});

builder.Services.AddSingleton<NominatimClient>();
builder.Services.AddSingleton<AddressNormalizer>();
builder.Services.AddSingleton<CanadianPostalCodeExtractor>();
builder.Services.AddScoped<GeocodingService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var cacheDbContext = scope.ServiceProvider
        .GetRequiredService<GeocodingCacheDbContext>();
    await cacheDbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
