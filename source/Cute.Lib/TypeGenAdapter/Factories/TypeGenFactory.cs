using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;

namespace Cute.Lib.TypeGenAdapter;

public class TypeGenFactory
{
    public static ITypeGenAdapter Create(GenTypeLanguage language, DisplayActions displayActions)
    {
        return language switch
        {
            GenTypeLanguage.TypeScript => new TypeScriptTypeGenAdapter(displayActions),
            GenTypeLanguage.CSharp => new CSharpTypeGenAdapter(displayActions),
            GenTypeLanguage.Excel => new ExcelTypeGenAdapter(displayActions),
            _ => throw new CliException($"No languge type generator adapter exists matching {language}."),
        };
    }
}