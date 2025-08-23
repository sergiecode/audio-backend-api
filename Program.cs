using AudioBackend.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/audio-backend-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Audio Backend API", 
        Version = "v1",
        Description = "A .NET Web API that orchestrates audio enhancement by consuming the Python microservice",
        Contact = new() 
        { 
            Name = "Sergie Code", 
            Url = new Uri("https://github.com/sergiecode") 
        }
    });
});

// Configure HttpClient for AudioProcessorService
builder.Services.AddHttpClient<AudioProcessorService>(client =>
{
    var baseUrl = builder.Configuration["AudioEnhancementService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(10); // Allow for long audio processing
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    MaxConnectionsPerServer = 10,
    UseCookies = false
});

// Register AudioProcessorService
builder.Services.AddScoped<AudioProcessorService>();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Audio Backend API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger at root
    });
    app.UseCors("DevelopmentPolicy");
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Service = "Audio Backend API",
    Version = "1.0.0"
}))
.WithName("HealthCheck")
.WithOpenApi();

try
{
    Log.Information("Starting Audio Backend API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
