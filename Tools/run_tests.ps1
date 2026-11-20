# 跑 EditMode 测试并落存证（走 AIBridge，不关 Editor）
# 用异步模式 + 轮询 test_status：同步模式会在跑测试期间长期占用 Editor 的命令队列，
# 一旦测试卡住，后续所有命令都会跟着排队超时。异步模式把「等待」放到客户端。
# 用法: & Tools\run_tests.ps1 [-Filter V1_V8] [-Mode editor] [-TimeoutSec 180]
param(
    [string]$Filter = '',
    [string]$Mode = 'editor',
    [int]$TimeoutSec = 180,
    [string]$Evidence = 'Docs\evidence\editmode_tests.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$bridge = Join-Path $root 'Tools\unity_bridge.ps1'

function CB {
    param([hashtable]$P)
    for ($i = 0; $i -lt 20; $i++) {
        try { return ((& $bridge @P) | ConvertFrom-Json) } catch { Start-Sleep -Milliseconds 800 }
    }
    throw "bridge 调用失败: $($P['Command'])"
}

function Parse([object]$value) {
    if ($value -is [string]) { return ($value | ConvertFrom-Json) }
    return $value
}

$params = @{ mode = $Mode; async_tests = $true; timeout = $TimeoutSec }
if ($Filter) { $params.filter = $Filter; $params.filter_type = 'testName' }
$paramsJson = $params | ConvertTo-Json -Compress

$start = CB @{ Action = 'exec'; Command = 'run_tests'; ParamsJson = $paramsJson }
Write-Host "已提交测试（异步）: $((Parse $start.result).result)"

$status = $null
$deadline = (Get-Date).AddSeconds($TimeoutSec + 30)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $resp = CB @{ Action = 'exec'; Command = 'test_status' }
    $s = Parse $resp.result
    $status = $s.status
    if ($status -in @('completed', 'failed', 'canceled', 'cancelled')) {
        $result = $s
        break
    }
    Write-Host "    ... $status"
}

if (-not $result) {
    Write-Host "RESULT: FAIL —— 等待测试超时（$status）"
    exit 2
}

$out = Join-Path $root $Evidence
($result | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $out -Encoding UTF8

$sum = $result.summary
Write-Host ("Total={0} Passed={1} Failed={2} Skipped={3} Duration={4}s" -f `
        $sum.total, $sum.passed, $sum.failed, $sum.skipped, $result.duration)
$result.results | Select-Object Status, FullName, Message | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
Write-Host "存证: $out"

if ($sum.failed -gt 0) { exit 2 }
exit 0
