using AudioBackend.Controllers;
using AudioBackend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace AudioBackend.Tests.Integration
{
    public class AudioControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly WireMockServer _mockPythonService;

        public AudioControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            // Start WireMock server to simulate Python service
            _mockPythonService = WireMockServer.Start(8001);

            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AudioEnhancementService:BaseUrl"] = _mockPythonService.Url!,
                        ["AudioEnhancementService:TimeoutSeconds"] = "30",
                        ["AudioEnhancementService:MaxFileSizeBytes"] = "10485760", // 10MB
                        ["AudioEnhancementService:AllowedFileExtensions:0"] = ".wav",
                        ["AudioEnhancementService:AllowedFileExtensions:1"] = ".mp3",
                        ["AudioEnhancementService:AllowedFileExtensions:2"] = ".flac"
                    });
                });
                
                builder.ConfigureServices(services =>
                {
                    // Remove the default HttpClient registration and replace with test configuration
                    var httpClientDescriptor = services.FirstOrDefault(x => 
                        x.ServiceType == typeof(HttpClient) && 
                        x.ImplementationType == typeof(HttpClient));
                    if (httpClientDescriptor != null)
                    {
                        services.Remove(httpClientDescriptor);
                    }

                    // Configure HttpClient for tests with the mock server URL
                    services.AddHttpClient<IAudioProcessorService, AudioProcessorService>(client =>
                    {
                        client.BaseAddress = new Uri(_mockPythonService.Url!);
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task UploadAudio_ValidWavFile_ReturnsSuccessResponse()
        {
            // Arrange
            SetupMockPythonServiceSuccess();
            var audioContent = CreateTestAudioFile("test.wav");

            // Act
            var response = await _client.PostAsync("/api/audio/upload", audioContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AudioUploadResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.ProcessingId.Should().NotBeNullOrEmpty();
            result.OutputFile.Should().NotBeNullOrEmpty();
            result.DownloadUrl.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task UploadAudio_NoFile_ReturnsBadRequest()
        {
            // Arrange - send request without the "file" parameter that the controller expects
            var content = new MultipartFormDataContent();
            content.Add(new StringContent("some data"), "other", "other.txt");

            // Act
            var response = await _client.PostAsync("/api/audio/upload", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ErrorResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Be("No audio file provided");
        }

        [Fact]
        public async Task UploadAudio_UnsupportedFormat_ReturnsBadRequest()
        {
            // Arrange
            var audioContent = CreateTestAudioFile("test.txt", "text/plain");

            // Act
            var response = await _client.PostAsync("/api/audio/upload", audioContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("Unsupported file format");
        }

        [Fact]
        public async Task UploadAudio_PythonServiceError_ReturnsBadRequest()
        {
            // Arrange
            SetupMockPythonServiceError();
            var audioContent = CreateTestAudioFile("test.wav");

            // Act
            var response = await _client.PostAsync("/api/audio/upload", audioContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("Audio enhancement service is currently unavailable");
        }

        [Fact]
        public async Task GetServiceHealth_PythonServiceHealthy_ReturnsHealthy()
        {
            // Arrange
            SetupMockPythonServiceHealth(true);

            // Act
            var response = await _client.GetAsync("/api/audio/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<HealthResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            result.Should().NotBeNull();
            result!.Status.Should().Be("Healthy");
            result.Message.Should().Be("Audio enhancement service is available");
        }

        [Fact]
        public async Task GetServiceHealth_PythonServiceUnhealthy_ReturnsServiceUnavailable()
        {
            // Arrange
            SetupMockPythonServiceHealth(false);

            // Act
            var response = await _client.GetAsync("/api/audio/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task DownloadProcessedAudio_ValidFile_ReturnsFile()
        {
            // Arrange
            var filename = "enhanced_test.wav";
            var audioData = Encoding.UTF8.GetBytes("fake audio data");
            SetupMockPythonServiceDownload(audioData);

            // Act
            var response = await _client.GetAsync($"/api/audio/download/{filename}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("audio/wav");
            
            var responseData = await response.Content.ReadAsByteArrayAsync();
            responseData.Should().BeEquivalentTo(audioData);
        }

        [Fact]
        public async Task ApiHealth_ReturnsHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().Contain("Healthy");
            responseContent.Should().Contain("Audio Backend API");
        }

        [Theory]
        [InlineData("test.wav", "audio/wav")]
        [InlineData("test.mp3", "audio/mpeg")]
        [InlineData("test.flac", "audio/flac")]
        public async Task UploadAudio_DifferentSupportedFormats_ReturnsSuccess(string filename, string contentType)
        {
            // Arrange
            SetupMockPythonServiceSuccess();
            var audioContent = CreateTestAudioFile(filename, contentType);

            // Act
            var response = await _client.PostAsync("/api/audio/upload", audioContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private void SetupMockPythonServiceSuccess()
        {
            // Setup health endpoint to return healthy
            _mockPythonService
                .Given(Request.Create().WithPath("/health").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("OK"));

            var successResponse = new
            {
                success = true,
                message = "Audio processing completed successfully",
                processing_id = "test-integration-123",
                output_file = "enhanced_test.wav",
                download_url = "/download/enhanced_test.wav",
                processing_details = new
                {
                    processing_time = 2.5,
                    file_size_mb = 0.5,
                    enhancement_applied = "AI Audio Enhancement"
                }
            };

            _mockPythonService
                .Given(Request.Create().WithPath("/process").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBodyAsJson(successResponse));
        }

        private void SetupMockPythonServiceError()
        {
            // Setup health endpoint to return unhealthy
            _mockPythonService
                .Given(Request.Create().WithPath("/health").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(503)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("Service Unavailable"));

            _mockPythonService
                .Given(Request.Create().WithPath("/process").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(400)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("Invalid audio format"));
        }

        private void SetupMockPythonServiceHealth(bool isHealthy)
        {
            var statusCode = isHealthy ? 200 : 503;
            
            _mockPythonService
                .Given(Request.Create().WithPath("/health").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(statusCode)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(isHealthy ? "OK" : "Service Unavailable"));
        }

        private void SetupMockPythonServiceDownload(byte[] audioData)
        {
            _mockPythonService
                .Given(Request.Create().WithPath("/download/*").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "audio/wav")
                    .WithBody(audioData));
        }

        private static MultipartFormDataContent CreateTestAudioFile(string filename, string contentType = "audio/wav")
        {
            var content = new MultipartFormDataContent();
            var audioData = GenerateTestAudioData();
            var fileContent = new ByteArrayContent(audioData);
            fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            content.Add(fileContent, "file", filename);
            return content;
        }

        private static byte[] GenerateTestAudioData()
        {
            // Generate a simple WAV file header + some audio data
            var header = new byte[44]; // Basic WAV header
            // WAV file signature
            var riff = Encoding.ASCII.GetBytes("RIFF");
            var wave = Encoding.ASCII.GetBytes("WAVE");
            var fmt = Encoding.ASCII.GetBytes("fmt ");
            var data = Encoding.ASCII.GetBytes("data");
            
            Array.Copy(riff, 0, header, 0, 4);
            Array.Copy(wave, 0, header, 8, 4);
            Array.Copy(fmt, 0, header, 12, 4);
            Array.Copy(data, 0, header, 36, 4);
            
            var audioData = new byte[1000]; // Sample audio data
            var random = new Random();
            random.NextBytes(audioData);
            
            return header.Concat(audioData).ToArray();
        }

        public void Dispose()
        {
            _mockPythonService?.Stop();
            _mockPythonService?.Dispose();
            _client?.Dispose();
            _factory?.Dispose();
        }
    }
    
    // Response models for deserialization in tests
    public class AudioUploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ProcessingId { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public ProcessingDetails? ProcessingDetails { get; set; }
    }

    public class ProcessingDetails
    {
        public double ProcessingTime { get; set; }
        public double FileSizeMb { get; set; }
        public string EnhancementApplied { get; set; } = string.Empty;
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
