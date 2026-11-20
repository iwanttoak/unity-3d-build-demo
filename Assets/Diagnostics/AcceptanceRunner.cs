using UnityEngine;

namespace Demo3d.Diagnostics
{
    /// <summary>
    /// 统一验收清单 V1–V8 的**全流程**驱动：走真实场景（BuildController + HUD + 实例化建筑），
    /// 每一步返回 <c>编号|PASS/FAIL|期望|实际</c> 一行，供脚本逐条截图归档。
    /// 与 EditMode 的 <c>V1_V8_AcceptanceTests</c> 互补：那边验规则，这边验「接上场景与界面之后仍然对」。
    /// 调用（Play Mode）：eval <c>return Demo3d.Diagnostics.AcceptanceRunner.RunStep("V1");</c>
    /// </summary>
    public static class AcceptanceRunner
    {
        private static BuildController Controller
        {
            get
            {
                var c = Object.FindFirstObjectByType<BuildController>();
                if (c == null)
                {
                    throw new System.InvalidOperationException("场景里没有 BuildController");
                }

                return c;
            }
        }

        /// <summary>把 Inspector 上的配置改成指定值（模拟考生在 Inspector 里改 initialCoins / buildCost）。</summary>
        public static string ApplyConfig(int initialCoins, int buildCost)
        {
            var controller = Controller;
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(controller);
            so.FindProperty("m_InitialCoins").intValue = initialCoins;
            so.FindProperty("m_BuildCost").intValue = buildCost;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(controller);
#else
            throw new System.InvalidOperationException("ApplyConfig 只能在 Editor 内使用");
#endif
            // 注意：这里**不**调用 Restart —— 改配置本身不应立刻改变当前金币，
            // 题目要求的是「重新运行或重新开始后按新配置生效」。
            return $"config-set initialCoins={initialCoins} buildCost={buildCost} coins-now={controller.Coins}";
        }

        /// <summary>按当前 Inspector 配置重开（等价于点「重新开始」）。</summary>
        public static string RestartNow()
        {
            var controller = Controller;
            controller.Restart();
            return $"restarted coins={controller.Coins} buildCost={controller.BuildCost} buildings={controller.BuildingCount}";
        }

        public static string RunStep(string id)
        {
            var controller = Controller;

            switch (id)
            {
                case "V1":
                {
                    // 默认配置，从 180 金币开始，在格子 A 建造 → 只有 1 个建筑，金币 120
                    controller.Restart();
                    controller.SelectSlot(0);
                    controller.Build();
                    return Line("V1", controller.BuildingCount == 1 && controller.Coins == 120,
                        "1 个建筑、金币 120",
                        $"buildings={controller.BuildingCount} coins={controller.Coins}");
                }

                case "V2":
                {
                    // 保留 V1 状态，对格子 A 连续快速点击建造 → 仍 1 个建筑、仍 120
                    for (var i = 0; i < 8; i++)
                    {
                        controller.Build();
                    }

                    return Line("V2", controller.BuildingCount == 1 && controller.Coins == 120,
                        "仍 1 个建筑、金币仍 120",
                        $"buildings={controller.BuildingCount} coins={controller.Coins} msg={controller.StatusMessage}");
                }

                case "V3":
                {
                    // 在 B、C 各建一次，再尝试在空格 D 建造 → 3 个建筑、余额 0、D 不建造
                    controller.SelectSlot(1);
                    controller.Build();
                    controller.SelectSlot(2);
                    controller.Build();
                    controller.SelectSlot(3);
                    controller.Build();
                    return Line("V3",
                        controller.BuildingCount == 3 && controller.Coins == 0 && !controller.Model.IsOccupied(3),
                        "3 个建筑、余额 0、D 不建造",
                        $"buildings={controller.BuildingCount} coins={controller.Coins} D occupied={controller.Model.IsOccupied(3)} msg={controller.StatusMessage}");
                }

                case "V4":
                {
                    // 拆除 A，再次点击该空格的拆除 → 第一次 30，第二次仍 30
                    controller.SelectSlot(0);
                    controller.Demolish();
                    var afterFirst = controller.Coins;
                    controller.Demolish();
                    var afterSecond = controller.Coins;
                    return Line("V4",
                        afterFirst == 30 && afterSecond == 30 && !controller.Model.IsOccupied(0),
                        "第一次 30、第二次仍 30",
                        $"first={afterFirst} second={afterSecond} msg={controller.StatusMessage}");
                }

                case "V5":
                {
                    // 重新开始，再选择 A 建造 → 重开无建筑/无选择/180；随后建成 120
                    controller.Restart();
                    var resetOk = controller.BuildingCount == 0 && !controller.HasSelection && controller.Coins == 180;
                    controller.SelectSlot(0);
                    controller.Build();
                    return Line("V5",
                        resetOk && controller.BuildingCount == 1 && controller.Coins == 120,
                        "重开 0 建筑/无选择/180；再建后 1 建筑 120",
                        $"resetOk={resetOk} buildings={controller.BuildingCount} coins={controller.Coins}");
                }

                case "V6":
                {
                    // 连续重新开始 2 次，再完成一次建造 → 每次状态一致；最终 1 建筑 120
                    var consistent = true;
                    for (var i = 0; i < 2; i++)
                    {
                        controller.Restart();
                        consistent &= controller.BuildingCount == 0 && !controller.HasSelection && controller.Coins == 180;
                    }

                    controller.SelectSlot(0);
                    controller.Build();
                    return Line("V6",
                        consistent && controller.BuildingCount == 1 && controller.Coins == 120,
                        "两次重开状态一致；最终 1 建筑 120",
                        $"consistent={consistent} buildings={controller.BuildingCount} coins={controller.Coins}");
                }

                case "V7":
                {
                    // 前提：已用 120/40 配置启动新一轮（由脚本先 ApplyConfig + RestartNow）
                    var configOk = controller.Coins == 120 && controller.BuildCost == 40;
                    controller.SelectSlot(0);
                    controller.Build();
                    controller.SelectSlot(1);
                    controller.Build();
                    controller.SelectSlot(2);
                    controller.Build();
                    var afterThree = controller.Coins;
                    controller.SelectSlot(3);
                    controller.Build();               // 第 4 次：金币不足
                    var fourthFailed = controller.Coins == 0 && controller.BuildingCount == 3;
                    controller.SelectSlot(2);
                    controller.Demolish();            // 有效拆除 → 30
                    var afterDemolish = controller.Coins;
                    controller.Demolish();            // 重复拆除 → 仍 30
                    var afterRepeat = controller.Coins;
                    return Line("V7",
                        configOk && afterThree == 0 && fourthFailed && afterDemolish == 30 && afterRepeat == 30,
                        "3 次后余额 0、第 4 次失败、拆除 30、重复拆除仍 30",
                        $"configOk={configOk} afterThree={afterThree} fourthFailed={fourthFailed} demolish={afterDemolish}/{afterRepeat}");
                }

                case "V8":
                {
                    // 前提：已恢复默认配置并重新开始 → 180
                    var start = controller.Coins;
                    controller.SelectSlot(0);
                    controller.Build();               // 120
                    var c1 = controller.Coins;
                    controller.Demolish();            // 150
                    var c2 = controller.Coins;
                    controller.Build();               // 90
                    var c3 = controller.Coins;
                    return Line("V8",
                        start == 180 && c1 == 120 && c2 == 150 && c3 == 90 && controller.BuildingCount == 1,
                        "金币依次 180、120、150、90；A 最终 1 个建筑",
                        $"coins={start},{c1},{c2},{c3} buildings={controller.BuildingCount}");
                }

                default:
                    return $"UNKNOWN|FAIL|未知编号|{id}";
            }
        }

        private static string Line(string id, bool ok, string expected, string actual)
        {
            return $"{id}|{(ok ? "PASS" : "FAIL")}|{expected}|{actual}";
        }
    }
}
