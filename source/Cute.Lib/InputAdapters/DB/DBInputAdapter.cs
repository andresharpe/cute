using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.Base;
using Cute.Lib.InputAdapters.DB.Model;
using Cute.Lib.Serializers;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Npgsql;
using Scriban;
using System.Data.Common;

namespace Cute.Lib.InputAdapters.DB
{
    public class DBInputAdapter(
        DBDataAdapterConfig adapter,
        ContentfulConnection contentfulConnection,
        ContentLocales contentLocales,
        IReadOnlyDictionary<string, string?> envSettings,
        IEnumerable<ContentType> contentTypes)
        : MappedInputAdapterBase(adapter.connectionString, adapter, contentfulConnection, contentLocales, envSettings, contentTypes)
    {

        public override async Task<int> GetRecordCountAsync()
        {
            if (_results is not null && _results.Count > 0) return _results.Count;

            _contentType = _contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == adapter.ContentType)
                ?? throw new CliException($"Content type '{adapter.ContentType}' does not exist.");

            _serializer = new EntrySerializer(_contentType, _contentLocales);

            var connectionStringDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["connectionString"] = adapter.connectionString });
            var connectionString = connectionStringDict["connectionString"];

            var queryDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["query"] = adapter.query });
            var query = queryDict["query"];

            _results = new List<Dictionary<string, string>>();
            using var connection = GetDbConnection(connectionString);
            await connection.OpenAsync();

            _results.AddRange(MakeDBCall(connection));

            _currentRecordIndex = 0;

            return _results.Count;
        }

        private List<Dictionary<string, string>> MakeDBCall(DbConnection connection)
        {
            var skipTotal = 0;
            var returnValue = new List<Dictionary<string, string>>();
            while (true)
            {
                if (adapter.Pagination is not null)
                {
                    _scriptObject.SetValue(adapter.Pagination.LimitKey, adapter.Pagination.LimitMax, true);
                    _scriptObject.SetValue(adapter.Pagination.SkipKey, skipTotal, true);

                    skipTotal += adapter.Pagination.LimitMax;
                }

                var queryDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["query"] = adapter.query });
                var query = queryDict["query"];

                var hasRows = false;
                foreach (var row in connection.Query(query, buffered: false))
                {
                    hasRows = true;
                    returnValue.AddRange(MapResultValues(JArray.FromObject(new[] { JObject.FromObject(row) })));
                    ActionNotifier?.Invoke($"...returned {returnValue.Count} entries...");
                }

                if (!hasRows)
                {
                    break;
                }
            }

            return returnValue;
        }

        private async Task<List<Dictionary<string, string>>> MakeDBCallsForEnumerators(SqlConnection connection, int level = 0, List<Dictionary<string, string>> returnVal = null!)
        {
            if (_entryEnumerators is null) throw new CliException("No entry enumerators defined.");

            if (level > _entryEnumerators.Length - 1)
            {
                returnVal.AddRange(MakeDBCall(connection) ?? []);

                return returnVal;
            }

            returnVal ??= [];

            Template? filterTemplate = null;

            if (adapter.EnumerateForContentTypes[level].Filter is not null)
            {
                filterTemplate = Template.Parse(adapter.EnumerateForContentTypes[level].Filter);
            }

            var padding = new string(' ', level * 3);

            await foreach (var (obj, _) in _entryEnumerators[level])
            {
                obj.Fields["id"] = obj.SystemProperties.Id;

                string contentType = adapter.EnumerateForContentTypes[level].ContentType;

                _scriptObject.SetValue(contentType, obj.Fields, true);

                var filterResult = filterTemplate?.Render(_scriptObject);

                if (filterTemplate is null ||
                    (filterResult is not null && filterResult.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    ActionNotifier?.Invoke($"{padding}Processing '{contentType}' - '{obj.Fields["title"]?["en"]}'..");

                    _ = await MakeDBCallsForEnumerators(connection, level + 1, returnVal);
                }
                else
                {
                    ActionNotifier?.Invoke($"{padding}Skipping '{contentType}' - '{obj.Fields["title"]?["en"]}'..");
                }

                _scriptObject.Remove(contentType);
            }

            return returnVal;
        }

        private DbConnection GetDbConnection(string connectionString)
        {
            var provider = adapter.provider.ToLowerInvariant();
            switch (provider)
                {
                case "sqlserver":
                    return new SqlConnection(connectionString);
                case "mysql":
                    return new MySqlConnection(connectionString);
                case "postgresql":
                    return new NpgsqlConnection(connectionString);
                case "sqlite":
                    return new SqliteConnection(connectionString);
                // Add other providers as needed
                default:
                    throw new CliException($"Provider '{adapter.provider}' is not supported.");
            }
        }
    }
}
