using FluentAssertions;
using Cute.Lib.Scriban;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;

namespace Cute.Unit.Tests;

// Shares the static CuteFunctions.GoogleApiKey with CuteFunctionsPlacesTests - never run in parallel.
[Collection("CuteFunctions.GoogleApiKey")]
public class CuteFunctionsFacilitiesTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 9, 30, 0, DateTimeKind.Utc);

    private static CuteFunctions.FacilityCandidate Place(string id, int straight, double? rating = 4.5, int reviews = 500, string? status = "OPERATIONAL") => new()
    {
        Id = id,
        Name = $"Google {id}",
        Address = $"{id} Straat, Amsterdam",
        PrimaryTypeDisplay = "Cafe",
        Rating = rating,
        UserRatingCount = reviews,
        PriceLevel = "PRICE_LEVEL_MODERATE",
        GoogleMapsUri = $"https://maps.google.com/?cid={id}",
        Lat = 52.37,
        Lon = 4.89,
        StraightLineMeters = straight,
        BusinessStatus = status,
    };

    private static CuteFunctions.FacilitySearchResult Search(params (string Category, CuteFunctions.FacilityCandidate Place)[] items) =>
        new(52.37, 4.89, CuteFunctions.FacilityMaxRadiusMeters,
            CuteFunctions.MergeFacilityCandidates(items.GroupBy(i => i.Category).Select(g => (g.Key, g.Select(i => i.Place).ToList())), CuteFunctions.FacilityMaxRadiusMeters));

    private static IReadOnlyList<CuteFunctions.WalkingRoute?> NoRoutes(IReadOnlyList<CuteFunctions.FacilityCandidate> places) =>
        places.Select(_ => (CuteFunctions.WalkingRoute?)null).ToList();

    [Fact]
    public void NearbyFacilities_IsCallableFromPrompt_AndRequiresGoogleKey()
    {
        CuteFunctions.GoogleApiKey = null;

        var scriptObject = new ScriptObject();
        scriptObject.SetValue("cute", new CuteFunctions(), true);
        scriptObject.SetValue("dataLocation", JObject.Parse("""{ "latLon": { "lat": 41.1, "lon": -8.6 } }"""), true);

        var act = () => Template.Parse("{{ cute.nearby_facilities dataLocation.latLon.lat dataLocation.latLon.lon }}")
            .Render(scriptObject, memberRenamer: m => char.ToLowerInvariant(m.Name[0]) + m.Name[1..]);

        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Cute__GoogleApiKey"));
    }

    [Fact]
    public void Merge_KeepsOnePerPlace_NotesOtherCategories_DropsClosedAndFar()
    {
        var shared = Place("pub", 200);

        var search = Search(
            ("restaurant", shared),
            ("restaurant", Place("closed", 100, status: "CLOSED_TEMPORARILY")),
            ("restaurant", Place("far", CuteFunctions.FacilityMaxRadiusMeters + 1)),
            ("bar", Place("pub", 200)),
            ("bar", Place("wine", 300)));

        search.Candidates.Select(c => c.Id).Should().Equal("pub", "wine");
        search.Candidates[0].Categories.Should().Equal("restaurant", "bar");
    }

    [Fact]
    public void Build_KeepsModelOrder_DropsUnknownIds_TakesFactsFromGoogle_CapsAtFive()
    {
        var places = Enumerable.Range(1, 7).Select(i => ("cafe", Place($"p{i}", 100 * i))).ToArray();
        var search = Search(places);

        var model = JObject.Parse("""
        { "facilities": [
            { "placeId": "p6", "category": "cafe", "name": "Renamed", "rating": 5 },
            { "placeId": "invented", "category": "bar" },
            { "placeId": "p2", "category": "gym" },
            { "placeId": "p6" },
            { "placeId": "p1" }, { "placeId": "p3" }, { "placeId": "p4" }, { "placeId": "p5" }
        ] }
        """);

        var field = CuteFunctions.BuildNearestFacilitiesField(model, search, NoRoutes, Now);

        var items = (JArray)field["facilities"]!;
        items.Select(f => f["placeId"]!.Value<string>()).Should().Equal("p6", "p2", "p1", "p3", "p4");
        items[0]["name"]!.Value<string>().Should().Be("Google p6", "names always come from Google");
        items[0]["rating"]!.Value<double>().Should().Be(4.5);
        items[0]["priceLevel"]!.Value<string>().Should().Be("moderate");
        items[1]["category"]!.Value<string>().Should().Be("cafe", "a category the place wasn't found under is ignored");
        items[0]["distanceType"]!.Value<string>().Should().Be("straight_line");
        field["maxRadiusMiles"]!.Value<double>().Should().Be(1);
        field["generatedAt"]!.Value<string>().Should().Be("2026-10-30T09:30:00Z");
    }

    [Fact]
    public void Build_UsesModelCategory_WhenPlaceHasIt()
    {
        var search = Search(("restaurant", Place("pub", 200)), ("bar", Place("pub", 200)));

        var field = CuteFunctions.BuildNearestFacilitiesField(
            JArray.Parse("""[ { "placeId": "pub", "category": "bar" } ]"""), search, NoRoutes, Now);

        field["facilities"]![0]!["category"]!.Value<string>().Should().Be("bar");
    }

    [Fact]
    public void Build_AddsWalkingRoutes_ForChosenPlacesOnly()
    {
        var search = Search(("cafe", Place("a", 300)), ("cafe", Place("b", 500)), ("gym", Place("c", 700)));

        IReadOnlyList<CuteFunctions.FacilityCandidate>? asked = null;

        var field = CuteFunctions.BuildNearestFacilitiesField(
            JObject.Parse("""{ "facilities": [ { "placeId": "c" }, { "placeId": "a" } ] }"""),
            search,
            places => { asked = places; return [new CuteFunctions.WalkingRoute(900, 11), null]; },
            Now);

        asked!.Select(p => p.Id).Should().Equal("c", "a");

        var items = (JArray)field["facilities"]!;
        items[0]["distanceMetres"]!.Value<int>().Should().Be(900);
        items[0]["walkingMinutes"]!.Value<int>().Should().Be(11);
        items[0]["distanceType"]!.Value<string>().Should().Be("walking");
        items[1]["distanceMetres"]!.Value<int>().Should().Be(300);
        items[1]["distanceType"]!.Value<string>().Should().Be("straight_line");
    }

    [Fact]
    public void Build_NothingChosen_StillStoresAnObject_AndSkipsRoutes()
    {
        var called = false;

        var field = CuteFunctions.BuildNearestFacilitiesField(
            JObject.Parse("""{ "facilities": [] }"""), Search(),
            places => { called = true; return []; }, Now);

        called.Should().BeFalse();
        field.HasValues.Should().BeTrue();
        ((JArray)field["facilities"]!).Should().BeEmpty();
    }

    [Fact]
    public void Briefing_GroupsByCategory_WithRatingsAndIds()
    {
        var text = CuteFunctions.FormatFacilityBriefing(Search(
            ("restaurant", Place("r1", 250, 4.7, 1234)),
            ("bar", Place("r1", 250)),
            ("gym", Place("g1", 600, null, 0))));

        text.Should().Contain("- r1 | Google r1 | Cafe | 4.7 | 1,234 reviews | moderate | 250 m | r1 Straat, Amsterdam | bar");
        text.Should().Contain("- g1 | Google g1 | Cafe | no rating | 0 reviews | moderate | 600 m");
        text.Should().MatchRegex("CAFES[^\n]*\r?\n- none found");
        text.IndexOf("RESTAURANTS", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("GYMS", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PRICE_LEVEL_MODERATE", "moderate")]
    [InlineData("PRICE_LEVEL_VERY_EXPENSIVE", "very expensive")]
    [InlineData("PRICE_LEVEL_UNSPECIFIED", null)]
    [InlineData(null, null)]
    public void PriceLevelText_ReadsGoogleEnum(string? input, string? expected)
    {
        CuteFunctions.PriceLevelText(input).Should().Be(expected);
    }
}
