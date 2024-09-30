using Contentful.Core;
using Contentful.Core.Configuration;
using Contentful.Core.Errors;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Extensions;
using Cute.Lib.RateLimiters;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful;

public class ContentfulConnection
{
    public ContentfulConnection(HttpClient httpClient,
        IContentfulOptionsProvider contentfulOptionsProvider)
    {
        _httpClient = httpClient;
        _contentfulOptions = contentfulOptionsProvider.GetContentfulOptions();
        Builder.InitializeLazies(this);
    }

    protected ContentfulConnection()
    { }

    private static readonly RateLimiter rateLimiter = new(requestsPerBatch: 7);

    private HttpClient _httpClient = null!;
    private ContentfulOptions _contentfulOptions = null!;
    private ContentfulClient _contentfulDeliveryClient = null!;
    private ContentfulClient _contentfulPreviewClient = null!;
    private ContentfulManagementClient _contentfulManagementClient = null!;

    private Lazy<Task<IEnumerable<ContentType>>> _contentTypes = null!;
    private Lazy<Task<IEnumerable<Locale>>> _locales = null!;
    private Lazy<Task<Locale>> _defaultLocale = null!;
    private Lazy<Task<ContentLocales>> _contentLocales = null!;
    private Lazy<Task<IEnumerable<ContentfulEnvironment>>> _environments = null!;
    private Lazy<Task<ContentfulEnvironment>> _defaultEnvironment = null!;
    private Lazy<Task<IEnumerable<Space>>> _spaces = null!;
    private Lazy<Task<Space>> _defaultSpace = null!;
    private Lazy<Task<IEnumerable<ContentTypeExtended>>> _contentTypesExtended = null!;
    private Lazy<Task<User>> _currentUser = null!;

    // Public interface with async properties
    public ContentfulOptions Options => new()
    {
        BaseUrl = _contentfulOptions.BaseUrl,
        DeliveryApiKey = _contentfulOptions.DeliveryApiKey,
        PreviewApiKey = _contentfulOptions.PreviewApiKey,
        SpaceId = _contentfulOptions.SpaceId,
        Environment = _contentfulOptions.Environment,
        ManagementApiKey = _contentfulOptions.ManagementApiKey
    };

    public string ManagementApiKey => _contentfulOptions.ManagementApiKey;

    public static RateLimiter RateLimiter => rateLimiter;

    public ContentfulGraphQlClient GraphQL { get; private set; } = null!;

    public async Task<IEnumerable<ContentType>> GetContentTypesAsync() => await _contentTypes.Value;

    public async Task<ContentType> GetContentTypeAsync(string contentTypeId) => (await _contentTypes.Value)
        .First(ct => ct.SystemProperties.Id.Equals(contentTypeId));

    public async Task<IEnumerable<Locale>> GetLocalesAsync() => await _locales.Value;

    public async Task<Locale> GetDefaultLocaleAsync() => await _defaultLocale.Value;

    public async Task<ContentLocales> GetContentLocalesAsync() => await _contentLocales.Value;

    public async Task<IEnumerable<ContentfulEnvironment>> GetEnvironmentsAsync() => await _environments.Value;

    public async Task<ContentfulEnvironment> GetDefaultEnvironmentAsync() => await _defaultEnvironment.Value;

    public async Task<IEnumerable<Space>> GetSpacesAsync() => await _spaces.Value;

    public async Task<Space> GetDefaultSpaceAsync() => await _defaultSpace.Value;

    public async Task<User> GetCurrentUserAsync() => await _currentUser.Value;

    public async Task<IEnumerable<ContentTypeExtended>> GetContentTypeExtendedAsync() => await _contentTypesExtended.Value;

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetManagementEntries<T>(EntryQuery entryQuery) where T : class, new()
        => GetEntries(entryQuery, q => _contentfulManagementClient.GetEntriesCollection<T>(q));

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetManagementEntries<T>(ContentType contentType) where T : class, new()
        => GetEntries(new EntryQuery.Builder()
                .WithContentType(contentType)
                .Build(),
            q => _contentfulManagementClient.GetEntriesCollection<T>(q));

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetManagementEntries<T>(string contentTypeId) where T : class, new()
        => GetEntries(new EntryQuery.Builder()
                .WithContentType(contentTypeId)
                .Build(),
            q => _contentfulManagementClient.GetEntriesCollection<T>(q));

    public async Task<Entry<dynamic>> GetManagementEntryAsync(string id,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.GetEntry(id),
                    actionMessage, actionNotifier, errorNotifier);

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetDeliveryEntries<T>(EntryQuery entryQuery) where T : class, new()
        => GetEntries(entryQuery, q => _contentfulDeliveryClient.GetEntries<T>(queryString: q));

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetDeliveryEntries<T>(ContentType contentType) where T : class, new()
    => GetEntries(new EntryQuery.Builder()
            .WithContentType(contentType)
            .Build(),
        q => _contentfulDeliveryClient.GetEntries<T>(queryString: q));

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetDeliveryEntries<T>(string contentTypeId) where T : class, new()
        => GetEntries(new EntryQuery.Builder()
                .WithContentType(contentTypeId)
                .Build(),
            q => _contentfulDeliveryClient.GetEntries<T>(queryString: q));

    public async Task<T> GetDeliveryEntryAsync<T>(string id,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulDeliveryClient.GetEntry<T>(id),
                    actionMessage, actionNotifier, errorNotifier);

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetPreviewEntries<T>(EntryQuery entryQuery) where T : class, new()
        => GetEntries(entryQuery, q => _contentfulPreviewClient.GetEntries<T>(queryString: q));

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetPreviewEntries<T>(ContentType contentType) where T : class, new()
        => GetEntries(new EntryQuery.Builder()
            .WithContentType(contentType)
            .Build(),
        q => _contentfulPreviewClient.GetEntries<T>(queryString: q));

    public async Task<T> GetPreviewEntryAsync<T>(string id,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulPreviewClient.GetEntry<T>(id),
                    actionMessage, actionNotifier, errorNotifier);

    public IAsyncEnumerable<(T Entry, int TotalEntries)> GetPreviewEntries<T>(string contentTypeId) where T : class, new()
        => GetEntries(new EntryQuery.Builder()
                .WithContentType(contentTypeId)
                .Build(),
            q => _contentfulPreviewClient.GetEntries<T>(queryString: q));

    /// <summary>
    /// by default, the key field is "fields.key"
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fieldValue"></param>
    /// <returns></returns>
    public T? GetPreviewEntryByKey<T>(string fieldValue) where T : class, new()
        => GetPreviewEntryByKey<T>("fields.key", fieldValue);

    public T? GetPreviewEntryByKey<T>(string fieldName, string fieldValue) where T : class, new()
    => GetPreviewEntries<T>(
        new EntryQuery.Builder()
            .WithContentType(typeof(T).Name.ToCamelCase())
            .WithQueryConfig(qb => qb.FieldEquals(FixFieldName(fieldName), fieldValue))
            .WithLimit(1)
            .Build()
        )
        .ToBlockingEnumerable()
        .Select(e => e.Entry)
        .FirstOrDefault();

    public IEnumerable<T> GetAllPreviewEntries<T>() where T : class, new()
    => GetPreviewEntries<T>(typeof(T).Name.ToCamelCase())
        .ToBlockingEnumerable()
        .Select(e => e.Entry);

    public async Task DeleteEntryAsync(string id, int version,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.DeleteEntry(id, version),
                    actionMessage, actionNotifier, errorNotifier);

    public async Task CreateOrUpdateEntryAsync<T>(T entry, string id, int? version,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.CreateOrUpdateEntry<T>(entry, id, version: version),
                    actionMessage, actionNotifier, errorNotifier);

    public async Task CreateOrUpdateEntryAsync(Entry<dynamic> entry, int? version,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.CreateOrUpdateEntry(entry, version: version),
                    actionMessage, actionNotifier, errorNotifier);

    public async Task CreateOrUpdateEntryAsync<T>(T entry, string id, int? version, string contentTypeId,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.CreateOrUpdateEntry(entry, id, version: version, contentTypeId: contentTypeId),
                    actionMessage, actionNotifier, errorNotifier);

    public async Task<Entry<dynamic>> PublishEntryAsync(string id, int version,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
        => await RateLimiter.SendRequestAsync(
                    () => _contentfulManagementClient.PublishEntry(id, version: version),
                    actionMessage, actionNotifier, errorNotifier);

    public async Task CreateContentTypeAsync(ContentType contentType)
    {
        if (contentType is null)
        {
            return;
        }

        contentType.Name = contentType.Name
            .RemoveEmojis()
            .Trim();

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        contentType.Metadata = null;

        // end: hack

        contentType = await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.CreateOrUpdateContentType(contentType));

        _ = await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.ActivateContentType(contentType.SystemProperties.Id, 1));

        var contentTypes = await _contentTypes.Value;

        _contentTypes = new(GetContentTypes, true);
    }

    public async Task<ContentType> CloneContentTypeAsync(ContentType contentType, string contentTypeId)
    {
        if (contentType is null)
        {
            return await Task.FromResult(new ContentType());
        }

        var clonedContentType = JObject.FromObject(contentType).DeepClone().ToObject<ContentType>();

        if (clonedContentType is null)
        {
            throw new ArgumentException("This should not occur. Cloning object using Newtonsoft.Json hack failed.");
        }

        clonedContentType.Name = clonedContentType.Name
            .RemoveEmojis()
            .Trim();

        if (clonedContentType.SystemProperties.Id != contentTypeId)
        {
            clonedContentType.SystemProperties.Id = contentTypeId;
            clonedContentType.Name = contentTypeId;
        }

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        clonedContentType.Metadata = null;

        // end: hack

        clonedContentType = await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.CreateOrUpdateContentType(clonedContentType));

        await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.ActivateContentType(contentTypeId, 1));

        _contentTypes = new(GetContentTypes, true);

        return clonedContentType;
    }

    public async Task DeleteContentTypeAsync(ContentType contentType)
    {
        await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.DeactivateContentType(contentType.Id()));

        await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.DeleteContentType(contentType.Id()));

        _contentTypes = new(GetContentTypes, true);
    }

    public async Task CreateOrUpdateContentTypeAsync(ContentType contentType, int? version = null)
    {
        await RateLimiter.SendRequestAsync(() =>
            _contentfulManagementClient.CreateOrUpdateContentType(contentType, version: version));

        _contentTypes = new(GetContentTypes, true);
    }

    public class Builder
    {
        private readonly ContentfulConnection _contentfulConnection = new();

        public Builder WithHttpClient(HttpClient httpClient)
        {
            _contentfulConnection._httpClient = httpClient;
            return this;
        }

        public Builder WithOptionsProvider(IContentfulOptionsProvider contentfulOptionsProvider)
        {
            _contentfulConnection._contentfulOptions = contentfulOptionsProvider.GetContentfulOptions();
            return this;
        }

        public Builder WithOptions(ContentfulOptions contentfulOptions)
        {
            _contentfulConnection._contentfulOptions = contentfulOptions;
            return this;
        }

        public ContentfulConnection Build()
        {
            _ = _contentfulConnection._httpClient
                ?? throw new ArgumentNullException($"Call '{nameof(WithHttpClient)}' before calling '{nameof(Build)}'.");

            _ = _contentfulConnection._contentfulOptions
                ?? throw new ArgumentNullException($"Call '{nameof(WithOptionsProvider)}' before calling '{nameof(Build)}'.");

            InitializeLazies(_contentfulConnection);

            return _contentfulConnection;
        }

        internal static void InitializeLazies(ContentfulConnection contentfulConnection)
        {
            contentfulConnection._contentfulManagementClient =
                new ContentfulManagementClient(contentfulConnection._httpClient, contentfulConnection._contentfulOptions);

            contentfulConnection._contentfulDeliveryClient =
                new ContentfulClient(contentfulConnection._httpClient, contentfulConnection._contentfulOptions);

            contentfulConnection._contentfulPreviewClient =
                new ContentfulClient(contentfulConnection._httpClient, contentfulConnection._contentfulOptions);

            contentfulConnection._contentTypes =
                new(contentfulConnection.GetContentTypes, true);

            contentfulConnection._locales =
                new(contentfulConnection.GetLocales, true);

            contentfulConnection._defaultLocale =
                new(() =>
                    contentfulConnection._locales.Value.ContinueWith(task =>
                        task.Result.First(l => l.Default)
                    ), true);

            contentfulConnection._contentLocales =
                new(contentfulConnection.GetContentLocales, true);

            contentfulConnection._environments =
                new(contentfulConnection.GetEnvironments, true);

            contentfulConnection._defaultEnvironment = new(() =>
                contentfulConnection._environments.Value.ContinueWith(task =>
                    task.Result.First(e =>
                        e.SystemProperties.Id.Equals(contentfulConnection._contentfulOptions.Environment)
                    )
                ), true);

            contentfulConnection._spaces =
                new(contentfulConnection.GetSpaces, true);

            contentfulConnection._defaultSpace = new(() =>
                contentfulConnection._spaces.Value.ContinueWith(task =>
                    task.Result.First(e =>
                        e.SystemProperties.Id.Equals(contentfulConnection._contentfulOptions.SpaceId)
                    )
                ), true);

            contentfulConnection._currentUser =
                new(contentfulConnection.GetCurrentUser, true);

            contentfulConnection._contentTypesExtended =
                new(contentfulConnection.GetContentTypesEntriesCount, true);

            contentfulConnection.GraphQL = new ContentfulGraphQlClient(contentfulConnection, contentfulConnection._httpClient);
        }
    }

    private async Task<IEnumerable<ContentType>> GetContentTypes()
    {
        return await RateLimiter.SendRequestAsync(() => _contentfulManagementClient.GetContentTypes());
    }

    private async Task<IEnumerable<Locale>> GetLocales()
    {
        return await RateLimiter.SendRequestAsync(() => _contentfulManagementClient.GetLocalesCollection());
    }

    private async Task<ContentLocales> GetContentLocales()
    {
        var locales = await _locales.Value;

        var defaultLocale = await _defaultLocale.Value;

        return new ContentLocales(
            locales.Select(l => l.Code).ToArray(),
            defaultLocale.Code
        );
    }

    private async Task<IEnumerable<ContentfulEnvironment>> GetEnvironments()
    {
        return await RateLimiter.SendRequestAsync(() => _contentfulManagementClient.GetEnvironments());
    }

    private async Task<IEnumerable<Space>> GetSpaces()
    {
        return await RateLimiter.SendRequestAsync(() => _contentfulManagementClient.GetSpaces());
    }

    private async Task<User> GetCurrentUser()
    {
        return await RateLimiter.SendRequestAsync(() => _contentfulManagementClient.GetCurrentUser());
    }

    private async Task<IEnumerable<ContentTypeExtended>> GetContentTypesEntriesCount()
    {
        var contentTypes = await _contentTypes.Value;

        var tasks = contentTypes
            .ToDictionary(ct => ct,

                ct =>
                {
                    var queryBuilder = new QueryBuilder<Entry<JObject>>()
                        .ContentTypeIs(ct.SystemProperties.Id)
                        .Include(0)
                        .Limit(0);

                    return RateLimiter.SendRequestAsync(() =>
                    _contentfulManagementClient.GetEntriesCollection(queryBuilder));
                }
            );

        await Task.WhenAll(tasks.Values.ToArray());

        return tasks.Select(kv => new ContentTypeExtended(kv.Key, kv.Value.Result.Total));
    }

    private static async IAsyncEnumerable<(T Entry, int TotalEntries)> GetEntries<T>(
        EntryQuery entryQuery, Func<string, Task<ContentfulCollection<T>>> collectionFetcher) where T : class, new()
    {
        var skip = 0;

        var limit = entryQuery.Limit ?? int.MaxValue;

        while (true)
        {
            var queryString = entryQuery.GetQuery(skip);

            ContentfulCollection<T>? entries = null;
            try
            {
                entries = await RateLimiter.SendRequestAsync(() =>
                    collectionFetcher(queryString));
            }
            catch (Exception ex)
            {
                if (ex.InnerException is ContentfulException ce)
                {
                    if (ce.Message.StartsWith("Response size too big. Maximum allowed response size:"))
                    {
                        entryQuery.HalveThePageSize();
                        continue;
                    }
                }
                throw;
            }

            if (entries is null) yield break;

            if (!entries.Any()) yield break;

            foreach (var entry in entries)
            {
                yield return (Entry: entry, TotalEntries: entries.Total);

                if (--limit == 0) yield break;
            }

            skip += entryQuery.PageSize;
        }
    }

    private static string FixFieldName(string fieldName)
    {
        if (fieldName.StartsWith("fields.")) return fieldName;

        return $"fields.{fieldName}";
    }
}