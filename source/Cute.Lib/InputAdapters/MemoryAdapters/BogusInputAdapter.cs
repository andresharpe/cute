using Bogus;
using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentTestData;
using Cute.Lib.Serializers;

namespace Cute.Lib.InputAdapters.MemoryAdapters;

public class BogusInputAdapter : InputAdapterBase
{
    private readonly ContentType _contentType;

    private readonly ContentLocales _contentLocales;

    private readonly int _count;

    private readonly EntrySerializer _serializer;

    private readonly Faker<Location> _testUserLocationFaker;

    private readonly Faker<TestUser> _testUserFaker;

    public new string SourceName => nameof(Bogus);

    public BogusInputAdapter(ContentType contentType, ContentLocales contentLocales, int count)
        : base(nameof(Bogus))
    {
        _contentType = contentType;

        _contentLocales = contentLocales;

        _count = count;

        _testUserLocationFaker = new Faker<Location>()
            .RuleFor(o => o.Lat, f => f.Address.Latitude())
            .RuleFor(o => o.Lon, f => f.Address.Longitude());

        _testUserFaker = new Faker<TestUser>()
            .RuleFor(o => o.Name, f => f.Name.FullName())
            .RuleFor(o => o.Key, (f, u) => f.Internet.Email(u.Name))
            .RuleFor(o => o.Title, (f, u) => u.Name.ToUpper().Replace(' ', '_'))
            .RuleFor(o => o.BirthDate, f => f.Person.DateOfBirth)
            .RuleFor(o => o.Age, (f, u) => DateTime.UtcNow.Year - u.BirthDate.Year)
            .RuleFor(o => o.Location, f => _testUserLocationFaker.Generate());

        _serializer = new EntrySerializer(contentType, contentLocales);
    }

    public override Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        var entryObj = _testUserFaker.Generate();

        var locale = _contentLocales.DefaultLocale;

        var flatEntry = _serializer.CreateNewFlatEntry();
        flatEntry[$"key.{locale}"] = entryObj.Key;
        flatEntry[$"title.{locale}"] = entryObj.Title;
        flatEntry[$"name.{locale}"] = entryObj.Name;
        flatEntry[$"age.{locale}"] = entryObj.Age;
        flatEntry[$"birthDate.{locale}"] = entryObj.BirthDate;
        flatEntry[$"location.{locale}.lat"] = entryObj.Location.Lat;
        flatEntry[$"location.{locale}.lon"] = entryObj.Location.Lon;

        return Task.FromResult<IDictionary<string, object?>?>(flatEntry);
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_count);
    }

    public override async IAsyncEnumerable<IDictionary<string, object?>> GetRecordsAsync()
    {
        Randomizer.Seed = new Random(421319956);

        for (var i = 0; i < _count; i++)
        {
            var record = await GetRecordAsync();

            if (record == null) continue;

            yield return record;
        }
    }
}