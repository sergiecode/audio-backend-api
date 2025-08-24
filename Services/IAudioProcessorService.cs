namespace AudioBackend.Services
{
    /// <summary>
    /// Interface for the audio processor service
    /// </summary>
    public interface IAudioProcessorService
    {
        /// <summary>
        /// Processes an audio file by sending it to the Python microservice
        /// </summary>
        /// <param name="audioFile">The audio file to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The processing result from the Python service</returns>
        Task<AudioProcessingResponse> ProcessAudioAsync(IFormFile audioFile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads processed audio from the Python microservice
        /// </summary>
        /// <param name="fileName">Name of the processed file to download</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The processed audio data or null if not found</returns>
        Task<byte[]?> DownloadProcessedAudioAsync(string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the Python audio processing service is healthy
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the service is healthy, false otherwise</returns>
        Task<bool> CheckServiceHealthAsync(CancellationToken cancellationToken = default);
    }
}
