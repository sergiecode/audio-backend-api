using AudioBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioBackend.Controllers
{
    /// <summary>
    /// Controller for handling audio processing operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AudioController : ControllerBase
    {
        private readonly AudioProcessorService _audioProcessorService;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            AudioProcessorService audioProcessorService,
            ILogger<AudioController> logger)
        {
            _audioProcessorService = audioProcessorService;
            _logger = logger;
        }

        /// <summary>
        /// Uploads and processes an audio file through the Python microservice
        /// </summary>
        /// <param name="file">The audio file to process (WAV, MP3, FLAC, M4A, AAC, OGG)</param>
        /// <returns>Processing result with download URL or error message</returns>
        /// <response code="200">Audio file processed successfully</response>
        /// <response code="400">Invalid file or processing error</response>
        /// <response code="413">File too large</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(AudioUploadResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 413)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> UploadAudio([FromForm] IFormFile file)
        {
            try
            {
                _logger.LogInformation("Received audio upload request for file: {FileName}", file?.FileName ?? "unknown");

                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file provided in upload request");
                    return BadRequest(new ErrorResponse("No audio file provided"));
                }

                // Process the audio file through the Python microservice
                var result = await _audioProcessorService.ProcessAudioAsync(file, HttpContext.RequestAborted);

                if (result.Success)
                {
                    _logger.LogInformation("Audio file processed successfully: {ProcessingId}", result.ProcessingId);
                    
                    var response = new AudioUploadResponse
                    {
                        Success = true,
                        Message = result.Message,
                        ProcessingId = result.ProcessingId,
                        OutputFile = result.OutputFile,
                        DownloadUrl = result.DownloadUrl,
                        ProcessingDetails = result.ProcessingDetails != null ? new ProcessingDetails
                        {
                            ProcessingTime = result.ProcessingDetails.ProcessingTime,
                            FileSizeMb = result.ProcessingDetails.FileSizeMb,
                            EnhancementApplied = result.ProcessingDetails.EnhancementApplied
                        } : null
                    };

                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning("Audio processing failed: {Message}", result.Message);
                    return BadRequest(new ErrorResponse(result.Message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during audio upload processing");
                return StatusCode(500, new ErrorResponse("An unexpected error occurred while processing the audio file"));
            }
        }

        /// <summary>
        /// Downloads a processed audio file
        /// </summary>
        /// <param name="filename">The filename of the processed audio file</param>
        /// <returns>The processed audio file</returns>
        /// <response code="200">Audio file downloaded successfully</response>
        /// <response code="404">File not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("download/{filename}")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> DownloadProcessedAudio(string filename)
        {
            try
            {
                _logger.LogInformation("Download request for processed audio: {Filename}", filename);

                var downloadUrl = $"/download/{filename}";
                var audioData = await _audioProcessorService.DownloadProcessedAudioAsync(downloadUrl, HttpContext.RequestAborted);

                if (audioData != null && audioData.Length > 0)
                {
                    _logger.LogInformation("Successfully retrieved processed audio: {Filename}", filename);
                    
                    var contentType = GetContentTypeFromFilename(filename);
                    return File(audioData, contentType, filename);
                }
                else
                {
                    _logger.LogWarning("Processed audio file not found: {Filename}", filename);
                    return NotFound(new ErrorResponse($"Processed audio file '{filename}' not found"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading processed audio: {Filename}", filename);
                return StatusCode(500, new ErrorResponse("Error downloading the processed audio file"));
            }
        }

        /// <summary>
        /// Checks the health status of the Python microservice
        /// </summary>
        /// <returns>Health status information</returns>
        /// <response code="200">Service is healthy</response>
        /// <response code="503">Service is unavailable</response>
        [HttpGet("health")]
        [ProducesResponseType(typeof(HealthResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 503)]
        public async Task<IActionResult> GetServiceHealth()
        {
            try
            {
                var isHealthy = await _audioProcessorService.CheckServiceHealthAsync(HttpContext.RequestAborted);
                
                if (isHealthy)
                {
                    return Ok(new HealthResponse
                    {
                        Status = "Healthy",
                        Message = "Audio enhancement service is available",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(503, new ErrorResponse("Audio enhancement service is currently unavailable"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service health");
                return StatusCode(503, new ErrorResponse("Unable to check service health"));
            }
        }

        /// <summary>
        /// Gets the appropriate content type based on the filename extension
        /// </summary>
        /// <param name="filename">The filename</param>
        /// <returns>The content type</returns>
        private static string GetContentTypeFromFilename(string filename)
        {
            var extension = Path.GetExtension(filename).ToLowerInvariant();
            return extension switch
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

    /// <summary>
    /// Response model for successful audio upload
    /// </summary>
    public class AudioUploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ProcessingId { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public ProcessingDetails? ProcessingDetails { get; set; }
    }

    /// <summary>
    /// Processing details for the response
    /// </summary>
    public class ProcessingDetails
    {
        public double ProcessingTime { get; set; }
        public double FileSizeMb { get; set; }
        public string EnhancementApplied { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error response model
    /// </summary>
    public class ErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public ErrorResponse(string message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Health response model
    /// </summary>
    public class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
