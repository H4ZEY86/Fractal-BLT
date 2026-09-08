$ErrorActionPreference = 'Stop'

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " FRACTAL-BLT // ZERO-ALLOCATION INFERENCE SHOWCASE      " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Ensure port 5000 is clear
$conflictingProcess = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
if ($conflictingProcess) {
    Write-Host "[SYSTEM] Port 5000 is locked. Attempting to terminate conflicting process..." -ForegroundColor Yellow
    Stop-Process -Id $conflictingProcess.OwningProcess -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# 2. Spawn the Kestrel backend
Write-Host "[SYSTEM] Initializing FractalServe NativeAOT backend..." -ForegroundColor Magenta
$processArgs = @{
    FilePath = "dotnet"
    ArgumentList = "run", "--project", "C:\Fractal-BLT\FractalServe"
    WindowStyle = "Hidden"
    PassThru = $true
}
$serverProcess = Start-Process @processArgs

# Wait for Kestrel to bind to port 5000
$maxRetries = 20
$retryCount = 0
$serverReady = $false

while (-not $serverReady -and $retryCount -lt $maxRetries) {
    Start-Sleep -Milliseconds 500
    $connection = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
    if ($connection) {
        $serverReady = $true
    }
    $retryCount++
}

if (-not $serverReady) {
    Write-Host "[ERROR] FractalServe failed to bind to port 5000." -ForegroundColor Red
    if (-not $serverProcess.HasExited) { Stop-Process -Id $serverProcess.Id -Force }
    exit 1
}

Write-Host "[SYSTEM] FractalServe Online. Memory-mapped NVMe DMA initialized." -ForegroundColor Green
Write-Host ""
Write-Host "USER: Initialize sequence: test tensor read stream." -ForegroundColor DarkGray
Write-Host ""
Write-Host "FRACTAL-BLT: " -NoNewline -ForegroundColor Cyan

# 3. Stream SSE using native .NET HttpClient
try {
    # Ensure System.Net.Http is loaded for Windows PowerShell 5.1
    Add-Type -AssemblyName System.Net.Http
    
    $client = [System.Net.Http.HttpClient]::new()
    
    # We must use HttpCompletionOption.ResponseHeadersRead to stream chunks live
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "http://localhost:5000/v1/chat/completions")
    $jsonPayload = '{"model":"qwen3.8-27b","messages":[{"role":"user","content":"Initialize sequence: test tensor read stream."}],"stream":true}'
    $request.Content = [System.Net.Http.StringContent]::new($jsonPayload, [System.Text.Encoding]::UTF8, "application/json")
    
    $responseTask = $client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead)
    $responseTask.Wait()
    $response = $responseTask.Result

    if (-not $response.IsSuccessStatusCode) {
        throw "API Error: $($response.StatusCode)"
    }

    $streamTask = $response.Content.ReadAsStreamAsync()
    $streamTask.Wait()
    $stream = $streamTask.Result
    $reader = [System.IO.StreamReader]::new($stream)

    while (-not $reader.EndOfStream) {
        $line = $reader.ReadLine()
        
        if ($line.StartsWith("data: ")) {
            $dataStr = $line.Substring(6).Trim()
            
            if ($dataStr -eq "[DONE]") {
                break
            }
            if ([string]::IsNullOrWhiteSpace($dataStr)) {
                continue
            }
            
            try {
                $obj = $dataStr | ConvertFrom-Json
                $token = $obj.choices[0].delta.content
                if ($null -ne $token) {
                    Write-Host -NoNewline $token
                    # Artificially slow down terminal output for the video recording pacing
                    Start-Sleep -Milliseconds 20
                }
            } catch {
                # Ignore JSON parse errors on partial chunks
            }
        }
    }
}
finally {
    Write-Host ""
    Write-Host ""
    Write-Host "[SYSTEM] Terminating NVMe DMA pipeline..." -ForegroundColor Yellow
    
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host "[SYSTEM] Execution Complete." -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Cyan
}
