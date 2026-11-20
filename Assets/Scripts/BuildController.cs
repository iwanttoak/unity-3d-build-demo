using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo3d
{
    /// <summary>
    /// T3 主控：把纯 C# 的 <see cref="BuildGameModel"/> 接到场景上。
    /// 职责边界：模型管规则（金币、占用、选中），本类只做「把操作转成模型调用」与「把模型状态同步到场景/UI」。
    /// UI 按钮（T4）只需要调用 <see cref="Build"/> / <see cref="Demolish"/> / <see cref="Restart"/>
    /// 并订阅 <see cref="StateChanged"/>，不直接碰金币与格子。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildController : MonoBehaviour
    {
        [Header("配置（Inspector 可改；重新开始/重新运行按当前值生效）")]
        [SerializeField, Min(0)] private int m_InitialCoins = BuildGameModel.DefaultInitialCoins;
        [SerializeField, Min(1)] private int m_BuildCost = BuildGameModel.DefaultBuildCost;

        [Header("引用")]
        [Tooltip("四个建造格（按 Index 匹配，顺序可任意）")]
        [SerializeField] private BuildSlot[] m_Slots = Array.Empty<BuildSlot>();

        [Tooltip("建筑 Prefab：轴心在底部中心，挂到格子顶面的 BuildingAnchor 上")]
        [SerializeField] private GameObject m_BuildingPrefab;

        private readonly Dictionary<int, GameObject> m_BuiltObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, BuildSlot> m_SlotByIndex = new Dictionary<int, BuildSlot>();
        private BuildGameModel m_Model;

        /// <summary>金币/选中/占用/文案任一变化时触发，UI 据此整体刷新。</summary>
        public event Action StateChanged;

        public BuildGameModel Model
        {
            get
            {
                EnsureInitialized();
                return m_Model;
            }
        }

        /// <summary>一次操作的反馈文案（成功或失败原因），供 HUD 显示。</summary>
        public string StatusMessage { get; private set; } = string.Empty;

        public int Coins
        {
            get
            {
                EnsureInitialized();
                return m_Model.Coins;
            }
        }

        public int InitialCoins
        {
            get
            {
                EnsureInitialized();
                return m_Model.InitialCoins;
            }
        }

        public int BuildCost
        {
            get
            {
                EnsureInitialized();
                return m_Model.BuildCost;
            }
        }

        public int BuildingCount
        {
            get
            {
                EnsureInitialized();
                return m_Model.BuildingCount;
            }
        }

        public bool HasSelection
        {
            get
            {
                EnsureInitialized();
                return m_Model.HasSelection;
            }
        }

        public int SelectedIndex
        {
            get
            {
                EnsureInitialized();
                return m_Model.SelectedIndex;
            }
        }

        public string SelectedLabel
        {
            get
            {
                var slot = GetSlot(SelectedIndex);
                return slot != null ? slot.Label : "无";
            }
        }

        public bool IsSelectedSlotOccupied
        {
            get
            {
                EnsureInitialized();
                return m_Model.IsOccupied(SelectedIndex);
            }
        }

        /// <summary>HUD 用的选中格描述，例如「C（已建造）」。</summary>
        public string SelectionText
        {
            get
            {
                if (!HasSelection)
                {
                    return "未选择";
                }

                return IsSelectedSlotOccupied ? $"{SelectedLabel}（已建造）" : $"{SelectedLabel}（空地）";
            }
        }

        public bool CanBuildSelected => HasSelection && !IsSelectedSlotOccupied && Coins >= BuildCost;

        public bool CanDemolishSelected => HasSelection && IsSelectedSlotOccupied;

        // ---------------------------------------------------------------- 生命周期
        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 懒初始化：EditMode 下（脚本/工具/自动化）Awake 不会执行，
        /// 而运行时也要防「读状态早于 Awake」。所以模型与订阅统一在这里建，入口全都先调它。
        /// </summary>
        private bool EnsureInitialized()
        {
            if (m_Model != null)
            {
                return true;
            }

            var slotCount = Mathf.Max(1, m_Slots != null ? m_Slots.Length : 1);
            m_Model = new BuildGameModel(slotCount, Mathf.Max(0, m_InitialCoins), Mathf.Max(1, m_BuildCost));
            m_Model.Changed += SyncVisuals;

            if (m_Slots == null)
            {
                return true;
            }

            foreach (var slot in m_Slots)
            {
                if (slot == null || m_SlotByIndex.ContainsKey(slot.Index))
                {
                    continue;
                }

                m_SlotByIndex.Add(slot.Index, slot);
                slot.Clicked += HandleSlotClick;
            }

            SyncVisuals();
            return true;
        }

        private void OnDestroy()
        {
            foreach (var slot in m_Slots)
            {
                if (slot != null)
                {
                    slot.Clicked -= HandleSlotClick;
                }
            }

            if (m_Model != null)
            {
                m_Model.Changed -= SyncVisuals;
            }
        }

        // ---------------------------------------------------------------- 输入入口
        /// <summary>格子被点击：切换选中。点击同一个格子不取消选中（题目未要求取消，保持选中更利于连续操作）。</summary>
        public void HandleSlotClick(BuildSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            EnsureInitialized();

            StatusMessage = slot.Index == m_Model.SelectedIndex
                ? $"格子 {slot.Label} 已选中"
                : $"已选中格子 {slot.Label}";
            m_Model.Select(slot.Index);
            Notify(); // Select 内部已触发 SyncVisuals，这里只补一次 UI 刷新（含新文案）
        }

        /// <summary>供自动化/演示脚本按 Index 选中。</summary>
        public bool SelectSlot(int index)
        {
            var slot = GetSlot(index);
            if (slot == null)
            {
                return false;
            }

            HandleSlotClick(slot);
            return true;
        }

        // ---------------------------------------------------------------- 三个按钮
        public void Build()
        {
            EnsureInitialized();
            var outcome = m_Model.TryBuildSelected();
            StatusMessage = Describe(outcome);
            Notify();
        }

        public void Demolish()
        {
            EnsureInitialized();
            var outcome = m_Model.TryDemolishSelected();
            StatusMessage = Describe(outcome);
            Notify();
        }

        /// <summary>
        /// 重新开始：清建筑、清占用、取消选中，金币回到**当前 Inspector 配置**的初始值。
        /// 运行期改过 Inspector 再点重开，按新值生效（题目 V7/V8 的前提）。
        /// </summary>
        public void Restart()
        {
            EnsureInitialized();
            m_Model.Restart(Mathf.Max(0, m_InitialCoins), Mathf.Max(1, m_BuildCost));
            StatusMessage = "已重新开始";
            Notify();
        }

        // ---------------------------------------------------------------- 视觉同步
        private void SyncVisuals()
        {
            if (m_Slots == null || m_Model == null)
            {
                return;
            }

            for (var i = 0; i < m_Slots.Length; i++)
            {
                var slot = m_Slots[i];
                if (slot == null)
                {
                    continue;
                }

                var occupied = m_Model.IsOccupied(slot.Index);
                slot.SetSelected(slot.Index == m_Model.SelectedIndex);
                slot.SetOccupied(occupied);

                if (occupied)
                {
                    EnsureBuilding(slot);
                }
                else
                {
                    RemoveBuilding(slot.Index);
                }
            }
        }

        private void EnsureBuilding(BuildSlot slot)
        {
            if (m_BuiltObjects.ContainsKey(slot.Index))
            {
                return;
            }

            if (m_BuildingPrefab == null)
            {
                Debug.LogError($"[Demo3d] 格子 {slot.Label} 需要建筑，但 BuildController 没有配 Building Prefab。", this);
                return;
            }

            var instance = Instantiate(m_BuildingPrefab, slot.BuildingAnchor);
            instance.name = $"Building {slot.Label}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            m_BuiltObjects.Add(slot.Index, instance);
        }

        private void RemoveBuilding(int slotIndex)
        {
            if (!m_BuiltObjects.TryGetValue(slotIndex, out var instance))
            {
                return;
            }

            m_BuiltObjects.Remove(slotIndex);
            if (instance == null)
            {
                return;
            }

            // Play Mode 下 Destroy 要到帧末才真正执行；先停用，保证这一刻起
            // 视觉与碰撞体都立刻消失，不会出现「已拆除但还能点到/看到」的一帧。
            instance.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private BuildSlot GetSlot(int index)
        {
            return m_SlotByIndex.TryGetValue(index, out var slot) ? slot : null;
        }

        private void Notify()
        {
            StateChanged?.Invoke();
        }

        private string Describe(BuildOutcome outcome)
        {
            switch (outcome)
            {
                case BuildOutcome.Success:
                    return $"在格子 {SelectedLabel} 建造完成，-{BuildCost}";
                case BuildOutcome.NoSelection:
                    return "请先点击一个格子";
                case BuildOutcome.SlotOccupied:
                    return $"格子 {SelectedLabel} 已有建筑";
                case BuildOutcome.NotEnoughCoins:
                    return $"金币不足（需要 {BuildCost}，当前 {Coins}）";
                default:
                    return "无效的格子";
            }
        }

        private string Describe(DemolishOutcome outcome)
        {
            switch (outcome)
            {
                case DemolishOutcome.Success:
                    return $"拆除格子 {SelectedLabel}，+{EconomyModel.DemolishRefund}";
                case DemolishOutcome.NoSelection:
                    return "请先点击一个格子";
                case DemolishOutcome.SlotEmpty:
                    return $"格子 {SelectedLabel} 是空地，无法拆除";
                default:
                    return "无效的格子";
            }
        }
    }
}
