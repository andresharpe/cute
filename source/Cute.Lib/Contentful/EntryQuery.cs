using Contentful.Core.Models;
using Contentful.Core.Search;
using System.Text;

namespace Cute.Lib.Contentful;

public class EntryQuery
{
    private string _contentTypeId = null!;
    private string _orderByField = null!;
    private int _pageSize = 1000;
    private int? _limit = null!;
    private Action<QueryBuilder<object>>? _queryConfigurator = null!;
    private string? _queryString = null!;
    private int _includeLevels = 2;
    private string _locale = string.Empty;

    public int PageSize => _pageSize;
    public int? Limit => _limit;

    internal int HalveThePageSize()
    {
        _pageSize /= 2;
        return _pageSize;
    }

    public string GetQuery(int skip = 0)
    {
        var queryBuilder = new QueryBuilder<object>()
            .ContentTypeIs(_contentTypeId)
            .Include(_includeLevels)
            .Limit(_pageSize)
            .Skip(skip);

        if (!string.IsNullOrEmpty(_locale))
        {
            queryBuilder.LocaleIs(_locale);
        }

        if (_orderByField != null)
        {
            if (_orderByField.StartsWith("fields."))
            {
                queryBuilder.OrderBy(_orderByField);
            }
            else
            {
                queryBuilder.OrderBy($"fields.{_orderByField}");
            }
        }

        if (_queryConfigurator is not null)
        {
            _queryConfigurator(queryBuilder);
        }

        var fullQueryString = new StringBuilder(queryBuilder.Build());

        if (_queryString is not null)
        {
            fullQueryString.Append('&');
            fullQueryString.Append(_queryString);
        }

        return fullQueryString.ToString();
    }

    public class Builder
    {
        private readonly EntryQuery _entryQuery = new();

        public Builder WithContentType(string contentTypeId)
        {
            _entryQuery._contentTypeId = contentTypeId;
            return this;
        }

        public Builder WithContentType(ContentType contentType)
        {
            _entryQuery._contentTypeId = contentType.SystemProperties.Id;
            _entryQuery._orderByField ??= contentType.DisplayField;

            return this;
        }

        public Builder WithOrderByField(string orderField)
        {
            _entryQuery._orderByField = orderField;
            return this;
        }

        public Builder WithPageSize(int pageSize)
        {
            _entryQuery._pageSize = pageSize;
            return this;
        }

        public Builder WithLimit(int limit)
        {
            _entryQuery._limit = limit;
            return this;
        }

        public Builder WithQueryConfig(Action<QueryBuilder<object>>? queryConfigurator)
        {
            _entryQuery._queryConfigurator = queryConfigurator;
            return this;
        }

        public Builder WithQueryString(string queryString)
        {
            _entryQuery._queryString = queryString;
            return this;
        }

        public Builder WithIncludeLevels(int includeLevels)
        {
            _entryQuery._includeLevels = includeLevels;
            return this;
        }

        public Builder WithLocale(string locale)
        {
            _entryQuery._locale = locale;
            return this;
        }

        public EntryQuery Build()
        {
            _ = _entryQuery._contentTypeId
                ?? throw new ArgumentNullException($"Call '{nameof(WithContentType)}' before calling '{nameof(Build)}'.");

            if (_entryQuery._limit is not null && _entryQuery._limit < _entryQuery._pageSize)
            {
                _entryQuery._pageSize = _entryQuery._limit.Value;
            }

            return _entryQuery;
        }
    }
}