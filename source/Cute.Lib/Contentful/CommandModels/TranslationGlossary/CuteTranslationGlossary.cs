using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cute.Lib.Contentful.CommandModels.TranslationGlossary
{
    public class CuteTranslationGlossary
    {
        public Dictionary<string, string> Key { get; set; } = default!;
        public Dictionary<string, string> Title { get; set; } = default!;
    }
}
