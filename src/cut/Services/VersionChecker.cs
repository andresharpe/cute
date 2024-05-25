using System.Reflection;
using Spectre.Console;

namespace Cut.Services;

public static class VersionChecker
{
    private const string projectReleasePage = "https://github.com/andresharpe/cut/releases/latest";

    public static async Task CheckForLatestVersion()
    {
        try
        {
            var installedVersion = GetInstalledCliVersion();

            using var request = new HttpRequestMessage(HttpMethod.Get, projectReleasePage);

            request.Headers.Add("Accept", "text/html");

            using var client = new HttpClient( new HttpClientHandler { AllowAutoRedirect = false });

            using var response = await client.SendAsync(request);

            using var content = response.Content;

            if(response.StatusCode != System.Net.HttpStatusCode.Found) 
            {
                return;
            }

            if (response.Headers is null)
            {
                return;
            }

            if (response.Headers.Location is null)
            {
                return;
            }

            var latestVersion = response.Headers.Location.Segments.LastOrDefault();

            if (latestVersion is null)
            {
                return;
            }

            if (latestVersion.FirstOrDefault() == 'v')
            {
                latestVersion = latestVersion[1..]; // remove the 'v' prefix. equivalent to `latest.Substring(1, latest.Length - 1)`
            }

            var installedVersionNo = Convert.ToInt32(installedVersion.Replace(".", ""));

            var latestVersionNo = Convert.ToInt32(latestVersion.Replace(".", ""));
            
            if (installedVersionNo < latestVersionNo)
            {
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine(@$"[seagreen1]This version of [bold underline]cut[/] ({installedVersion}) is older than that of the latest version ({latestVersion}). Update the tool for the latest features and bug fixes:[/]");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine(@$"[bold seagreen1]dotnet tool update -g cut[/]");
                AnsiConsole.MarkupLine("");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw;
            // fail silently
        }
    }

    public static string GetInstalledCliVersion()
    {
        var installedVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        installedVersion = installedVersion[0..^2]; 

        return installedVersion;
    }
}
