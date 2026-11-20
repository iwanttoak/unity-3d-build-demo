# Unity Pipeline (AIBridge) 客户端：通过 Editor 内 HTTP 服务执行命令
# 端点: http://127.0.0.1:<port>/api/{status,commands,exec,progress,job}
# 鉴权: Authorization: Bearer <evalToken>（来自 Library/Pipeline/.unity-pipeline-port）
# 用法:
#   & Tools\unity_bridge.ps1 -Action status
#   & Tools\unity_bridge.ps1 -Action commands
#   & Tools\unity_bridge.ps1 -Action exec -Command editor_status
#   & Tools\unity_bridge.ps1 -Action exec -Command eval -ParamsJson '{"code":"return 1+1;"}'
param(
    [ValidateSet('status', 'commands', 'exec', 'eval', 'raw')]
    [string]$Action = 'status',
    [string]$Command,
    [string]$ParamsJson = '{}',
    [string]$Path = '',
    [switch]$Job
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$descPath = Join-Path $root 'Library\Pipeline\.unity-pipeline-port'
if (-not (Test-Path $descPath)) { throw "未找到实例描述文件（Editor 未启动 Pipeline 服务？）: $descPath" }

$desc = Get-Content -LiteralPath $descPath -Raw | ConvertFrom-Json
if (-not (Get-Process -Id $desc.pid -ErrorAction SilentlyContinue)) { throw "描述文件里的 Editor 进程已不在: pid=$($desc.pid)" }

$base = "http://127.0.0.1:$($desc.port)"
$headers = @{ Authorization = "Bearer $($desc.evalToken)"; 'Content-Type' = 'application/json' }

function Invoke-UnityApi {
    param([string]$UrlPath, [string]$Method = 'GET', [string]$Body = $null)
    try {
        if ($Method -eq 'POST') {
            $r = Invoke-WebRequest -Uri "$base$UrlPath" -Method POST -Headers $headers -Body $Body -TimeoutSec 600 -UseBasicParsing
        } else {
            $r = Invoke-WebRequest -Uri "$base$UrlPath" -Method GET -Headers $headers -TimeoutSec 600 -UseBasicParsing
        }
        return ($r.Content | ConvertFrom-Json)
    } catch {
        $resp = $_.Exception.Response
        if ($resp) {
            $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $body = $sr.ReadToEnd()
            throw "HTTP $([int]$resp.StatusCode) on $UrlPath :: $body"
        }
        throw
    }
}

switch ($Action) {
    'status' {
        Invoke-UnityApi -UrlPath '/api/status' | ConvertTo-Json -Depth 12
    }
    'commands' {
        $r = Invoke-UnityApi -UrlPath '/api/commands'
        $names = @()
        if ($r.commands) { $names = $r.commands | ForEach-Object { if ($_.name) { $_.name } else { $_ } } }
        elseif ($r -is [array]) { $names = $r }
        if ($names.Count -gt 0) { $names | Sort-Object } else { $r | ConvertTo-Json -Depth 12 }
    }
    'exec' {
        if (-not $Command) { throw '-Action exec 需要 -Command <命令名>' }
        $payload = @{ command = $Command; parameters = (ConvertFrom-Json $ParamsJson) }
        if ($Job) { $payload.job = $true }
        $body = $payload | ConvertTo-Json -Depth 12 -Compress
        Invoke-UnityApi -UrlPath '/api/exec' -Method POST -Body $body | ConvertTo-Json -Depth 20
    }
    'eval' {
        if (-not $Command) { throw '-Action eval 需要 -Command <C# 代码，最后 return 表达式>' }
        $body = @{ command = 'eval'; parameters = @{ code = $Command } } | ConvertTo-Json -Depth 12 -Compress
        Invoke-UnityApi -UrlPath '/api/exec' -Method POST -Body $body | ConvertTo-Json -Depth 20
    }
    'raw' {
        Invoke-UnityApi -UrlPath $Path | ConvertTo-Json -Depth 20
    }
}
