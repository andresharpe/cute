using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.Utilities;
using Cute.Services;
using ICSharpCode.SharpZipLib.Zip;
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

    private string _extractedFile = default!;

    public class Settings : CommandSettings
    {
        [CommandOption("-i|--input-file")]
        [Description("The path to the input file.")]
        public string InputFile { get; set; } = "metaGetData";

        [CommandOption("-o|--output-folder")]
        [Description("The output folder.")]
        public string OutputFolder { get; set; } = "metaGetData";

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

        _extractedFile = _extractedFile = Path.GetDirectoryName(settings.InputFile) + @"\" +
            Path.GetFileNameWithoutExtension(settings.InputFile) + ".output"
            + Path.GetExtension(settings.InputFile);

        await FilterAndExtractGeos(settings);
        if (settings.Zip)
        {
            var zipFile = await ZipExtractedGeos(settings);
            File.Delete(Path.Combine(settings.OutputFolder, Path.GetFileName(zipFile)));
            File.Move(zipFile, Path.Combine(settings.OutputFolder, Path.GetFileName(zipFile)));
        }
        else
        {
            File.Delete(Path.Combine(settings.OutputFolder, Path.GetFileName(_extractedFile)));
            File.Move(_extractedFile, Path.Combine(settings.OutputFolder, Path.GetFileName(_extractedFile)));
        }
        return 0;
    }

    private async Task<string> ZipExtractedGeos(Settings settings)
    {
        var zipFile = Path.GetDirectoryName(_extractedFile) + @"\" + Path.GetFileNameWithoutExtension(_extractedFile) + ".zip";

        _console.WriteNormalWithHighlights($"Starting compress to '{zipFile}'...", Globals.StyleHeading);

        using var zip = new ZipOutputStream(File.Create(zipFile));

        zip.SetLevel(9);

        if (settings.Password is not null)
        {
            zip.Password = settings.Password;
        }

        var entry = new ZipEntry(Path.GetFileName(_extractedFile))
        {
            DateTime = File.GetLastWriteTimeUtc(_extractedFile)
        };

        await zip.PutNextEntryAsync(entry);

        using var fileStream = File.OpenRead(_extractedFile);

        int sourceBytes;
        var buffer = new byte[4096];

        while (true)
        {
            sourceBytes = await fileStream.ReadAsync(buffer);

            if (sourceBytes == 0) break;

            await zip.WriteAsync(buffer.AsMemory(0, sourceBytes));
        }

        zip.Finish();

        zip.Close();

        _console.WriteNormalWithHighlights($"Completed compress to '{zipFile}'...", Globals.StyleHeading);

        return zipFile;
    }

    private async Task FilterAndExtractGeos(Settings settings)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        var inputFile = settings.InputFile;

        using var reader = new StreamReader(inputFile, System.Text.Encoding.UTF8);

        using var csvReader = new CsvReader(reader, config);

        using var writer = new StreamWriter(_extractedFile, false, System.Text.Encoding.UTF8);

        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csvReader.ReadAsync().ConfigureAwait(false);

        csvReader.Context.RegisterClassMap<SimplemapsGeoMap>();

        csvReader.ReadHeader();

        csvWriter.WriteHeader<GeoOutputFormat>();

        csvWriter.NextRecord();

        if (csvReader.HeaderRecord is null) return;

        var recordsRead = 0;
        var recordsWritten = 0;
        var outputEvery = 100000;
        var nextOutput = outputEvery;

        _console.WriteNormal("Reading existing geos for countries...");

        var countryToGeoId = ContentfulEntryEnumerator.DeliveryEntries<JObject>(ContentfulClient, "dataCountry")
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .Select(e => new
            {
                Iso2Code = e["iso2code"]?.ToString() ?? throw new CliException($"Invalid country code '{e["iso2code"]}'"),
                GeoId = (string.IsNullOrEmpty(e["dataGeoEntry"]?.ToString())
                    ? string.Empty
                    : e.SelectToken("$.dataGeoEntry.sys.id")!.Value<string>())
                    ?? throw new CliException("'dataCountry' id surely can not be empty here?")
            })
            .Where(e => !string.IsNullOrEmpty(e.GeoId))
            .ToDictionary(o => o.Iso2Code, o => o.GeoId);

        _console.WriteNormal("Reading existing geos for states and provinces...");

        var adminCodeToGeoId = ContentfulEntryEnumerator.DeliveryEntries<JObject>(ContentfulClient, "dataGeo",
                queryConfigurator: q => q.FieldEquals("fields.geoType", "state-or-province"))
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .Select(e => new
            {
                Key = e["key"]?.ToString() ?? throw new CliException($"Invalid key value '{e["key"]}'"),
                GeoId = e["$id"]?.ToString() ?? throw new CliException($"Invalid geo identifier '{e["$id"]}'"),
            })
            .Where(e => e.GeoId is not null)
            .ToDictionary(o => o.Key, o => o.GeoId);

        _console.WriteNormal("Reading existing locations...");

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

            WriteStateOrProvinveEntryIfMissing(record, csvWriter, countryToGeoId, adminCodeToGeoId);

            var (tzStandardOffset, tzDaylightSavingOffset) = record.Timezone.ToTimeZoneOffsets();

            var newRecord = new GeoOutputFormat()
            {
                Id = null,
                Key = record.Id,
                Title = $"{record.CountryName} | {record.AdminName} | {record.CityName}",
                Name = record.CityName,
                AlternateNames = record.CityAlternateName.Replace(',', '\u2E32').Replace('|', ','),
                DataGeoParent = adminCodeToGeoId[record.AdminCode],
                GeoType = "city-or-town",
                GeoSubType = string.IsNullOrEmpty(record.Capital)
                    ? (record.PopulationProper > 10000 ? "city" : "town")
                    : $"city:capital:{record.Capital}",
                Lat = record.Lat,
                Lon = record.Lon,
                Population = record.PopulationProper,
                Density = record.Density,
                TimeZoneStandardOffset = tzStandardOffset,
                TimeZoneDaylightSavingsOffset = tzDaylightSavingOffset,
            };

            csvWriter.WriteRecord(newRecord);

            csvWriter.NextRecord();

            recordsWritten++;

            if (recordsRead > nextOutput)
            {
                _console.WriteNormalWithHighlights($"Read {recordsRead} and wrote {recordsWritten} records...", Globals.StyleHeading);
                nextOutput = recordsRead + outputEvery;
                await csvWriter.FlushAsync().ConfigureAwait(false);
            }
        }

        _console.WriteNormalWithHighlights($"Read {recordsRead} and wrote {recordsWritten} records...", Globals.StyleHeading);

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"New data...: {_extractedFile}", Globals.StyleHeading);

        _console.WriteBlankLine();

        return;
    }

    private static void WriteStateOrProvinveEntryIfMissing(SimplemapsGeoInput record, CsvWriter csvWriter,
        Dictionary<string, string> countryToToGeoId, Dictionary<string, string> adminCodeToGeoId)
    {
        if (adminCodeToGeoId.ContainsKey(record.AdminCode))
        {
            return;
        }

        var newRecord = new GeoOutputFormat()
        {
            Id = ContentfulIdGenerator.NewId(),
            Key = record.AdminCode,
            Title = $"{record.CountryName} | {record.AdminName}",
            Name = record.AdminName,
            DataGeoParent = countryToToGeoId[record.CountryIso2],
            GeoType = "state-or-province",
            GeoSubType = record.AdminType,
            Lat = record.Lat,
            Lon = record.Lon,
        };

        csvWriter.WriteRecord(newRecord);

        csvWriter.NextRecord();

        adminCodeToGeoId.Add(record.AdminCode, newRecord.Id);
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

    public class GeoOutputFormat
    {
        public string? Id { get; internal set; } = default!;
        public string Key { get; set; } = default!;
        public string Title { get; internal set; } = default!;
        public string DataGeoParent { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string AlternateNames { get; set; } = default!;
        public string GeoType { get; internal set; } = default!;
        public string? GeoSubType { get; set; } = default!;
        public double Lat { get; set; } = default!;
        public double Lon { get; set; } = default!;
        public int? Population { get; set; } = default!;
        public double? Density { get; set; } = default!;
        public string? TimeZoneStandardOffset { get; set; } = default!;
        public string? TimeZoneDaylightSavingsOffset { get; set; } = default!;
    }
}