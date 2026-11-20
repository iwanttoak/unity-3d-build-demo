using System.Collections.Generic;
using Demo3d;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Demo3d.EditorTools
{
    /// <summary>
    /// T2 场景构建器：地面 + A–D 四个建造格 + 相机 + EventSystem（新输入系统）。
    /// 幂等：重复执行会先清掉上次生成的 <c>BuildDemo</c> 与 <c>EventSystem</c>，再重建。
    /// 菜单：Demo3d/重建场景(T2)；命令行：eval <c>return Demo3d.EditorTools.Demo3dSceneBuilder.BuildScene();</c>
    /// </summary>
    public static class Demo3dSceneBuilder
    {
        private const string RootName = "BuildDemo";
        private const string EventSystemName = "EventSystem";

        private static readonly string[] SlotLabels = { "A", "B", "C", "D" };

        // 四个格子横向排开，方便演示 V1–V8 的连续操作
        private static readonly float[] SlotX = { -4.5f, -1.5f, 1.5f, 4.5f };

        [MenuItem("Demo3d/重建场景(T2)")]
        public static void BuildSceneFromMenu()
        {
            BuildScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Demo3d] 场景已重建并保存。");
        }

        public static string BuildScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // 1) 清理旧的生成物
            DestroyIfExists(RootName);
            DestroyIfExists(EventSystemName);

            // 2) 地面
            var root = new GameObject(RootName);
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", root.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f); // 14 x 14 单位
            Tint(ground.GetComponent<Renderer>(), new Color(0.16f, 0.18f, 0.21f, 1f));

            // 3) 四个建造格
            var slotsRoot = new GameObject("Slots");
            slotsRoot.transform.SetParent(root.transform, false);

            for (var i = 0; i < SlotLabels.Length; i++)
            {
                CreateSlot(slotsRoot.transform, i, SlotLabels[i], SlotX[i]);
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

            // 相机位置定了，再把格子标签转向相机（否则文字会倒着或不朝向镜头）
            FaceLabelsToCamera(camera);

            // 5) EventSystem + 新输入系统的 UI 模块（旧 StandaloneInputModule 在 activeInputHandler=1 下会抛异常）
            var eventSystemGo = new GameObject(EventSystemName);
            eventSystemGo.AddComponent<EventSystem>();
            var uiModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Demo3d] 场景构建完成：Ground + 4 个建造格 + 相机 + EventSystem。");
            return "built";
        }

        private static void CreateSlot(Transform parent, int index, string label, float x)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Slot {label}";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0.1f, 0f);
            go.transform.localScale = new Vector3(2.2f, 0.2f, 2.2f);

            // 建筑挂载点放在格子顶面
            var anchor = new GameObject("BuildingAnchor");
            anchor.transform.SetParent(go.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var body = go.GetComponent<Renderer>();

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
            var lines = new List<string>();
            foreach (var slot in slots)
            {
                lines.Add($"#{slot.Index} {slot.Label} @ {slot.transform.position} occupied={slot.IsOccupied} selected={slot.IsSelected}");
            }

            return string.Join(" | ", lines);
        }
    }
}
