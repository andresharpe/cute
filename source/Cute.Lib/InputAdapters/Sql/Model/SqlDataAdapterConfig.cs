using Cute.Lib.InputAdapters.Base.Models;

namespace Cute.Lib.InputAdapters.Sql.Model
{
    public class SqlDataAdapterConfig : DataAdapterConfigBase
    {
        public string connectionString { get; set; } = default!;
        public string query { get; set; } = default!;
    }
}
