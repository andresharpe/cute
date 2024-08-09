using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Utilities;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace Cute.Commands;

public sealed class SeedDataCommand : LoggedInCommand<SeedDataCommand.Settings>
{
    public SeedDataCommand(IConsoleWriter console, ILogger<GetDataCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings)
        : base(console, logger, contentfulConnection, appSettings)
    {
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-i|--input-file")]
        [Description("The id of the content type containing data sync definitions. Default is 'metaGetData'.")]
        public string InputFile { get; set; } = "metaGetData";

        [CommandOption("-c|--content-type")]
        [Description("The id of the content type containing location data. Default is 'dataLocation'.")]
        public string ContentType { get; set; } = "dataLocation";

        [CommandOption("-k|--kilometer-radius")]
        [Description("The distance in kilometers to nearest location")]
        public int KilometerRadius { get; set; } = 50;

        [CommandOption("-m|--min-population")]
        [Description("The city or town minimum population to include")]
        public int MinPopulation { get; set; } = 10000;

        [CommandOption("-p|--password")]
        [Description("The password to protect the Zip file with")]
        public string Password { get; set; } = default!;

        [CommandOption("-z|--zip")]
        [Description("Output a zip file instead of a csv. Can be password protected with '--password'.")]
        public bool Zip { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        await FilterAndExtractGeos(settings);

        if (settings.Zip)
        {
            await ZipExtractedGeos(settings);
        }

        return 0;
    }

    private async Task ZipExtractedGeos(Settings settings)
    {
        var inputFile = settings.InputFile;

        // var zipFile = Path.GetDirectoryName(inputFile) + @"\" + Path.GetFileNameWithoutExtension(inputFile) + ".output.zip";

        await Task.Delay(1);

        return;
    }

    private async Task FilterAndExtractGeos(Settings settings)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        var inputFile = settings.InputFile;

        var outputFile = Path.GetDirectoryName(inputFile) + @"\" + Path.GetFileNameWithoutExtension(inputFile) + ".output" + Path.GetExtension(inputFile);

        using var reader = new StreamReader(inputFile, System.Text.Encoding.UTF8);

        using var csvReader = new CsvReader(reader, config);

        using var writer = new StreamWriter(outputFile, false, System.Text.Encoding.UTF8);

        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csvReader.ReadAsync().ConfigureAwait(false);

        csvReader.Context.RegisterClassMap<SimplemapsGeoMap>();

        csvReader.ReadHeader();

        csvWriter.WriteHeader<SimplemapsGeoOutput>();

        csvWriter.NextRecord();

        if (csvReader.HeaderRecord is null) return;

        var recordsRead = 0;
        var recordsWritten = 0;
        var outputEvery = 100000;
        var nextOutput = outputEvery;

        _console.WriteNormal("Reading locations...");

        var dataLocations = ContentfulEntryEnumerator.DeliveryEntries<JObject>(ContentfulClient, settings.ContentType)
            .ToBlockingEnumerable()
            .Select(e => new
            {
                CountryCode = e.Entry["dataCountryEntry"]?["iso2code"]?.Value<string>(),
                Lat = e.Entry["latLng"]?["lat"]?.Value<double>() ?? 0.0f,
                Lon = e.Entry["latLng"]?["lon"]?.Value<double>() ?? 0.0f,
            })
            .Where(i => i.CountryCode is not null)
            .ToList();

        _console.WriteNormal("Reading country codes...");

        var dataCountryCode = dataLocations.Select(o => o.CountryCode).ToHashSet();

        _console.WriteNormalWithHighlights($"Reading '{inputFile}'...", Globals.StyleHeading);

        var timeZones = new Dictionary<string, string>();
        var adminTypes = new Dictionary<string, string>();
        var adminNames = new Dictionary<string, string>();
        var capitalTypes = new Dictionary<string, string>();

        while (await csvReader.ReadAsync().ConfigureAwait(false))
        {
            var record = csvReader.GetRecord<SimplemapsGeoInput>();

            recordsRead++;

            if (record.SameName) continue;

            if (record.Population is null || record.Population < settings.MinPopulation) continue;

            if (!dataCountryCode.Contains(record.CountryIso2)) continue;

            var boundingBox = Haversine.GetBoundingBox(record.Lon, record.Lat, settings.KilometerRadius);

            var nearLocations = dataLocations
                .Any(l => boundingBox.Contains(l.Lon, l.Lat));

            if (!nearLocations) continue;

            var newRecord = new SimplemapsGeoOutput()
            {
                Id = record.Id,
                CountryIso2 = record.CountryIso2,
                CityName = record.CityName,
                CityAlternateName = record.CityAlternateName,
                Lat = record.Lat,
                Lon = record.Lon,
                AdminCode = record.AdminCode,
                AdminType = record.AdminType,
                CapitalType = record.Capital,
                Density = record.Density,
                Population = record.Population,
                PopulationProper = record.PopulationProper,
                Timezone = record.Timezone,
            };

            csvWriter.WriteRecord(newRecord);

            csvWriter.NextRecord();

            recordsWritten++;

            if (recordsRead > nextOutput)
            {
                _console.WriteNormalWithHighlights($"Read {recordsRead} and wrote {recordsWritten} records...", Globals.StyleHeading);
                nextOutput = recordsRead + outputEvery;
            }
        }

        _console.WriteNormalWithHighlights($"Read {recordsRead} and wrote {recordsWritten} records...", Globals.StyleHeading);

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"New data...: {outputFile}", Globals.StyleHeading);

        _console.WriteBlankLine();

        return;
    }

    public sealed class SimplemapsGeoMap : ClassMap<SimplemapsGeoInput>
    {
        public SimplemapsGeoMap()
        {
            AutoMap(CultureInfo.InvariantCulture);

            Map(m => m.Population).Convert(r => ConvertStringToInt(r.Row["population"]));

            Map(m => m.PopulationProper).Convert(r => ConvertStringToInt(r.Row["population_proper"]));
        }
    }

    private static int? ConvertStringToInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var dbl = Convert.ToDouble(value);

        return (int)dbl;
    }

    public class SimplemapsGeoInput
    {
        [Name("city")]
        public string CityName { get; set; } = default!;

        [Name("city_ascii")]
        public string CityNameAscii { get; set; } = default!;

        [Name("city_alt")]
        public string CityAlternateName { get; set; } = default!;

        [Name("lat")]
        public double Lat { get; set; } = default!;

        [Name("lng")]
        public double Lon { get; set; } = default!;

        [Name("country")]
        public string CountryName { get; set; } = default!;

        [Name("iso2")]
        public string CountryIso2 { get; set; } = default!;

        [Name("iso3")]
        public string CountryIso3 { get; set; } = default!;

        [Name("admin_name")]
        public string AdminName { get; set; } = default!;

        [Name("admin_name_ascii")]
        public string AdminNameAscii { get; set; } = default!;

        [Name("admin_code")]
        public string AdminCode { get; set; } = default!;

        [Name("admin_type")]
        public string AdminType { get; set; } = default!;

        [Name("capital")]
        public string Capital { get; set; } = default!;

        [Name("density")]
        public double Density { get; set; } = default!;

        [Name("population")]
        public int? Population { get; set; } = default!;

        [Name("population_proper")]
        public int? PopulationProper { get; set; } = default!;

        [Name("ranking")]
        public int Ranking { get; set; } = default!;

        [Name("timezone")]
        public string Timezone { get; set; } = default!;

        [Name("same_name")]
        public bool SameName { get; set; } = default!;

        [Name("id")]
        public string Id { get; set; } = default!;
    }

    public class SimplemapsGeoOutput
    {
        public string Id { get; set; } = default!;
        public string CountryIso2 { get; set; } = default!;
        public string CityName { get; set; } = default!;
        public string CityAlternateName { get; set; } = default!;
        public double Lat { get; set; } = default!;
        public double Lon { get; set; } = default!;
        public string AdminCode { get; set; } = default!;
        public string? AdminType { get; set; } = default!;
        public string? CapitalType { get; set; } = default!;
        public double Density { get; set; } = default!;
        public int? Population { get; set; } = default!;
        public int? PopulationProper { get; set; } = default!;
        public string? Timezone { get; set; } = default!;
    }
}