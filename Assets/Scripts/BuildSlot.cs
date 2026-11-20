using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Demo3d
{
    /// <summary>
    /// 一个可建造格子。只负责「自己长什么样」和「被点了通知谁」，
    /// 不持有金币、不判断能否建造——那些都在 <see cref="BuildGameModel"/> 里。
    /// <para>
    /// 点击走 uGUI 的事件系统（相机上的 <c>PhysicsRaycaster</c>），而不是自己 Update 里
    /// 手搓 <c>Physics.Raycast</c>：这样 UI 按钮天然优先消费指针事件，
    /// 点按钮不会穿透到场景（题目「点击 UI 按钮只执行该按钮的操作」）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");  // URP Lit
        private static readonly int ColorId = Shader.PropertyToID("_Color");          // Built-in fallback

        [Header("身份")]
        [SerializeField] private int m_Index = -1;
        [SerializeField] private string m_Label = "?";

        [Header("引用（留空则运行时自动查找子物体）")]
        [SerializeField] private Renderer m_Body;
        [SerializeField] private Transform m_BuildingAnchor;

        [Header("配色")]
        [SerializeField] private Color m_IdleColor = new Color(0.48f, 0.50f, 0.55f, 1f);
        [SerializeField] private Color m_HoverColor = new Color(0.62f, 0.66f, 0.74f, 1f);
        [SerializeField] private Color m_SelectedColor = new Color(0.95f, 0.76f, 0.30f, 1f);
        [SerializeField] private Color m_OccupiedColor = new Color(0.30f, 0.49f, 0.94f, 1f);

        private MaterialPropertyBlock m_Block;

        /// <summary>格子被左键点击。控制器订阅它来改选中态。</summary>
        public event Action<BuildSlot> Clicked;

        public int Index => m_Index;

        public string Label => m_Label;

        public bool IsOccupied { get; private set; }

        public bool IsSelected { get; private set; }

        public bool IsHovered { get; private set; }

        /// <summary>建筑的挂载点。建筑实例以它为父节点，位置归零即为格子中心。</summary>
        public Transform BuildingAnchor => m_BuildingAnchor != null ? m_BuildingAnchor : transform;

        /// <summary>供场景构建脚本/预制体初始化用。</summary>
        public void Configure(int index, string label, Renderer body, Transform buildingAnchor)
        {
            m_Index = index;
            m_Label = label;
            m_Body = body;
            m_BuildingAnchor = buildingAnchor;
            Repaint();
        }

        public void SetSelected(bool selected)
        {
            if (IsSelected == selected)
            {
                return;
            }

            IsSelected = selected;
            Repaint();
        }

        public void SetOccupied(bool occupied)
        {
            if (IsOccupied == occupied)
            {
                return;
            }

            IsOccupied = occupied;
            Repaint();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
            Repaint();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            Repaint();
        }

        private void Awake()
        {
            if (m_Body == null)
            {
                m_Body = GetComponentInChildren<Renderer>();
            }

            if (m_BuildingAnchor == null)
            {
                m_BuildingAnchor = transform;
            }
        }

        private void Start()
        {
            Repaint();
        }

#if UNITY_EDITOR
        /// <summary>编辑期改参数/加载场景时也刷新配色，免得 Scene 视图里看到的和运行期不一致。</summary>
        private void OnValidate()
        {
            if (m_Body == null)
            {
                m_Body = GetComponentInChildren<Renderer>();
            }

            Repaint();
        }
#endif

        private void Repaint()
        {
            var body = m_Body != null ? m_Body : GetComponentInChildren<Renderer>();
            if (body == null)
            {
                return;
            }

            Color color;
            if (IsSelected)
            {
                color = m_SelectedColor;
            }
            else if (IsOccupied)
            {
                color = m_OccupiedColor;
            }
            else
            {
                color = IsHovered ? m_HoverColor : m_IdleColor;
            }

            m_Block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(m_Block);
            m_Block.SetColor(BaseColorId, color);
            m_Block.SetColor(ColorId, color);
            body.SetPropertyBlock(m_Block);
        }

#if UNITY_EDITOR
        /// <summary>在 Scene 视图里画出格子编号，方便右键检查。</summary>
        private void OnDrawGizmos()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"{m_Label} ({m_Index})");
        }
#endif
    }
}
