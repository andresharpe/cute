using System.Text;
using System.Text.RegularExpressions;
using Cute.Config;
using Cute.Lib.Exceptions;
using Cute.Services.Translation.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cute.Services.Translation;

public class AzureTranslator : ITranslator
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _region;
    private readonly HttpClient _httpClient;
    private readonly string? _categoryId;

    private readonly string _pattern = @"{{.*?}}";

    public AzureTranslator(AppSettings settings, HttpClient httpClient)
    {
        _apiKey = settings.AzureTranslatorApiKey;
        _endpoint = settings.AzureTranslatorEndpoint;
        _region = settings.AzureTranslatorRegion;
        _httpClient = httpClient;
        if(settings.GetSettings().TryGetValue("Cute__AzureTranslationCategory", out var categoryId))
        {
            _categoryId = categoryId;
        }
    }

    public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
    {
        var result = await Translate(fromLanguageCode, [toLanguageCode], textToTranslate);

        return result?.Select(result => new TranslationResponse
        {
            Text = result.Text,
            TargetLanguage = result.To
        }).FirstOrDefault();
    }

    public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
    {
        var result = await Translate(fromLanguageCode, toLanguageCodes, textToTranslate);
        return result?.Select(result => new TranslationResponse
        {
            Text = result.Text,
            TargetLanguage = result.To
        }).ToArray();
    }

    public async Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
    {
        var result = await Translate(fromLanguageCode, toLanguageCodes, textToTranslate, true);
        return result?.Select(result => new TranslationResponse
        {
            Text = result.Text,
            TargetLanguage = result.To
        }).ToArray();
    }

    public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, string toLanguageCode)
    {
        var result = await Translate(fromLanguageCode, [toLanguageCode], textToTranslate, true);

        return result?.Select(result => new TranslationResponse
        {
            Text = result.Text,
            TargetLanguage = result.To
        }).FirstOrDefault();
    }

    private async Task<AzureTranslationResponse[]?> Translate(string fromLanguageCode, IEnumerable<string> toLanguageCodes, string textToTranslate, bool useCustomModel = false)
    {
        var matches = Regex.Matches(textToTranslate, _pattern);
        string processedTextToTranslate = Regex.Replace(textToTranslate, _pattern, match =>
        {
            return $"<mstrans:dictionary translation=\"{match.Value}\">{match.Value}</mstrans:dictionary>";
        });

        var toLanguageCodesUrl = string.Join("", toLanguageCodes.Select(c => $"&to={c}"));

        var route = $"/translate?api-version=3.0&from={fromLanguageCode}{toLanguageCodesUrl}";

        if(useCustomModel)
        {
            if (!string.IsNullOrEmpty(_categoryId))
            {
                route += $"&category={_categoryId}";
            }
            else
            {
                throw new CliException("Category is not provided for custom model translation");
            }
        }

        var body = new object[] { new { Text = processedTextToTranslate } };

        var requestBody = JsonConvert.SerializeObject(body);

        using var request = new HttpRequestMessage();

        request.Method = HttpMethod.Post;

        request.RequestUri = new Uri(_endpoint + route);

        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);

        request.Headers.Add("Ocp-Apim-Subscription-Region", _region);

        HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);

        string result = await response.Content.ReadAsStringAsync();

        var resultAsObject = JsonConvert.DeserializeObject<JArray>(result)?[0]["translations"] as JArray;

        return resultAsObject?.ToObject<AzureTranslationResponse[]>();
    }
}

public class AzureTranslationResponse
{
    public string Text { get; set; } = default!;
    public string To { get; set; } = default!;
}