using System;

namespace Demo3d
{
    /// <summary>建造请求的结果。UI 依赖它给出「为什么没建成」的明确反馈。</summary>
    public enum BuildOutcome
    {
        Success = 0,
        NoSelection = 1,
        SlotOccupied = 2,
        NotEnoughCoins = 3,
        InvalidSlot = 4,
    }

    /// <summary>拆除请求的结果。</summary>
    public enum DemolishOutcome
    {
        Success = 0,
        NoSelection = 1,
        SlotEmpty = 2,
        InvalidSlot = 3,
    }

    /// <summary>
    /// 建造板模型：格子占用 + 当前选中格 + 金币，全部集中在这里。
    /// 表现层（MonoBehaviour / UI）只读它的状态、只调它的方法，不各自维护一份状态，
    /// 这是「UI 金币与格子状态及时一致」的结构性保证。
    /// 纯 C#、无 UnityEngine 依赖，V1–V8 可在 EditMode 里逐条断言。
    /// </summary>
    public sealed class BuildGameModel
    {
        public const int DefaultSlotCount = 4;
        public const int DefaultInitialCoins = 180;
        public const int DefaultBuildCost = 60;
        public const int NoSelectionIndex = -1;

        private readonly bool[] m_Occupied;
        private int m_SelectedIndex = NoSelectionIndex;

        public BuildGameModel(
            int slotCount = DefaultSlotCount,
            int initialCoins = DefaultInitialCoins,
            int buildCost = DefaultBuildCost)
        {
            if (slotCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "格子数量至少为 1。");
            }

            m_Occupied = new bool[slotCount];
            Economy = new EconomyModel(initialCoins, buildCost);
        }

        /// <summary>状态发生任何变化时触发，UI 据此刷新。</summary>
        public event Action Changed;

        public EconomyModel Economy { get; }

        public int SlotCount => m_Occupied.Length;

        public int Coins => Economy.Coins;

        public int BuildCost => Economy.BuildCost;

        public int InitialCoins => Economy.InitialCoins;

        /// <summary>当前选中的格子下标；未选中为 <see cref="NoSelectionIndex"/>。</summary>
        public int SelectedIndex => m_SelectedIndex;

        public bool HasSelection => IsValidIndex(m_SelectedIndex);

        /// <summary>已建造数量（= 被占用的格子数）。</summary>
        public int BuildingCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < m_Occupied.Length; i++)
                {
                    if (m_Occupied[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < m_Occupied.Length;
        }

        public bool IsOccupied(int index)
        {
            return IsValidIndex(index) && m_Occupied[index];
        }

        /// <summary>选中一个格子。下标非法则返回 false 且不改动选中态。</summary>
        public bool Select(int index)
        {
            if (!IsValidIndex(index))
            {
                return false;
            }

            m_SelectedIndex = index;
            RaiseChanged();
            return true;
        }

        /// <summary>取消选中（重新开始会用到）。</summary>
        public void ClearSelection()
        {
            m_SelectedIndex = NoSelectionIndex;
            RaiseChanged();
        }

        /// <summary>对「当前选中格」建造。未选中 → <see cref="BuildOutcome.NoSelection"/>。</summary>
        public BuildOutcome TryBuildSelected()
        {
            if (!HasSelection)
            {
                return BuildOutcome.NoSelection;
            }

            return TryBuild(m_SelectedIndex);
        }

        /// <summary>
        /// 对指定格子建造。三个前置条件缺一不可：下标有效、格子为空、金币足够。
        /// <para>
        /// 连点保护：占用检查与置位在**同一个同步调用内**完成，中间没有协程、没有下一帧、
        /// 没有 await，所以「金币仍够时连续快速点已占用格子的建造」第二次起必然命中
        /// <see cref="BuildOutcome.SlotOccupied"/>，不会重复扣费、不会生成重叠建筑。
        /// </para>
        /// <para>置位在扣费之前：任何异常都不会出现「扣了钱却没建」的中间态。</para>
        /// </summary>
        public BuildOutcome TryBuild(int index)
        {
            if (!IsValidIndex(index))
            {
                return BuildOutcome.InvalidSlot;
            }

            if (m_Occupied[index])
            {
                return BuildOutcome.SlotOccupied;
            }

            if (!Economy.CanAffordBuild)
            {
                return BuildOutcome.NotEnoughCoins;
            }

            m_Occupied[index] = true;
            Economy.TrySpendBuild();
            RaiseChanged();
            return BuildOutcome.Success;
        }

        /// <summary>拆除「当前选中格」的建筑。未选中 → <see cref="DemolishOutcome.NoSelection"/>。</summary>
        public DemolishOutcome TryDemolishSelected()
        {
            if (!HasSelection)
            {
                return DemolishOutcome.NoSelection;
            }

            return TryDemolish(m_SelectedIndex);
        }

        /// <summary>
        /// 拆除指定格子的建筑，清占用并返还固定 30。
        /// 空格 / 重复拆除 → <see cref="DemolishOutcome.SlotEmpty"/>，**不加钱**（V4 的第二次点击）。
        /// </summary>
        public DemolishOutcome TryDemolish(int index)
        {
            if (!IsValidIndex(index))
            {
                return DemolishOutcome.InvalidSlot;
            }

            if (!m_Occupied[index])
            {
                return DemolishOutcome.SlotEmpty;
            }

            m_Occupied[index] = false;
            Economy.RefundDemolish();
            RaiseChanged();
            return DemolishOutcome.Success;
        }

        /// <summary>
        /// 重新开始：清空建筑与占用、取消选中、金币回到**当前配置**的初始值。
        /// 传入配置是为了让「运行期改过 Inspector 后再重开」也能按新值生效；
        /// 不传则沿用构造时的配置。
        /// </summary>
        public void Restart(int initialCoins, int buildCost)
        {
            for (var i = 0; i < m_Occupied.Length; i++)
            {
                m_Occupied[i] = false;
            }

            m_SelectedIndex = NoSelectionIndex;
            Economy.Reconfigure(initialCoins, buildCost);
            RaiseChanged();
        }

        public void Restart()
        {
            Restart(Economy.InitialCoins, Economy.BuildCost);
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
