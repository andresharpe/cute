using System;

namespace Cute.Services.Markdown.Console.Extensions;

public static class ObjectExtensions
{
    public static string ToNotNullString(this object obj) =>
        obj.ToString() ?? string.Empty;
}
