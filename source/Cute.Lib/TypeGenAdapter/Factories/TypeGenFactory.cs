using Cute.Lib.Enums;
using Cute.Lib.Exceptions;

namespace Cute.Lib.TypeGenAdapter;

public class TypeGenFactory
{
    public static ITypeGenAdapter Create(GenTypeLanguage language)
    {
        return language switch
        {
            GenTypeLanguage.TypeScript => new TypeScriptTypeGenAdapter(),
            GenTypeLanguage.CSharp => new CSharpTypeGenAdapter(),
            GenTypeLanguage.Excel => new ExcelTypeGenAdapter(),
            _ => throw new CliException($"No languge type generator adapter exists matching {language}."),
        };
    }
}