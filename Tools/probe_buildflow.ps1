# T3 流程自检：EditMode 与 Play Mode 各跑一次 BuildFlowProbe，并落存证与截图
# 用法: & Tools\probe_buildflow.ps1 [-SkipPlay] [-SkipScreenshot]
param(
    [switch]$SkipPlay,
    [switch]$SkipScreenshot
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$bridge = Join-Path $root 'Tools\unity_bridge.ps1'
$evidence = Join-Path $root 'Docs\evidence'

function CB {
    param([hashtable]$P)
    for ($i = 0; $i -lt 40; $i++) {
        try { return (& $bridge @P) } catch { Start-Sleep -Milliseconds 1200 }
    }
    throw "bridge 调用失败: $($P['Action'])"
}

function Get-PlayMode {
    $st = (CB @{ Action = 'exec'; Command = 'editor_status' }).result
    if ($st -is [string]) { $st = $st | ConvertFrom-Json }
    return $st.playMode
}

function Wait-PlayMode([string]$target, [int]$tries = 30) {
    for ($i = 0; $i -lt $tries; $i++) {
        if ((Get-PlayMode) -eq $target) { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Enter-PlayMode([int]$tries = 10) {
    for ($i = 0; $i -lt $tries; $i++) {
        $j = (CB @{ Action = 'exec'; Command = 'editor_play' }) | ConvertFrom-Json
        if ($j.result -eq 'Already in play mode' -or (Get-PlayMode) -eq 'playing') { return $true }
        if ($j.error) { Write-Host "    editor_play 重试: $($j.errorDetails)" }
        Start-Sleep -Seconds 2
    }
    return ((Get-PlayMode) -eq 'playing')
}

function Invoke-Probe([string]$label, [string]$file) {
    $r = CB @{ Action = 'eval'; Command = 'return Demo3d.Diagnostics.BuildFlowProbe.Run();' }
    $j = $r | ConvertFrom-Json
    $res = $j.result
    $text = $res.result
    if (-not $text) {
        Write-Host "[$label] eval 失败: $($res.error) $($res.errorDetails)"
        return $false
    }
    $text | Set-Content -LiteralPath (Join-Path $evidence $file) -Encoding UTF8
    $summary = ($text -split "`r?`n") | Where-Object { $_ -match 'RESULT' }
    $fails = ($text -split "`r?`n") | Where-Object { $_ -match '^FAIL' }
    Write-Host "[$label] $summary"
    $fails | ForEach-Object { Write-Host "    $_" }
    return ($fails.Count -eq 0)
}

function Invoke-HudProbe([string]$label, [string]$file) {
    $r = CB @{ Action = 'eval'; Command = 'return Demo3d.Diagnostics.HudProbe.Run();' }
    $j = $r | ConvertFrom-Json
    $res = $j.result
    $text = $res.result
    if (-not $text) {
        Write-Host "[$label] eval 失败: $($res.error) $($res.errorDetails)"
        return $false
    }
    $text | Set-Content -LiteralPath (Join-Path $evidence $file) -Encoding UTF8
    $summary = ($text -split "`r?`n") | Where-Object { $_ -match 'RESULT' }
    $fails = ($text -split "`r?`n") | Where-Object { $_ -match '^FAIL' }
    Write-Host "[$label] $summary"
    $fails | ForEach-Object { Write-Host "    $_" }
    return ($fails.Count -eq 0)
}

$ok = Invoke-Probe 'EditMode' 't3_buildflow_probe.txt'

if (-not $SkipPlay) {
    CB @{ Action = 'exec'; Command = 'save_scene' } | Out-Null
    if (-not (Enter-PlayMode)) { throw '进入 Play Mode 超时' }
    Start-Sleep -Seconds 2
    $ok = (Invoke-Probe 'PlayMode' 't3_buildflow_probe_playmode.txt') -and $ok
    $ok = (Invoke-HudProbe 'HUD' 't4_hud_probe.txt') -and $ok

    if (-not $SkipScreenshot) {
        $demo = @'
var c = UnityEngine.Object.FindFirstObjectByType<Demo3d.BuildController>();
c.Restart(); c.SelectSlot(0); c.Build(); c.SelectSlot(2); c.Build(); c.SelectSlot(2);
return c.Coins + "|" + c.BuildingCount;
'@
        (CB @{ Action = 'eval'; Command = $demo }) | Out-Null

        # 纯 3D 视角（不含 UI）
        $shot = @{ view = 'game'; output = (Join-Path $evidence 't3_built_playmode.png'); width = 1280; height = 720 } | ConvertTo-Json -Compress
        (CB @{ Action = 'exec'; Command = 'screenshot'; ParamsJson = $shot }) | Out-Null

        # 合成画面（含 Overlay HUD，仅 Play Mode 可得）
        $uiShot = @{ width = 1280; height = 720; source = 'screen'; save_path = 'Docs/evidence/t4_hud_normal.png' } | ConvertTo-Json -Compress
        $saved = ((CB @{ Action = 'exec'; Command = 'capture_game_view'; ParamsJson = $uiShot }) | ConvertFrom-Json).result
        if ($saved -is [string]) { $saved = $saved | ConvertFrom-Json }
        Write-Host "[Screenshot] $($saved.savedPath)"

        # capture_game_view 的 save_path 会被解析到 Assets/ 下，这里挪回 Docs/evidence 并清理
        $tmpDir = Join-Path $root 'Assets\Docs\evidence'
        if (Test-Path $tmpDir) {
            Get-ChildItem $tmpDir -Filter *.png | ForEach-Object { Move-Item -LiteralPath $_.FullName -Destination (Join-Path $evidence $_.Name) -Force }
        }
        if (Test-Path (Join-Path $root 'Assets\Docs')) { Remove-Item -LiteralPath (Join-Path $root 'Assets\Docs') -Recurse -Force }
        if (Test-Path (Join-Path $root 'Assets\Docs.meta')) { Remove-Item -LiteralPath (Join-Path $root 'Assets\Docs.meta') -Force }
        Write-Host "[Screenshot] 已归档到 Docs/evidence"
    }

    CB @{ Action = 'exec'; Command = 'editor_stop' } | Out-Null
    Wait-PlayMode 'stopped' | Out-Null
    Start-Sleep -Seconds 2
}

if ($ok) { Write-Host 'RESULT: PASS'; exit 0 }
Write-Host 'RESULT: FAIL'; exit 2
