using Demo3d;
using NUnit.Framework;

namespace Demo3d.Tests
{
    /// <summary>
    /// 笔试卷「统一验收清单」V1–V8 的逐条断言。
    /// 全部针对纯 C# 的 <see cref="BuildGameModel"/>，不经过 UI/场景，因此可重复、可回归。
    /// 格子下标：A=0, B=1, C=2, D=3。
    /// </summary>
    [TestFixture]
    [Category("Acceptance")]
    public sealed class V1_V8_AcceptanceTests
    {
        private const int SlotA = 0;
        private const int SlotB = 1;
        private const int SlotC = 2;
        private const int SlotD = 3;

        private BuildGameModel m_Game;

        [SetUp]
        public void SetUp()
        {
            // 每条用例都从默认配置的全新状态开始：180 金币 / 60 建造费 / 4 格。
            m_Game = new BuildGameModel();
        }

        // ---------------------------------------------------------------- V1
        [Test]
        [Category("V1")]
        public void V1_默认配置在A建造_只有1个建筑且金币为120()
        {
            Assert.AreEqual(BuildGameModel.DefaultInitialCoins, m_Game.Coins, "初始金币应为 180。");

            m_Game.Select(SlotA);
            var outcome = m_Game.TryBuildSelected();

            Assert.AreEqual(BuildOutcome.Success, outcome);
            Assert.AreEqual(1, m_Game.BuildingCount, "只有 1 个建筑。");
            Assert.IsTrue(m_Game.IsOccupied(SlotA), "A 应为已占用。");
            Assert.AreEqual(120, m_Game.Coins, "180 - 60 = 120。");
        }

        // ---------------------------------------------------------------- V2
        [Test]
        [Category("V2")]
        public void V2_金币仍够时连点已占用格子的建造_不重复扣费不重叠()
        {
            // 先完成 V1，得到「A 已建、金币 120」的状态。
            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());

            // 连续快速点击 20 次：金币仍 > 0，但格子已占用，全部必须被挡下。
            for (var i = 0; i < 20; i++)
            {
                Assert.AreEqual(
                    BuildOutcome.SlotOccupied,
                    m_Game.TryBuildSelected(),
                    $"第 {i + 1} 次重复点击应返回 SlotOccupied。");
            }

            Assert.AreEqual(1, m_Game.BuildingCount, "仍只有 1 个建筑。");
            Assert.AreEqual(120, m_Game.Coins, "金币仍为 120，未重复扣费。");
        }

        // ---------------------------------------------------------------- V3
        [Test]
        [Category("V3")]
        public void V3_在BC各建一次后尝试在空格D建造_总计3个建筑余额0且D不建造()
        {
            // A 已建（V1/V2 状态）。
            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());

            m_Game.Select(SlotB);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());

            m_Game.Select(SlotC);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());

            Assert.AreEqual(3, m_Game.BuildingCount);
            Assert.AreEqual(0, m_Game.Coins, "180 - 3×60 = 0。");

            // 在空格 D 建造：金币不足（0 < 60）。
            m_Game.Select(SlotD);
            var outcome = m_Game.TryBuildSelected();

            Assert.AreEqual(BuildOutcome.NotEnoughCoins, outcome, "D 应因金币不足而失败。");
            Assert.IsFalse(m_Game.IsOccupied(SlotD), "D 不建造。");
            Assert.AreEqual(3, m_Game.BuildingCount, "总计仍是 3 个建筑。");
            Assert.AreEqual(0, m_Game.Coins, "失败不扣费，余额仍为 0。");
        }

        // ---------------------------------------------------------------- V4
        [Test]
        [Category("V4")]
        public void V4_拆除A后再次点同一空格拆除_第一次返30第二次仍为30()
        {
            // 前置：A、B、C 各建一次，余额 0（V3 结束状态）。
            m_Game.Select(SlotA);
            m_Game.TryBuildSelected();
            m_Game.Select(SlotB);
            m_Game.TryBuildSelected();
            m_Game.Select(SlotC);
            m_Game.TryBuildSelected();
            Assert.AreEqual(0, m_Game.Coins);

            // 第一次有效拆除：清占用 + 返还固定 30。
            m_Game.Select(SlotA);
            Assert.AreEqual(DemolishOutcome.Success, m_Game.TryDemolishSelected());
            Assert.IsFalse(m_Game.IsOccupied(SlotA), "A 的占用应被清除。");
            Assert.AreEqual(30, m_Game.Coins, "第一次返还后为 30。");

            // 第二次点击同一（已空）格子：不返钱。
            Assert.AreEqual(DemolishOutcome.SlotEmpty, m_Game.TryDemolishSelected());
            Assert.AreEqual(30, m_Game.Coins, "第二次仍为 30，空格拆除不加钱。");
        }

        // ---------------------------------------------------------------- V5
        [Test]
        [Category("V5")]
        public void V5_重新开始再选A建造_重开无建筑无选择金币180_随后建造余额120()
        {
            m_Game.Select(SlotB);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());
            Assert.AreEqual(120, m_Game.Coins);

            m_Game.Restart();

            Assert.AreEqual(0, m_Game.BuildingCount, "重开后无建筑。");
            Assert.IsFalse(m_Game.HasSelection, "重开后无选择。");
            Assert.AreEqual(BuildGameModel.NoSelectionIndex, m_Game.SelectedIndex);
            Assert.AreEqual(180, m_Game.Coins, "重开时金币恢复 180。");

            // 重开后还能正常操作。
            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());
            Assert.AreEqual(120, m_Game.Coins, "建造成功后余额 120。");
            Assert.AreEqual(1, m_Game.BuildingCount);
        }

        // ---------------------------------------------------------------- V6
        [Test]
        [Category("V6")]
        public void V6_连续重新开始两次再建造_每次状态一致且最终1个建筑余额120()
        {
            // 先弄脏状态，确认重开真能清干净。
            m_Game.Select(SlotA);
            m_Game.TryBuildSelected();
            m_Game.Select(SlotB);
            m_Game.TryBuildSelected();
            m_Game.TryDemolishSelected();
            Assert.AreEqual(1, m_Game.BuildingCount);

            for (var round = 1; round <= 2; round++)
            {
                m_Game.Restart();

                Assert.AreEqual(0, m_Game.BuildingCount, $"第 {round} 次重开后无建筑。");
                Assert.IsFalse(m_Game.HasSelection, $"第 {round} 次重开后无选择。");
                Assert.AreEqual(180, m_Game.Coins, $"第 {round} 次重开后金币 180。");
            }

            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());

            Assert.AreEqual(1, m_Game.BuildingCount, "最终只有 1 个建筑。");
            Assert.AreEqual(120, m_Game.Coins, "最终余额 120。");
        }

        // ---------------------------------------------------------------- V7
        [Test]
        [Category("V7")]
        public void V7_测试配置120与40_建3次后第4次失败_拆除30重复拆除仍30()
        {
            // 「改为 120 金币、费用 40，启动新一轮」= 用新配置构造全新状态。
            var game = new BuildGameModel(
                BuildGameModel.DefaultSlotCount,
                initialCoins: 120,
                buildCost: 40);

            Assert.AreEqual(120, game.Coins, "新配置初始金币应为 120。");
            Assert.AreEqual(40, game.BuildCost, "新配置建造费应为 40。");

            foreach (var slot in new[] { SlotA, SlotB, SlotC })
            {
                game.Select(slot);
                Assert.AreEqual(BuildOutcome.Success, game.TryBuildSelected(), $"格子 {slot} 应建造成功。");
            }

            Assert.AreEqual(3, game.BuildingCount);
            Assert.AreEqual(0, game.Coins, "120 - 3×40 = 0。");

            // 第 4 次：金币不足。
            game.Select(SlotD);
            Assert.AreEqual(BuildOutcome.NotEnoughCoins, game.TryBuildSelected(), "第 4 次建造应失败。");
            Assert.IsFalse(game.IsOccupied(SlotD));
            Assert.AreEqual(0, game.Coins, "失败不扣费。");

            // 拆除返还仍固定 30（与 40 的建造费无关）。
            game.Select(SlotC);
            Assert.AreEqual(DemolishOutcome.Success, game.TryDemolishSelected());
            Assert.AreEqual(30, game.Coins, "有效拆除后为 30。");

            Assert.AreEqual(DemolishOutcome.SlotEmpty, game.TryDemolishSelected());
            Assert.AreEqual(30, game.Coins, "重复拆除同一位置仍为 30。");
        }

        // ---------------------------------------------------------------- V8
        [Test]
        [Category("V8")]
        public void V8_恢复默认配置重开后_金币依次180_120_150_90且A最终1个建筑()
        {
            m_Game.Restart(
                BuildGameModel.DefaultInitialCoins,
                BuildGameModel.DefaultBuildCost);

            Assert.AreEqual(180, m_Game.Coins, "恢复默认配置并重开后金币 180。");

            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());
            Assert.AreEqual(120, m_Game.Coins, "建造后 120。");

            Assert.AreEqual(DemolishOutcome.Success, m_Game.TryDemolishSelected());
            Assert.AreEqual(150, m_Game.Coins, "拆除返还 30 → 150。");

            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());
            Assert.AreEqual(90, m_Game.Coins, "再次建造 → 90。");

            Assert.AreEqual(1, m_Game.BuildingCount, "A 最终只有 1 个建筑。");
            Assert.IsTrue(m_Game.IsOccupied(SlotA));
        }

        // ------------------------------------------------ 补充：边界与不变量
        [Test]
        [Category("Edge")]
        public void 未选择格子时建造拆除均被拒绝且状态不变()
        {
            Assert.IsFalse(m_Game.HasSelection);

            Assert.AreEqual(BuildOutcome.NoSelection, m_Game.TryBuildSelected());
            Assert.AreEqual(180, m_Game.Coins);
            Assert.AreEqual(0, m_Game.BuildingCount);

            Assert.AreEqual(DemolishOutcome.NoSelection, m_Game.TryDemolishSelected());
            Assert.AreEqual(180, m_Game.Coins);
        }

        [Test]
        [Category("Edge")]
        public void 空格拆除不加钱_且非法下标不改变任何状态()
        {
            m_Game.Select(SlotA);
            Assert.AreEqual(DemolishOutcome.SlotEmpty, m_Game.TryDemolishSelected(), "空格拆除应返回 SlotEmpty。");
            Assert.AreEqual(180, m_Game.Coins, "空格拆除不加钱。");

            Assert.AreEqual(BuildOutcome.InvalidSlot, m_Game.TryBuild(-1));
            Assert.AreEqual(DemolishOutcome.InvalidSlot, m_Game.TryDemolish(99));
            Assert.AreEqual(180, m_Game.Coins, "非法下标不得改变金币。");
            Assert.AreEqual(0, m_Game.BuildingCount, "非法下标不得产生建筑。");

            // 非法下标不得改变「原有」选中态（此时 A 仍处于选中）。
            Assert.IsFalse(m_Game.Select(-1), "非法下标应被拒绝。");
            Assert.AreEqual(SlotA, m_Game.SelectedIndex, "选中态应保持为 A。");

            m_Game.ClearSelection();
            Assert.IsFalse(m_Game.HasSelection, "ClearSelection 后应为未选中。");
            Assert.AreEqual(BuildGameModel.NoSelectionIndex, m_Game.SelectedIndex);
        }

        [Test]
        [Category("Edge")]
        public void 运行期改配置后重开应按新配置生效()
        {
            // 模拟「在 Inspector 把 180/60 改成 120/40，然后点重新开始」。
            m_Game.Restart(initialCoins: 120, buildCost: 40);

            Assert.AreEqual(120, m_Game.Coins, "重开后按新配置的初始值。");
            Assert.AreEqual(40, m_Game.BuildCost);

            m_Game.Select(SlotA);
            Assert.AreEqual(BuildOutcome.Success, m_Game.TryBuildSelected());
            Assert.AreEqual(80, m_Game.Coins, "应按 40 扣费。");
        }

        [Test]
        [Category("Edge")]
        public void 失败操作不得触发状态变更事件()
        {
            var changedCount = 0;
            m_Game.Changed += () => changedCount++;

            // 失败路径：未选中、金币不足、重复拆除。
            m_Game.TryBuildSelected();
            Assert.AreEqual(0, changedCount, "未选中建造不应通知 UI 刷新。");

            m_Game.Select(SlotA);
            Assert.AreEqual(1, changedCount, "选中变化应通知一次。");

            m_Game.TryBuildSelected();
            Assert.AreEqual(2, changedCount, "建造成功应通知一次。");

            m_Game.TryBuildSelected(); // 重复建造失败
            Assert.AreEqual(2, changedCount, "失败的建造不应通知。");

            m_Game.TryDemolishSelected();
            Assert.AreEqual(3, changedCount, "拆除成功应通知一次。");

            m_Game.TryDemolishSelected(); // 重复拆除失败
            Assert.AreEqual(3, changedCount, "失败的拆除不应通知。");
        }

        [Test]
        [Category("Edge")]
        public void 拆除返还恒为30与建造费用无关()
        {
            Assert.AreEqual(30, EconomyModel.DemolishRefund, "题目规定返还固定 30。");

            var cheap = new BuildGameModel(4, initialCoins: 120, buildCost: 40);
            cheap.Select(SlotA);
            cheap.TryBuildSelected();
            var afterBuild = cheap.Coins;
            cheap.TryDemolishSelected();

            Assert.AreEqual(afterBuild + EconomyModel.DemolishRefund, cheap.Coins);
        }
    }
}
