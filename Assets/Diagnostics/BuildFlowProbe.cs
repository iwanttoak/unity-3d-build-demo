using System.Text;
using UnityEngine;

namespace Demo3d.Diagnostics
{
    /// <summary>
    /// T3 建造/拆除流程自检：直接驱动 <see cref="BuildController"/>，逐条核对规则并返回 PASS/FAIL 报告。
    /// 不依赖 Play Mode、不依赖鼠标，用于在 Editor 里一次性验证「模型 → 场景」这条链路。
    /// 调用：eval <c>return Demo3d.Diagnostics.BuildFlowProbe.Run();</c>
    /// </summary>
    public static class BuildFlowProbe
    {
        private static StringBuilder s_Report;
        private static int s_Fails;

        public static string Run()
        {
            var controller = Object.FindFirstObjectByType<BuildController>();
            if (controller == null)
            {
                return "FAIL: 场景里没有 BuildController（先跑 Demo3dSceneBuilder.BuildScene）";
            }

            s_Report = new StringBuilder();
            s_Fails = 0;

            // 0) 归零到默认配置
            controller.Restart();
            Check("Restart 后金币 180", controller.Coins == 180, $"coins={controller.Coins}");
            Check("Restart 后无建筑", controller.BuildingCount == 0, $"buildings={controller.BuildingCount}");
            Check("Restart 后无选中", !controller.HasSelection, $"selected={controller.SelectedLabel}");

            // 1) 未选中直接建造 → 拒绝，不扣费
            controller.Build();
            Check("未选中建造不扣费", controller.Coins == 180, $"coins={controller.Coins}");
            Check("未选中建造无建筑", controller.BuildingCount == 0, $"buildings={controller.BuildingCount}");
            Check("未选中建造有提示", controller.StatusMessage.Contains("请先"), controller.StatusMessage);

            // 2) 选 A 建造 → 120 / 1（V1）
            controller.SelectSlot(0);
            Check("选中 A 后标记已选中", controller.SelectedLabel == "A", controller.SelectedLabel);
            controller.Build();
            Check("A 建造后金币 120", controller.Coins == 120, $"coins={controller.Coins}");
            Check("A 建造后建筑数 1", controller.BuildingCount == 1, $"buildings={controller.BuildingCount}");
            Check("A 建造后场景实例 1", CountBuildingObjects() == 1, $"instances={CountBuildingObjects()}");

            // 3) 连点建造 → 不重复扣费、不叠建筑（V2）
            for (var i = 0; i < 5; i++)
            {
                controller.Build();
            }

            Check("连点 5 次后金币仍 120", controller.Coins == 120, $"coins={controller.Coins}");
            Check("连点 5 次后建筑数仍 1", controller.BuildingCount == 1, $"buildings={controller.BuildingCount}");
            Check("连点 5 次后场景实例仍 1", CountBuildingObjects() == 1, $"instances={CountBuildingObjects()}");
            Check("连点被拒有提示", controller.StatusMessage.Contains("已有建筑"), controller.StatusMessage);

            // 4) 在 D 建造 → 60 / 2
            controller.SelectSlot(3);
            controller.Build();
            Check("D 建造后金币 60", controller.Coins == 60, $"coins={controller.Coins}");
            Check("D 建造后建筑数 2", controller.BuildingCount == 2, $"buildings={controller.BuildingCount}");

            // 5) 拆除 D → 返还固定 30（V4 前半）
            controller.Demolish();
            Check("拆除 D 后金币 90", controller.Coins == 90, $"coins={controller.Coins}");
            Check("拆除 D 后建筑数 1", controller.BuildingCount == 1, $"buildings={controller.BuildingCount}");
            Check("拆除 D 后场景实例 1", CountBuildingObjects() == 1, $"instances={CountBuildingObjects()}");

            // 6) 重复拆除 → 不加钱（V4 后半）
            controller.Demolish();
            Check("重复拆除后金币仍 90", controller.Coins == 90, $"coins={controller.Coins}");
            Check("重复拆除有提示", controller.StatusMessage.Contains("空地"), controller.StatusMessage);

            // 7) 在 B 建造 → 30；再在 C 建造因金币不足失败（V3 的机制）
            controller.SelectSlot(1);
            controller.Build();
            Check("B 建造后金币 30", controller.Coins == 30, $"coins={controller.Coins}");
            controller.SelectSlot(2);
            controller.Build();
            Check("金币不足建造后仍 30", controller.Coins == 30, $"coins={controller.Coins}");
            Check("金币不足建造无建筑增加", controller.BuildingCount == 2, $"buildings={controller.BuildingCount}");
            Check("金币不足有提示", controller.StatusMessage.Contains("金币不足"), controller.StatusMessage);

            // 8) 重开 → 全清、金币回默认（V5）
            controller.Restart();
            Check("重开后金币 180", controller.Coins == 180, $"coins={controller.Coins}");
            Check("重开后建筑数 0", controller.BuildingCount == 0, $"buildings={controller.BuildingCount}");
            Check("重开后场景实例 0", CountBuildingObjects() == 0, $"instances={CountBuildingObjects()}");
            Check("重开后无选中", !controller.HasSelection, $"selected={controller.SelectedLabel}");

            s_Report.AppendLine(s_Fails == 0 ? "RESULT: PASS" : $"RESULT: FAIL ({s_Fails})");
            return s_Report.ToString();
        }

        private static int CountBuildingObjects()
        {
            var count = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                // 只数「还活着且在场景里可见」的实例：Play Mode 下 Destroy 到帧末才生效，
                // 控制器会先 SetActive(false)，用它来做同步可见性判断。
                if (t.name.StartsWith("Building ") && t.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (!ok)
            {
                s_Fails++;
            }

            s_Report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}  [{detail}]");
        }
    }
}
