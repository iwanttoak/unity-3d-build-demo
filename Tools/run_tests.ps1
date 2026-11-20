# 一键跑 EditMode 测试并落存证（走 AIBridge，不关 Editor）
# 用法: & Tools\run_tests.ps1 [-Filter V1_V8] [-Mode editor]
param(
    [string]$Filter = '',
    [string]$Mode = 'editor',
    [string]$Evidence = 'Docs\evidence\editmode_tests.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$bridge = Join-Path $root 'Tools\unity_bridge.ps1'

# 等编译完成后才能跑测试
if (Test-Path (Join-Path $root 'Library\Pipeline\.unity-pipeline-port')) {
    & $bridge -Action status | Out-Null
}

$params = @{ mode = $Mode; timeout = 300 }
if ($Filter) { $params.filter = $Filter; $params.filter_type = 'testName' }
$paramsJson = $params | ConvertTo-Json -Compress

$raw = & $bridge -Action exec -Command run_tests -ParamsJson $paramsJson
$j = $raw | ConvertFrom-Json
$res = $j.result
if ($res -is [string]) { $res = $res | ConvertFrom-Json }

$out = Join-Path $root $Evidence
$raw | Set-Content -LiteralPath $out -Encoding UTF8

Write-Host ("Total={0} Passed={1} Failed={2} Skipped={3} Duration={4}s" -f `
        $res.Summary.Total, $res.Summary.Passed, $res.Summary.Failed, $res.Summary.Skipped, $res.Duration)
$res.Results | Select-Object Status, FullName, Message | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
Write-Host "存证: $out"

if ($res.Summary.Failed -gt 0) { exit 2 }
exit 0
