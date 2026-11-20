using System.Collections.Generic;
using System.Text;
using Demo3d;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Demo3d.EditorTools
{
    /// <summary>
    /// T2/T3 场景构建器：地面 + A–D 四格 + 相机 + EventSystem + BuildController。
    /// 幂等：重复执行会先清掉上次生成的 <c>BuildDemo</c> / <c>EventSystem</c>，再重建。
    /// 菜单：Demo3d/重建场景(T2+T3)；命令行：eval <c>return Demo3d.EditorTools.Demo3dSceneBuilder.BuildScene();</c>
    /// </summary>
    public static class Demo3dSceneBuilder
    {
        private const string RootName = "BuildDemo";
        private const string EventSystemName = "EventSystem";
        private const string GameName = "Game";
        private const string HudName = "HUD";
        private const string BuildingPrefabPath = "Assets/Prefabs/Building.prefab";
        private const string SlotMaterialPath = "Assets/Materials/Slot.mat";
        private const string BuildingMaterialPath = "Assets/Materials/Building.mat";

        private static readonly string[] SlotLabels = { "A", "B", "C", "D" };

        // 四个格子横向排开，方便演示 V1–V8 的连续操作
        private static readonly float[] SlotX = { -4.5f, -1.5f, 1.5f, 4.5f };

        [MenuItem("Demo3d/重建场景(T2+T3)")]
        public static void BuildSceneFromMenu()
        {
            BuildScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Demo3d] 场景已重建并保存。");
        }

        public static string BuildScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // 0) 素材与 Prefab（缺就建）
            var slotMaterial = EnsureMaterial(SlotMaterialPath, new Color(0.48f, 0.50f, 0.55f, 1f));
            var buildingMaterial = EnsureMaterial(BuildingMaterialPath, new Color(0.35f, 0.62f, 0.72f, 1f));
            var buildingPrefab = EnsureBuildingPrefab(buildingMaterial);

            // 1) 清理旧的生成物
            DestroyIfExists(RootName);
            DestroyIfExists(EventSystemName);
            DestroyIfExists(GameName);
            DestroyIfExists(HudName);

            // 2) 地面
            var root = new GameObject(RootName);
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", root.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f); // 14 x 14 单位
            Tint(ground.GetComponent<Renderer>(), new Color(0.16f, 0.18f, 0.21f, 1f));

            // 3) 四个建造格
            var slotsRoot = new GameObject("Slots");
            slotsRoot.transform.SetParent(root.transform, false);

            var slots = new List<BuildSlot>(SlotLabels.Length);
            for (var i = 0; i < SlotLabels.Length; i++)
            {
                slots.Add(CreateSlot(slotsRoot.transform, i, SlotLabels[i], SlotX[i], slotMaterial));
            }

            // 4) 相机：斜俯视，能同时看到四个格子与上方的 UI 区域
            var camera = Camera.main;
            if (camera != null)
            {
                var camTransform = camera.transform;
                camTransform.position = new Vector3(0f, 6.2f, -8f);
                camTransform.LookAt(new Vector3(0f, 0.3f, 0f));
                camera.fieldOfView = 50f;
                camera.nearClipPlane = 0.1f;

                // 关键：场景点击靠 PhysicsRaycaster 投递到 IPointerClickHandler
                if (camera.GetComponent<PhysicsRaycaster>() == null)
                {
                    camera.gameObject.AddComponent<PhysicsRaycaster>();
                }
            }
            else
            {
                Debug.LogWarning("[Demo3d] 场景里没有 Main Camera，跳过相机设置。");
            }

            // 相机位置定了，再把格子标签摆成与屏幕平行
            FaceLabelsToCamera(camera);

            // 5) EventSystem + 新输入系统的 UI 模块
            var eventSystemGo = new GameObject(EventSystemName);
            eventSystemGo.AddComponent<EventSystem>();
            var uiModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();

            // 6) 主控：把模型接到场景
            var gameGo = new GameObject(GameName);
            var controller = gameGo.AddComponent<BuildController>();
            var so = new SerializedObject(controller);
            var slotsProp = so.FindProperty("m_Slots");
            slotsProp.arraySize = slots.Count;
            for (var i = 0; i < slots.Count; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }

            so.FindProperty("m_BuildingPrefab").objectReferenceValue = buildingPrefab;
            so.FindProperty("m_InitialCoins").intValue = BuildGameModel.DefaultInitialCoins;
            so.FindProperty("m_BuildCost").intValue = BuildGameModel.DefaultBuildCost;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 7) HUD：金币/建筑数/选中状态 + 三个按钮
            CreateHud(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Demo3d] 场景构建完成：Ground + 4 格 + 相机 + EventSystem + BuildController + HUD。");
            return "built";
        }

        // ---------------------------------------------------------------- HUD
        private static HUDController CreateHud(BuildController controller)
        {
            var canvasGo = new GameObject(HudName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 左上信息面板
            var panel = CreatePanel(canvasGo.transform, "InfoPanel", new Vector2(24f, -24f), new Vector2(360f, 172f));

            var coinsText = CreateText(panel.transform, "CoinsText", "金币：180",
                new Vector2(16f, -14f), new Vector2(328f, 42f), 30, TextAnchor.MiddleLeft, font);
            var buildingText = CreateText(panel.transform, "BuildingText", "建筑：0 / 4",
                new Vector2(16f, -60f), new Vector2(328f, 32f), 24, TextAnchor.MiddleLeft, font);
            var selectionText = CreateText(panel.transform, "SelectionText", "选中：未选择",
                new Vector2(16f, -96f), new Vector2(328f, 32f), 24, TextAnchor.MiddleLeft, font);
            var hintText = CreateText(panel.transform, "HintText", "点格子选择 · 点按钮建造/拆除",
                new Vector2(16f, -128f), new Vector2(328f, 30f), 18, TextAnchor.MiddleLeft, font);
            hintText.color = new Color(0.70f, 0.74f, 0.80f, 1f);

            // 底部操作区
            var statusText = CreateText(canvasGo.transform, "StatusText", "点击格子进行选择",
                new Vector2(0f, 132f), new Vector2(920f, 40f), 24, TextAnchor.MiddleCenter, font, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var buildButton = CreateButton(canvasGo.transform, "BuildButton", "建造",
                new Vector2(-240f, 52f), new Color(0.20f, 0.47f, 0.78f, 1f), font);
            var demolishButton = CreateButton(canvasGo.transform, "DemolishButton", "拆除",
                new Vector2(0f, 52f), new Color(0.62f, 0.36f, 0.22f, 1f), font);
            var restartButton = CreateButton(canvasGo.transform, "RestartButton", "重新开始",
                new Vector2(240f, 52f), new Color(0.24f, 0.44f, 0.40f, 1f), font);

            var hud = canvasGo.AddComponent<HUDController>();
            hud.Configure(controller, coinsText, buildingText, selectionText, statusText,
                buildButton, demolishButton, restartButton);
            return hud;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.55f);
            image.raycastTarget = true; // 面板区域也算 UI，不把点击漏给场景
            return go;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Font font,
            Vector2? anchor = null,
            Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var a = anchor ?? new Vector2(0f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = a;
            rt.anchorMax = a;
            rt.pivot = pivot ?? new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; // 文本不吃射线，点击交给按钮的 Image
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Color baseColor, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(200f, 64f);

            var image = go.GetComponent<Image>();
            image.color = baseColor;
            image.raycastTarget = true; // 关键：UI 吃掉指针事件，不穿透到场景

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelText = CreateText(go.transform, "Label", label,
                Vector2.zero, new Vector2(200f, 64f), 28, TextAnchor.MiddleCenter, font,
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            var labelRt = labelText.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            labelText.color = Color.white;

            return button;
        }

        private static BuildSlot CreateSlot(Transform parent, int index, string label, float x, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Slot {label}";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0.1f, 0f);
            go.transform.localScale = new Vector3(2.2f, 0.2f, 2.2f);

            var body = go.GetComponent<Renderer>();
            if (material != null)
            {
                body.sharedMaterial = material;
            }

            // 建筑挂载点放在格子顶面
            var anchor = new GameObject("BuildingAnchor");
            anchor.transform.SetParent(go.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            // 格子标签（TextMesh 无碰撞体，不挡射线）
            var labelGo = new GameObject($"Label {label}");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = 72;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            var slot = go.AddComponent<BuildSlot>();
            slot.Configure(index, label, body, anchor.transform);
            return slot;
        }

        /// <summary>建筑 Prefab：轴心在底部中心，方形体，便于挂到格子顶面。</summary>
        private static GameObject EnsureBuildingPrefab(Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("Building");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(1.3f, 1.1f, 1.3f);
            if (material != null)
            {
                body.GetComponent<Renderer>().sharedMaterial = material;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Demo3d] 找不到 URP Lit，退回默认材质。");
                return null;
            }

            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>把所有 TextMesh 标签摆成与屏幕平行（朝向与相机一致），文字才不会歪。</summary>
        private static void FaceLabelsToCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                text.transform.rotation = camera.transform.rotation;
            }
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Tint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(Shader.PropertyToID("_BaseColor"), color);
            block.SetColor(Shader.PropertyToID("_Color"), color);
            renderer.SetPropertyBlock(block);
        }

        private static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            while (existing != null)
            {
                Object.DestroyImmediate(existing);
                existing = GameObject.Find(name);
            }
        }

        /// <summary>把场景里所有 BuildSlot 收集出来（调试/取证用）。</summary>
        public static string DescribeSlots()
        {
            var slots = Object.FindObjectsByType<BuildSlot>(FindObjectsSortMode.None);
            var sb = new StringBuilder();
            foreach (var slot in slots)
            {
                sb.Append($"#{slot.Index} {slot.Label} occupied={slot.IsOccupied} selected={slot.IsSelected}  ");
            }

            return sb.ToString();
        }
    }
}
