using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services.Translation.Interfaces;
using System.Text;
using System.Text.Json;

namespace Cute.Services.Translation
{
    public class TranslateGemmaTranslator : ITranslator
    {
        private const string MODEL_NAME = "translategemma-27b-it";
        private const string API_ENDPOINT = "http://localhost:1234/api/v1/chat";
        private const int DEFAULT_TIMEOUT_SECONDS = 120;
        
        private readonly HttpClient _httpClient;

        public TranslateGemmaTranslator()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)
            };
        }
        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCodes, null);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await TranslateMultipleLanguages(textToTranslate, fromLanguageCode, toLanguageCodes, null, cuteContentTypeTranslation?.TranslationContext, null);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, Dictionary<string, string>? glossary = null)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCode, null, glossary);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            return await TranslateSingleLanguage(textToTranslate, fromLanguageCode, toLanguageCode, null, cuteContentTypeTranslation?.TranslationContext, glossary);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            var languageCodes = toLanguages.Select(l => l.Iso2Code);
            return await TranslateMultipleLanguages(textToTranslate, fromLanguageCode, languageCodes, null, null, glossaries);
        }

        public async Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            var firstLanguage = toLanguages.FirstOrDefault();
            if (firstLanguage == null) return Array.Empty<TranslationResponse>();
            
            var languageCodes = toLanguages.Select(l => l.Iso2Code);
            return await TranslateMultipleLanguages(textToTranslate, fromLanguageCode, languageCodes, firstLanguage.TranslationContext, null, glossaries);
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null)
        {
            return await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null, glossary);
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            return await TranslateSingleLanguage(textToTranslate, fromLanguageCode, toLanguage.Iso2Code, toLanguage.TranslationContext, cuteContentTypeTranslation?.TranslationContext, glossary);
        }

        private async Task<TranslationResponse[]?> TranslateMultipleLanguages(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, string? languagePrompt, string? contentTypePrompt, Dictionary<string, Dictionary<string, string>>? glossaries)
        {
            var results = new List<TranslationResponse>();
            var toLanguageCodesArray = toLanguageCodes.ToArray();
            
            foreach (var targetLanguage in toLanguageCodesArray)
            {
                // Extract glossary for this specific language
                Dictionary<string, string>? glossary = null;
                if (glossaries != null && glossaries.TryGetValue(targetLanguage, out var langGlossary))
                {
                    glossary = langGlossary;
                }
                
                var singleResult = await TranslateSingleLanguage(textToTranslate, fromLanguageCode, targetLanguage, languagePrompt, contentTypePrompt, glossary);
                
                if (singleResult != null)
                {
                    results.Add(singleResult);
                }
            }
            
            return results.ToArray();
        }

        private async Task<TranslationResponse?> TranslateSingleLanguage(string textToTranslate, string fromLanguageCode, string toLanguageCode, string? languagePrompt, string? contentTypePrompt, Dictionary<string, string>? glossary)
        {
            var systemPrompt = BuildSystemPrompt(fromLanguageCode, toLanguageCode, languagePrompt, contentTypePrompt, glossary);
            
            var requestBody = new
            {
                model = MODEL_NAME,
                system_prompt = systemPrompt,
                input = textToTranslate,
                max_output_tokens = 7900//4096
            };
            
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS));
            
            try
            {
                var response = await _httpClient.PostAsync(API_ENDPOINT, content, cts.Token);
                response.EnsureSuccessStatusCode();
                
                var responseStream = await response.Content.ReadAsStreamAsync();
                var lmStudioResponse = await JsonSerializer.DeserializeAsync<LMStudioResponse>(responseStream);
                
                if (lmStudioResponse?.Output != null && lmStudioResponse.Output.Length > 0)
                {
                    var translatedText = lmStudioResponse.Output[0].Content?.Trim();
                    
                    if (!string.IsNullOrEmpty(translatedText))
                    {
                        return new TranslationResponse
                        {
                            Text = translatedText,
                            TargetLanguage = toLanguageCode
                        };
                    }
                }
                
                return null;
            }
            catch (OperationCanceledException ex)
            {
                var txt = ex.Message;
                return null;
            }
            catch (HttpRequestException ex)
            {
                var txt = ex.Message;
                return null;
            }
        }
        
        private string BuildSystemPrompt(string fromLanguageCode, string toLanguageCode, string? languagePrompt, string? contentTypePrompt, Dictionary<string, string>? glossary)
        {
            var promptParts = new List<string>();
            
            promptParts.Add($"You are a professional translator. Translate the input text from {fromLanguageCode} to {toLanguageCode}.");
            
            if (!string.IsNullOrEmpty(languagePrompt))
            {
                promptParts.Add(languagePrompt);
            }
            
            if (!string.IsNullOrEmpty(contentTypePrompt))
            {
                promptParts.Add(contentTypePrompt);
            }
            
            if (glossary != null && glossary.Count > 0)
            {
                promptParts.Add($"Consider the following glossary ({fromLanguageCode}:{toLanguageCode}) when translating:");
                foreach (var entry in glossary)
                {
                    promptParts.Add($"{entry.Key} : {entry.Value}");
                }
            }
            
            promptParts.Add("Return ONLY the translated text without any additional explanation or formatting.");
            
            return string.Join(" ", promptParts);
        }

        private class LMStudioResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("output")]
            public LMStudioOutputItem[]? Output { get; set; }
        }

        private class LMStudioOutputItem
        {
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string? Type { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
