# Demo3d · 小型 3D 建造交互 Demo（Unity AI 招聘笔试作品）

一块地面、四个固定建造位（A–D）、一种建筑 Prefab；鼠标点格子选中，UI 显示金币与选中格状态，提供建造 / 拆除 / 重新开始三个按钮。

**当前状态**

| 项目 | 结果 |
|---|---|
| 统一验收清单 V1–V8 | ✅ 全部 PASS（真实场景 + HUD，逐步截图归档） |
| EditMode 单元测试 | ✅ 13 / 13 |
| 流程探针（EditMode / PlayMode / HUD） | ✅ 29 / 29 / 30 全 PASS |
| 工程基线自检 | ✅ 19 项 PASS / 0 失败（另 1 条是 Editor 占用信息项） |
| 一键复跑 | ✅ 四个脚本全部 exit 0（最近一次完整验证见文末「验证记录」） |

---

## 1. 环境

| 项 | 值 |
|---|---|
| Unity | **6000.3.18f1**（Unity 6.3 LTS） |
| 渲染管线 | URP 17.3.0（Universal 3D 模板） |
| 输入 | **仅新输入系统**（`activeInputHandler=1`，Input System 1.19.0）→ 禁止旧 `Input` API |
| 测试 | com.unity.test-framework 1.6.0 |
| Editor 内桥 | com.unity.pipeline 0.5.0-exp.1（HTTP `127.0.0.1:7800`） |

> 工程用了官方 *Unity Pipeline* 包把 Editor 暴露成 HTTP 服务，脚本因此能在**不关闭 Editor、不用 batchmode** 的情况下建场景、跑测试、切 Play Mode、截图。细节见 [Docs/AIBridge通道.md](Docs/AIBridge通道.md)。

## 2. 直接体验

1. Unity Hub 打开本工程（`6000.3.18f1`）；
2. 打开场景 `Assets/Scenes/SampleScene.unity`；
3. 点 **Play**；
4. 操作：**点地面上的 A/B/C/D 格子选中** → 点 **建造**（-60）/ **拆除**（+30）/ **重新开始**（回初始金币）。

界面：左上显示金币、建筑数、选中格状态；底部状态行显示每次操作的结果（失败为橙色）。

> 场景是**可重建**的。若层级被弄乱，执行菜单 `Demo3d/重建场景(T2+T3)`，或跑第 3 节的一键脚本。

## 3. 一键复跑

**前置**：Unity Editor 正在打开本工程（桥接服务随 Editor 启动）。

```powershell
# ① 工程基线自检（19 项 PASS + 1 条信息项，退出码=结论）
& Tools\baseline_check.ps1

# ② EditMode 单元测试（V1–V8 规则断言，落存证）
& Tools\run_tests.ps1

# ③ 流程探针：EditMode + PlayMode + HUD + 截图（29/29/30）
& Tools\probe_buildflow.ps1

# ④ V1–V8 全流程验收：逐步截图 + 生成报告
& Tools\run_acceptance.ps1

# ⑤（可选）把文档导出成 Word
python Tools\md2docx.py Docs\提交信息表.md Docs\提交信息表.docx
python Tools\md2docx.py Docs\提示词记录.md Docs\提示词记录.docx
```

全都退出码 `0` 即代表通过。产物固定落到 `Docs/evidence/`：

| 脚本 | 产物 |
|---|---|
| `run_tests.ps1` | `Docs/evidence/editmode_tests.json` |
| `probe_buildflow.ps1` | `t3_buildflow_probe.txt`、`t3_buildflow_probe_playmode.txt`、`t4_hud_probe.txt`、`t3_built_playmode.png`、`t4_hud_normal.png` |
| `run_acceptance.ps1` | `V1V8_acceptance_report.md` + `acceptance/V1…V8.png` × 8 |

### 卡住时的排查顺序

1. **脚本改动后 Unity 没反应**：Unity 只在窗口获得焦点时做资源刷新。把 Unity 窗口点一下（或按 `Ctrl+R`）。
2. **`/api/exec` 一直超时但 `/api/status` 正常**：说明 Editor 的命令队列被某条命令占住了（通常是测试跑挂）。解法：改一个 `.cs` 文件并让 Unity 重新编译（域重载会把队列重置），或用菜单 `Assets/Refresh`。`run_tests.ps1` 已改为**异步提交 + 轮询** `test_status`，正常情况下不会再占住队列。
3. **`editor_status` 显示 `paused`**：Console 开了 Error Pause，异常会把播放停住。脚本已把 `paused` 视作「在播放」并自动恢复；手动恢复是点 Play 面板的暂停键。
4. **场景被弄乱**：菜单 `Demo3d/重建场景(T2+T3)`，或 `probe_buildflow.ps1` 会自动重建。

## 4. 项目结构

```
Assets/
├─ Scripts/                        ← 运行时程序集 Demo3d.Runtime（纯逻辑与表现分离）
│  ├─ EconomyModel.cs              金币模型：初始值/建造费/扣费/返还 30（常量）/重置/重配
│  ├─ BuildGameModel.cs            占用位 + 选中格 + 结果枚举（Build/DemolishOutcome）
│  ├─ BuildSlot.cs                 一个格子：uGUI 点击回调、选中/占用配色（MPB）
│  ├─ BuildController.cs           ★ 主控：把模型接到场景（派生式建筑实例、拆除、重开、文案）
│  └─ HUDController.cs             HUD：显示金币/建筑数/选中态，按钮转发给控制器
├─ Editor/                         ← 编辑器程序集 Demo3d.Editor
│  └─ Demo3dSceneBuilder.cs        ★ 幂等场景生成器（地面/4 格/相机/EventSystem/HUD/Prefab）
├─ Diagnostics/                    ← 自检程序集 Demo3d.Diagnostics
│  ├─ BuildFlowProbe.cs            29 项流程自检（模型占用数 vs 场景实例数）
│  ├─ HudProbe.cs                  30 项界面自检（含「按按钮不穿透」）
│  └─ AcceptanceRunner.cs          V1–V8 全流程驱动，每步返回 `编号|PASS|期望|实际`
├─ Tests/EditMode/                 ← EditMode 测试程序集
│  └─ V1_V8_AcceptanceTests.cs     13 条断言（V1–V8 + 5 条边界）
├─ Scenes/SampleScene.unity        主场景（打开后点 Play 即可体验）
├─ Prefabs/Building.prefab         建筑 Prefab（轴心在底部，顶面尺寸 1.3×1.1×1.3）
└─ Materials/{Slot,Building}.mat   格子/建筑材质

Tools/                             ← 全部验证脚本（PowerShell，走 AIBridge）
├─ unity_bridge.ps1                AIBridge 客户端（Bearer 鉴权、UTF-8 请求体）
├─ baseline_check.ps1              20 项工程基线自检
├─ check_unity_log.ps1             编译门禁：抓 Editor.log 的 error CS
├─ run_tests.ps1                   跑 EditMode 测试并落存证
├─ probe_buildflow.ps1             流程 + HUD 探针（含「测试后场景重载」守卫）
└─ run_acceptance.ps1              V1–V8 全流程验收 + 截图归档 + 报告

Docs/                              ← 过程文档与证据
├─ 需求分析与任务拆解.md            需求还原、规则值、V1–V8 映射、T0–T9 拆解
├─ T0…T5_*.md                      各阶段实现说明与验证记录
├─ 提示词记录.md                    ★ 提交要求：逐轮提示词与关键过程（含真实返工）
├─ 提示词记录.docx                  ↑ 的 Word 版（可直接提交）
├─ 提交信息表.md / .docx            提交前要填的信息汇总（A 表填完即可交）
├─ 进度看板.md                      T0–T9 状态与可用命令
├─ AIBridge通道.md                  Editor 桥接的端点与命令清单
├─ exam_requirements_raw.txt        PDF 原文提取留档
└─ evidence/                       所有可复查的证据（截图/JSON/报告）
```

## 5. 规则与实现位置（错一个数就掉分）

| 规则 | 值 | 代码位置 |
|---|---|---|
| 初始金币 | 180（默认）/ 120（测试配置） | `EconomyModel.InitialCoins`，`BuildController.m_InitialCoins`（Inspector 可改） |
| 单次建造费用 | 60 / 40 | `BuildController.m_BuildCost`（Inspector 可改） |
| 拆除返还 | **固定 30**，与建造费无关 | `EconomyModel.DemolishRefund`（常量） |
| 可建造 | 已选中 + 格子空 + 金币够 | `BuildGameModel.TryBuild` |
| 连点不重复扣费 | 同步幂等：占用检查 → 置位 → 扣费，中间无 await/协程 | 同上，见 [Docs/T1_EconomyModel.md](Docs/T1_EconomyModel.md) |
| 拆除幂等 | 先判占用，空格/重复拆除不加钱 | `BuildGameModel.TryDemolish` |
| 重开 | 清占用、清选中、金币回**当前配置**值 | `BuildController.Restart` |
| UI 不穿透 | Canvas `GraphicRaycaster` + 按钮 Image `raycastTarget` + 相机 `PhysicsRaycaster` | `Demo3dSceneBuilder.CreateHud` / `BuildSlot` |

## 6. 验收对照（证据可点开）

| 编号 | 期望 | 证据 |
|---|---|---|
| V1 | 1 建筑 / 120 | [acceptance/V1_建A.png](Docs/evidence/acceptance/V1_建A.png) |
| V2 | 连点仍 1 / 120 | [V2_连点A.png](Docs/evidence/acceptance/V2_连点A.png) |
| V3 | 3 建筑 / 0，D 不建 | [V3_余额0.png](Docs/evidence/acceptance/V3_余额0.png) |
| V4 | 30 → 仍 30 | [V4_拆除A.png](Docs/evidence/acceptance/V4_拆除A.png) |
| V5 | 重开 180 → 建后 120 | [V5_重开再建.png](Docs/evidence/acceptance/V5_重开再建.png) |
| V6 | 连重开一致 → 1/120 | [V6_连重开.png](Docs/evidence/acceptance/V6_连重开.png) |
| V7 | 120/40：3 次到 0，第 4 失败，拆除 30/30 | [V7_测试配置120_40.png](Docs/evidence/acceptance/V7_测试配置120_40.png) |
| V8 | 180→120→150→90 | [V8_默认序列.png](Docs/evidence/acceptance/V8_默认序列.png) |

汇总报告：[Docs/evidence/V1V8_acceptance_report.md](Docs/evidence/V1V8_acceptance_report.md)｜说明：[Docs/T5_验收.md](Docs/T5_验收.md)

## 7. 关键设计决策

1. **规则只有一个来源**：金币、占用、选中全部住在 `BuildGameModel`/`EconomyModel`；控制器只转发，HUD 只显示，`BuildSlot` 只管长相。避免"两处都能改钱"。
2. **建筑实例由占用状态派生**（`SyncVisuals` 里幂等 `EnsureBuilding`/`RemoveBuilding`），而不是在建造方法里直接 Instantiate → 连点、重开、拆除自动保持一致。
3. **点击走 uGUI 事件系统**（`PhysicsRaycaster` + `IPointerClickHandler`），不手搓 `Physics.Raycast` → UI 天然优先消费指针事件，"点 UI 不触发场景点击"是结构保证。
4. **场景可重建 + 验证可脚本化**：`Demo3dSceneBuilder` 幂等生成场景，所有验证都能一条命令复跑并留证据，不依赖人工点。

## 8. 已知限制 / 未完成

- **真实鼠标点击链路的模拟输入验证**未做：目前验证到"事件能到达 `BuildSlot`/按钮"与"UI 不给场景吃穿"的结构层面（`Docs/T4_HUD.md`、`Docs/T5_验收.md`）。
- **录屏 / 可体验链接**（题目二选一）未产出：工程未安装 WebGL 模块，本机无 ffmpeg；已具备逐张截图证据，录屏需另做。
- 视觉为功能优先（方块/圆柱级），题目说明"视觉精致度不单独加分"。
- 未做移动端/存档/联网/对象池（题目明确不需要）。

## 9. 文档索引

| 文档 | 内容 |
|---|---|
| [Docs/需求分析与任务拆解.md](Docs/需求分析与任务拆解.md) | 需求还原、V1–V8 映射、T0–T9 任务拆解、风险预案 |
| [Docs/T0_工程基线.md](Docs/T0_工程基线.md) | 工程事实核对（版本/管线/输入/资产）、基线约束 |
| [Docs/T1_EconomyModel.md](Docs/T1_EconomyModel.md) | 经济模型、连点保护原理、13 条断言 |
| [Docs/T2_场景与格子.md](Docs/T2_场景与格子.md) | 场景布局、`BuildSlot`、点击链路验证 |
| [Docs/T3_控制器.md](Docs/T3_控制器.md) | 主控职责边界、派生式实例、29 项自检 |
| [Docs/T4_HUD.md](Docs/T4_HUD.md) | HUD 结构、按钮派生、不穿透的三层保证、30 项自检 |
| [Docs/T5_验收.md](Docs/T5_验收.md) | V1–V8 全流程结果与工具链坑位 |
| [Docs/提示词记录.md](Docs/提示词记录.md) | **提交用**：8 轮真实提示词、上下文、处理与证据（含两次真实返工） |
| [Docs/进度看板.md](Docs/进度看板.md) | 任务状态总览 + 常用命令 |

## 10. 提交前待补

- 姓名、AI 工具与模型名称、实际投入时间、录屏链接 —— **统一填在 [Docs/提交信息表.md](Docs/提交信息表.md) 的 A 表**（其中 ⬜ 格需你补）；
- 最终录屏或可体验链接（若选录屏路线，可按 `Docs/evidence/acceptance/` 的 V1–V8 顺序演示）。

## 11. 验证记录（最近一次完整复跑）

```
① Tools\baseline_check.ps1   → RESULT: 19 项通过, 0 项失败           exit=0
② Tools\run_tests.ps1        → Total=13 Passed=13 Failed=0           exit=0
③ Tools\probe_buildflow.ps1  → EditMode PASS / PlayMode PASS / HUD PASS → RESULT: PASS   exit=0
④ Tools\run_acceptance.ps1   → V1–V8 全部 PASS → RESULT: PASS        exit=0
```

环境：Unity 6000.3.18f1 + URP 17.3.0，Editor 保持打开（AIBridge 在线），未使用 batchmode。
