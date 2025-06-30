using Cute.Lib.InputAdapters.Base.Models;

namespace Cute.Lib.InputAdapters.DB.Model
{
    public class DBDataAdapterConfig : DataAdapterConfigBase
    {
        public string provider { get; set; } = default!;
        public string connectionString { get; set; } = default!;
        public string query { get; set; } = default!;
    }
}
