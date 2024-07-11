using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.OutputAdapters;

namespace Cute.Lib.TypeGenAdapter;

public class TypeGenFactory
{
    public static ITypeGenAdapter Create(GenTypeLanguage language)
    {
        return language switch
        {
            GenTypeLanguage.TypeScript => new TypeScriptTypeGenAdapter(),
            GenTypeLanguage.CSharp => new CSharpTypeGenAdapter(),
            _ => throw new CliException($"No languge type generator adapter exists matching {language}."),
        };
    }
}