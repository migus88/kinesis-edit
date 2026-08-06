using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.Core.Tests.Geometry.Visual
{
    /// <summary>
    /// Spatial navigation over the authored Freestyle Edge RGB board: the four directions from a
    /// key in the middle of the left half, the crossing of the split gap in both directions, the
    /// hotkey column, the four board edges, and the reachability walk that catches a scoring bug
    /// the per-direction cases miss.
    /// </summary>
    public class KeyAdjacencyTests
    {
        private const int ExpectedKeyCount = 95;

        /// <summary>Caps row, left half: the "s" position, one column in from "a" (spec 05 §4.2).</summary>
        private const int LeftHalfCapsRowKey = 55;

        private static KeyboardVisual Visual => VisualCatalog.FreestyleEdgeRgb;

        public static IEnumerable<object[]> AllDirections()
        {
            yield return new object[] { NavigationDirection.Up };
            yield return new object[] { NavigationDirection.Down };
            yield return new object[] { NavigationDirection.Left };
            yield return new object[] { NavigationDirection.Right };
        }

        [Theory]
        // Left/Right walk the caps row; Up lands on the tab-row cap with the largest overlap;
        // Down ties between two shift-row caps and the lower index wins.
        [InlineData(NavigationDirection.Left, 54)]
        [InlineData(NavigationDirection.Right, 56)]
        [InlineData(NavigationDirection.Up, 38)]
        [InlineData(NavigationDirection.Down, 70)]
        public void Next_FromTheMiddleOfTheLeftHalf_LandsOnThePhysicalNeighbour(
            NavigationDirection direction,
            int expectedIndex)
        {
            var next = KeyAdjacency.Next(Visual, LeftHalfCapsRowKey, direction);

            Assert.NotNull(next);
            Assert.Equal(expectedIndex, next!.Index);
        }

        [Fact]
        public void Next_RightFromTheLastKeyOfTheLeftHalf_CrossesTheSplitGap()
        {
            // 58 is the right-most caps-row cap of the left half; 59 the left-most of the right
            // half, a full 1U gap away. Nothing special-cases the gap: the row overlap wins.
            var next = KeyAdjacency.Next(Visual, 58, NavigationDirection.Right);

            Assert.NotNull(next);
            Assert.Equal(59, next!.Index);
        }

        [Fact]
        public void Next_LeftFromTheFirstKeyOfTheRightHalf_CrossesBackToTheLeftHalf()
        {
            var next = KeyAdjacency.Next(Visual, 59, NavigationDirection.Left);

            Assert.NotNull(next);
            Assert.Equal(58, next!.Index);
        }

        [Fact]
        public void Next_LeftFromTheTypingBlock_EntersTheHotkeyColumn()
        {
            // 53 is Caps Lock; the hotkey pair on its row is hk7/hk8 (51, 52), with 52 adjacent.
            var next = KeyAdjacency.Next(Visual, 53, NavigationDirection.Left);

            Assert.NotNull(next);
            Assert.Equal(52, next!.Index);
            Assert.Equal(KeyCluster.Function, next.Cluster);
        }

        [Fact]
        public void Next_RightFromTheHotkeyColumn_LeavesItForTheTypingBlock()
        {
            var next = KeyAdjacency.Next(Visual, 52, NavigationDirection.Right);

            Assert.NotNull(next);
            Assert.Equal(53, next!.Index);
            Assert.Equal(KeyCluster.Main, next.Cluster);
        }

        [Fact]
        public void Next_WithinTheHotkeyColumn_WalksTheGridInEveryDirection()
        {
            Assert.Equal(18, KeyAdjacency.Next(Visual, 17, NavigationDirection.Right)?.Index);
            Assert.Equal(17, KeyAdjacency.Next(Visual, 18, NavigationDirection.Left)?.Index);
            Assert.Equal(34, KeyAdjacency.Next(Visual, 17, NavigationDirection.Down)?.Index);
            Assert.Equal(17, KeyAdjacency.Next(Visual, 34, NavigationDirection.Up)?.Index);
        }

        [Theory]
        // Top row up, bottom row down, the left-most cap left, the right-most cap right.
        [InlineData(1, NavigationDirection.Up)]
        [InlineData(0, NavigationDirection.Up)]
        [InlineData(88, NavigationDirection.Down)]
        [InlineData(83, NavigationDirection.Down)]
        [InlineData(17, NavigationDirection.Left)]
        [InlineData(83, NavigationDirection.Left)]
        // 0 is the 2U hk0 at the very top-left: its centre sits at X 1.0, so every 1U hotkey below
        // it (centre X 0.5) is strictly to its left. Left must still be a board edge, not a
        // diagonal walk down the hotkey column.
        [InlineData(0, NavigationDirection.Left)]
        [InlineData(16, NavigationDirection.Right)]
        [InlineData(94, NavigationDirection.Right)]
        public void Next_AtABoardEdge_ReturnsNull(int fromIndex, NavigationDirection direction)
        {
            Assert.Null(KeyAdjacency.Next(Visual, fromIndex, direction));
        }

        [Fact]
        public void Next_HorizontallyWithoutRowOverlap_ReturnsNull()
        {
            // "The key to my left" is always on my row. Keys 1 and 2 are strictly left and right of
            // key 0's centre but share none of its Y span, so neither is a horizontal neighbour —
            // this is the rule that keeps hk0's Left a board edge.
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 1, 0),
                    new KeyVisual(1, 0, 1),
                    new KeyVisual(2, 2, 1)
                });

            Assert.Null(KeyAdjacency.Next(visual, 0, NavigationDirection.Left));
            Assert.Null(KeyAdjacency.Next(visual, 0, NavigationDirection.Right));
        }

        [Fact]
        public void Next_VerticallyWithoutColumnOverlap_PrefersTheKeyInsideTheCone()
        {
            // Both candidates sit one row below key 0 with no X overlap, so both are in the
            // fallback tier: 2 is one unit across (a thumb-cluster shape — Advantage2/360 put
            // their clusters exactly there), 1 is two units across. The nearer one wins.
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0, 0),
                    new KeyVisual(1, 2, 1),
                    new KeyVisual(2, 1, 1)
                });

            Assert.Equal(2, KeyAdjacency.Next(visual, 0, NavigationDirection.Down)?.Index);
        }

        [Fact]
        public void Next_VerticallyOutsideTheCone_ReturnsNull()
        {
            // Same fallback tier, but the only candidate lies further across than it lies down.
            // The cone (perpendicular <= primary) refuses it rather than teleporting diagonally.
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0, 0),
                    new KeyVisual(1, 4, 1)
                });

            Assert.Null(KeyAdjacency.Next(visual, 0, NavigationDirection.Down));
            Assert.Null(KeyAdjacency.Next(visual, 1, NavigationDirection.Up));
        }

        [Theory]
        [MemberData(nameof(AllDirections))]
        public void Next_ForAnUnknownIndex_ReturnsNullInsteadOfThrowing(NavigationDirection direction)
        {
            Assert.Null(KeyAdjacency.Next(Visual, 999, direction));
            Assert.Null(KeyAdjacency.Next(Visual, -1, direction));
        }

        [Fact]
        public void Next_ForEveryKeyAndDirection_StaysInsideTheVisual()
        {
            foreach (var key in Visual.Keys)
            {
                foreach (var direction in Directions())
                {
                    var next = KeyAdjacency.Next(Visual, key.Index, direction);

                    if (next is null)
                    {
                        continue;
                    }

                    Assert.NotEqual(key.Index, next.Index);
                    Assert.True(Visual.TryGetKey(next.Index, out _));
                }
            }
        }

        [Fact]
        public void Next_WalkedBreadthFirstFromOneKey_ReachesEveryKeyOfTheBoard()
        {
            // The per-direction cases only pin the moves they name; a key that is never any
            // neighbour's best candidate would survive them all and be unreachable by keyboard.
            var visited = new HashSet<int> { 0 };
            var queue = new Queue<int>();
            queue.Enqueue(0);

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();

                foreach (var direction in Directions())
                {
                    var next = KeyAdjacency.Next(Visual, index, direction);

                    if (next is not null && visited.Add(next.Index))
                    {
                        queue.Enqueue(next.Index);
                    }
                }
            }

            Assert.Equal(ExpectedKeyCount, Visual.Keys.Count);
            Assert.Equal(Visual.Keys.Select(key => key.Index).ToHashSet(), visited);
        }

        [Fact]
        public void Next_ForAnUndefinedDirection_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => KeyAdjacency.Next(Visual, 0, NavigationDirection.None));
        }

        [Fact]
        public void Next_WithoutAVisual_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => KeyAdjacency.Next(null!, 0, NavigationDirection.Right));
        }

        private static IEnumerable<NavigationDirection> Directions()
        {
            yield return NavigationDirection.Up;
            yield return NavigationDirection.Down;
            yield return NavigationDirection.Left;
            yield return NavigationDirection.Right;
        }
    }
}
