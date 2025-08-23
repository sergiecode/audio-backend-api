# 🎵 Audio Backend API

**Created by Sergie Code** - AI Tools for Musicians

A professional C# .NET 8 Web API that serves as an orchestration layer for audio enhancement by consuming the Python audio-enhancer-service microservice. This backend provides a clean REST API interface for musicians and developers to enhance audio files using AI-powered processing.

## 📋 Project Overview

This .NET Web API acts as a middleware service that:
- ✅ Accepts audio file uploads from clients
- ✅ Validates and processes audio files
- ✅ Forwards files to the Python audio enhancement microservice
- ✅ Returns processed audio results in JSON format
- ✅ Provides health monitoring and error handling
- ✅ Offers comprehensive logging and monitoring

### Integration Flow
```
Client → .NET Backend → Python Microservice → Enhanced Audio Response
```

1. **Client** uploads audio file to .NET API
2. **.NET Backend** validates file and forwards to Python service
3. **Python Microservice** processes audio using AI enhancement
4. **Enhanced Audio** response is returned through the .NET API back to client

## 🏗️ Project Structure

```
audio-backend-api/
├── AudioBackend.csproj           # Project configuration and dependencies
├── Program.cs                    # Application entry point and configuration
├── appsettings.json             # Configuration settings
├── Controllers/
│   └── AudioController.cs       # REST API endpoints for audio processing
├── Services/
│   └── AudioProcessorService.cs # Business logic for Python service integration
├── logs/                        # Application logs (auto-generated)
└── README.md                    # This file
```

## 🚀 Getting Started

### Prerequisites

- **.NET 8.0 SDK** or later
- **Python Audio Enhancement Service** running (see setup below)
- **Visual Studio 2022** or **VS Code** (recommended)

### 1. Clone and Setup

```bash
# Clone the repository
git clone <your-repository-url>
cd audio-backend-api

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build
```

### 2. Configure Python Service URL

Edit `appsettings.json` to configure the Python microservice URL:

```json
{
  "AudioEnhancementService": {
    "BaseUrl": "http://localhost:8000",
    "TimeoutSeconds": 600,
    "MaxFileSizeBytes": 104857600,
    "AllowedFileExtensions": [".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg"]
  }
}
```

**Configuration Options:**
- `BaseUrl`: URL of the Python audio enhancement service
- `TimeoutSeconds`: Maximum time to wait for processing (10 minutes default)
- `MaxFileSizeBytes`: Maximum allowed file size (100MB default)
- `AllowedFileExtensions`: Supported audio formats

### 3. Start the Python Audio Enhancement Service

Before running the .NET API, ensure the Python microservice is running:

```bash
# Navigate to your Python service directory
cd ../audio-enhancer-service

# Activate virtual environment
source venv/bin/activate  # Linux/macOS
# or
venv\Scripts\Activate.ps1  # Windows PowerShell

# Start the Python service
python -m uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

Verify it's running by visiting: http://localhost:8000/docs

### 4. Run the .NET API

```bash
# Run in development mode
dotnet run

# Or run with specific environment
dotnet run --environment Development
```

The API will be available at:
- **Swagger UI**: https://localhost:7095 (or http://localhost:5095)
- **Health Check**: https://localhost:7095/health

## 📋 API Endpoints

### 1. Upload Audio for Processing

**POST** `/api/audio/upload`

Uploads an audio file and processes it through the Python microservice.

**Request:**
- Content-Type: `multipart/form-data`
- Form field: `file` (audio file)

**Response:**
```json
{
  "success": true,
  "message": "Audio processing completed successfully",
  "processingId": "unique-processing-id",
  "outputFile": "enhanced_audio.wav",
  "downloadUrl": "/download/enhanced_audio.wav",
  "processingDetails": {
    "processingTime": 12.5,
    "fileSizeMb": 5.2,
    "enhancementApplied": "AI Audio Enhancement"
  }
}
```

### 2. Download Processed Audio

**GET** `/api/audio/download/{filename}`

Downloads the processed audio file.

**Response:** Binary audio file

### 3. Service Health Check

**GET** `/api/audio/health`

Checks the health of the Python microservice.

**Response:**
```json
{
  "status": "Healthy",
  "message": "Audio enhancement service is available",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 4. API Health Check

**GET** `/health`

Checks the health of the .NET API itself.

## 🧪 Testing the API

### Using cURL

```bash
# Upload an audio file for processing
curl -X POST "https://localhost:7095/api/audio/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@path/to/your/audio.wav"

# Check service health
curl -X GET "https://localhost:7095/api/audio/health"

# Download processed file
curl -X GET "https://localhost:7095/api/audio/download/enhanced_audio.wav" \
  --output "enhanced_audio.wav"
```

### Using PowerShell

```powershell
# Upload audio file
$uri = "https://localhost:7095/api/audio/upload"
$filePath = "C:\path\to\your\audio.wav"
$form = @{
    file = Get-Item $filePath
}
Invoke-RestMethod -Uri $uri -Method Post -Form $form

# Check health
Invoke-RestMethod -Uri "https://localhost:7095/api/audio/health" -Method Get
```

### Using HTTP Client (VS Code REST Client)

```http
### Upload audio file
POST https://localhost:7095/api/audio/upload
Content-Type: multipart/form-data; boundary=boundary

--boundary
Content-Disposition: form-data; name="file"; filename="audio.wav"
Content-Type: audio/wav

< ./path/to/audio.wav
--boundary--

### Check health
GET https://localhost:7095/api/audio/health

### Download processed file
GET https://localhost:7095/api/audio/download/enhanced_audio.wav
```

## 🔧 Development Configuration

### Logging

The API uses Serilog for structured logging:
- Console output for development
- File logging to `logs/audio-backend-{date}.txt`
- Configurable log levels in `appsettings.json`

### CORS

CORS is enabled for development to allow cross-origin requests from frontend applications.

### Swagger/OpenAPI

Full API documentation is available via Swagger UI when running in development mode.

## 🐳 Production Deployment

### Docker Deployment

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AudioBackend.csproj", "."]
RUN dotnet restore "AudioBackend.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "AudioBackend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AudioBackend.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AudioBackend.dll"]
```

### Environment Variables

For production, use environment variables:

```bash
export AudioEnhancementService__BaseUrl="https://your-python-service.com"
export AudioEnhancementService__TimeoutSeconds="600"
export AudioEnhancementService__MaxFileSizeBytes="104857600"
```

## 📊 Monitoring and Health Checks

The API includes comprehensive health monitoring:

1. **API Health**: `/health` - Monitors the .NET API itself
2. **Service Health**: `/api/audio/health` - Monitors Python microservice connectivity
3. **Structured Logging**: All operations are logged with correlation IDs
4. **Error Handling**: Graceful error responses with appropriate HTTP status codes

## 🎯 Features

- ✅ **File Validation**: Supports WAV, MP3, FLAC, M4A, AAC, OGG formats
- ✅ **Size Limits**: Configurable maximum file size (100MB default)
- ✅ **Timeout Handling**: Long-running audio processing support
- ✅ **Error Recovery**: Comprehensive error handling and logging
- ✅ **Health Monitoring**: Real-time service health checks
- ✅ **API Documentation**: Auto-generated Swagger documentation
- ✅ **CORS Support**: Cross-origin request support for web applications
- ✅ **Structured Logging**: Professional logging with Serilog
- ✅ **Configuration Management**: Environment-based configuration

## 🔍 Troubleshooting

### Common Issues

1. **Python Service Not Available**
   - Ensure the Python service is running on the configured URL
   - Check the `BaseUrl` in `appsettings.json`
   - Verify firewall settings

2. **File Upload Fails**
   - Check file format is supported
   - Verify file size is within limits
   - Ensure sufficient disk space

3. **Timeout Errors**
   - Increase `TimeoutSeconds` for large files
   - Check Python service performance
   - Monitor system resources

### Logs Location

- **Development**: Console output + `logs/audio-backend-{date}.txt`
- **Production**: Configure log aggregation (e.g., ELK stack, Azure Monitor)

## 🤝 Contributing

This project is part of Sergie Code's AI tools for musicians series. Contributions are welcome!

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📝 License

This project is created for educational purposes as part of Sergie Code's programming tutorials.

## 💼 About Sergie Code

**Software Engineer & YouTube Educator**

Passionate about creating AI tools for musicians and teaching programming through practical projects. This audio enhancement API is part of a comprehensive suite of AI tools designed to empower musicians and audio professionals.

**Connect with Sergie Code:**
- 🎥 **YouTube**: [Sergie Code Channel]
- 💼 **LinkedIn**: [Sergie Code Profile]  
- 🐙 **GitHub**: [sergiecode]

---

*Built with ❤️ for the music and developer community*

**Happy Coding! 🚀**
