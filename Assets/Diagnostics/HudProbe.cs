using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo3d.Diagnostics
{
    /// <summary>
    /// T4 HUD 自检：验证「按钮 → 控制器 → 模型 → 界面」这条链路，以及按钮点击不会穿透到场景格子。
    /// 需要在 Play Mode 下跑（UI 事件系统与 Awake 才完整）。
    /// 调用：eval <c>return Demo3d.Diagnostics.HudProbe.Run();</c>
    /// </summary>
    public static class HudProbe
    {
        private static StringBuilder s_Report;
        private static int s_Fails;

        public static string Run()
        {
            var hud = Object.FindFirstObjectByType<HUDController>();
            if (hud == null)
            {
                return "FAIL: 场景里没有 HUDController（先跑 Demo3dSceneBuilder.BuildScene）";
            }

            var controller = hud.Controller;
            if (controller == null)
            {
                return "FAIL: HUD 没有绑定 BuildController";
            }

            s_Report = new StringBuilder();
            s_Fails = 0;

            // ---------- 1. 结构：UI 必须真的能挡住射线 ----------
            var canvas = hud.GetComponent<Canvas>();
            Check("Canvas 存在且为 Overlay", canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay,
                canvas != null ? canvas.renderMode.ToString() : "null");
            Check("Canvas 挂 GraphicRaycaster", hud.GetComponent<GraphicRaycaster>() != null, "");
            Check("EventSystem 用的是 InputSystemUIInputModule",
                EventSystem.current != null && EventSystem.current.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule,
                EventSystem.current != null && EventSystem.current.currentInputModule != null
                    ? EventSystem.current.currentInputModule.GetType().Name
                    : "null");

            var buildButton = FindButton("BuildButton");
            var demolishButton = FindButton("DemolishButton");
            var restartButton = FindButton("RestartButton");
            Check("三个按钮都能找到", buildButton != null && demolishButton != null && restartButton != null, "");

            if (buildButton == null || demolishButton == null || restartButton == null)
            {
                return Finish();
            }

            var buildImage = buildButton.GetComponent<Image>();
            Check("建造按钮 Image raycastTarget=true（挡得住穿透）", buildImage != null && buildImage.raycastTarget, "");
            Check("按钮都在 Canvas 下（同一 GraphicRaycaster 管）",
                buildButton.transform.IsChildOf(hud.transform), "");

            // ---------- 2. 初始状态：未选中 → 两个操作按钮都不可用 ----------
            controller.Restart();
            Check("重开后建造按钮不可用", !buildButton.interactable, $"interactable={buildButton.interactable}");
            Check("重开后拆除按钮不可用", !demolishButton.interactable, $"interactable={demolishButton.interactable}");
            Check("重开后金币显示 180", ReadText("CoinsText").Contains("180"), ReadText("CoinsText"));

            // ---------- 3. 选空格子 → 建造按钮可用 ----------
            controller.SelectSlot(0);
            Check("选空格子后建造按钮可用", buildButton.interactable, $"interactable={buildButton.interactable}");
            Check("选空格子后拆除按钮不可用", !demolishButton.interactable, $"interactable={demolishButton.interactable}");
            Check("选中格显示为 A（空地）", ReadText("SelectionText").Contains("A（空地）"), ReadText("SelectionText"));

            // ---------- 4. 按「建造」按钮 → 走完整链路 ----------
            var selectedBefore = controller.SelectedIndex;
            Click(buildButton);
            Check("按建造后金币 120", controller.Coins == 120, $"coins={controller.Coins}");
            Check("按建造后建筑数 1", controller.BuildingCount == 1, $"buildings={controller.BuildingCount}");
            Check("按建造后金币文本同步", ReadText("CoinsText").Contains("120"), ReadText("CoinsText"));
            Check("按建造后建筑数文本同步", ReadText("BuildingText").Contains("1 / 4"), ReadText("BuildingText"));
            Check("按建造按钮没有改变选中格（未穿透到场景）", controller.SelectedIndex == selectedBefore,
                $"before={selectedBefore} after={controller.SelectedIndex}");
            Check("按建造后有成功提示", ReadText("StatusText").Contains("建造完成"), ReadText("StatusText"));
            Check("占用后建造按钮转不可用", !buildButton.interactable, $"interactable={buildButton.interactable}");
            Check("占用后拆除按钮转可用", demolishButton.interactable, $"interactable={demolishButton.interactable}");

            // ---------- 5. 按「拆除」按钮 → 返还 30 ----------
            Click(demolishButton);
            Check("按拆除后金币 150", controller.Coins == 150, $"coins={controller.Coins}");
            Check("按拆除后建筑数 0", controller.BuildingCount == 0, $"buildings={controller.BuildingCount}");
            Check("按拆除后有返还提示", ReadText("StatusText").Contains("+30"), ReadText("StatusText"));

            // ---------- 6. 失败路径：文案要变橙（LastOperationFailed） ----------
            controller.SelectSlot(1);
            Click(buildButton);   // B 建造 → 90
            controller.Build();   // 强制造一次失败（B 已占用），验证配色分支
            Check("失败后 LastOperationFailed=true", controller.LastOperationFailed, $"failed={controller.LastOperationFailed}");
            var statusText = FindText("StatusText");
            var expected = new Color(1f, 0.78f, 0.30f, 1f);
            Check("失败文案变橙色", statusText != null && ColorsClose(statusText.color, expected),
                statusText != null ? statusText.color.ToString() : "null");

            // ---------- 7. 按「重新开始」按钮 → 回初始 ----------
            Click(restartButton);
            Check("按重开后金币 180", controller.Coins == 180, $"coins={controller.Coins}");
            Check("按重开后建筑数 0", controller.BuildingCount == 0, $"buildings={controller.BuildingCount}");
            Check("按重开后无选中", !controller.HasSelection, controller.SelectionText);
            Check("按重开后金币文本回 180", ReadText("CoinsText").Contains("180"), ReadText("CoinsText"));
            Check("按重开后状态文案为普通色", statusText != null && !ColorsClose(statusText.color, expected), "");

            return Finish();
        }

        private static string Finish()
        {
            s_Report.AppendLine(s_Fails == 0 ? "RESULT: PASS" : $"RESULT: FAIL ({s_Fails})");
            return s_Report.ToString();
        }

        private static Button FindButton(string name)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            return null;
        }

        private static Text FindText(string name)
        {
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (text.name == name)
                {
                    return text;
                }
            }

            return null;
        }

        private static string ReadText(string name)
        {
            var text = FindText(name);
            return text != null ? text.text : "<missing>";
        }

        /// <summary>用 uGUI 的事件派发点击按钮，等价于「鼠标点在按钮上」。</summary>
        private static void Click(Button button)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static bool ColorsClose(Color a, Color b, float tolerance = 0.02f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance
                   && Mathf.Abs(a.g - b.g) < tolerance
                   && Mathf.Abs(a.b - b.b) < tolerance;
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
