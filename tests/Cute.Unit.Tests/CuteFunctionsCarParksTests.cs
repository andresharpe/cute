using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Scriban;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;

namespace Cute.Unit.Tests;

// Shares the static CuteFunctions.GoogleApiKey with CuteFunctionsPlacesTests - never run in parallel.
[Collection("CuteFunctions.GoogleApiKey")]
public class CuteFunctionsCarParksTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 15, 40, 0, DateTimeKind.Utc);

    private static CuteFunctions.CarParkCandidate Candidate(string id, int straight, int? walk = null, string? status = "OPERATIONAL") => new()
    {
        Id = id,
        Name = $"Google {id}",
        Address = $"{id} Street, London",
        Lat = 51.5,
        Lon = -0.12,
        StraightLineMeters = straight,
        WalkingMeters = walk,
        WalkingMinutes = walk is null ? null : Math.Max(1, walk.Value / 80),
        BusinessStatus = status,
    };

    private static CuteFunctions.CarParkSearchResult Search(params CuteFunctions.CarParkCandidate[] candidates) =>
        new(51.5, -0.12, CuteFunctions.CarParkMaxRadiusMeters, true, candidates);

    [Fact]
    public void NearbyCarParks_IsCallableFromPrompt_AndRequiresGoogleKey()
    {
        CuteFunctions.GoogleApiKey = null;

        var scriptObject = new ScriptObject();
        scriptObject.SetValue("cute", new CuteFunctions(), true);
        scriptObject.SetValue("dataLocation", JObject.Parse("""{ "latLon": { "lat": 40.1, "lon": -3.7 } }"""), true);

        var act = () => Template.Parse("{{ cute.nearby_car_parks dataLocation.latLon.lat dataLocation.latLon.lon }}")
            .Render(scriptObject, memberRenamer: m => char.ToLowerInvariant(m.Name[0]) + m.Name[1..]);

        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Cute__GoogleApiKey"));
    }

    [Fact]
    public void Filter_DropsClosedAndOutOfRange_DedupesAndCaps()
    {
        var found = new List<CuteFunctions.CarParkCandidate>
        {
            Candidate("a", 100),
            Candidate("a", 100),
            Candidate("closed", 50, status: "CLOSED_PERMANENTLY"),
            Candidate("far", CuteFunctions.CarParkMaxRadiusMeters + 1),
            Candidate("nostatus", 200, status: null),
        };
        found.AddRange(Enumerable.Range(0, 20).Select(i => Candidate($"x{i}", 300 + i)));

        var result = CuteFunctions.FilterCarParkCandidates(found, CuteFunctions.CarParkMaxRadiusMeters);

        result.Should().HaveCount(CuteFunctions.CarParkCandidateCount);
        result.Select(c => c.Id).Should().StartWith(new[] { "a", "nostatus", "x0" });
        result.Select(c => c.Id).Should().NotContain(new[] { "closed", "far" });
    }

    [Fact]
    public void Build_KeepsOnlyGooglePlaceIds_AndTakesFactsFromGoogle()
    {
        var search = Search(Candidate("p1", 300, 400), Candidate("p2", 500, 650));

        var model = JObject.Parse("""
        { "carParks": [
            { "placeId": "invented", "name": "Made Up Car Park", "address": "1 Fake St", "lat": 0, "lng": 0 },
            { "placeId": "p2", "name": "Q-Park Oxford Street", "lat": 1, "distanceMetres": 5 },
            { "placeId": "p2", "name": "Duplicate" }
        ] }
        """);

        var field = CuteFunctions.BuildNearbyCarParksField(model, search, Now);

        var parks = (JArray)field["carParks"]!;
        parks.Should().HaveCount(1);
        parks[0]["placeId"]!.Value<string>().Should().Be("p2");
        parks[0]["name"]!.Value<string>().Should().Be("Q-Park Oxford Street");
        parks[0]["address"]!.Value<string>().Should().Be("p2 Street, London");
        parks[0]["lat"]!.Value<double>().Should().Be(51.5);
        parks[0]["distanceMetres"]!.Value<int>().Should().Be(650);
        parks[0]["distanceType"]!.Value<string>().Should().Be("walking");
        parks[0]["straightLineMetres"]!.Value<int>().Should().Be(500);
        field["generatedAt"]!.Value<string>().Should().Be("2026-10-29T15:40:00Z");
        field["maxRadiusMiles"]!.Value<double>().Should().Be(2);
    }

    [Fact]
    public void Build_SortsByWalkingDistance_AndCapsAtFive()
    {
        var candidates = Enumerable.Range(1, 8).Select(i => Candidate($"p{i}", 100 * i, 1000 - 100 * i)).ToArray();

        var model = new JArray(candidates.Select(c => new JObject { ["placeId"] = c.Id, ["name"] = "" }));

        var field = CuteFunctions.BuildNearbyCarParksField(model, Search(candidates), Now);

        var parks = (JArray)field["carParks"]!;
        parks.Should().HaveCount(CuteFunctions.CarParkMaxResults);
        parks.Select(p => p["placeId"]!.Value<string>()).Should().Equal("p8", "p7", "p6", "p5", "p4");
        parks[0]["name"]!.Value<string>().Should().Be("Google p8", "an empty name falls back to Google's");
    }

    [Fact]
    public void Build_NoCarParks_StillStoresAnObject()
    {
        var field = CuteFunctions.BuildNearbyCarParksField(JObject.Parse("""{ "carParks": [] }"""), Search(), Now);

        field.HasValues.Should().BeTrue("an empty array would be treated as missing and retried every run");
        ((JArray)field["carParks"]!).Should().BeEmpty();
    }

    [Fact]
    public void Build_StraightLineFallback_WhenNoWalkingRoute()
    {
        var field = CuteFunctions.BuildNearbyCarParksField(
            JObject.Parse("""{ "carParks": [ { "placeId": "p1", "name": "Car Park" } ] }"""),
            Search(Candidate("p1", 1609)), Now);

        var park = field["carParks"]![0]!;
        park["distanceType"]!.Value<string>().Should().Be("straight_line");
        park["distanceMiles"]!.Value<double>().Should().Be(1.0);
        park["walkingMinutes"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void Briefing_ListsPlaceIds_NearestWalkFirst()
    {
        var text = CuteFunctions.FormatCarParkBriefing(Search(Candidate("far", 900, 1200), Candidate("near", 300, 400)));

        text.Should().Contain("- near | Google near | parking | near Street, London | walk 400 m, 5 min (300 m)");
        text.IndexOf("- near", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("- far", StringComparison.Ordinal));
    }

    [Fact]
    public void Briefing_SaysWhenNothingFound()
    {
        CuteFunctions.FormatCarParkBriefing(Search()).Should().Contain("- none within the radius");
    }

    [Theory]
    [InlineData("345s", 6)]
    [InlineData("20s", 1)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("abc", null)]
    public void ParseDurationMinutes_ReadsRoutesDurations(string? input, int? expected)
    {
        CuteFunctions.ParseDurationMinutes(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("  ```\r\n{\"a\":1}\r\n```  ", "{\"a\":1}")]
    public void StripCodeFence_ReturnsInnerJson(string input, string expected)
    {
        GenerateBulkAction.StripCodeFence(input).Should().Be(expected);
    }
}
