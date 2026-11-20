using System;

namespace Demo3d
{
    /// <summary>
    /// 纯 C# 金币模型，不依赖 UnityEngine，便于在 EditMode 测试里直接断言规则。
    /// 依据笔试卷「默认规则」：初始 180 / 单次建造 60 / 单次有效拆除返还固定 30；
    /// 「参数验证配置」：初始 120 / 建造 40 / 拆除仍返还 30。
    /// </summary>
    public sealed class EconomyModel
    {
        /// <summary>
        /// 拆除返还固定 30：题目明确「与建造费用是否调整无关」，因此写成常量，
        /// 既不受 Inspector 配置影响，也不参与 V7 的 40 元建造费用。
        /// </summary>
        public const int DemolishRefund = 30;

        private int m_InitialCoins;
        private int m_BuildCost;

        public EconomyModel(int initialCoins, int buildCost)
        {
            ValidateConfig(initialCoins, buildCost);
            m_InitialCoins = initialCoins;
            m_BuildCost = buildCost;
            Coins = initialCoins;
        }

        /// <summary>当前配置的初始金币（重开时恢复到它）。</summary>
        public int InitialCoins => m_InitialCoins;

        /// <summary>当前配置的单次建造费用。</summary>
        public int BuildCost => m_BuildCost;

        /// <summary>当前金币。</summary>
        public int Coins { get; private set; }

        /// <summary>金币是否足够建造一次。</summary>
        public bool CanAffordBuild => Coins >= m_BuildCost;

        /// <summary>
        /// 扣一次建造费。调用方必须先判断 <see cref="CanAffordBuild"/>，
        /// 这里再兜一层，保证「金币不足不扣费」。
        /// </summary>
        public bool TrySpendBuild()
        {
            if (!CanAffordBuild)
            {
                return false;
            }

            Coins -= m_BuildCost;
            return true;
        }

        /// <summary>返还一次拆除费用（固定 30）。调用方保证只对「真正拆掉了建筑」的情况调用。</summary>
        public void RefundDemolish()
        {
            Coins += DemolishRefund;
        }

        /// <summary>金币恢复到当前配置的初始值。</summary>
        public void Reset()
        {
            Coins = m_InitialCoins;
        }

        /// <summary>
        /// 更新配置并立即重置。对应题目「改 Inspector 配置后，重新运行或重新开始，应按新配置生效」：
        /// 「重新开始」走这里，就能用上运行期改过的值。
        /// </summary>
        public void Reconfigure(int initialCoins, int buildCost)
        {
            ValidateConfig(initialCoins, buildCost);
            m_InitialCoins = initialCoins;
            m_BuildCost = buildCost;
            Reset();
        }

        private static void ValidateConfig(int initialCoins, int buildCost)
        {
            if (initialCoins < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCoins), initialCoins, "初始金币不能为负数。");
            }

            if (buildCost < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(buildCost), buildCost, "建造费用按正整数处理（题目明确不考察负数费用）。");
            }
        }
    }
}

