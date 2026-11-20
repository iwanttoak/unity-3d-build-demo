# T0 基线自检：验证工程事实与目录/程序集骨架是否就位
# 用法: pwsh -File Tools\baseline_check.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$results = New-Object System.Collections.Generic.List[object]

function Check([string]$name, [scriptblock]$test, [string]$expect) {
    try {
        $actual = & $test
        $ok = [bool]$actual
    } catch {
        $actual = "EXC: $($_.Exception.Message)"
        $ok = $false
    }
    $script:results.Add([pscustomobject]@{
        Item   = $name
        Expect = $expect
        Actual = if ($actual -is [string]) { $actual } else { "ok" }
        Status = if ($ok) { 'PASS' } else { 'FAIL' }
    })
    return $ok
}

# --- 1. 工程版本与管线 ---
Check 'Unity 版本 6000.3.18f1' {
    (Get-Content "$root\ProjectSettings\ProjectVersion.txt" -Raw) -match 'm_EditorVersion: 6000\.3\.18f1'
} 'ProjectVersion.txt 命中 6000.3.18f1' | Out-Null

Check 'URP 渲染管线已绑定 (GraphicsSettings)' {
    (Get-Content "$root\ProjectSettings\GraphicsSettings.asset" -Raw) -match 'm_CustomRenderPipeline: \{fileID: 11400000'
} 'm_CustomRenderPipeline 指向 PC_RPAsset' | Out-Null

Check 'URP 包 17.3.0 已安装' {
    (Get-Content "$root\Packages\manifest.json" -Raw) -match '"com\.unity\.render\-pipelines\.universal": "17\.3\.0"'
} 'manifest 含 URP 17.3.0' | Out-Null

Check '仅启用新输入系统 (activeInputHandler=1)' {
    (Get-Content "$root\ProjectSettings\ProjectSettings.asset" -Raw) -match 'activeInputHandler: 1'
} 'activeInputHandler: 1（禁止写旧 Input API）' | Out-Null

Check 'SampleScene 已加入 Build Settings' {
    (Get-Content "$root\ProjectSettings\EditorBuildSettings.asset" -Raw) -match 'Assets/Scenes/SampleScene\.unity'
} 'EditorBuildSettings 含 SampleScene' | Out-Null

# --- 2. 目录骨架 ---
foreach ($d in 'Assets\Scripts', 'Assets\Prefabs', 'Assets\Materials', 'Assets\Scenes', 'Assets\Tests\EditMode', 'Assets\Diagnostics', 'Docs\evidence', 'Tools') {
    Check "目录存在 $d" { Test-Path (Join-Path $root $d) } '目录存在' | Out-Null
}

# --- 3. 程序集定义 ---
foreach ($a in 'Assets\Scripts\Demo3d.Runtime.asmdef', 'Assets\Diagnostics\Demo3d.Diagnostics.asmdef', 'Assets\Tests\EditMode\Demo3d.Tests.EditMode.asmdef') {
    Check "asmdef 存在 $a" { Test-Path (Join-Path $root $a) } 'asmdef 存在' | Out-Null
}

Check 'Runtime asmdef 引用 UnityEngine.UI + Unity.InputSystem' {
    $j = Get-Content "$root\Assets\Scripts\Demo3d.Runtime.asmdef" -Raw | ConvertFrom-Json
    ($j.references -contains 'UnityEngine.UI') -and ($j.references -contains 'Unity.InputSystem')
} 'references 含 UI 与 InputSystem' | Out-Null

Check 'Tests asmdef 引用 Runtime 且限定 Editor' {
    $j = Get-Content "$root\Assets\Tests\EditMode\Demo3d.Tests.EditMode.asmdef" -Raw | ConvertFrom-Json
    ($j.references -contains 'Demo3d.Runtime') -and ($j.includePlatforms -contains 'Editor') -and ($j.precompiledReferences -contains 'nunit.framework.dll')
} 'references/includePlatforms/precompiledReferences 正确' | Out-Null

# --- 4. 运行环境 ---
Check 'Unity Editor 可执行文件存在' {
    Test-Path 'D:\study\Unity\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe'
} 'Unity.exe 存在' | Out-Null

$editorProc = Get-Process Unity -ErrorAction SilentlyContinue
$results.Add([pscustomobject]@{
    Item = 'Editor 是否正在占用工程'
    Expect = '信息项：占用中则 batchmode 需先关闭'
    Actual = if ($editorProc) { "运行中 ($($editorProc.Count) 进程)" } else { '未运行' }
    Status = 'INFO'
})

$results | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
$fail = @($results | Where-Object Status -eq 'FAIL').Count
Write-Host ("RESULT: {0} 项通过, {1} 项失败" -f @($results | Where-Object Status -eq 'PASS').Count, $fail)
exit ([int]($fail -gt 0))
