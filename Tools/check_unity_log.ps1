# 编译门禁：从 Unity Editor.log 里抓本次会话的 C# 编译错误/警告
# 用法: pwsh -File Tools\check_unity_log.ps1            # 看最近 600 行
#       pwsh -File Tools\check_unity_log.ps1 -All       # 看整个日志文件
# 退出码: 0 = 无编译错误, 2 = 发现 error CS / 编译失败
param(
    [int]$TailLines = 600,
    [switch]$All
)

$log = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
if (-not (Test-Path $log)) { Write-Host "找不到 Editor.log: $log"; exit 1 }

$lines = if ($All) { Get-Content -LiteralPath $log } else { Get-Content -LiteralPath $log -Tail $TailLines }

$errPatterns = 'error CS\d+|Compilation failed|Scripts have compiler errors|Assembly .* will not be loaded'
$warnPatterns = 'warning CS\d+|warning CS'

$errors = $lines | Select-String -Pattern $errPatterns
$warnings = $lines | Select-String -Pattern $warnPatterns

Write-Host "=== 日志: $log (扫描 $(if ($All) {'全文件'} else {"最后 $TailLines 行"})) ==="
if ($warnings) {
    Write-Host "--- 警告 ($($warnings.Count)) ---"
    $warnings | Select-Object -First 20 | ForEach-Object { $_.Line.Trim() }
}
if ($errors) {
    Write-Host "--- 错误 ($($errors.Count)) ---"
    $errors | Select-Object -First 40 | ForEach-Object { $_.Line.Trim() }
    Write-Host "RESULT: FAIL —— 存在编译错误"
    exit 2
}
Write-Host "RESULT: PASS —— 无编译错误"
exit 0
