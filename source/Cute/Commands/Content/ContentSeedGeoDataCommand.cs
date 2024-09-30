using Contentful.Core.Models;
using Contentful.Core.Search;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.FileAdapters;
using Cute.Lib.Utilities;
using Cute.Services;
using Cute.UiComponents;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

using static Cute.Commands.Content.ContentSeedGeoDataCommand;
using File = System.IO.File;

namespace Cute.Commands.Content;

public sealed class ContentSeedGeoDataCommand(IConsoleWriter console, ILogger<ContentSeedGeoDataCommand> logger,
    AppSettings appSettings, HttpClient httpClient, HttpClient googleClient)

    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private string _extractedFile = default!;
    private readonly HttpClient _httpClient = httpClient;
    private readonly HttpClient _googleClient = googleClient;
    private string _googleApiKey = string.Empty;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-i|--input-file")]
        [Description("The path to the input file.")]
        public string InputFile { get; set; } = string.Empty;

        [CommandOption("-o|--output-folder")]
        [Description("The output folder.")]
        public string OutputFolder { get; set; } = string.Empty;

        [CommandOption("-c|--content-type-prefix")]
        [Description("The id of the content type containing location data.")]
        public string ContentTypePrefix { get; set; } = "data";

        [CommandOption("-l|--large-kilometer-radius")]
        [Description("The distance in kilometers for large city to nearest location")]
        public int LargeKilometerRadius { get; set; } = 50;

        [CommandOption("-m|--small-kilometer-radius")]
        [Description("The distance in kilometers for small city to nearest location")]
        public int SmallKilometerRadius { get; set; } = 2;

        [CommandOption("-n|--large-population")]
        [Description("The city or town minimum population for large cities")]
        public int LargePopulation { get; set; } = 10000;

        [CommandOption("-h|--huge-population")]
        [Description("The city or town minimum population for large cities")]
        public int HugePopulation { get; set; } = 40000;

        [CommandOption("-p|--password")]
        [Description("The password to protect the Zip file with")]
        public string Password { get; set; } = default!;

        [CommandOption("-u|--upload")]
        [Description("Uploads the csv file to Contentful.")]
        public bool Upload { get; set; } = default!;

        [CommandOption("-z|--zip")]
        [Description("Output a zip file instead of a csv. Can be password protected with '--password'.")]
        public bool Zip { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.InputFile))
        {
            return ValidationResult.Error($"Path to input file '{settings.InputFile}' was not found.");
        }

        if (!Path.Exists(settings.OutputFolder))
        {
            return ValidationResult.Error($"Path to outout folder '{settings.OutputFolder}' was not found.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        if (!AppSettings.GetSettings().TryGetValue("Cute__GoogleApiKey", out _googleApiKey!))
        {
            throw new CliException("Google API key not found in environment variables. (Cute__GoogleApiKey)");
        }

        _extractedFile = _extractedFile = Path.GetDirectoryName(settings.InputFile) + @"\" +
            Path.GetFileNameWithoutExtension(settings.InputFile) + ".output" +
            Path.GetExtension(settings.InputFile);

        await FilterAndExtractGeos(settings);

        await RemoveSingleLeafHeirarchies();

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

        if (settings.Upload)
        {
            var prefix = settings.ContentTypePrefix;
            var contentTypeId = await ResolveContentTypeId($"{prefix}Geo") ?? throw new CliException($"Content type '{prefix}Geo' not found.");
            var contentType = await GetContentTypeOrThrowError(contentTypeId);
            var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();
            var contentLocales = new ContentLocales([defaultLocale.Code], defaultLocale.Code);

            if (!ConfirmWithPromptChallenge($"upload the extracted data to {contentTypeId}"))
            {
                return -1;
            }

            await PerformBulkOperations([

                new UpsertBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(contentLocales)
                    .WithNewEntries(
                        FileInputAdapterFactory.Create(
                            InputFileFormat.Csv,
                            contentTypeId,
                            _extractedFile
                        ))
                    .WithMatchField(nameof(GeoOutputFormat.Key).ToCamelCase())
                    .WithApplyChanges(true)
                    .WithVerbosity(settings.Verbosity),

                new PublishBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(contentLocales)
                    .WithVerbosity(settings.Verbosity)

            ]);
        }

        return 0;
    }

    public static int CsvRecordCount(string fileName)
    {
        var lineCounter = 0;
        using StreamReader reader = new(fileName, System.Text.Encoding.UTF8);
        while (reader.ReadLine() != null)
        {
            lineCounter++;
        }
        return lineCounter - 1; // ignore header
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
        _console.WriteRuler();

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Counting geo universe records in {settings.InputFile}...", Globals.StyleHeading);

        var inputEntriesCount = CsvRecordCount(settings.InputFile);

        _console.WriteNormalWithHighlights($"Found {inputEntriesCount:N0} entries...", Globals.StyleHeading);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        var inputFile = settings.InputFile;

        using var reader = new StreamReader(inputFile, System.Text.Encoding.UTF8);

        using var csvReader = new CsvReader(reader, config);

        using var writer = new StreamWriter(_extractedFile + ".tmp1.csv", false, System.Text.Encoding.UTF8);

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
        var animateEvery = 25000;
        var nextOutput = outputEvery;
        var prefix = settings.ContentTypePrefix;

        _console.WriteNormalWithHighlights($"Checking Contentful data in {AppSettings.ContentfulDefaultEnvironment}", Globals.StyleHeading);

        var dataLocations = ContentfulConnection.GetDeliveryEntries<JObject>($"{prefix}Location")
            .ToBlockingEnumerable()
            .Select(e => new
            {
                CountryCode = e.Entry.SelectToken($"{prefix}CountryEntry.iso2code")?.Value<string>(),
                Lat = e.Entry.SelectToken("latLng.lat")?.Value<double>() ?? 0.0f,
                Lon = e.Entry.SelectToken("latLng.lon")?.Value<double>() ?? 0.0f,
            })
            .Where(i => i.CountryCode is not null)
            .ToList();

        _console.WriteNormalWithHighlights($"...{dataLocations.Count:N0} locations found.", Globals.StyleHeading);

        var dataCountryCode = dataLocations.Select(o => o.CountryCode).ToHashSet();

        _console.WriteNormalWithHighlights($"...{dataCountryCode.Count:N0} related countries found.", Globals.StyleHeading);

        var countryToGeoId = ContentfulConnection
                .GetPreviewEntries<GeoInfo>(
                    new EntryQuery.Builder()
                        .WithContentType($"{prefix}Geo")
                        .WithQueryConfig(q => q.FieldEquals("fields.geoType", "country"))
                        .Build()
            )
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .Where(e => dataCountryCode.Contains(e.Key))
            .ToDictionary(o => o.Key);

        _console.WriteNormalWithHighlights($"...{countryToGeoId.Count:N0} existing country Geos found.", Globals.StyleHeading);

        var adminCodeToGeoId = ContentfulConnection
            .GetPreviewEntries<GeoInfo>(
                new EntryQuery.Builder()
                    .WithContentType($"{prefix}Geo")
                    .WithQueryConfig(q => q.FieldEquals("fields.geoType", "state-or-province"))
                    .Build()
            )
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .ToDictionary(o => o.Key);

        _console.WriteNormalWithHighlights($"...{adminCodeToGeoId.Count:N0} existing state and province Geos found.", Globals.StyleHeading);

        var countryCodeToInfo = ContentfulConnection
            .GetPreviewEntries<GeoInfo>($"{prefix}Country")
             .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .Where(e => dataCountryCode.Contains(e.Key))
            .ToDictionary(o => o.Key, o => o);

        _console.WriteNormalWithHighlights($"...{countryCodeToInfo.Count:N0} country names read.", Globals.StyleHeading);

        _console.WriteBlankLine();
        _console.WriteRuler();
        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Exporting country geos..", Globals.StyleHeading);

        foreach (var countryCode in dataCountryCode)
        {
            await WriteCountryEntryIfMissing(countryCode!, csvWriter, countryToGeoId, countryCodeToInfo);
        }

        _console.WriteNormalWithHighlights($"Extracting from geo universe..", Globals.StyleHeading);

        List<string> animation = [
            Emoji.Known.GlobeShowingAsiaAustralia,
            Emoji.Known.GlobeShowingEuropeAfrica,
            Emoji.Known.GlobeShowingAmericas
        ];

        var animationFrame = 0;

        await ProgressBars.Instance()
            .AutoClear(false)
            .StartAsync(async ctx =>
        {
            var taskExtract = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{animation[animationFrame++]} Extracting data..[/]");

            taskExtract.MaxValue = inputEntriesCount;

            while (await csvReader.ReadAsync().ConfigureAwait(false))
            {
                var record = csvReader.GetRecord<SimplemapsGeoInput>();

                recordsRead++;
                taskExtract.Increment(1);
                if (--animateEvery < 1)
                {
                    animateEvery = 25000;
                    taskExtract.Description = $"{animation[animationFrame++]} {_console.FormatToMarkup($"...records extracted: {recordsWritten:N0} from {recordsRead:N0}", Globals.StyleNormal, Globals.StyleHeading)}..";
                    if (animationFrame >= animation.Count) animationFrame = 0;
                }

                if (record.SameName) continue;

                if (!dataCountryCode.Contains(record.CountryIso2)) continue;

                if (record.Ranking > 2 && (record.Population is null || record.Population < settings.HugePopulation))
                {
                    continue;
                }

                var boundingBoxNear = Haversine.GetBoundingBox(record.Lon, record.Lat, settings.LargeKilometerRadius);

                var nearLocations = dataLocations
                    .Any(l => boundingBoxNear.Contains(l.Lon, l.Lat));

                if (!nearLocations) continue;

                if (record.Population is null || record.Population < settings.LargePopulation)
                {
                    var boundingBoxVeryNear = Haversine.GetBoundingBox(record.Lon, record.Lat, settings.SmallKilometerRadius);

                    var veryNearLocations = dataLocations
                        .Any(l => boundingBoxVeryNear.Contains(l.Lon, l.Lat));

                    if (!veryNearLocations) continue;
                }

                var adminCode = await WriteStateOrProvinveEntryIfMissing(record, csvWriter, countryToGeoId, adminCodeToGeoId);

                var (tzStandardOffset, tzDaylightSavingOffset) = record.Timezone.ToTimeZoneOffsets();

                var newRecord = new GeoOutputFormat()
                {
                    Id = null,
                    Key = record.Id,
                    Title = $"{record.CountryName} | {record.AdminName} | {record.CityName}",
                    Name = record.CityName,
                    AlternateNames = record.CityAlternateName.Replace(',', '\u2E32').Replace('|', ','),
                    DataGeoParent = adminCodeToGeoId[adminCode].Sys.Id,
                    GeoType = "city-or-town",
                    GeoSubType = string.IsNullOrEmpty(record.Capital)
                        ? record.PopulationProper > 10000 ? "city" : "town"
                        : $"city:capital:{record.Capital}",
                    Lat = record.Lat,
                    Lon = record.Lon,
                    Ranking = record.Ranking,
                    Population = record.Population ?? 0,
                    Density = record.Density,
                    TimeZoneStandardOffset = tzStandardOffset,
                    TimeZoneDaylightSavingsOffset = tzDaylightSavingOffset,
                    GooglePlacesId = await GetGooglePlacesId($"{record.CityName}, {record.AdminName}, {record.CountryName}")
                };

                csvWriter.WriteRecord(newRecord);

                csvWriter.NextRecord();

                recordsWritten++;

                if (recordsRead > nextOutput)
                {
                    nextOutput = recordsRead + outputEvery;
                    await csvWriter.FlushAsync().ConfigureAwait(false);
                }
            }
            taskExtract.Description = $"{animation[0]} {_console.FormatToMarkup($"Read {recordsRead:N0} and extracted {recordsWritten:N0} records.", Globals.StyleNormal, Globals.StyleHeading)}";
            taskExtract.StopTask();
        });

        _console.WriteBlankLine();

        _console.WriteRuler();

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"New data...: {_extractedFile}", Globals.StyleHeading);

        _console.WriteBlankLine();

        _console.WriteRuler();

        File.WriteAllText(_extractedFile + ".heirarchy.json", JsonConvert.SerializeObject(adminCodeToGeoId
            .Select(g => g.Value)
            .Select(g => new GeoInfoCompact
            {
                Id = g.Sys.Id,
                Name = g.Name,
                ParentId = g.DataGeoParent.Sys.Id,
                ParentName = g.DataGeoParent.Name,
                Count = g.Count,
            })
        ));

        return;
    }

    private async Task<string> WriteCountryEntryIfMissing(string countryCode,
        CsvWriter csvWriter,
        Dictionary<string, GeoInfo> countryGeoInfo,
        Dictionary<string, GeoInfo> countryInfoList)
    {
        if (countryGeoInfo.TryGetValue(countryCode, out GeoInfo? existingEntry))
        {
            existingEntry.Count++;
        }

        var countryInfo = countryInfoList[countryCode];

        var newRecord = new GeoOutputFormat()
        {
            Id = existingEntry?.Sys.Id ?? ContentfulIdGenerator.NewId(),
            Key = countryCode,
            Title = countryInfo.Name,
            Name = countryInfo.Name,
            Lat = countryInfo.LatLon.Lat,
            Lon = countryInfo.LatLon.Lon,
            Population = countryInfo.Population,
            GeoType = "country",
            GooglePlacesId = countryInfo.GooglePlacesId,
            // DataGeoParent = "todo"" // will be "Americas", "Asiapac" etc. can probably be setup manually
        };

        if (existingEntry is null)
        {
            existingEntry = new() { Key = countryCode, Sys = new() { Id = newRecord.Id }, Count = 1 };
            countryGeoInfo.Add(countryCode, existingEntry);
        }

        if (existingEntry.Count == 1)
        {
            newRecord.GooglePlacesId ??= await GetGooglePlacesId(countryInfo.Name);
            csvWriter.WriteRecord(newRecord);
            csvWriter.NextRecord();
        }

        return countryCode;
    }

    private async Task<string> WriteStateOrProvinveEntryIfMissing(SimplemapsGeoInput record, CsvWriter csvWriter,
        Dictionary<string, GeoInfo> countryToToGeoId, Dictionary<string, GeoInfo> adminCodeToGeoId)
    {
        if (record.AdminType.StartsWith("London borough"))
        {
            record.AdminCode = "GB-LND";
            record.AdminName = "London";
        }

        var adminCode = string.IsNullOrEmpty(record.AdminCode)
                ? $"{record.CountryName}|{record.CityName}".ToUpper()
                : record.AdminCode.Trim();

        var adminName = string.IsNullOrEmpty(record.AdminName)
                ? record.CityName.ToUpper()
                : record.AdminName;

        if (adminCodeToGeoId.TryGetValue(adminCode, out GeoInfo? existingEntry))
        {
            existingEntry.Count++;
        }

        var newRecord = new GeoOutputFormat()
        {
            Id = existingEntry?.Sys.Id ?? ContentfulIdGenerator.NewId(),
            Key = existingEntry?.Key ?? adminCode,
            Title = existingEntry?.Title ?? $"{record.CountryName} | {adminName} ({adminCode})",
            Name = existingEntry?.Name ?? record.AdminName,
            DataGeoParent = existingEntry?.DataGeoParent.Sys.Id ?? countryToToGeoId[record.CountryIso2].Sys.Id,
            GeoType = "state-or-province",
            GeoSubType = record.AdminType,
            Lat = record.Lat,
            Lon = record.Lon,
            GooglePlacesId = existingEntry?.GooglePlacesId,
        };

        if (existingEntry is null)
        {
            existingEntry = new()
            {
                Key = newRecord.Key,
                Sys = new() { Id = newRecord.Id },
                Title = newRecord.Title,
                Name = newRecord.Name,
                Count = 1,
                DataGeoParent = new()
                {
                    Sys = new() { Id = countryToToGeoId[record.CountryIso2].Sys.Id }
                }
            };

            adminCodeToGeoId.Add(adminCode, existingEntry);
        }

        if (existingEntry.Count == 1)
        {
            newRecord.GooglePlacesId ??= await GetGooglePlacesId($"{adminName}, {record.CountryName}");
            csvWriter.WriteRecord(newRecord);
            csvWriter.NextRecord();
        }

        return adminCode;
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

    public class ValidationInputFormat
    {
        public string Country { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int? GeoId { get; set; } = default!;
        public int? ParentGeoId { get; set; } = default!;
        public double? Lat { get; set; } = default!;
        public double? Lon { get; set; } = default!;
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
        [Name("sys.Id")]
        public string? Id { get; set; } = default!;

        [Name("key.en")]
        public string Key { get; set; } = default!;

        [Name("title.en")]
        public string Title { get; set; } = default!;

        [Name("dataGeoParent.en")]
        public string DataGeoParent { get; set; } = default!;

        [Name("name.en")]
        public string Name { get; set; } = default!;

        [Name("alternateNames.en[]")]
        public string AlternateNames { get; set; } = default!;

        [Name("geoType.en")]
        public string GeoType { get; set; } = default!;

        [Name("geoSubType.en")]
        public string? GeoSubType { get; set; } = default!;

        [Name("latLong.en.lat")]
        public double Lat { get; set; } = default!;

        [Name("latLong.en.lon")]
        public double Lon { get; set; } = default!;

        [Name("ranking.en")]
        public int Ranking { get; set; } = default!;

        [Name("population.en")]
        public int? Population { get; set; } = default!;

        [Name("density.en")]
        public double? Density { get; set; } = default!;

        [Name("timeZoneStandardOffset.en")]
        public string? TimeZoneStandardOffset { get; set; } = default!;

        [Name("timeZoneDaylightSavingsOffset.en")]
        public string? TimeZoneDaylightSavingsOffset { get; set; } = default!;

        [Name("googlePlacesId.en")]
        public string? GooglePlacesId { get; set; } = default!;
    }

    public class GeoInfo()
    {
        public SystemProperties Sys { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Name { get; set; } = default!;
        public GeoInfo DataGeoParent { get; set; } = default!;
        public Location LatLon { get; set; } = default!;
        public int? Population { get; set; } = 0;
        public string? GooglePlacesId { get; set; } = default!;
        public int Count { get; set; } = 0;
    }

    public class GeoInfoCompact
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string ParentId { get; set; } = default!;
        public string ParentName { get; set; } = default!;
        public int Count { get; set; } = 0;
    }

    private async Task RemoveSingleLeafHeirarchies()
    {
        var stateAndProvinceCount = JsonConvert.DeserializeObject<List<GeoInfoCompact>>(
            File.ReadAllText(_extractedFile + ".heirarchy.json")
        );

        if (stateAndProvinceCount == null) return;

        var adminCodeToGeoId = stateAndProvinceCount
            .ToDictionary(g => g.Id, g => g);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        using var reader = new StreamReader(_extractedFile + ".tmp1.csv", System.Text.Encoding.UTF8);

        using var csvReader = new CsvReader(reader, config);

        using var writer = new StreamWriter(_extractedFile, false, System.Text.Encoding.UTF8);

        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csvReader.ReadAsync().ConfigureAwait(false);

        csvReader.ReadHeader();

        csvWriter.WriteHeader<GeoOutputFormat>();

        csvWriter.NextRecord();

        var rewriteLinks = new Dictionary<string, string>();

        var recordsRead = 0;
        var outputEvery = 1000;
        var nextOutput = outputEvery;
        var recordsWritten = 0;

        while (await csvReader.ReadAsync().ConfigureAwait(false))
        {
            var record = csvReader.GetRecord<GeoOutputFormat>();

            recordsRead++;

            if (recordsRead > nextOutput)
            {
                _console.WriteNormalWithHighlights($"Read and relinked heirarchies for {recordsRead:N0} entries...", Globals.StyleHeading);
                nextOutput = recordsRead + outputEvery;
            }

            if (record.GeoType == "country")
            {
                csvWriter.WriteRecord(record);

                csvWriter.NextRecord();

                recordsWritten++;

                continue;
            }

            if (record.GeoType == "state-or-province")
            {
                if (adminCodeToGeoId[record.Id!].Count > 1)
                {
                    csvWriter.WriteRecord(record);

                    csvWriter.NextRecord();

                    recordsWritten++;

                    continue;
                }

                // supress this - don't write

                rewriteLinks.Add(record.Id!, record.DataGeoParent!);

                continue;
            }

            // cities-and-towns

            if (rewriteLinks.TryGetValue(record.DataGeoParent, out var newLink))
            {
                record.DataGeoParent = newLink;
            }

            csvWriter.WriteRecord(record);

            csvWriter.NextRecord();

            recordsWritten++;
        }

        _console.WriteNormalWithHighlights($"{recordsWritten}/{recordsRead} records were re-linked and written.", Globals.StyleHeading);

        _console.WriteBlankLine();

        _console.WriteRuler();
    }

    private async Task<string?> GetGooglePlacesId(string? placeName)
    {
        if (placeName is null) return null;

        var retries = 0;
        var success = false;

        try
        {
            var url = $"https://maps.googleapis.com/maps/api/place/textsearch/json?query={Uri.EscapeDataString(placeName)}&key={_googleApiKey}";

            while (!success)
            {
                var response = await _googleClient.GetAsync(url);

                success = response.IsSuccessStatusCode;

                if (!success)
                {
                    if (++retries < 4)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                    throw new CliException($"Google Places API request failed with status code {response.StatusCode}");
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(responseBody);

                return json["results"]?[0]?["place_id"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _console.WriteNormalWithHighlights($"Google Places API request failed for '{placeName}': {ex.Message}. {ex.InnerException?.Message}", Globals.StyleHeading);
        }
        return null;
    }
}