using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>s41: x6 location text (OV2 <c>0x5fae</c>) — temples are text-only sites; 1/2/3-line pages by arg decade.</summary>
public partial class LocationTextTests
{
    private static OverworldTraversal Traversal => GameDataRepository.LoadPhantasie1().CreateTraversal();

    private sealed class CountingRng : IRandomSource
    {
        public int Draws { get; private set; }
        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            return minInclusive;
        }
    }

    private static OverworldMoveResult StepOnto(int region, int x, int y, IRandomSource? rng = null)
    {
        // Step west from the cell to the east (the source tile is not checked).
        var state = new OverworldState { RegionId = region, X = x + 1, Y = y };
        return Traversal.TryMove(state, CardinalDirection.West, rng);
    }

    [Theory]
    [InlineData(5, new[] { 5 })]
    [InlineData(10, new[] { 0, 1 })]
    [InlineData(14, new[] { 4, 5 })]
    [InlineData(20, new[] { 0, 1, 2 })]
    [InlineData(32, new[] { 12, 13, 14 })] // ≥ 20 is the else branch: arg − 20, no fourth decade
    public void Pages_ByArgDecade(int arg, int[] pages) =>
        Assert.Equal(pages, OverworldTraversal.LocationTextPages(arg));

}
