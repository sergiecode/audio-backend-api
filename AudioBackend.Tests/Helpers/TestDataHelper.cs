using System.Text;

namespace AudioBackend.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating test data and mock objects for audio processing tests
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Generates a basic WAV file header with sample audio data
        /// </summary>
        /// <param name="sizeInBytes">Size of the audio data in bytes</param>
        /// <returns>Byte array representing a WAV file</returns>
        public static byte[] GenerateTestWavFile(int sizeInBytes = 1000)
        {
            var header = CreateWavHeader(sizeInBytes);
            var audioData = GenerateAudioData(sizeInBytes);
            return header.Concat(audioData).ToArray();
        }

        /// <summary>
        /// Creates a basic WAV file header
        /// </summary>
        /// <param name="audioDataSize">Size of the audio data</param>
        /// <returns>WAV header bytes</returns>
        private static byte[] CreateWavHeader(int audioDataSize)
        {
            var header = new byte[44];
            var fileSize = audioDataSize + 36;

            // RIFF header
            Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, 0, 4);
            Array.Copy(BitConverter.GetBytes(fileSize), 0, header, 4, 4);
            Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, 8, 4);

            // fmt subchunk
            Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, 12, 4);
            Array.Copy(BitConverter.GetBytes(16), 0, header, 16, 4); // Subchunk1Size
            Array.Copy(BitConverter.GetBytes((short)1), 0, header, 20, 2); // AudioFormat (PCM)
            Array.Copy(BitConverter.GetBytes((short)1), 0, header, 22, 2); // NumChannels
            Array.Copy(BitConverter.GetBytes(44100), 0, header, 24, 4); // SampleRate
            Array.Copy(BitConverter.GetBytes(88200), 0, header, 28, 4); // ByteRate
            Array.Copy(BitConverter.GetBytes((short)2), 0, header, 32, 2); // BlockAlign
            Array.Copy(BitConverter.GetBytes((short)16), 0, header, 34, 2); // BitsPerSample

            // data subchunk
            Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, 36, 4);
            Array.Copy(BitConverter.GetBytes(audioDataSize), 0, header, 40, 4);

            return header;
        }

        /// <summary>
        /// Generates random audio data
        /// </summary>
        /// <param name="size">Size in bytes</param>
        /// <returns>Random audio data</returns>
        private static byte[] GenerateAudioData(int size)
        {
            var audioData = new byte[size];
            var random = new Random(42); // Fixed seed for consistent test data
            random.NextBytes(audioData);
            return audioData;
        }

        /// <summary>
        /// Gets the MIME type for a given file extension
        /// </summary>
        /// <param name="extension">File extension (with or without dot)</param>
        /// <returns>MIME type string</returns>
        public static string GetMimeType(string extension)
        {
            var ext = extension.StartsWith('.') ? extension : $".{extension}";
            return ext.ToLowerInvariant() switch
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

        /// <summary>
        /// Creates a sample Python service success response
        /// </summary>
        /// <param name="processingId">Processing ID</param>
        /// <param name="outputFile">Output file name</param>
        /// <returns>Python service response object</returns>
        public static object CreatePythonServiceSuccessResponse(
            string processingId = "test-123", 
            string outputFile = "enhanced_test.wav")
        {
            return new
            {
                success = true,
                message = "Audio processing completed successfully",
                processing_id = processingId,
                input_file = "test.wav",
                output_file = outputFile,
                output_path = $"/app/outputs/{outputFile}",
                download_url = $"/download/{outputFile}",
                processing_details = new
                {
                    processing_time = 5.2,
                    file_size_mb = 1.5,
                    enhancement_applied = "AI Audio Enhancement"
                }
            };
        }

        /// <summary>
        /// Creates a sample Python service error response
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Python service error response object</returns>
        public static object CreatePythonServiceErrorResponse(string errorMessage = "Processing failed")
        {
            return new
            {
                success = false,
                message = errorMessage,
                error_code = "PROCESSING_ERROR"
            };
        }

        /// <summary>
        /// Creates test file extensions and their corresponding MIME types
        /// </summary>
        /// <returns>Dictionary of file extensions and MIME types</returns>
        public static Dictionary<string, string> GetSupportedAudioFormats()
        {
            return new Dictionary<string, string>
            {
                { ".wav", "audio/wav" },
                { ".mp3", "audio/mpeg" },
                { ".flac", "audio/flac" },
                { ".m4a", "audio/mp4" },
                { ".aac", "audio/aac" },
                { ".ogg", "audio/ogg" }
            };
        }

        /// <summary>
        /// Creates test file extensions that are not supported
        /// </summary>
        /// <returns>Array of unsupported file extensions</returns>
        public static string[] GetUnsupportedAudioFormats()
        {
            return new[] { ".txt", ".pdf", ".doc", ".jpg", ".png", ".zip" };
        }
    }
}
