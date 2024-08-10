using ICSharpCode.SharpZipLib.Zip;
using System.Text;

namespace Cute.Lib.Extensions;

public static class HttpContentExtensions

{
    public static async Task<string?> ReadAndUnzipAsCsv(this HttpContent content, string? optionalSecret)
    {
        using var zip = new ZipInputStream(await content.ReadAsStreamAsync());

        if (optionalSecret is not null)
        {
            zip.Password = optionalSecret;
        }

        var entry = zip.GetNextEntry();

        if (entry is null) return null;

        Encoding encoding = Encoding.UTF8;

        using var streamReader = new StreamReader(zip, encoding);

        return await streamReader.ReadToEndAsync();
    }
}