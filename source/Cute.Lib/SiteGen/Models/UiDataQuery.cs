using Contentful.Core.Models;

namespace Cute.Lib.SiteGen.Models;

public class UiDataQuery
{
   public string Key {get; set;} = default!;
   public string Title {get; set;} = default!;
   public string Query {get; set;} = default!;
   public string JsonSelector {get; set;} = default!;
   public string VariablePrefix {get; set;} = default!;
}
