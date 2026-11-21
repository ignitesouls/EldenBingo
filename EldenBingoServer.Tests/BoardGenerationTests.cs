using EldenBingoCommon;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EldenBingoServer.Tests;

public class BoardGenerationTests
{
    [Fact]
    public void ForcedCenterMustPassCategoryAndRegionLimits()
    {
        var blockedCategory = new JObject { ["name"] = "Blocked category", ["center"] = 1, ["category"] = "Blocked" };
        var blockedRegion = new JObject { ["name"] = "Blocked regions", ["center"] = 1, ["regions"] = new JArray("North", "South") };
        var allowedCenter = new JObject { ["name"] = "Allowed center", ["center"] = 1 };
        var root = Squares(8, blockedCategory, blockedRegion, allowedCenter);
        root["categoryLimits"] = new JObject { ["Blocked"] = 0 };
        root["setRegionLimits"] = new JArray(RegionGroup("Route", "North", "South"));

        var board = Generate(root);

        Assert.NotNull(board);
        Assert.Equal("Allowed center", board.Squares[4].Text);
        Assert.DoesNotContain(board.Squares, square => square.Text.StartsWith("Blocked"));
        ((JArray)root["squares"]!).Remove(allowedCenter);
        Assert.Null(Generate(root));
    }

    [Fact]
    public void CategoryCaseVariantsCountOnce()
    {
        var root = Squares(8, new JObject { ["name"] = "Only boss", ["categories"] = new JArray("Boss", "boss") });
        root["categoryMinimums"] = new JObject { ["Boss"] = 2 };

        Assert.Null(Generate(root));
    }

    [Fact]
    public void RegionGroupsWithMatchingDisplayNamesRemainIndependent()
    {
        var root = Squares(7, new JObject { ["name"] = "North", ["region"] = "North" },
            new JObject { ["name"] = "South", ["region"] = "South" });
        root["setRegionLimits"] = new JArray(RegionGroup("Route", "North"), RegionGroup("route", "South"));

        Assert.NotNull(Generate(root));
    }

    [Fact]
    public void MinimumSelectionLeavesSpaceForLegalCenter()
    {
        var root = Squares(0, Enumerable.Range(0, 9).Select(index => new JObject
        {
            ["name"] = $"Outside {index}", ["category"] = "Required", ["center"] = 0, ["weight"] = 1000000000
        }).Append(new JObject { ["name"] = "Legal center", ["category"] = "Required" }).ToArray());
        root["categoryMinimums"] = new JObject { ["Required"] = 9 };

        var board = Generate(root);

        Assert.NotNull(board);
        Assert.Equal("Legal center", board.Squares[4].Text);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void InvalidRandomChoicesFailWhenLoaded(string choices)
    {
        var root = Squares(8, new JObject { ["name"] = "Find %item%", ["item"] = JArray.Parse(choices) });

        var error = Assert.Throws<ArgumentException>(() => new BingoBoardGenerator(root, 177));

        Assert.Contains("item", error.Message);
    }

    [Fact]
    public void EvenBoardsIgnoreCenterRules()
    {
        var root = Squares(14, new JObject { ["name"] = "Forced A", ["center"] = 1 },
            new JObject { ["name"] = "Forced B", ["center"] = 1 });

        var board = Generate(root, 4);

        Assert.NotNull(board);
        Assert.Equal(16, board.Squares.Length);
        Assert.Contains(board.Squares, square => square.Text == "Forced A");
        Assert.Contains(board.Squares, square => square.Text == "Forced B");
    }

    [Fact]
    public void DistributionTesterKeepsSourceIndicesForSkippedAndDuplicateRows()
    {
        var root = Squares(7, new JObject { ["name"] = "" }, new JObject { ["name"] = "Duplicate" },
            new JObject { ["name"] = "Duplicate" });
        var report = BoardDistributionTester.Run(root.ToString(), Settings(3), 4);

        Assert.Equal(4, report.Successful);
        Assert.Equal(new[] { 9, 10 }, report.Objectives.Where(row => row.Objective == "Duplicate").Select(row => row.Index));
        Assert.All(report.Objectives, row => Assert.Equal(4, row.Selected));
    }

    private static JObject Squares(int fillers, params JObject[] extra) => new()
    {
        ["squares"] = new JArray(Enumerable.Range(1, fillers)
            .Select(index => new JObject { ["name"] = $"Objective {index}" }).Concat(extra))
    };

    private static JObject RegionGroup(string name, params string[] regions) => new()
    {
        ["name"] = name,
        ["regionLimit"] = 1,
        ["regionNames"] = new JArray(regions)
    };

    private static BingoGameSettings Settings(int size) =>
        new(size, true, false, new HashSet<EldenRingClasses>(), 1, 0, 177, 0, 0);

    private static ServerBingoBoard? Generate(JObject root, int size = 3)
    {
        var definition = SquareSetDefinition.Parse(root.ToString());
        var room = new ServerRoom("Test", "", null!, Settings(size)) { CategoryConfig = definition.Categories };
        return new BingoBoardGenerator(definition.Root, 177).CreateBingoBoard(room);
    }
}
