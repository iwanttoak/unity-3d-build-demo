# V1–V8 全流程验收：真实场景 + HUD，逐步断言并截图归档
# 用法: & Tools\run_acceptance.ps1 [-SkipShots]
param(
    [switch]$SkipShots
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$bridge = Join-Path $root 'Tools\unity_bridge.ps1'
$evidence = Join-Path $root 'Docs\evidence'
$shotDir = Join-Path $evidence 'acceptance'
New-Item -ItemType Directory -Force -Path $shotDir | Out-Null

function CB {
    param([hashtable]$P)
    for ($i = 0; $i -lt 40; $i++) {
        try {
            # unity_bridge.ps1 把结果以 JSON 文本输出，这里统一解析成对象
            return ((& $bridge @P) | ConvertFrom-Json)
        } catch { Start-Sleep -Milliseconds 1200 }
    }
    throw "bridge 调用失败"
}

function Eval([string]$code) {
    $j = CB @{ Action = 'eval'; Command = $code }
    $r = $j.result
    if ($r -is [string]) { $r = $r | ConvertFrom-Json }
    if ($r.error) { throw "eval 失败: $($r.error) $($r.errorDetails)" }
    return $r.result
}

function Get-PlayMode {
    $st = (CB @{ Action = 'exec'; Command = 'editor_status' }).result
    if ($st -is [string]) { $st = $st | ConvertFrom-Json }
    return $st.playMode
}

# Play Mode 有三种状态：playing / paused / stopped。
# 注意：Console 开了 Error Pause 时，任何异常都会把编辑器停成 paused，
# 后面的步骤就会以为「没进播放」，所以 paused 也当「在播放」并自动恢复。
function Test-InPlay {
    return ((Get-PlayMode) -in @('playing', 'paused'))
}

function Resume-IfPaused {
    if ((Get-PlayMode) -eq 'paused') {
        (CB @{ Action = 'eval'; Command = 'UnityEditor.EditorApplication.isPaused = false; return "unpaused";' }) | Out-Null
        Start-Sleep -Milliseconds 500
    }
}

function Enter-PlayMode {
    for ($i = 0; $i -lt 10; $i++) {
        if (Test-InPlay) { Resume-IfPaused; return $true }
        (CB @{ Action = 'exec'; Command = 'editor_play' }) | Out-Null
        Start-Sleep -Seconds 1
    }
    if (Test-InPlay) { Resume-IfPaused; return $true }
    return $false
}

function Stop-PlayMode {
    (CB @{ Action = 'exec'; Command = 'editor_stop' }) | Out-Null
    for ($i = 0; $i -lt 20; $i++) {
        if ((Get-PlayMode) -eq 'stopped') { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Save-Shot([string]$name) {
    if ($SkipShots) { return }
    $rel = "Docs/evidence/acceptance/$name.png"
    $json = @{ width = 1280; height = 720; source = 'screen'; save_path = $rel } | ConvertTo-Json -Compress
    (CB @{ Action = 'exec'; Command = 'capture_game_view'; ParamsJson = $json }) | Out-Null

    # capture_game_view 的 save_path 会被解析到 Assets/ 下，这里挪回 Docs/evidence
    $tmp = Join-Path $root 'Assets\Docs\evidence\acceptance'
    if (Test-Path $tmp) {
        Get-ChildItem $tmp -Filter *.png | ForEach-Object {
            Move-Item -LiteralPath $_.FullName -Destination (Join-Path $shotDir $_.Name) -Force
        }
    }
    foreach ($cleanup in @('Assets\Docs\evidence', 'Assets\Docs')) {
        $p = Join-Path $root $cleanup
        if (Test-Path $p) { Remove-Item -LiteralPath $p -Recurse -Force }
    }
    $meta = Join-Path $root 'Assets\Docs.meta'
    if (Test-Path $meta) { Remove-Item -LiteralPath $meta -Force }
    Write-Host ("    [shot] " + (Join-Path 'Docs/evidence/acceptance' "$name.png"))
}

# ---------------------------------------------------------------- 准备
Write-Host '=== 准备：干净场景 + Play Mode ==='
if (Test-InPlay) { Stop-PlayMode | Out-Null }
(CB @{ Action = 'eval'; Command = 'return Demo3d.EditorTools.Demo3dSceneBuilder.BuildScene();' }) | Out-Null
(CB @{ Action = 'exec'; Command = 'save_scene' }) | Out-Null
if (-not (Enter-PlayMode)) { throw '进入 Play Mode 超时' }
Start-Sleep -Seconds 2

Write-Host "配置复位: " + (Eval 'return Demo3d.Diagnostics.AcceptanceRunner.ApplyConfig(180,60);')
Write-Host "重新开始: " + (Eval 'return Demo3d.Diagnostics.AcceptanceRunner.RestartNow();')

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# V1–V8 全流程验收报告')
$lines.Add('')
$lines.Add('执行方式：AIBridge 驱动 Play Mode，逐步调用 `Demo3d.Diagnostics.AcceptanceRunner.RunStep("Vx")`，每步截图归档到 `Docs/evidence/acceptance/`。')
$lines.Add('')
$lines.Add('| 编号 | 结果 | 期望 | 实际 |')
$lines.Add('|---|---|---|---|')

$allPass = $true

function Record([string]$raw) {
    # 格式: Vx|PASS|期望|实际
    $parts = $raw.Split('|')
    if ($parts.Count -lt 4) { Write-Host "    !! 无法解析: $raw"; return }
    $id = $parts[0]; $status = $parts[1]; $expected = $parts[2]; $actual = $parts[3]
    $script:lines.Add("| $id | $status | $expected | $actual |")
    if ($status -ne 'PASS') { $script:allPass = $false }
    Write-Host ("    {0} {1}  {2}" -f $id, $status, $actual)
}

# ---------------------------------------------------------------- V1–V6（默认配置，顺序演示）
Write-Host '=== V1–V6（默认 180/60，顺序演示）==='
$steps = @(
    @{ Id = 'V1'; Shot = 'V1_建A' },
    @{ Id = 'V2'; Shot = 'V2_连点A' },
    @{ Id = 'V3'; Shot = 'V3_余额0' },
    @{ Id = 'V4'; Shot = 'V4_拆除A' },
    @{ Id = 'V5'; Shot = 'V5_重开再建' },
    @{ Id = 'V6'; Shot = 'V6_连重开' }
)
foreach ($s in $steps) {
    Resume-IfPaused
    $r = Eval ("return Demo3d.Diagnostics.AcceptanceRunner.RunStep(`"$($s.Id)`");")
    Record $r
    Save-Shot $s.Shot
}

# ---------------------------------------------------------------- V7（120/40 配置）
Write-Host '=== V7（改成 120 金币 / 40 费用）==='
$coinsBefore = [int](Eval 'var c = UnityEngine.Object.FindFirstObjectByType<Demo3d.BuildController>(); return c.Coins;')
$cfg = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.ApplyConfig(120,40);'
Write-Host "    $cfg"
$coinsAfterConfig = [int](Eval 'var c = UnityEngine.Object.FindFirstObjectByType<Demo3d.BuildController>(); return c.Coins;')
$configDeferred = ($coinsBefore -eq $coinsAfterConfig)
$allPass = $allPass -and $configDeferred
$lines.Add('')
$lines.Add("V7 前提：Inspector 改为 initialCoins=120, buildCost=40。")
$lines.Add('')
$lines.Add(("改配置本身不改变当前金币：改前 {0}、改后 {1} → {2}" -f $coinsBefore, $coinsAfterConfig, $(if ($configDeferred) { 'PASS' } else { 'FAIL' })))
$lines.Add('')
$lines.Add('| 编号 | 结果 | 期望 | 实际 |')
$lines.Add('|---|---|---|---|')
Write-Host "    改配置前后金币: $coinsBefore -> $coinsAfterConfig (deferred=$configDeferred)"
$restart = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.RestartNow();'
Write-Host "    $restart"
$r7 = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.RunStep("V7");'
Record $r7
Save-Shot 'V7_测试配置120_40'

# ---------------------------------------------------------------- V8（恢复默认配置）
Write-Host '=== V8（恢复默认 180/60 并重开）==='
$cfg2 = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.ApplyConfig(180,60);'
Write-Host "    $cfg2"
$restart2 = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.RestartNow();'
Write-Host "    $restart2"
$r8 = Eval 'return Demo3d.Diagnostics.AcceptanceRunner.RunStep("V8");'
Record $r8
Save-Shot 'V8_默认序列'

# ---------------------------------------------------------------- 收尾
$lines.Add('')
$lines.Add("总结果：**" + ($(if ($allPass) { 'PASS' } else { 'FAIL' })) + "**")
$lines.Add('')
$lines.Add('截图（`Docs/evidence/acceptance/`）：V1_建A、V2_连点A、V3_余额0、V4_拆除A、V5_重开再建、V6_连重开、V7_测试配置120_40、V8_默认序列')

$report = Join-Path $evidence 'V1V8_acceptance_report.md'
[System.IO.File]::WriteAllText($report, ($lines -join "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "报告: $report"

Write-Host '=== 收尾：退出 Play Mode 并保存干净场景 ==='
Stop-PlayMode | Out-Null
Start-Sleep -Seconds 2
(CB @{ Action = 'eval'; Command = 'return Demo3d.EditorTools.Demo3dSceneBuilder.BuildScene();' }) | Out-Null
(CB @{ Action = 'exec'; Command = 'save_scene' }) | Out-Null

if ($allPass) { Write-Host 'RESULT: PASS'; exit 0 }
Write-Host 'RESULT: FAIL'; exit 2
