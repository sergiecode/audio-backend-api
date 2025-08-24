#!/usr/bin/env pwsh

# Test runner script for Audio Backend API
# Created by Sergie Code - AI Tools for Musicians

param(
    [string]$TestType = "All",  # All, Unit, Integration
    [switch]$Coverage = $false,
    [switch]$Verbose = $false,
    [string]$Filter = "",
    [switch]$NoBuild = $false
)

Write-Host "🎵 Audio Backend API Test Runner" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Check if dotnet is installed
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        throw "❌ .NET SDK is not installed or not in PATH"
    }

    # Display .NET version
    $dotnetVersion = dotnet --version
    Write-Host "📦 .NET SDK Version: $dotnetVersion" -ForegroundColor Green

    # Navigate to the test project directory
    $testProjectPath = "AudioBackend.Tests"
    if (-not (Test-Path $testProjectPath)) {
        throw "❌ Test project not found at: $testProjectPath"
    }

    # Build the solution first (unless --no-build is specified)
    if (-not $NoBuild) {
        Write-Host "🔨 Building solution..." -ForegroundColor Yellow
        dotnet build --configuration Debug
        if ($LASTEXITCODE -ne 0) {
            throw "❌ Build failed"
        }
        Write-Host "✅ Build completed successfully" -ForegroundColor Green
    }

    # Prepare test command
    $testCommand = @("test", $testProjectPath)
    
    # Add configuration
    $testCommand += @("--configuration", "Debug")
    
    # Add no-build if specified
    if ($NoBuild) {
        $testCommand += "--no-build"
    }

    # Add verbosity
    if ($Verbose) {
        $testCommand += @("--verbosity", "detailed")
    } else {
        $testCommand += @("--verbosity", "normal")
    }

    # Add test filter based on test type
    switch ($TestType.ToLower()) {
        "unit" {
            $testCommand += @("--filter", "FullyQualifiedName~Services|FullyQualifiedName~Controllers&FullyQualifiedName!~Integration")
            Write-Host "🧪 Running Unit Tests..." -ForegroundColor Yellow
        }
        "integration" {
            $testCommand += @("--filter", "FullyQualifiedName~Integration")
            Write-Host "🔗 Running Integration Tests..." -ForegroundColor Yellow
        }
        "all" {
            Write-Host "🧪 Running All Tests..." -ForegroundColor Yellow
        }
        default {
            throw "❌ Invalid test type: $TestType. Valid options: All, Unit, Integration"
        }
    }

    # Add custom filter if provided
    if ($Filter) {
        if ($TestType.ToLower() -ne "all") {
            Write-Warning "⚠️  Custom filter will override test type filter"
        }
        $testCommand += @("--filter", $Filter)
        Write-Host "🔍 Using custom filter: $Filter" -ForegroundColor Cyan
    }

    # Add coverage collection
    if ($Coverage) {
        $testCommand += @("--collect", "XPlat Code Coverage")
        Write-Host "📊 Code coverage collection enabled" -ForegroundColor Cyan
    }

    # Add logger for better output
    $testCommand += @("--logger", "console;verbosity=normal")

    # Run the tests
    Write-Host "⚡ Executing tests..." -ForegroundColor Blue
    Write-Host "Command: dotnet $($testCommand -join ' ')" -ForegroundColor Gray
    
    $startTime = Get-Date
    & dotnet @testCommand
    $exitCode = $LASTEXITCODE
    $endTime = Get-Date
    $duration = $endTime - $startTime

    # Display results
    Write-Host "" -ForegroundColor White
    Write-Host "📋 Test Execution Summary" -ForegroundColor Cyan
    Write-Host "=========================" -ForegroundColor Cyan
    Write-Host "⏱️  Duration: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor White
    
    if ($exitCode -eq 0) {
        Write-Host "✅ All tests passed!" -ForegroundColor Green
        
        if ($Coverage) {
            Write-Host "" -ForegroundColor White
            Write-Host "📊 Code Coverage Report" -ForegroundColor Cyan
            Write-Host "=======================" -ForegroundColor Cyan
            
            # Find coverage files
            $coverageFiles = Get-ChildItem -Path "AudioBackend.Tests/TestResults" -Filter "*.xml" -Recurse -ErrorAction SilentlyContinue
            if ($coverageFiles) {
                Write-Host "📄 Coverage files generated:" -ForegroundColor Green
                $coverageFiles | ForEach-Object { Write-Host "   $($_.FullName)" -ForegroundColor Gray }
                Write-Host "💡 Use a tool like ReportGenerator to view detailed coverage reports" -ForegroundColor Yellow
            } else {
                Write-Host "⚠️  No coverage files found" -ForegroundColor Yellow
            }
        }
        
        Write-Host "" -ForegroundColor White
        Write-Host "🎉 Test execution completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "❌ Some tests failed!" -ForegroundColor Red
        Write-Host "💡 Check the test output above for details" -ForegroundColor Yellow
        exit $exitCode
    }

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}

# Display usage examples
Write-Host "" -ForegroundColor White
Write-Host "💡 Usage Examples:" -ForegroundColor Cyan
Write-Host "  .\run-tests.ps1                     # Run all tests" -ForegroundColor Gray
Write-Host "  .\run-tests.ps1 -TestType Unit      # Run unit tests only" -ForegroundColor Gray
Write-Host "  .\run-tests.ps1 -TestType Integration # Run integration tests only" -ForegroundColor Gray
Write-Host "  .\run-tests.ps1 -Coverage           # Run with code coverage" -ForegroundColor Gray
Write-Host "  .\run-tests.ps1 -Verbose           # Run with detailed output" -ForegroundColor Gray
Write-Host "  .\run-tests.ps1 -Filter 'AudioController' # Run specific tests" -ForegroundColor Gray
