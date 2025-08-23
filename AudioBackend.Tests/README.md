# 🧪 Audio Backend API Tests

**Created by Sergie Code** - AI Tools for Musicians

Comprehensive test suite for the Audio Backend API, including unit tests, integration tests, and performance tests.

## 📋 Test Overview

This test project provides comprehensive coverage for the Audio Backend API with the following test types:

### 🔧 Unit Tests
- **AudioProcessorService Tests** - Core business logic testing
- **AudioController Tests** - API endpoint testing
- **Validation Tests** - File validation and security testing

### 🔗 Integration Tests
- **End-to-End API Testing** - Complete request/response flow
- **Python Service Integration** - Mocked external service testing
- **Error Scenario Testing** - Real-world failure handling

### ⚡ Performance Tests
- **Response Time Testing** - Ensures API meets performance requirements
- **Memory Usage Testing** - Validates efficient resource usage
- **Concurrent Load Testing** - Tests under multiple simultaneous requests

## 🚀 Running Tests

### Prerequisites
- .NET 8.0 SDK
- PowerShell (for test runner script)

### Quick Start

```bash
# Run all tests
dotnet test

# Or use the PowerShell test runner
.\run-tests.ps1
```

### Test Runner Options

The included PowerShell script (`run-tests.ps1`) provides additional options:

```powershell
# Run all tests
.\run-tests.ps1

# Run only unit tests
.\run-tests.ps1 -TestType Unit

# Run only integration tests
.\run-tests.ps1 -TestType Integration

# Run with code coverage
.\run-tests.ps1 -Coverage

# Run with verbose output
.\run-tests.ps1 -Verbose

# Run specific tests
.\run-tests.ps1 -Filter "AudioController"

# Run without rebuilding
.\run-tests.ps1 -NoBuild
```

### Manual Test Commands

```bash
# Build and run all tests
dotnet test AudioBackend.Tests --configuration Debug

# Run with code coverage
dotnet test AudioBackend.Tests --collect:"XPlat Code Coverage"

# Run specific test categories
dotnet test AudioBackend.Tests --filter "FullyQualifiedName~Integration"
dotnet test AudioBackend.Tests --filter "FullyQualifiedName~Performance"

# Run with detailed output
dotnet test AudioBackend.Tests --verbosity detailed
```

## 📊 Test Coverage

The test suite covers:

### AudioProcessorService (95%+ coverage)
- ✅ File validation (format, size, content)
- ✅ HTTP communication with Python service
- ✅ Error handling and retry logic
- ✅ Health check functionality
- ✅ Download functionality

### AudioController (95%+ coverage)
- ✅ File upload endpoint
- ✅ Download endpoint
- ✅ Health check endpoint
- ✅ Error response handling
- ✅ Content type detection

### Integration Scenarios (90%+ coverage)
- ✅ Complete request/response flow
- ✅ Python service integration (mocked)
- ✅ Various file formats
- ✅ Error scenarios
- ✅ Service health monitoring

## 🧪 Test Structure

```
AudioBackend.Tests/
├── Controllers/
│   └── AudioControllerTests.cs          # API endpoint tests
├── Services/
│   ├── AudioProcessorServiceTests.cs    # Core service tests
│   └── ValidationTests.cs               # Input validation tests
├── Integration/
│   └── AudioControllerIntegrationTests.cs # End-to-end tests
├── Performance/
│   └── PerformanceTests.cs              # Performance benchmarks
├── Helpers/
│   └── TestDataHelper.cs                # Test utilities and data generation
├── GlobalUsings.cs                      # Global test dependencies
├── appsettings.Testing.json             # Test configuration
└── AudioBackend.Tests.csproj            # Test project configuration
```

## 🔍 Test Categories

### Unit Tests
Focus on individual components in isolation:
- Method-level testing
- Mocked dependencies
- Fast execution (< 1ms per test)
- No external dependencies

### Integration Tests
Test component interaction:
- API endpoint testing
- Mocked external services
- Real HTTP requests
- Configuration testing

### Performance Tests
Validate performance requirements:
- Response time limits
- Memory usage validation
- Concurrent request handling
- Resource cleanup verification

## 📦 Test Dependencies

Key testing packages used:

```xml
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.8" />
<PackageReference Include="WireMock.Net" Version="1.5.58" />
```

### Package Purposes
- **xUnit** - Test framework
- **Moq** - Mocking framework for dependencies
- **FluentAssertions** - Readable assertion syntax
- **AspNetCore.Mvc.Testing** - Integration testing for ASP.NET Core
- **WireMock.Net** - HTTP service mocking for Python service

## 🎯 Test Examples

### Unit Test Example
```csharp
[Fact]
public async Task ProcessAudioAsync_ValidWavFile_ReturnsSuccessResult()
{
    // Arrange
    var mockFile = CreateMockAudioFile("test.wav", "audio/wav", 1024);
    SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

    // Act
    var result = await _audioProcessorService.ProcessAudioAsync(mockFile);

    // Assert
    result.Should().NotBeNull();
    result.Success.Should().BeTrue();
    result.ProcessingId.Should().Be("test-123");
}
```

### Integration Test Example
```csharp
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
}
```

## 🔧 Test Configuration

### Test Settings
The `appsettings.Testing.json` file contains test-specific configuration:

```json
{
  "AudioEnhancementService": {
    "BaseUrl": "http://localhost:8001",
    "TimeoutSeconds": 30,
    "MaxFileSizeBytes": 10485760
  }
}
```

### Environment Variables
For CI/CD pipelines, you can override settings using environment variables:

```bash
export AudioEnhancementService__BaseUrl="http://test-service:8000"
export AudioEnhancementService__TimeoutSeconds="60"
```

## 🚀 Continuous Integration

### GitHub Actions Example
```yaml
name: Test Audio Backend API

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    - name: Restore dependencies
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore
    - name: Test
      run: dotnet test --no-build --collect:"XPlat Code Coverage"
```

## 📈 Performance Benchmarks

The performance tests validate:

| Test | Expected Performance |
|------|---------------------|
| File Validation | < 100ms for small files |
| Large File Validation | < 500ms for 50MB files |
| Health Check | < 50ms response time |
| Concurrent Requests | 10 concurrent requests < 2s |
| Memory Usage | < 10x file size increase |

## 🔍 Troubleshooting Tests

### Common Issues

1. **Integration Tests Fail**
   - Ensure no other service is running on port 8001
   - Check WireMock setup in test configuration

2. **Performance Tests Fail**
   - Run on a dedicated test machine
   - Disable other resource-intensive applications

3. **Coverage Reports Missing**
   - Ensure `coverlet.collector` package is installed
   - Use `--collect:"XPlat Code Coverage"` flag

### Debug Tips

```bash
# Run a specific test with detailed output
dotnet test --filter "ProcessAudioAsync_ValidWavFile_ReturnsSuccessResult" --verbosity detailed

# Run tests and keep the terminal open
dotnet test --logger "console;verbosity=detailed" --no-build
```

## 💡 Contributing to Tests

When adding new functionality:

1. **Add unit tests** for new methods/classes
2. **Add integration tests** for new endpoints
3. **Update performance tests** if performance characteristics change
4. **Maintain 90%+ code coverage**

### Test Naming Convention
```
MethodName_Scenario_ExpectedResult
```

Examples:
- `ProcessAudioAsync_ValidFile_ReturnsSuccess`
- `UploadAudio_InvalidFormat_ReturnsBadRequest`
- `GetHealth_ServiceDown_ReturnsUnavailable`

---

## 💼 About Sergie Code

**Software Engineer & YouTube Educator**

This comprehensive test suite demonstrates professional testing practices for .NET APIs, ensuring reliability and maintainability of AI tools for musicians.

**Connect with Sergie Code:**
- 🎥 **YouTube**: [Sergie Code Channel]
- 💼 **LinkedIn**: [Sergie Code Profile]
- 🐙 **GitHub**: [sergiecode]

---

*Built with ❤️ for the music and developer community*

**Happy Testing! 🧪**
