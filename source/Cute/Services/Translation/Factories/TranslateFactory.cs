using Cute.Lib.Enums;
using Cute.Services.Translation.Interfaces;

namespace Cute.Services.Translation.Factories
{
    public class TranslateFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public TranslateFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITranslator Create(TranslationService service)
        {
            switch (service)
            {
                case TranslationService.Google:
                    return _serviceProvider.GetRequiredService<GoogleTranslator>();
                case TranslationService.Deepl:
                    return _serviceProvider.GetRequiredService<DeeplTranslator>();
                case TranslationService.AzureOpenAi:
                    return _serviceProvider.GetRequiredService<AzureOpenAiTranslator>();
                case TranslationService.TranslateGemma:
                    return _serviceProvider.GetRequiredService<TranslateGemmaTranslator>();
                case TranslationService.Azure:
                default:
                    return _serviceProvider.GetRequiredService<AzureTranslator>();
            }
        }
    }
}
