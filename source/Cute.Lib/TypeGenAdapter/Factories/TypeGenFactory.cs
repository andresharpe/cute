using Cute.Lib.Enums;
using Cute.Lib.Exceptions;

namespace Cute.Lib.TypeGenAdapter;

public class TypeGenFactory
{
    public static ITypeGenAdapter Create(GenTypeLanguage language, Func<FormattableString, bool> fileExistsWarningChallenge)
    {
        return language switch
        {
            GenTypeLanguage.TypeScript => new TypeScriptTypeGenAdapter(fileExistsWarningChallenge),
            GenTypeLanguage.CSharp => new CSharpTypeGenAdapter(fileExistsWarningChallenge),
            GenTypeLanguage.Excel => new ExcelTypeGenAdapter(fileExistsWarningChallenge),
            _ => throw new CliException($"No languge type generator adapter exists matching {language}."),
        };
    }
}