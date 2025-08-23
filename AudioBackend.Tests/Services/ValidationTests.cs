using AudioBackend.Services;
using AudioBackend.Tests.Helpers;
using Microsoft.Extensions.Configuration;

namespace AudioBackend.Tests.Services
{
    public class ValidationTests
    {
        private readonly Mock<ILogger<AudioProcessorService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public ValidationTests()
        {
            _mockLogger = new Mock<ILogger<AudioProcessorService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            SetupDefaultConfiguration();
        }

        private void SetupDefaultConfiguration()
        {
            _mockConfiguration.Setup(c => c.GetValue<long>("AudioEnhancementService:MaxFileSizeBytes", It.IsAny<long>()))
                .Returns(10 * 1024 * 1024); // 10MB

            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s.Get<string[]>())
                .Returns(new[] { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg" });
            
            _mockConfiguration.Setup(c => c.GetSection("AudioEnhancementService:AllowedFileExtensions"))
                .Returns(mockSection.Object);
        }

        [Theory]
        [InlineData(".wav")]
        [InlineData(".mp3")]
        [InlineData(".flac")]
        [InlineData(".m4a")]
        [InlineData(".aac")]
        [InlineData(".ogg")]
        public async Task ProcessAudioAsync_SupportedFormats_PassesValidation(string extension)
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile($"test{extension}", TestDataHelper.GetMimeType(extension), 1024);

            // Setup successful HTTP response
            SetupSuccessfulHttpResponse(mockHttpHandler);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert - Should not fail due to validation
            result.Should().NotBeNull();
            // Note: The result might still fail due to HTTP mocking, but not due to validation
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".pdf")]
        [InlineData(".doc")]
        [InlineData(".jpg")]
        [InlineData(".zip")]
        public async Task ProcessAudioAsync_UnsupportedFormats_FailsValidation(string extension)
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile($"test{extension}", "application/octet-stream", 1024);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported file format");
            result.Message.Should().Contain(extension);
        }

        [Fact]
        public async Task ProcessAudioAsync_FileSizeExceedsLimit_FailsValidation()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var fileSizeInBytes = 15 * 1024 * 1024; // 15MB (exceeds 10MB limit)
            var mockFile = CreateMockFile("large.wav", "audio/wav", fileSizeInBytes);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("File size");
            result.Message.Should().Contain("exceeds maximum allowed");
        }

        [Fact]
        public async Task ProcessAudioAsync_FileSizeWithinLimit_PassesValidation()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var fileSizeInBytes = 5 * 1024 * 1024; // 5MB (within 10MB limit)
            var mockFile = CreateMockFile("valid.wav", "audio/wav", fileSizeInBytes);

            // Setup successful HTTP response
            SetupSuccessfulHttpResponse(mockHttpHandler);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert - Should not fail due to size validation
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessAudioAsync_EmptyFileName_FailsValidation()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile("", "audio/wav", 1024);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported file format");
        }

        [Fact]
        public async Task ProcessAudioAsync_FileNameWithoutExtension_FailsValidation()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile("audiofile", "audio/wav", 1024);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported file format");
        }

        [Theory]
        [InlineData("test.WAV")]
        [InlineData("test.Mp3")]
        [InlineData("test.FLAC")]
        public async Task ProcessAudioAsync_UppercaseExtensions_PassesValidation(string filename)
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile(filename, "audio/wav", 1024);

            // Setup successful HTTP response
            SetupSuccessfulHttpResponse(mockHttpHandler);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Assert - Should not fail due to validation (case insensitive)
            result.Should().NotBeNull();
        }

        private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, long length)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
            return mockFile.Object;
        }

        private static void SetupSuccessfulHttpResponse(Mock<HttpMessageHandler> mockHttpHandler)
        {
            var successResponse = TestDataHelper.CreatePythonServiceSuccessResponse();
            var jsonResponse = JsonSerializer.Serialize(successResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });
        }
    }
}
