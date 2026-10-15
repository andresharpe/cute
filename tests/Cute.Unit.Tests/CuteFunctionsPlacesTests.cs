using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Scriban;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;

namespace Cute.Unit.Tests;

public class CuteFunctionsPlacesTests
{
    private static string Render(string template, JObject dataLocation)
    {
        var scriptObject = new ScriptObject();
        scriptObject.SetValue("cute", new CuteFunctions(), true);
        scriptObject.SetValue("dataLocation", dataLocation, true);

        // Same renderer settings as GenerateBulkAction.RenderTemplate
        return Template.Parse(template).Render(scriptObject, memberRenamer: member => char.ToLowerInvariant(member.Name[0]) + member.Name[1..]);
    }

    [Fact]
    public void NearbyPlaces_IsCallableFromPrompt_AndRequiresGoogleKey()
    {
        CuteFunctions.GoogleApiKey = null;

        var location = JObject.Parse("""{ "latLon": { "lat": 52.3731, "lon": 4.8926 } }""");

        var act = () => Render("{{ cute.nearby_places dataLocation.latLon.lat dataLocation.latLon.lon 500 }}", location);

        // Reaching the key check proves the function is bound as 'nearby_places'
        // and that JValue coordinates convert to numbers.
        act.Should().Throw<Exception>()
            .Where(e => e.ToString().Contains("Cute__GoogleApiKey"));
    }

    [Fact]
    public void NearbyPlaces_RejectsNonNumericCoordinates()
    {
        CuteFunctions.GoogleApiKey = "dummy";

        var location = JObject.Parse("""{ "latLon": { "lat": "not-a-number", "lon": 4.8926 } }""");

        var act = () => Render("{{ cute.nearby_places dataLocation.latLon.lat dataLocation.latLon.lon }}", location);

        act.Should().Throw<Exception>()
            .Where(e => e.ToString().Contains("must be a number"));

        CuteFunctions.GoogleApiKey = null;
    }

    [Theory]
    [InlineData("Dam Square", "Dam Square")]
    [InlineData("  \"Dam Square\"  ", "Dam Square")]
    [InlineData("\u201CDe 9 Straatjes\u201D", "De 9 Straatjes")]
    [InlineData("'Rokin'", "Rokin")]
    [InlineData("\n\nZuidas Mahlerlaan\r\nBecause it is the nearest station.", "Zuidas Mahlerlaan")]
    [InlineData("", "")]
    [InlineData("   \n  ", "")]
    public void ToSingleLine_KeepsOnlyTheValue(string input, string expected)
    {
        GenerateBulkAction.ToSingleLine(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("zzzAmsterdam, Amstel (CLOSED)", true)]
    [InlineData("zzAMSTERDAM, Singel(closed)", true)]
    [InlineData("AMSTERDAM, Sloterdijk Station (Closed)", true)]
    [InlineData("Amsterdam, Sloterdijk", false)]
    [InlineData("Herengracht 280", false)]
    [InlineData(null, false)]
    public void IsClosedCentre_DetectsClosedMarkers(string? value, bool expected)
    {
        CuteFunctions.IsClosedCentre(value).Should().Be(expected);
    }
}
