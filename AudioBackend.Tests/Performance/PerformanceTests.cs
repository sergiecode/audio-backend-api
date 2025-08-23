using AudioBackend.Services;
using AudioBackend.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace AudioBackend.Tests.Performance
{
    /// <summary>
    /// Performance tests for the Audio Backend API
    /// These tests help ensure the API performs within acceptable limits
    /// </summary>
    public class PerformanceTests
    {
        private readonly Mock<ILogger<AudioProcessorService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public PerformanceTests()
        {
            _mockLogger = new Mock<ILogger<AudioProcessorService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            SetupDefaultConfiguration();
        }

        private void SetupDefaultConfiguration()
        {
            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s.Value).Returns((100 * 1024 * 1024).ToString()); // 100MB
            _mockConfiguration.Setup(c => c.GetSection("AudioEnhancementService:MaxFileSizeBytes"))
                .Returns(mockSection.Object);

            // Mock the file extensions section differently to avoid extension method issues
            var mockExtensionsSection = new Mock<IConfigurationSection>();
            var extensions = new[] { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg" };
            
            // Create mock child sections for each extension
            var mockChildren = new List<IConfigurationSection>();
            for (int i = 0; i < extensions.Length; i++)
            {
                var childSection = new Mock<IConfigurationSection>();
                childSection.Setup(s => s.Value).Returns(extensions[i]);
                childSection.Setup(s => s.Key).Returns(i.ToString());
                mockChildren.Add(childSection.Object);
            }
            
            mockExtensionsSection.Setup(s => s.GetChildren()).Returns(mockChildren);
            _mockConfiguration.Setup(c => c.GetSection("AudioEnhancementService:AllowedFileExtensions"))
                .Returns(mockExtensionsSection.Object);
        }

        [Fact]
        public async Task FileValidation_SmallFile_CompletesUnder100Ms()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile("test.wav", "audio/wav", 1024); // 1KB file
            SetupFastHttpResponse(mockHttpHandler);

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            var result = await service.ProcessAudioAsync(mockFile);
            stopwatch.Stop();

            // Validation should be very fast
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
                "File validation should complete quickly for small files");
        }

        [Fact]
        public async Task FileValidation_LargeFile_CompletesUnder500Ms()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile("large.wav", "audio/wav", 50 * 1024 * 1024); // 50MB file
            SetupFastHttpResponse(mockHttpHandler);

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            var result = await service.ProcessAudioAsync(mockFile);
            stopwatch.Stop();

            // Even large file validation should be reasonably fast
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
                "File validation should complete within reasonable time even for large files");
        }

        [Fact]
        public async Task ConcurrentValidation_MultipleFiles_HandlesLoad()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            SetupFastHttpResponse(mockHttpHandler);

            var tasks = new List<Task<AudioProcessingResponse>>();
            
            // Create 10 concurrent validation tasks
            for (int i = 0; i < 10; i++)
            {
                var mockFile = CreateMockFile($"test{i}.wav", "audio/wav", 1024);
                tasks.Add(service.ProcessAudioAsync(mockFile));
            }

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // All validations should complete within a reasonable time
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
                "Concurrent validations should complete within reasonable time");

            // All results should be present
            results.Should().HaveCount(10);
            results.Should().AllSatisfy(r => r.Should().NotBeNull());
        }

        [Theory]
        [InlineData(1024)]      // 1KB
        [InlineData(1024 * 10)] // 10KB
        [InlineData(1024 * 100)] // 100KB
        [InlineData(1024 * 1024)] // 1MB
        public async Task MemoryUsage_DifferentFileSizes_DoesNotExceedLimits(long fileSize)
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            var mockFile = CreateMockFile("test.wav", "audio/wav", fileSize);
            SetupFastHttpResponse(mockHttpHandler);

            // Measure memory before
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryBefore = GC.GetTotalMemory(false);

            // Act
            var result = await service.ProcessAudioAsync(mockFile);

            // Measure memory after
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = memoryAfter - memoryBefore;
            
            // Memory increase should be reasonable (not more than 10x the file size)
            memoryIncrease.Should().BeLessThan(fileSize * 10,
                $"Memory usage should be reasonable for file size {fileSize} bytes");
        }

        [Fact]
        public async Task HealthCheck_ResponseTime_IsAcceptable()
        {
            // Arrange
            var mockHttpHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpHandler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new AudioProcessorService(httpClient, _mockLogger.Object, _mockConfiguration.Object);
            
            mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/health")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("OK")
                });

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            var isHealthy = await service.CheckServiceHealthAsync();
            stopwatch.Stop();

            // Health check should be very fast
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
                "Health check should respond quickly");
            
            isHealthy.Should().BeTrue();
        }

        private static IFormFile CreateMockFile(string fileName, string contentType, long length)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            
            // Create a memory stream with the specified length
            var data = new byte[length];
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(data));
            
            return mockFile.Object;
        }

        private static void SetupFastHttpResponse(Mock<HttpMessageHandler> mockHttpHandler)
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
