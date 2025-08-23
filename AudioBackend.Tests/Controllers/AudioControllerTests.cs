using AudioBackend.Controllers;
using AudioBackend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AudioBackend.Tests.Controllers
{
    public class AudioControllerTests
    {
        private readonly Mock<IAudioProcessorService> _mockAudioProcessorService;
        private readonly Mock<ILogger<AudioController>> _mockLogger;
        private readonly AudioController _controller;

        public AudioControllerTests()
        {
            _mockAudioProcessorService = new Mock<IAudioProcessorService>();
            _mockLogger = new Mock<ILogger<AudioController>>();
            _controller = new AudioController(_mockAudioProcessorService.Object, _mockLogger.Object);
            
            // Initialize HttpContext for the controller
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task UploadAudio_ValidFile_ReturnsOkResult()
        {
            // Arrange
            var mockFile = CreateMockFile("test.wav", 1024);
            var expectedResponse = AudioProcessingResponse.CreateSuccess(
                "test-123", 
                "enhanced_test.wav", 
                "/download/enhanced_test.wav", 
                "Processing completed successfully",
                new PythonServiceProcessingDetails
                {
                    ProcessingTime = 5.2,
                    FileSizeMb = 0.001,
                    EnhancementApplied = "AI Audio Enhancement"
                }
            );

            _mockAudioProcessorService.Setup(s => s.ProcessAudioAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UploadAudio(mockFile);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeOfType<AudioUploadResponse>();
            
            var response = okResult.Value as AudioUploadResponse;
            response!.Success.Should().BeTrue();
            response.ProcessingId.Should().Be("test-123");
            response.OutputFile.Should().Be("enhanced_test.wav");
            response.DownloadUrl.Should().Be("/download/enhanced_test.wav");
            response.ProcessingDetails.Should().NotBeNull();
            response.ProcessingDetails!.ProcessingTime.Should().Be(5.2);
        }

        [Fact]
        public async Task UploadAudio_NullFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.UploadAudio(null!);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().BeOfType<ErrorResponse>();
            
            var errorResponse = badRequestResult.Value as ErrorResponse;
            errorResponse!.Message.Should().Be("No audio file provided");
        }

        [Fact]
        public async Task UploadAudio_EmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = CreateMockFile("empty.wav", 0);

            // Act
            var result = await _controller.UploadAudio(mockFile);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UploadAudio_ProcessingFails_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = CreateMockFile("test.wav", 1024);
            var failureResponse = AudioProcessingResponse.Failure("Processing failed");

            _mockAudioProcessorService.Setup(s => s.ProcessAudioAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failureResponse);

            // Act
            var result = await _controller.UploadAudio(mockFile);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().BeOfType<ErrorResponse>();
            
            var errorResponse = badRequestResult.Value as ErrorResponse;
            errorResponse!.Message.Should().Be("Processing failed");
        }

        [Fact]
        public async Task UploadAudio_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var mockFile = CreateMockFile("test.wav", 1024);

            _mockAudioProcessorService.Setup(s => s.ProcessAudioAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.UploadAudio(mockFile);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            objectResult.Value.Should().BeOfType<ErrorResponse>();
            
            var errorResponse = objectResult.Value as ErrorResponse;
            errorResponse!.Message.Should().Be("An unexpected error occurred while processing the audio file");
        }

        [Fact]
        public async Task DownloadProcessedAudio_ValidFile_ReturnsFileResult()
        {
            // Arrange
            var filename = "enhanced_test.wav";
            var audioData = new byte[] { 1, 2, 3, 4, 5 };

            _mockAudioProcessorService.Setup(s => s.DownloadProcessedAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(audioData);

            // Act
            var result = await _controller.DownloadProcessedAudio(filename);

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult!.FileContents.Should().BeEquivalentTo(audioData);
            fileResult.ContentType.Should().Be("audio/wav");
            fileResult.FileDownloadName.Should().Be(filename);
        }

        [Fact]
        public async Task DownloadProcessedAudio_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            var filename = "nonexistent.wav";

            _mockAudioProcessorService.Setup(s => s.DownloadProcessedAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _controller.DownloadProcessedAudio(filename);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().BeOfType<ErrorResponse>();
            
            var errorResponse = notFoundResult.Value as ErrorResponse;
            errorResponse!.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task DownloadProcessedAudio_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var filename = "test.wav";

            _mockAudioProcessorService.Setup(s => s.DownloadProcessedAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Download failed"));

            // Act
            var result = await _controller.DownloadProcessedAudio(filename);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task GetServiceHealth_ServiceHealthy_ReturnsOk()
        {
            // Arrange
            _mockAudioProcessorService.Setup(s => s.CheckServiceHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.GetServiceHealth();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeOfType<HealthResponse>();
            
            var healthResponse = okResult.Value as HealthResponse;
            healthResponse!.Status.Should().Be("Healthy");
            healthResponse.Message.Should().Be("Audio enhancement service is available");
        }

        [Fact]
        public async Task GetServiceHealth_ServiceUnhealthy_ReturnsServiceUnavailable()
        {
            // Arrange
            _mockAudioProcessorService.Setup(s => s.CheckServiceHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.GetServiceHealth();

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(503);
            objectResult.Value.Should().BeOfType<ErrorResponse>();
            
            var errorResponse = objectResult.Value as ErrorResponse;
            errorResponse!.Message.Should().Be("Audio enhancement service is currently unavailable");
        }

        [Theory]
        [InlineData("test.wav", "audio/wav")]
        [InlineData("test.mp3", "audio/mpeg")]
        [InlineData("test.flac", "audio/flac")]
        [InlineData("test.m4a", "audio/mp4")]
        [InlineData("test.aac", "audio/aac")]
        [InlineData("test.ogg", "audio/ogg")]
        [InlineData("test.unknown", "application/octet-stream")]
        public async Task DownloadProcessedAudio_DifferentFormats_ReturnsCorrectContentType(string filename, string expectedContentType)
        {
            // Arrange
            var audioData = new byte[] { 1, 2, 3, 4, 5 };

            _mockAudioProcessorService.Setup(s => s.DownloadProcessedAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(audioData);

            // Act
            var result = await _controller.DownloadProcessedAudio(filename);

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult!.ContentType.Should().Be(expectedContentType);
        }

        private static IFormFile CreateMockFile(string fileName, long length)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
            return mockFile.Object;
        }
    }
}
