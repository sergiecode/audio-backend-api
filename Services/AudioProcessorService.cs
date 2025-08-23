using System.Net;
using System.Text.Json;

namespace AudioBackend.Services
{
    /// <summary>
    /// Service responsible for processing audio files by communicating with the Python microservice
    /// </summary>
    public class AudioProcessorService : IAudioProcessorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AudioProcessorService> _logger;
        private readonly IConfiguration _configuration;

        public AudioProcessorService(
            HttpClient httpClient, 
            ILogger<AudioProcessorService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Processes an audio file by sending it to the Python microservice
        /// </summary>
        /// <param name="audioFile">The audio file to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The processing result from the Python service</returns>
        public async Task<AudioProcessingResponse> ProcessAudioAsync(
            IFormFile audioFile, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for null file first
                if (audioFile == null)
                {
                    _logger.LogWarning("Null audio file provided for processing");
                    return AudioProcessingResponse.Failure("No audio file provided");
                }

                _logger.LogInformation("Starting audio processing for file: {FileName}", audioFile.FileName);

                // Validate file before processing
                var validationResult = ValidateAudioFile(audioFile);
                if (!validationResult.IsValid)
                {
                    return AudioProcessingResponse.Failure(validationResult.ErrorMessage);
                }

                // Check if the Python service is healthy
                var isHealthy = await CheckServiceHealthAsync(cancellationToken);
                if (!isHealthy)
                {
                    return AudioProcessingResponse.Failure("Audio enhancement service is currently unavailable");
                }

                // Prepare multipart form data
                using var formData = new MultipartFormDataContent();
                using var fileStream = audioFile.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                
                // Set appropriate content type based on file extension
                streamContent.Headers.ContentType = GetContentType(audioFile.FileName);
                formData.Add(streamContent, "file", audioFile.FileName);

                // Send request to Python microservice
                _logger.LogInformation("Sending file to Python microservice: {FileName}", audioFile.FileName);
                var response = await _httpClient.PostAsync("/process", formData, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var result = JsonSerializer.Deserialize<PythonServiceResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                    _logger.LogInformation("Audio processing completed successfully for file: {FileName}", audioFile.FileName);
                    
                    return AudioProcessingResponse.CreateSuccess(
                        result?.ProcessingId ?? "unknown",
                        result?.OutputFile ?? "unknown",
                        result?.DownloadUrl ?? "",
                        result?.Message ?? "Processing completed successfully",
                        result?.ProcessingDetails
                    );
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Python service returned error {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    return AudioProcessingResponse.Failure(
                        $"Audio processing failed: {response.StatusCode} - {errorContent}"
                    );
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Audio processing timed out for file: {FileName}", audioFile?.FileName ?? "unknown");
                return AudioProcessingResponse.Failure("Audio processing timed out. Please try with a smaller file.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while processing audio file: {FileName}", audioFile?.FileName ?? "unknown");
                return AudioProcessingResponse.Failure("Audio enhancement service is currently unavailable");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing audio file: {FileName}", audioFile?.FileName ?? "unknown");
                return AudioProcessingResponse.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads the processed audio file from the Python service
        /// </summary>
        /// <param name="downloadUrl">The download URL returned by the Python service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The processed audio file as byte array</returns>
        public async Task<byte[]?> DownloadProcessedAudioAsync(
            string downloadUrl, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Downloading processed audio from: {DownloadUrl}", downloadUrl);

                var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _logger.LogInformation("Successfully downloaded processed audio, size: {Size} bytes", audioData.Length);
                
                return audioData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading processed audio from: {DownloadUrl}", downloadUrl);
                return null;
            }
        }

        /// <summary>
        /// Checks if the Python microservice is healthy and available
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the service is healthy, false otherwise</returns>
        public async Task<bool> CheckServiceHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for Python microservice");
                return false;
            }
        }

        /// <summary>
        /// Validates the uploaded audio file
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <returns>Validation result</returns>
        private ValidationResult ValidateAudioFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ValidationResult.Invalid("No audio file provided");

            var maxFileSize = _configuration.GetSection("AudioEnhancementService:MaxFileSizeBytes").Value;
            var maxFileSizeBytes = string.IsNullOrEmpty(maxFileSize) ? 104857600L : Convert.ToInt64(maxFileSize); // 100MB default
            if (file.Length > maxFileSizeBytes)
                return ValidationResult.Invalid($"File size ({file.Length} bytes) exceeds maximum allowed ({maxFileSizeBytes} bytes)");

            var allowedExtensionsSection = _configuration.GetSection("AudioEnhancementService:AllowedFileExtensions");
            var allowedExtensions = allowedExtensionsSection.GetChildren()
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();
            
            // Fallback if configuration is empty
            if (allowedExtensions.Length == 0)
                allowedExtensions = new[] { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg" };

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                return ValidationResult.Invalid($"Unsupported file format: {fileExtension}. Allowed formats: {string.Join(", ", allowedExtensions)}");

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Gets the appropriate content type for the audio file
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <returns>The content type</returns>
        private static System.Net.Http.Headers.MediaTypeHeaderValue GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".wav" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/wav"),
                ".mp3" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/mpeg"),
                ".flac" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/flac"),
                ".m4a" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/mp4"),
                ".aac" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/aac"),
                ".ogg" => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/ogg"),
                _ => System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream")
            };
        }
    }

    /// <summary>
    /// Represents the response from the audio processing operation
    /// </summary>
    public class AudioProcessingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ProcessingId { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public PythonServiceProcessingDetails? ProcessingDetails { get; set; }

        public static AudioProcessingResponse CreateSuccess(
            string processingId, 
            string outputFile, 
            string downloadUrl, 
            string message,
            PythonServiceProcessingDetails? details = null)
        {
            return new AudioProcessingResponse
            {
                Success = true,
                ProcessingId = processingId,
                OutputFile = outputFile,
                DownloadUrl = downloadUrl,
                Message = message,
                ProcessingDetails = details
            };
        }

        public static AudioProcessingResponse Failure(string message)
        {
            return new AudioProcessingResponse
            {
                Success = false,
                Message = message
            };
        }
    }

    /// <summary>
    /// Represents the response from the Python microservice
    /// </summary>
    public class PythonServiceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string InputFile { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string ProcessingId { get; set; } = string.Empty;
        public PythonServiceProcessingDetails? ProcessingDetails { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Processing details from the Python service
    /// </summary>
    public class PythonServiceProcessingDetails
    {
        public double ProcessingTime { get; set; }
        public double FileSizeMb { get; set; }
        public string EnhancementApplied { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the result of file validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static ValidationResult Valid() => new() { IsValid = true };
        public static ValidationResult Invalid(string message) => new() { IsValid = false, ErrorMessage = message };
    }
}
