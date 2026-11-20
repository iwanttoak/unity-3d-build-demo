using UnityEngine;
using UnityEngine.UI;

namespace Demo3d
{
    /// <summary>
    /// T4 的 HUD：只做「显示」和「把按钮点击转给控制器」。
    /// 它不做任何规则判断——金币够不够、格子空不空全由 <see cref="BuildController"/> 决定，
    /// 按钮的可用状态也是从控制器**派生**出来的（不自己算）。
    /// <para>
    /// 防穿透：Canvas 上挂 <c>GraphicRaycaster</c>，按钮的 Image 保持 raycastTarget，
    /// 指针事件被 UI 先消费，就不会再落到 3D 格子上——这是题目「点 UI 按钮不额外触发场景点击」的实现方式。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HUDController : MonoBehaviour
    {
        [Header("数据源")]
        [SerializeField] private BuildController m_Controller;

        [Header("文本")]
        [SerializeField] private Text m_CoinsText;
        [SerializeField] private Text m_BuildingText;
        [SerializeField] private Text m_SelectionText;
        [SerializeField] private Text m_StatusText;

        [Header("按钮")]
        [SerializeField] private Button m_BuildButton;
        [SerializeField] private Button m_DemolishButton;
        [SerializeField] private Button m_RestartButton;

        [Header("文案配色")]
        [SerializeField] private Color m_StatusNormalColor = new Color(0.86f, 0.89f, 0.93f, 1f);
        [SerializeField] private Color m_StatusErrorColor = new Color(1f, 0.78f, 0.30f, 1f);

        private bool m_Wired;

        public BuildController Controller => m_Controller;

        /// <summary>供场景构建脚本一次把引用塞进来。</summary>
        public void Configure(
            BuildController controller,
            Text coinsText,
            Text buildingText,
            Text selectionText,
            Text statusText,
            Button buildButton,
            Button demolishButton,
            Button restartButton)
        {
            m_Controller = controller;
            m_CoinsText = coinsText;
            m_BuildingText = buildingText;
            m_SelectionText = selectionText;
            m_StatusText = statusText;
            m_BuildButton = buildButton;
            m_DemolishButton = demolishButton;
            m_RestartButton = restartButton;
            Wire();
            Refresh();
        }

        private void Awake()
        {
            Wire();
            Refresh();
        }

        private void OnDestroy()
        {
            if (!m_Wired || m_Controller == null)
            {
                return;
            }

            m_Controller.StateChanged -= Refresh;
            m_BuildButton?.onClick.RemoveListener(OnBuildClicked);
            m_DemolishButton?.onClick.RemoveListener(OnDemolishClicked);
            m_RestartButton?.onClick.RemoveListener(OnRestartClicked);
        }

        private void Wire()
        {
            if (m_Wired || m_Controller == null)
            {
                return;
            }

            m_Wired = true;
            m_Controller.StateChanged += Refresh;

            if (m_BuildButton != null)
            {
                m_BuildButton.onClick.RemoveListener(OnBuildClicked);
                m_BuildButton.onClick.AddListener(OnBuildClicked);
            }

            if (m_DemolishButton != null)
            {
                m_DemolishButton.onClick.RemoveListener(OnDemolishClicked);
                m_DemolishButton.onClick.AddListener(OnDemolishClicked);
            }

            if (m_RestartButton != null)
            {
                m_RestartButton.onClick.RemoveListener(OnRestartClicked);
                m_RestartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        /// <summary>把控制器状态整块搬到 UI 上。只在状态变化时被调用，不做每帧轮询。</summary>
        public void Refresh()
        {
            if (m_Controller == null)
            {
                return;
            }

            if (m_CoinsText != null)
            {
                m_CoinsText.text = $"金币：{m_Controller.Coins}";
            }

            if (m_BuildingText != null)
            {
                m_BuildingText.text = $"建筑：{m_Controller.BuildingCount} / {m_Controller.SlotCount}";
            }

            if (m_SelectionText != null)
            {
                m_SelectionText.text = $"选中：{m_Controller.SelectionText}";
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = string.IsNullOrEmpty(m_Controller.StatusMessage) ? "点击格子进行选择" : m_Controller.StatusMessage;
                m_StatusText.color = m_Controller.LastOperationFailed ? m_StatusErrorColor : m_StatusNormalColor;
            }

            // 按钮可用状态由控制器派生，HUD 不自己判断规则
            if (m_BuildButton != null)
            {
                m_BuildButton.interactable = m_Controller.CanBuildSelected;
            }

            if (m_DemolishButton != null)
            {
                m_DemolishButton.interactable = m_Controller.CanDemolishSelected;
            }

            if (m_RestartButton != null)
            {
                m_RestartButton.interactable = true;
            }
        }

        // 按钮回调：只调用控制器，不直接改金币/格子（避免第二份真值）
        public void OnBuildClicked()
        {
            m_Controller?.Build();
        }

        public void OnDemolishClicked()
        {
            m_Controller?.Demolish();
        }

        public void OnRestartClicked()
        {
            m_Controller?.Restart();
        }
    }
}
