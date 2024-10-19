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
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.Utilities;
using Cute.Services;
using Cute.UiComponents;
using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
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
    private readonly List<Entry<JObject>> _newEntryRecords = [];

    private readonly List<GeoOutputFormat> _newRecords = [];

    private readonly HttpClient _httpClient = httpClient;

    private readonly HttpClient _googleClient = googleClient;

    private string _googleApiKey = string.Empty;

    private IEnumerable<GeoInfoCompact>? _adminCodeToGeoId;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-i|--input-file")]
        [Description("The path to the input file or the URL of a password protected ZIP containing CSV data.")]
        public string InputFileOrUrl { get; set; } = string.Empty;

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

        [CommandOption("-a|--apply")]
        [Description("Uploads and applies the changes to Contentful.")]
        public bool Apply { get; set; } = default!;

        [CommandOption("-p|--password")]
        [Description("The password of the online zip file containing CSV data.")]
        public string Password { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.InputFileOrUrl))
        {
            return ValidationResult.Error($"Path to input file '{settings.InputFileOrUrl}' was not found.");
        }
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        if (!AppSettings.GetSettings().TryGetValue("Cute__GoogleApiKey", out _googleApiKey!))
        {
            throw new CliException("Google API key not found in environment variables. (Cute__GoogleApiKey)");
        }

        await FilterAndExtractGeos(settings);

        RemoveSingleLeafHeirarchies();

        if (settings.Apply)
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
                    .WithNewEntries(_newEntryRecords)
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

    private static StreamReader GetStreamReaderFrom7zUrl(string url, string password)
    {
        var httpClient = new HttpClient();
        var response = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
        response.EnsureSuccessStatusCode();

        var responseStream = response.Content.ReadAsStreamAsync().Result;

        // Open the 7z archive using SharpCompress
        using var archive = SevenZipArchive.Open(responseStream, new ReaderOptions { Password = password });

        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory && entry.Key is not null && entry.Key.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var csvStream = new MemoryStream();
                entry.WriteTo(csvStream);
                csvStream.Seek(0, SeekOrigin.Begin);
                return new StreamReader(csvStream);
            }
        }

        throw new FileNotFoundException("CSV file not found in the 7z archive.");
    }

    public static int CsvRecordCount(string fileName)
    {
        return 4_368_038;
    }

    private async Task FilterAndExtractGeos(Settings settings)
    {
        _console.WriteRuler();

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Counting geo universe records in {settings.InputFileOrUrl}...", Globals.StyleHeading);

        var inputEntriesCount = CsvRecordCount(settings.InputFileOrUrl);

        _console.WriteNormalWithHighlights($"Found {inputEntriesCount:N0} entries...", Globals.StyleHeading);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        var inputFileOrUrl = settings.InputFileOrUrl;

        using StreamReader reader = Uri.IsWellFormedUriString(inputFileOrUrl, UriKind.Absolute)
            ? GetStreamReaderFrom7zUrl(inputFileOrUrl, settings.Password)
            : new StreamReader(inputFileOrUrl, System.Text.Encoding.UTF8);

        using var csvReader = new CsvReader(reader, config);

        await csvReader.ReadAsync().ConfigureAwait(false);

        csvReader.Context.RegisterClassMap<SimplemapsGeoMap>();

        csvReader.ReadHeader();

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
                CountryCode = e.Entry.SelectToken($"{prefix}CountryEntry.iso2Code")?.Value<string>(),
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

        var cityToGeoId = ContentfulConnection
            .GetPreviewEntries<GeoInfo>(
                new EntryQuery.Builder()
                    .WithContentType($"{prefix}Geo")
                    .WithQueryConfig(q => q.FieldEquals("fields.geoType", "city-or-town"))
                    .Build()
            )
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .ToDictionary(o => o.Key);

        _console.WriteNormalWithHighlights($"...{cityToGeoId.Count:N0} existing town and city Geos found.", Globals.StyleHeading);

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
            await WriteCountryEntryIfMissing(countryCode!, countryToGeoId, countryCodeToInfo);
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

                    var adminCode = await WriteStateOrProvinveEntryIfMissing(record, countryToGeoId, adminCodeToGeoId);

                    var (tzStandardOffset, tzDaylightSavingOffset) = record.Timezone.ToTimeZoneOffsets();

                    if (cityToGeoId.TryGetValue(record.Id, out GeoInfo? existingEntry))
                    {
                        existingEntry.Count++;
                    }

                    var newRecord = new GeoOutputFormat()
                    {
                        Id = existingEntry?.Sys.Id,
                        Key = record.Id,
                        Title = existingEntry?.Title ?? $"{record.CountryName} | {record.AdminName} | {record.CityName}",
                        Name = existingEntry?.Name ?? record.CityName,
                        AlternateNames = record.CityAlternateName.Replace(',', '\u2E32').Replace('|', ','),
                        DataGeoParent = existingEntry?.DataGeoParent?.Sys.Id ?? adminCodeToGeoId[adminCode].Sys.Id,
                        GeoType = "city-or-town",
                        GeoSubType = string.IsNullOrEmpty(record.Capital)
                            ? record.PopulationProper > 10000 ? "city" : "town"
                            : $"city:capital:{record.Capital}",
                        Lat = existingEntry?.LatLon?.Lat ?? record.Lat,
                        Lon = existingEntry?.LatLon?.Lon ?? record.Lon,
                        Ranking = record.Ranking,
                        Population = existingEntry?.Population ?? record.Population ?? 0,
                        Density = record.Density,
                        TimeZoneStandardOffset = tzStandardOffset,
                        TimeZoneDaylightSavingsOffset = tzDaylightSavingOffset,
                        GooglePlacesId = existingEntry?.GooglePlacesId ?? await GetGooglePlacesId($"{record.CityName}, {record.AdminName}, {record.CountryName}")
                    };

                    _newRecords.Add(newRecord);

                    recordsWritten++;

                    if (recordsRead > nextOutput)
                    {
                        nextOutput = recordsRead + outputEvery;
                    }
                }
                taskExtract.Description = $"{animation[0]} {_console.FormatToMarkup($"Read {recordsRead:N0} and extracted {recordsWritten:N0} records.", Globals.StyleNormal, Globals.StyleHeading)}";
                taskExtract.StopTask();
            });

        _console.WriteBlankLine();

        _console.WriteRuler();

        _console.WriteBlankLine();

        _adminCodeToGeoId = adminCodeToGeoId
            .Select(g => g.Value)
            .Select(g => new GeoInfoCompact
            {
                Id = g.Sys.Id,
                Name = g.Name,
                ParentId = g.DataGeoParent.Sys.Id,
                ParentName = g.DataGeoParent.Name,
                Count = g.Count,
            });

        return;
    }

    private async Task<string> WriteCountryEntryIfMissing(string countryCode,
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
            GooglePlacesId = existingEntry?.GooglePlacesId ?? countryInfo.GooglePlacesId,
        };

        if (existingEntry is null)
        {
            existingEntry = new() { Key = countryCode, Sys = new() { Id = newRecord.Id }, Count = 1 };
            countryGeoInfo.Add(countryCode, existingEntry);
        }

        if (existingEntry.Count == 1)
        {
            newRecord.GooglePlacesId ??= await GetGooglePlacesId(countryInfo.Name);
            _newRecords.Add(newRecord);
        }

        return countryCode;
    }

    private async Task<string> WriteStateOrProvinveEntryIfMissing(SimplemapsGeoInput record,
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
            DataGeoParent = existingEntry?.DataGeoParent?.Sys.Id ?? countryToToGeoId[record.CountryIso2].Sys.Id,
            GeoType = "state-or-province",
            GeoSubType = record.AdminType,
            Lat = existingEntry?.LatLon?.Lat ?? record.Lat,
            Lon = existingEntry?.LatLon?.Lon ?? record.Lon,
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
            _newRecords.Add(newRecord);
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

    private void RemoveSingleLeafHeirarchies()
    {
        var stateAndProvinceCount = _adminCodeToGeoId;

        if (stateAndProvinceCount == null) return;

        var adminCodeToGeoId = stateAndProvinceCount
            .ToDictionary(g => g.Id, g => g);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
        };

        var rewriteLinks = new Dictionary<string, string>();

        var recordsRead = 0;
        var outputEvery = 1000;
        var nextOutput = outputEvery;
        var recordsWritten = 0;

        foreach (var newRecord in _newRecords)
        {
            recordsRead++;

            if (recordsRead > nextOutput)
            {
                _console.WriteNormalWithHighlights($"Read and relinked heirarchies for {recordsRead:N0} entries...", Globals.StyleHeading);
                nextOutput = recordsRead + outputEvery;
            }

            if (newRecord.GeoType == "country")
            {
                var countryEntry = new Entry<JObject>
                {
                    SystemProperties = new SystemProperties
                    {
                        Id = newRecord.Id,
                    },
                    Fields = JObject.FromObject(newRecord)
                };

                _newEntryRecords.Add(countryEntry);

                recordsWritten++;

                continue;
            }

            if (newRecord.GeoType == "state-or-province")
            {
                if (adminCodeToGeoId[newRecord.Id!].Count > 1)
                {
                    var provinceEntry = new Entry<JObject>
                    {
                        SystemProperties = new SystemProperties
                        {
                            Id = newRecord.Id,
                        },
                        Fields = JObject.FromObject(newRecord)
                    };

                    _newEntryRecords.Add(provinceEntry);

                    recordsWritten++;

                    continue;
                }

                // supress this - don't write

                rewriteLinks.Add(newRecord.Id!, newRecord.DataGeoParent!);

                continue;
            }

            // cities-and-towns

            if (rewriteLinks.TryGetValue(newRecord.DataGeoParent, out var newLink))
            {
                newRecord.DataGeoParent = newLink;
            }

            var entry = new Entry<JObject>
            {
                SystemProperties = new SystemProperties
                {
                    Id = newRecord.Id,
                },
                Fields = JObject.FromObject(newRecord)
            };

            recordsWritten++;
        }

        _console.WriteNormalWithHighlights($"{recordsWritten}/{recordsRead} records were re-linked and written.", Globals.StyleHeading);

        _console.WriteBlankLine();

        _console.WriteRuler();
    }

    private async Task<string?> GetGooglePlacesId(string? placeName)
    {
        if (placeName is null) return null;

        if (_newRecords.Count >= 0) return string.Empty;

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