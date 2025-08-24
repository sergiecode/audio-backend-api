using AudioBackend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AudioBackend.Tests.Services
{
    public class AudioProcessorServiceTests
    {
        private readonly Mock<ILogger<AudioProcessorService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly AudioProcessorService _audioProcessorService;

        public AudioProcessorServiceTests()
        {
            _mockLogger = new Mock<ILogger<AudioProcessorService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:8000")
            };

            SetupDefaultConfiguration();
            _audioProcessorService = new AudioProcessorService(_httpClient, _mockLogger.Object, _mockConfiguration.Object);
        }

        private void SetupDefaultConfiguration()
        {
            // Setup configuration for max file size
            var maxFileSizeSection = new Mock<IConfigurationSection>();
            maxFileSizeSection.Setup(s => s.Value).Returns("104857600"); // 100MB
            _mockConfiguration.Setup(c => c.GetSection("AudioEnhancementService:MaxFileSizeBytes"))
                .Returns(maxFileSizeSection.Object);

            // Setup configuration for allowed file extensions using GetChildren() approach
            var allowedExtensionsSection = new Mock<IConfigurationSection>();
            var children = new List<IConfigurationSection>
            {
                CreateMockConfigurationSection("0", ".wav"),
                CreateMockConfigurationSection("1", ".mp3"),
                CreateMockConfigurationSection("2", ".flac"),
                CreateMockConfigurationSection("3", ".m4a"),
                CreateMockConfigurationSection("4", ".aac"),
                CreateMockConfigurationSection("5", ".ogg")
            };
            allowedExtensionsSection.Setup(s => s.GetChildren()).Returns(children);
            _mockConfiguration.Setup(c => c.GetSection("AudioEnhancementService:AllowedFileExtensions"))
                .Returns(allowedExtensionsSection.Object);
        }

        private IConfigurationSection CreateMockConfigurationSection(string key, string value)
        {
            var mock = new Mock<IConfigurationSection>();
            mock.Setup(s => s.Key).Returns(key);
            mock.Setup(s => s.Value).Returns(value);
            return mock.Object;
        }

        [Fact]
        public async Task ProcessAudioAsync_ValidWavFile_ReturnsSuccessResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("test.wav", "audio/wav", 1024);
            var expectedResponse = new PythonServiceResponse
            {
                Success = true,
                Message = "Processing completed successfully",
                ProcessingId = "test-123",
                OutputFile = "enhanced_test.wav",
                DownloadUrl = "/download/enhanced_test.wav",
                ProcessingDetails = new PythonServiceProcessingDetails
                {
                    ProcessingTime = 5.2,
                    FileSizeMb = 0.001,
                    EnhancementApplied = "AI Audio Enhancement"
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.ProcessingId.Should().Be("test-123");
            result.OutputFile.Should().Be("enhanced_test.wav");
            result.DownloadUrl.Should().Be("/download/enhanced_test.wav");
            result.ProcessingDetails.Should().NotBeNull();
            result.ProcessingDetails!.ProcessingTime.Should().Be(5.2);
        }

        [Fact]
        public async Task ProcessAudioAsync_NullFile_ReturnsFailureResult()
        {
            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(null!);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be("No audio file provided");
        }

        [Fact]
        public async Task ProcessAudioAsync_EmptyFile_ReturnsFailureResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("empty.wav", "audio/wav", 0);

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be("No audio file provided");
        }

        [Fact]
        public async Task ProcessAudioAsync_UnsupportedFileFormat_ReturnsFailureResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("test.txt", "text/plain", 1024);

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported file format: .txt");
        }

        [Fact]
        public async Task ProcessAudioAsync_FileTooLarge_ReturnsFailureResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("large.wav", "audio/wav", 200 * 1024 * 1024); // 200MB

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("File size");
            result.Message.Should().Contain("exceeds maximum allowed");
        }

        [Fact]
        public async Task ProcessAudioAsync_HttpRequestException_ReturnsFailureResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("test.wav", "audio/wav", 1024);
            
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Audio enhancement service is currently unavailable");
        }

        [Fact]
        public async Task ProcessAudioAsync_ServiceReturnsError_ReturnsFailureResult()
        {
            // Arrange
            var mockFile = CreateMockAudioFile("test.wav", "audio/wav", 1024);
            SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid audio format");

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Audio enhancement service is currently unavailable");
        }

        [Fact]
        public async Task CheckServiceHealthAsync_ServiceHealthy_ReturnsTrue()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.OK, "Healthy");

            // Act
            var result = await _audioProcessorService.CheckServiceHealthAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CheckServiceHealthAsync_ServiceUnhealthy_ReturnsFalse()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.ServiceUnavailable, "Unhealthy");

            // Act
            var result = await _audioProcessorService.CheckServiceHealthAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DownloadProcessedAudioAsync_ValidUrl_ReturnsAudioData()
        {
            // Arrange
            var expectedData = Encoding.UTF8.GetBytes("fake audio data");
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(expectedData)
                });

            // Act
            var result = await _audioProcessorService.DownloadProcessedAudioAsync("/download/test.wav");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedData);
        }

        [Theory]
        [InlineData(".wav")]
        [InlineData(".mp3")]
        [InlineData(".flac")]
        [InlineData(".m4a")]
        [InlineData(".aac")]
        [InlineData(".ogg")]
        public async Task ProcessAudioAsync_SupportedFormats_ReturnsSuccess(string extension)
        {
            // Arrange
            var mockFile = CreateMockAudioFile($"test{extension}", GetMimeType(extension), 1024);
            var expectedResponse = new PythonServiceResponse
            {
                Success = true,
                Message = "Processing completed successfully",
                ProcessingId = "test-123",
                OutputFile = $"enhanced_test{extension}",
                DownloadUrl = $"/download/enhanced_test{extension}"
            };

            SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

            // Act
            var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        private IFormFile CreateMockAudioFile(string fileName, string contentType, long length)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
            return mockFile.Object;
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, object responseContent)
        {
            var jsonContent = responseContent is string str ? str : JsonSerializer.Serialize(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(jsonContent)
                });
        }

        private static string GetMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                ".m4a" => "audio/mp4",
                ".aac" => "audio/aac",
                ".ogg" => "audio/ogg",
                _ => "application/octet-stream"
            };
        }
    }
}
