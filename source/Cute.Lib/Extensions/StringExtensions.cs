using System.Text;
using System.Text.RegularExpressions;

namespace Cute.Lib.Extensions;

public static partial class StringExtensions
{
    public static string CamelToPascalCase(this string value)
    {
        return Char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string ToGraphQLCase(this string value)
    {
        var sb = new StringBuilder();
        var isLastCharADigit = false;
        foreach (var ch in value)
        {
            sb.Append(isLastCharADigit ? char.ToUpperInvariant(ch) : ch);
            isLastCharADigit = char.IsDigit(ch);
        }
        return sb.ToString();
    }

    public static string[] SplitCamelCase(this string input)
    {
        return UpperCaseRegex().Replace(input, ";$1").Trim().Split(';');
    }

    public static string? UnQuote(this string? input)
    {
        if (input == null) return null;
        if (input.Length >= 2 && input[0] == '"' && input[^1] == '"')
        {
            return input[1..^1]; // Using range syntax to slice the string.
        }
        return input;
    }

    public static string RemoveFromEnd(this string original, string toRemove)
    {
        if (original.EndsWith(toRemove))
        {
            return original[..^toRemove.Length];
        }
        return original;
    }

    public static string ToSlug(this string phrase)
    {
        string str = phrase.RemoveAccent().ToLower();
        // invalid chars
        str = InvalidChars().Replace(str, "");
        // convert multiple spaces into one space
        str = MultiWhiteSpace().Replace(str, " ").Trim();
        // cut and trim
        str = str[..(str.Length <= 45 ? str.Length : 45)].Trim();
        str = WhiteSpace().Replace(str, "-"); // hyphens
        return str;
    }

    private static string RemoveAccent(this string text)
    {
        byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(text);
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    public static string RemoveEmojis(this string text)
    {
        return EmojiAndOtherUnicode().Replace(text, "");
    }

    public static string RemoveNewLines(this string text)
    {
        if (text.Contains('\n'))
        {
            text = text.Replace("\n", "");
        }

        if (text.Contains('\r'))
        {
            text = text.Replace("\r", "");
        }

        return text;
    }

    public static string Snip(this string text, int snipTo)
    {
        if (text.Length <= snipTo) return text;

        return text[..(snipTo - 1)] + "..";
    }

    public static IEnumerable<string> GetFixedLines(this ReadOnlySpan<char> input, int maxLength = 80, int? maxFirstLineLength = null)
    {
        var lines = new List<string>();

        if (maxFirstLineLength is not null && input.Length > maxFirstLineLength)
        {
            var length = Math.Min(maxFirstLineLength.Value, input.Length);
            var slice = input[..length];
            var lastBreakIndex = slice.LastIndexOfAny(' ', '\n', '\r');
            if (lastBreakIndex == -1)
            {
                lines.Add(string.Empty);
                maxFirstLineLength = maxLength;
            }
        }

        maxFirstLineLength ??= maxLength;

        while (!input.IsEmpty)
        {
            int maxLineLength = lines.Count == 0
                ? maxFirstLineLength.Value
                : maxLength;

            // Find the maximum slice we can take for this line
            var length = Math.Min(maxLineLength, input.Length);
            var slice = input[..length]; // Using the range operator here for slicing

            // Find the first occurrence of \r or \n
            var firstNewlineIndex = slice.IndexOfAny('\r', '\n');
            // Find the last occurrence of a space
            var lastSpaceIndex = slice.LastIndexOf(' ');

            if (lastSpaceIndex != -1 && firstNewlineIndex > lastSpaceIndex)
            {
                lastSpaceIndex = -1;
            }

            if (lastSpaceIndex != -1 && length < maxLineLength)
            {
                lastSpaceIndex = -1;
            }

            // Break at the first newline character
            if (firstNewlineIndex != -1 && (firstNewlineIndex < lastSpaceIndex || lastSpaceIndex == -1))
            {
                lines.Add(slice[..firstNewlineIndex].ToString());

                // Handle \r\n as a single line break
                if (firstNewlineIndex + 1 < input.Length && input[firstNewlineIndex] == '\r' && input[firstNewlineIndex + 1] == '\n')
                    input = input[(firstNewlineIndex + 2)..]; // Skip \r\n using range
                else
                    input = input[(firstNewlineIndex + 1)..]; // Skip \r or \n using range

                continue;
            }

            // Break at the last space if no newline is found earlier
            if (lastSpaceIndex != -1)
            {
                lines.Add(slice[..lastSpaceIndex].ToString());
                input = input[(lastSpaceIndex + 1)..]; // Skip the space using range
                continue;
            }

            // If no space or newline was found, just break at max length
            lines.Add(slice.ToString());
            input = input[length..]; // Move to the next chunk using range operator
        }

        return lines;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhiteSpace();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidChars();

    [GeneratedRegex(@"\s")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"[\u0000-\u0008\u000A-\u001F\u0100-\uFFFF]")]
    private static partial Regex EmojiAndOtherUnicode();

    public static (string StandardOffset, string DaylightSavingOffset) ToTimeZoneOffsets(this string timeZoneName)
    {
        return _timeZoneLookup[timeZoneName];
    }

    internal static Dictionary<string, (string StandardOffset, string DaylightSavingOffset)> _timeZoneLookup =
        new()
        {
            ["Africa/Abidjan"] = new("+00:00", "+00:00"),
            ["Africa/Accra"] = new("+00:00", "+00:00"),
            ["Africa/Addis_Ababa"] = new("+03:00", "+03:00"),
            ["Africa/Algiers"] = new("+01:00", "+01:00"),
            ["Africa/Asmara"] = new("+03:00", "+03:00"),
            ["Africa/Asmera"] = new("+03:00", "+03:00"),
            ["Africa/Bamako"] = new("+00:00", "+00:00"),
            ["Africa/Bangui"] = new("+01:00", "+01:00"),
            ["Africa/Banjul"] = new("+00:00", "+00:00"),
            ["Africa/Bissau"] = new("+00:00", "+00:00"),
            ["Africa/Blantyre"] = new("+02:00", "+02:00"),
            ["Africa/Brazzaville"] = new("+01:00", "+01:00"),
            ["Africa/Bujumbura"] = new("+02:00", "+02:00"),
            ["Africa/Cairo"] = new("+02:00", "+03:00"),
            ["Africa/Casablanca"] = new("+01:00", "+00:00"),
            ["Africa/Ceuta"] = new("+01:00", "+02:00"),
            ["Africa/Conakry"] = new("+00:00", "+00:00"),
            ["Africa/Dakar"] = new("+00:00", "+00:00"),
            ["Africa/Dar_es_Salaam"] = new("+03:00", "+03:00"),
            ["Africa/Djibouti"] = new("+03:00", "+03:00"),
            ["Africa/Douala"] = new("+01:00", "+01:00"),
            ["Africa/El_Aaiun"] = new("+01:00", "+00:00"),
            ["Africa/Freetown"] = new("+00:00", "+00:00"),
            ["Africa/Gaborone"] = new("+02:00", "+02:00"),
            ["Africa/Harare"] = new("+02:00", "+02:00"),
            ["Africa/Johannesburg"] = new("+02:00", "+02:00"),
            ["Africa/Juba"] = new("+02:00", "+02:00"),
            ["Africa/Kampala"] = new("+03:00", "+03:00"),
            ["Africa/Khartoum"] = new("+02:00", "+02:00"),
            ["Africa/Kigali"] = new("+02:00", "+02:00"),
            ["Africa/Kinshasa"] = new("+01:00", "+01:00"),
            ["Africa/Lagos"] = new("+01:00", "+01:00"),
            ["Africa/Libreville"] = new("+01:00", "+01:00"),
            ["Africa/Lome"] = new("+00:00", "+00:00"),
            ["Africa/Luanda"] = new("+01:00", "+01:00"),
            ["Africa/Lubumbashi"] = new("+02:00", "+02:00"),
            ["Africa/Lusaka"] = new("+02:00", "+02:00"),
            ["Africa/Malabo"] = new("+01:00", "+01:00"),
            ["Africa/Maputo"] = new("+02:00", "+02:00"),
            ["Africa/Maseru"] = new("+02:00", "+02:00"),
            ["Africa/Mbabane"] = new("+02:00", "+02:00"),
            ["Africa/Mogadishu"] = new("+03:00", "+03:00"),
            ["Africa/Monrovia"] = new("+00:00", "+00:00"),
            ["Africa/Nairobi"] = new("+03:00", "+03:00"),
            ["Africa/Ndjamena"] = new("+01:00", "+01:00"),
            ["Africa/Niamey"] = new("+01:00", "+01:00"),
            ["Africa/Nouakchott"] = new("+00:00", "+00:00"),
            ["Africa/Ouagadougou"] = new("+00:00", "+00:00"),
            ["Africa/Porto-Novo"] = new("+01:00", "+01:00"),
            ["Africa/Sao_Tome"] = new("+00:00", "+00:00"),
            ["Africa/Timbuktu"] = new("+00:00", "+00:00"),
            ["Africa/Tripoli"] = new("+02:00", "+02:00"),
            ["Africa/Tunis"] = new("+01:00", "+01:00"),
            ["Africa/Windhoek"] = new("+02:00", "+02:00"),
            ["America/Adak"] = new("−10:00", "−09:00"),
            ["America/Anchorage"] = new("−09:00", "−08:00"),
            ["America/Anguilla"] = new("−04:00", "−04:00"),
            ["America/Antigua"] = new("−04:00", "−04:00"),
            ["America/Araguaina"] = new("−03:00", "−03:00"),
            ["America/Argentina/Buenos_Aires"] = new("−03:00", "−03:00"),
            ["America/Argentina/Catamarca"] = new("−03:00", "−03:00"),
            ["America/Argentina/ComodRivadavia"] = new("−03:00", "−03:00"),
            ["America/Argentina/Cordoba"] = new("−03:00", "−03:00"),
            ["America/Argentina/Jujuy"] = new("−03:00", "−03:00"),
            ["America/Argentina/La_Rioja"] = new("−03:00", "−03:00"),
            ["America/Argentina/Mendoza"] = new("−03:00", "−03:00"),
            ["America/Argentina/Rio_Gallegos"] = new("−03:00", "−03:00"),
            ["America/Argentina/Salta"] = new("−03:00", "−03:00"),
            ["America/Argentina/San_Juan"] = new("−03:00", "−03:00"),
            ["America/Argentina/San_Luis"] = new("−03:00", "−03:00"),
            ["America/Argentina/Tucuman"] = new("−03:00", "−03:00"),
            ["America/Argentina/Ushuaia"] = new("−03:00", "−03:00"),
            ["America/Aruba"] = new("−04:00", "−04:00"),
            ["America/Asuncion"] = new("−04:00", "−03:00"),
            ["America/Atikokan"] = new("−05:00", "−05:00"),
            ["America/Atka"] = new("−10:00", "−09:00"),
            ["America/Bahia"] = new("−03:00", "−03:00"),
            ["America/Bahia_Banderas"] = new("−06:00", "−06:00"),
            ["America/Barbados"] = new("−04:00", "−04:00"),
            ["America/Belem"] = new("−03:00", "−03:00"),
            ["America/Belize"] = new("−06:00", "−06:00"),
            ["America/Blanc-Sablon"] = new("−04:00", "−04:00"),
            ["America/Boa_Vista"] = new("−04:00", "−04:00"),
            ["America/Bogota"] = new("−05:00", "−05:00"),
            ["America/Boise"] = new("−07:00", "−06:00"),
            ["America/Buenos_Aires"] = new("−03:00", "−03:00"),
            ["America/Cambridge_Bay"] = new("−07:00", "−06:00"),
            ["America/Campo_Grande"] = new("−04:00", "−04:00"),
            ["America/Cancun"] = new("−05:00", "−05:00"),
            ["America/Caracas"] = new("−04:00", "−04:00"),
            ["America/Catamarca"] = new("−03:00", "−03:00"),
            ["America/Cayenne"] = new("−03:00", "−03:00"),
            ["America/Cayman"] = new("−05:00", "−05:00"),
            ["America/Chicago"] = new("−06:00", "−05:00"),
            ["America/Chihuahua"] = new("−06:00", "−06:00"),
            ["America/Ciudad_Juarez"] = new("−07:00", "−06:00"),
            ["America/Coral_Harbour"] = new("−05:00", "−05:00"),
            ["America/Cordoba"] = new("−03:00", "−03:00"),
            ["America/Costa_Rica"] = new("−06:00", "−06:00"),
            ["America/Creston"] = new("−07:00", "−07:00"),
            ["America/Cuiaba"] = new("−04:00", "−04:00"),
            ["America/Curacao"] = new("−04:00", "−04:00"),
            ["America/Danmarkshavn"] = new("+00:00", "+00:00"),
            ["America/Dawson"] = new("−07:00", "−07:00"),
            ["America/Dawson_Creek"] = new("−07:00", "−07:00"),
            ["America/Denver"] = new("−07:00", "−06:00"),
            ["America/Detroit"] = new("−05:00", "−04:00"),
            ["America/Dominica"] = new("−04:00", "−04:00"),
            ["America/Edmonton"] = new("−07:00", "−06:00"),
            ["America/Eirunepe"] = new("−05:00", "−05:00"),
            ["America/El_Salvador"] = new("−06:00", "−06:00"),
            ["America/Ensenada"] = new("−08:00", "−07:00"),
            ["America/Fort_Nelson"] = new("−07:00", "−07:00"),
            ["America/Fort_Wayne"] = new("−05:00", "−04:00"),
            ["America/Fortaleza"] = new("−03:00", "−03:00"),
            ["America/Glace_Bay"] = new("−04:00", "−03:00"),
            ["America/Godthab"] = new("−02:00", "−01:00"),
            ["America/Goose_Bay"] = new("−04:00", "−03:00"),
            ["America/Grand_Turk"] = new("−05:00", "−04:00"),
            ["America/Grenada"] = new("−04:00", "−04:00"),
            ["America/Guadeloupe"] = new("−04:00", "−04:00"),
            ["America/Guatemala"] = new("−06:00", "−06:00"),
            ["America/Guayaquil"] = new("−05:00", "−05:00"),
            ["America/Guyana"] = new("−04:00", "−04:00"),
            ["America/Halifax"] = new("−04:00", "−03:00"),
            ["America/Havana"] = new("−05:00", "−04:00"),
            ["America/Hermosillo"] = new("−07:00", "−07:00"),
            ["America/Indiana/Indianapolis"] = new("−05:00", "−04:00"),
            ["America/Indiana/Knox"] = new("−06:00", "−05:00"),
            ["America/Indiana/Marengo"] = new("−05:00", "−04:00"),
            ["America/Indiana/Petersburg"] = new("−05:00", "−04:00"),
            ["America/Indiana/Tell_City"] = new("−06:00", "−05:00"),
            ["America/Indiana/Vevay"] = new("−05:00", "−04:00"),
            ["America/Indiana/Vincennes"] = new("−05:00", "−04:00"),
            ["America/Indiana/Winamac"] = new("−05:00", "−04:00"),
            ["America/Indianapolis"] = new("−05:00", "−04:00"),
            ["America/Inuvik"] = new("−07:00", "−06:00"),
            ["America/Iqaluit"] = new("−05:00", "−04:00"),
            ["America/Jamaica"] = new("−05:00", "−05:00"),
            ["America/Jujuy"] = new("−03:00", "−03:00"),
            ["America/Juneau"] = new("−09:00", "−08:00"),
            ["America/Kentucky/Louisville"] = new("−05:00", "−04:00"),
            ["America/Kentucky/Monticello"] = new("−05:00", "−04:00"),
            ["America/Knox_IN"] = new("−06:00", "−05:00"),
            ["America/Kralendijk"] = new("−04:00", "−04:00"),
            ["America/La_Paz"] = new("−04:00", "−04:00"),
            ["America/Lima"] = new("−05:00", "−05:00"),
            ["America/Los_Angeles"] = new("−08:00", "−07:00"),
            ["America/Louisville"] = new("−05:00", "−04:00"),
            ["America/Lower_Princes"] = new("−04:00", "−04:00"),
            ["America/Maceio"] = new("−03:00", "−03:00"),
            ["America/Managua"] = new("−06:00", "−06:00"),
            ["America/Manaus"] = new("−04:00", "−04:00"),
            ["America/Marigot"] = new("−04:00", "−04:00"),
            ["America/Martinique"] = new("−04:00", "−04:00"),
            ["America/Matamoros"] = new("−06:00", "−05:00"),
            ["America/Mazatlan"] = new("−07:00", "−07:00"),
            ["America/Mendoza"] = new("−03:00", "−03:00"),
            ["America/Menominee"] = new("−06:00", "−05:00"),
            ["America/Merida"] = new("−06:00", "−06:00"),
            ["America/Metlakatla"] = new("−09:00", "−08:00"),
            ["America/Mexico_City"] = new("−06:00", "−06:00"),
            ["America/Miquelon"] = new("−03:00", "−02:00"),
            ["America/Moncton"] = new("−04:00", "−03:00"),
            ["America/Monterrey"] = new("−06:00", "−06:00"),
            ["America/Montevideo"] = new("−03:00", "−03:00"),
            ["America/Montreal"] = new("−05:00", "−04:00"),
            ["America/Montserrat"] = new("−04:00", "−04:00"),
            ["America/Nassau"] = new("−05:00", "−04:00"),
            ["America/New_York"] = new("−05:00", "−04:00"),
            ["America/Nipigon"] = new("−05:00", "−04:00"),
            ["America/Nome"] = new("−09:00", "−08:00"),
            ["America/Noronha"] = new("−02:00", "−02:00"),
            ["America/North_Dakota/Beulah"] = new("−06:00", "−05:00"),
            ["America/North_Dakota/Center"] = new("−06:00", "−05:00"),
            ["America/North_Dakota/New_Salem"] = new("−06:00", "−05:00"),
            ["America/Nuuk"] = new("−02:00", "−01:00"),
            ["America/Ojinaga"] = new("−06:00", "−05:00"),
            ["America/Panama"] = new("−05:00", "−05:00"),
            ["America/Pangnirtung"] = new("−05:00", "−04:00"),
            ["America/Paramaribo"] = new("−03:00", "−03:00"),
            ["America/Phoenix"] = new("−07:00", "−07:00"),
            ["America/Port_of_Spain"] = new("−04:00", "−04:00"),
            ["America/Port-au-Prince"] = new("−05:00", "−04:00"),
            ["America/Porto_Acre"] = new("−05:00", "−05:00"),
            ["America/Porto_Velho"] = new("−04:00", "−04:00"),
            ["America/Puerto_Rico"] = new("−04:00", "−04:00"),
            ["America/Punta_Arenas"] = new("−03:00", "−03:00"),
            ["America/Rainy_River"] = new("−06:00", "−05:00"),
            ["America/Rankin_Inlet"] = new("−06:00", "−05:00"),
            ["America/Recife"] = new("−03:00", "−03:00"),
            ["America/Regina"] = new("−06:00", "−06:00"),
            ["America/Resolute"] = new("−06:00", "−05:00"),
            ["America/Rio_Branco"] = new("−05:00", "−05:00"),
            ["America/Rosario"] = new("−03:00", "−03:00"),
            ["America/Santa_Isabel"] = new("−08:00", "−07:00"),
            ["America/Santarem"] = new("−03:00", "−03:00"),
            ["America/Santiago"] = new("−04:00", "−03:00"),
            ["America/Santo_Domingo"] = new("−04:00", "−04:00"),
            ["America/Sao_Paulo"] = new("−03:00", "−03:00"),
            ["America/Scoresbysund"] = new("−02:00", "−01:00"),
            ["America/Shiprock"] = new("−07:00", "−06:00"),
            ["America/Sitka"] = new("−09:00", "−08:00"),
            ["America/St_Barthelemy"] = new("−04:00", "−04:00"),
            ["America/St_Johns"] = new("−03:30", "−02:30"),
            ["America/St_Kitts"] = new("−04:00", "−04:00"),
            ["America/St_Lucia"] = new("−04:00", "−04:00"),
            ["America/St_Thomas"] = new("−04:00", "−04:00"),
            ["America/St_Vincent"] = new("−04:00", "−04:00"),
            ["America/Swift_Current"] = new("−06:00", "−06:00"),
            ["America/Tegucigalpa"] = new("−06:00", "−06:00"),
            ["America/Thule"] = new("−04:00", "−03:00"),
            ["America/Thunder_Bay"] = new("−05:00", "−04:00"),
            ["America/Tijuana"] = new("−08:00", "−07:00"),
            ["America/Toronto"] = new("−05:00", "−04:00"),
            ["America/Tortola"] = new("−04:00", "−04:00"),
            ["America/Vancouver"] = new("−08:00", "−07:00"),
            ["America/Virgin"] = new("−04:00", "−04:00"),
            ["America/Whitehorse"] = new("−07:00", "−07:00"),
            ["America/Winnipeg"] = new("−06:00", "−05:00"),
            ["America/Yakutat"] = new("−09:00", "−08:00"),
            ["America/Yellowknife"] = new("−07:00", "−06:00"),
            ["Antarctica/Casey"] = new("+08:00", "+08:00"),
            ["Antarctica/Davis"] = new("+07:00", "+07:00"),
            ["Antarctica/DumontDUrville"] = new("+10:00", "+10:00"),
            ["Antarctica/Macquarie"] = new("+10:00", "+11:00"),
            ["Antarctica/Mawson"] = new("+05:00", "+05:00"),
            ["Antarctica/McMurdo"] = new("+12:00", "+13:00"),
            ["Antarctica/Palmer"] = new("−03:00", "−03:00"),
            ["Antarctica/Rothera"] = new("−03:00", "−03:00"),
            ["Antarctica/South_Pole"] = new("+12:00", "+13:00"),
            ["Antarctica/Syowa"] = new("+03:00", "+03:00"),
            ["Antarctica/Troll"] = new("+00:00", "+02:00"),
            ["Antarctica/Vostok"] = new("+05:00", "+05:00"),
            ["Arctic/Longyearbyen"] = new("+01:00", "+02:00"),
            ["Asia/Aden"] = new("+03:00", "+03:00"),
            ["Asia/Almaty"] = new("+05:00", "+05:00"),
            ["Asia/Amman"] = new("+03:00", "+03:00"),
            ["Asia/Anadyr"] = new("+12:00", "+12:00"),
            ["Asia/Aqtau"] = new("+05:00", "+05:00"),
            ["Asia/Aqtobe"] = new("+05:00", "+05:00"),
            ["Asia/Ashgabat"] = new("+05:00", "+05:00"),
            ["Asia/Ashkhabad"] = new("+05:00", "+05:00"),
            ["Asia/Atyrau"] = new("+05:00", "+05:00"),
            ["Asia/Baghdad"] = new("+03:00", "+03:00"),
            ["Asia/Bahrain"] = new("+03:00", "+03:00"),
            ["Asia/Baku"] = new("+04:00", "+04:00"),
            ["Asia/Bangkok"] = new("+07:00", "+07:00"),
            ["Asia/Barnaul"] = new("+07:00", "+07:00"),
            ["Asia/Beirut"] = new("+02:00", "+03:00"),
            ["Asia/Bishkek"] = new("+06:00", "+06:00"),
            ["Asia/Brunei"] = new("+08:00", "+08:00"),
            ["Asia/Calcutta"] = new("+05:30", "+05:30"),
            ["Asia/Chita"] = new("+09:00", "+09:00"),
            ["Asia/Choibalsan"] = new("+08:00", "+08:00"),
            ["Asia/Chongqing"] = new("+08:00", "+08:00"),
            ["Asia/Chungking"] = new("+08:00", "+08:00"),
            ["Asia/Colombo"] = new("+05:30", "+05:30"),
            ["Asia/Dacca"] = new("+06:00", "+06:00"),
            ["Asia/Damascus"] = new("+03:00", "+03:00"),
            ["Asia/Dhaka"] = new("+06:00", "+06:00"),
            ["Asia/Dili"] = new("+09:00", "+09:00"),
            ["Asia/Dubai"] = new("+04:00", "+04:00"),
            ["Asia/Dushanbe"] = new("+05:00", "+05:00"),
            ["Asia/Famagusta"] = new("+02:00", "+03:00"),
            ["Asia/Gaza"] = new("+02:00", "+03:00"),
            ["Asia/Harbin"] = new("+08:00", "+08:00"),
            ["Asia/Hebron"] = new("+02:00", "+03:00"),
            ["Asia/Ho_Chi_Minh"] = new("+07:00", "+07:00"),
            ["Asia/Hong_Kong"] = new("+08:00", "+08:00"),
            ["Asia/Hovd"] = new("+07:00", "+07:00"),
            ["Asia/Irkutsk"] = new("+08:00", "+08:00"),
            ["Asia/Istanbul"] = new("+03:00", "+03:00"),
            ["Asia/Jakarta"] = new("+07:00", "+07:00"),
            ["Asia/Jayapura"] = new("+09:00", "+09:00"),
            ["Asia/Jerusalem"] = new("+02:00", "+03:00"),
            ["Asia/Kabul"] = new("+04:30", "+04:30"),
            ["Asia/Kamchatka"] = new("+12:00", "+12:00"),
            ["Asia/Karachi"] = new("+05:00", "+05:00"),
            ["Asia/Kashgar"] = new("+06:00", "+06:00"),
            ["Asia/Kathmandu"] = new("+05:45", "+05:45"),
            ["Asia/Katmandu"] = new("+05:45", "+05:45"),
            ["Asia/Khandyga"] = new("+09:00", "+09:00"),
            ["Asia/Kolkata"] = new("+05:30", "+05:30"),
            ["Asia/Krasnoyarsk"] = new("+07:00", "+07:00"),
            ["Asia/Kuala_Lumpur"] = new("+08:00", "+08:00"),
            ["Asia/Kuching"] = new("+08:00", "+08:00"),
            ["Asia/Kuwait"] = new("+03:00", "+03:00"),
            ["Asia/Macao"] = new("+08:00", "+08:00"),
            ["Asia/Macau"] = new("+08:00", "+08:00"),
            ["Asia/Magadan"] = new("+11:00", "+11:00"),
            ["Asia/Makassar"] = new("+08:00", "+08:00"),
            ["Asia/Manila"] = new("+08:00", "+08:00"),
            ["Asia/Muscat"] = new("+04:00", "+04:00"),
            ["Asia/Nicosia"] = new("+02:00", "+03:00"),
            ["Asia/Novokuznetsk"] = new("+07:00", "+07:00"),
            ["Asia/Novosibirsk"] = new("+07:00", "+07:00"),
            ["Asia/Omsk"] = new("+06:00", "+06:00"),
            ["Asia/Oral"] = new("+05:00", "+05:00"),
            ["Asia/Phnom_Penh"] = new("+07:00", "+07:00"),
            ["Asia/Pontianak"] = new("+07:00", "+07:00"),
            ["Asia/Pyongyang"] = new("+09:00", "+09:00"),
            ["Asia/Qatar"] = new("+03:00", "+03:00"),
            ["Asia/Qostanay"] = new("+05:00", "+05:00"),
            ["Asia/Qyzylorda"] = new("+05:00", "+05:00"),
            ["Asia/Rangoon"] = new("+06:30", "+06:30"),
            ["Asia/Riyadh"] = new("+03:00", "+03:00"),
            ["Asia/Saigon"] = new("+07:00", "+07:00"),
            ["Asia/Sakhalin"] = new("+11:00", "+11:00"),
            ["Asia/Samarkand"] = new("+05:00", "+05:00"),
            ["Asia/Seoul"] = new("+09:00", "+09:00"),
            ["Asia/Shanghai"] = new("+08:00", "+08:00"),
            ["Asia/Singapore"] = new("+08:00", "+08:00"),
            ["Asia/Srednekolymsk"] = new("+11:00", "+11:00"),
            ["Asia/Taipei"] = new("+08:00", "+08:00"),
            ["Asia/Tashkent"] = new("+05:00", "+05:00"),
            ["Asia/Tbilisi"] = new("+04:00", "+04:00"),
            ["Asia/Tehran"] = new("+03:30", "+03:30"),
            ["Asia/Tel_Aviv"] = new("+02:00", "+03:00"),
            ["Asia/Thimbu"] = new("+06:00", "+06:00"),
            ["Asia/Thimphu"] = new("+06:00", "+06:00"),
            ["Asia/Tokyo"] = new("+09:00", "+09:00"),
            ["Asia/Tomsk"] = new("+07:00", "+07:00"),
            ["Asia/Ujung_Pandang"] = new("+08:00", "+08:00"),
            ["Asia/Ulaanbaatar"] = new("+08:00", "+08:00"),
            ["Asia/Ulan_Bator"] = new("+08:00", "+08:00"),
            ["Asia/Urumqi"] = new("+06:00", "+06:00"),
            ["Asia/Ust-Nera"] = new("+10:00", "+10:00"),
            ["Asia/Vientiane"] = new("+07:00", "+07:00"),
            ["Asia/Vladivostok"] = new("+10:00", "+10:00"),
            ["Asia/Yakutsk"] = new("+09:00", "+09:00"),
            ["Asia/Yangon"] = new("+06:30", "+06:30"),
            ["Asia/Yekaterinburg"] = new("+05:00", "+05:00"),
            ["Asia/Yerevan"] = new("+04:00", "+04:00"),
            ["Atlantic/Azores"] = new("−01:00", "+00:00"),
            ["Atlantic/Bermuda"] = new("−04:00", "−03:00"),
            ["Atlantic/Canary"] = new("+00:00", "+01:00"),
            ["Atlantic/Cape_Verde"] = new("−01:00", "−01:00"),
            ["Atlantic/Faeroe"] = new("+00:00", "+01:00"),
            ["Atlantic/Faroe"] = new("+00:00", "+01:00"),
            ["Atlantic/Jan_Mayen"] = new("+01:00", "+02:00"),
            ["Atlantic/Madeira"] = new("+00:00", "+01:00"),
            ["Atlantic/Reykjavik"] = new("+00:00", "+00:00"),
            ["Atlantic/South_Georgia"] = new("−02:00", "−02:00"),
            ["Atlantic/St_Helena"] = new("+00:00", "+00:00"),
            ["Atlantic/Stanley"] = new("−03:00", "−03:00"),
            ["Australia/ACT"] = new("+10:00", "+11:00"),
            ["Australia/Adelaide"] = new("+09:30", "+10:30"),
            ["Australia/Brisbane"] = new("+10:00", "+10:00"),
            ["Australia/Broken_Hill"] = new("+09:30", "+10:30"),
            ["Australia/Canberra"] = new("+10:00", "+11:00"),
            ["Australia/Currie"] = new("+10:00", "+11:00"),
            ["Australia/Darwin"] = new("+09:30", "+09:30"),
            ["Australia/Eucla"] = new("+08:45", "+08:45"),
            ["Australia/Hobart"] = new("+10:00", "+11:00"),
            ["Australia/LHI"] = new("+10:30", "+11:00"),
            ["Australia/Lindeman"] = new("+10:00", "+10:00"),
            ["Australia/Lord_Howe"] = new("+10:30", "+11:00"),
            ["Australia/Melbourne"] = new("+10:00", "+11:00"),
            ["Australia/North"] = new("+09:30", "+09:30"),
            ["Australia/NSW"] = new("+10:00", "+11:00"),
            ["Australia/Perth"] = new("+08:00", "+08:00"),
            ["Australia/Queensland"] = new("+10:00", "+10:00"),
            ["Australia/South"] = new("+09:30", "+10:30"),
            ["Australia/Sydney"] = new("+10:00", "+11:00"),
            ["Australia/Tasmania"] = new("+10:00", "+11:00"),
            ["Australia/Victoria"] = new("+10:00", "+11:00"),
            ["Australia/West"] = new("+08:00", "+08:00"),
            ["Australia/Yancowinna"] = new("+09:30", "+10:30"),
            ["Brazil/Acre"] = new("−05:00", "−05:00"),
            ["Brazil/DeNoronha"] = new("−02:00", "−02:00"),
            ["Brazil/East"] = new("−03:00", "−03:00"),
            ["Brazil/West"] = new("−04:00", "−04:00"),
            ["Canada/Atlantic"] = new("−04:00", "−03:00"),
            ["Canada/Central"] = new("−06:00", "−05:00"),
            ["Canada/Eastern"] = new("−05:00", "−04:00"),
            ["Canada/Mountain"] = new("−07:00", "−06:00"),
            ["Canada/Newfoundland"] = new("−03:30", "−02:30"),
            ["Canada/Pacific"] = new("−08:00", "−07:00"),
            ["Canada/Saskatchewan"] = new("−06:00", "−06:00"),
            ["Canada/Yukon"] = new("−07:00", "−07:00"),
            ["CET"] = new("+01:00", "+02:00"),
            ["Chile/Continental"] = new("−04:00", "−03:00"),
            ["Chile/EasterIsland"] = new("−06:00", "−05:00"),
            ["CST6CDT"] = new("−06:00", "−05:00"),
            ["Cuba"] = new("−05:00", "−04:00"),
            ["EET"] = new("+02:00", "+03:00"),
            ["Egypt"] = new("+02:00", "+03:00"),
            ["Eire"] = new("+00:00", "+01:00"),
            ["EST"] = new("−05:00", "−05:00"),
            ["EST5EDT"] = new("−05:00", "−04:00"),
            ["Etc/GMT"] = new("+00:00", "+00:00"),
            ["Etc/GMT+0"] = new("+00:00", "+00:00"),
            ["Etc/GMT+1"] = new("−01:00", "−01:00"),
            ["Etc/GMT+10"] = new("−10:00", "−10:00"),
            ["Etc/GMT+11"] = new("−11:00", "−11:00"),
            ["Etc/GMT+12"] = new("−12:00", "−12:00"),
            ["Etc/GMT+2"] = new("−02:00", "−02:00"),
            ["Etc/GMT+3"] = new("−03:00", "−03:00"),
            ["Etc/GMT+4"] = new("−04:00", "−04:00"),
            ["Etc/GMT+5"] = new("−05:00", "−05:00"),
            ["Etc/GMT+6"] = new("−06:00", "−06:00"),
            ["Etc/GMT+7"] = new("−07:00", "−07:00"),
            ["Etc/GMT+8"] = new("−08:00", "−08:00"),
            ["Etc/GMT+9"] = new("−09:00", "−09:00"),
            ["Etc/GMT0"] = new("+00:00", "+00:00"),
            ["Etc/GMT-0"] = new("+00:00", "+00:00"),
            ["Etc/GMT-1"] = new("+01:00", "+01:00"),
            ["Etc/GMT-10"] = new("+10:00", "+10:00"),
            ["Etc/GMT-11"] = new("+11:00", "+11:00"),
            ["Etc/GMT-12"] = new("+12:00", "+12:00"),
            ["Etc/GMT-13"] = new("+13:00", "+13:00"),
            ["Etc/GMT-14"] = new("+14:00", "+14:00"),
            ["Etc/GMT-2"] = new("+02:00", "+02:00"),
            ["Etc/GMT-3"] = new("+03:00", "+03:00"),
            ["Etc/GMT-4"] = new("+04:00", "+04:00"),
            ["Etc/GMT-5"] = new("+05:00", "+05:00"),
            ["Etc/GMT-6"] = new("+06:00", "+06:00"),
            ["Etc/GMT-7"] = new("+07:00", "+07:00"),
            ["Etc/GMT-8"] = new("+08:00", "+08:00"),
            ["Etc/GMT-9"] = new("+09:00", "+09:00"),
            ["Etc/Greenwich"] = new("+00:00", "+00:00"),
            ["Etc/UCT"] = new("+00:00", "+00:00"),
            ["Etc/Universal"] = new("+00:00", "+00:00"),
            ["Etc/UTC"] = new("+00:00", "+00:00"),
            ["Etc/Zulu"] = new("+00:00", "+00:00"),
            ["Europe/Amsterdam"] = new("+01:00", "+02:00"),
            ["Europe/Andorra"] = new("+01:00", "+02:00"),
            ["Europe/Astrakhan"] = new("+04:00", "+04:00"),
            ["Europe/Athens"] = new("+02:00", "+03:00"),
            ["Europe/Belfast"] = new("+00:00", "+01:00"),
            ["Europe/Belgrade"] = new("+01:00", "+02:00"),
            ["Europe/Berlin"] = new("+01:00", "+02:00"),
            ["Europe/Bratislava"] = new("+01:00", "+02:00"),
            ["Europe/Brussels"] = new("+01:00", "+02:00"),
            ["Europe/Bucharest"] = new("+02:00", "+03:00"),
            ["Europe/Budapest"] = new("+01:00", "+02:00"),
            ["Europe/Busingen"] = new("+01:00", "+02:00"),
            ["Europe/Chisinau"] = new("+02:00", "+03:00"),
            ["Europe/Copenhagen"] = new("+01:00", "+02:00"),
            ["Europe/Dublin"] = new("+00:00", "+01:00"),
            ["Europe/Gibraltar"] = new("+01:00", "+02:00"),
            ["Europe/Guernsey"] = new("+00:00", "+01:00"),
            ["Europe/Helsinki"] = new("+02:00", "+03:00"),
            ["Europe/Isle_of_Man"] = new("+00:00", "+01:00"),
            ["Europe/Istanbul"] = new("+03:00", "+03:00"),
            ["Europe/Jersey"] = new("+00:00", "+01:00"),
            ["Europe/Kaliningrad"] = new("+02:00", "+02:00"),
            ["Europe/Kiev"] = new("+02:00", "+03:00"),
            ["Europe/Kirov"] = new("+03:00", "+03:00"),
            ["Europe/Kyiv"] = new("+02:00", "+03:00"),
            ["Europe/Lisbon"] = new("+00:00", "+01:00"),
            ["Europe/Ljubljana"] = new("+01:00", "+02:00"),
            ["Europe/London"] = new("+00:00", "+01:00"),
            ["Europe/Luxembourg"] = new("+01:00", "+02:00"),
            ["Europe/Madrid"] = new("+01:00", "+02:00"),
            ["Europe/Malta"] = new("+01:00", "+02:00"),
            ["Europe/Mariehamn"] = new("+02:00", "+03:00"),
            ["Europe/Minsk"] = new("+03:00", "+03:00"),
            ["Europe/Monaco"] = new("+01:00", "+02:00"),
            ["Europe/Moscow"] = new("+03:00", "+03:00"),
            ["Europe/Nicosia"] = new("+02:00", "+03:00"),
            ["Europe/Oslo"] = new("+01:00", "+02:00"),
            ["Europe/Paris"] = new("+01:00", "+02:00"),
            ["Europe/Podgorica"] = new("+01:00", "+02:00"),
            ["Europe/Prague"] = new("+01:00", "+02:00"),
            ["Europe/Riga"] = new("+02:00", "+03:00"),
            ["Europe/Rome"] = new("+01:00", "+02:00"),
            ["Europe/Samara"] = new("+04:00", "+04:00"),
            ["Europe/San_Marino"] = new("+01:00", "+02:00"),
            ["Europe/Sarajevo"] = new("+01:00", "+02:00"),
            ["Europe/Saratov"] = new("+04:00", "+04:00"),
            ["Europe/Simferopol"] = new("+03:00", "+03:00"),
            ["Europe/Skopje"] = new("+01:00", "+02:00"),
            ["Europe/Sofia"] = new("+02:00", "+03:00"),
            ["Europe/Stockholm"] = new("+01:00", "+02:00"),
            ["Europe/Tallinn"] = new("+02:00", "+03:00"),
            ["Europe/Tirane"] = new("+01:00", "+02:00"),
            ["Europe/Tiraspol"] = new("+02:00", "+03:00"),
            ["Europe/Ulyanovsk"] = new("+04:00", "+04:00"),
            ["Europe/Uzhgorod"] = new("+02:00", "+03:00"),
            ["Europe/Vaduz"] = new("+01:00", "+02:00"),
            ["Europe/Vatican"] = new("+01:00", "+02:00"),
            ["Europe/Vienna"] = new("+01:00", "+02:00"),
            ["Europe/Vilnius"] = new("+02:00", "+03:00"),
            ["Europe/Volgograd"] = new("+03:00", "+03:00"),
            ["Europe/Warsaw"] = new("+01:00", "+02:00"),
            ["Europe/Zagreb"] = new("+01:00", "+02:00"),
            ["Europe/Zaporozhye"] = new("+02:00", "+03:00"),
            ["Europe/Zurich"] = new("+01:00", "+02:00"),
            ["Factory"] = new("+00:00", "+00:00"),
            ["GB"] = new("+00:00", "+01:00"),
            ["GB-Eire"] = new("+00:00", "+01:00"),
            ["GMT"] = new("+00:00", "+00:00"),
            ["GMT+0"] = new("+00:00", "+00:00"),
            ["GMT0"] = new("+00:00", "+00:00"),
            ["GMT-0"] = new("+00:00", "+00:00"),
            ["Greenwich"] = new("+00:00", "+00:00"),
            ["Hongkong"] = new("+08:00", "+08:00"),
            ["HST"] = new("−10:00", "−10:00"),
            ["Iceland"] = new("+00:00", "+00:00"),
            ["Indian/Antananarivo"] = new("+03:00", "+03:00"),
            ["Indian/Chagos"] = new("+06:00", "+06:00"),
            ["Indian/Christmas"] = new("+07:00", "+07:00"),
            ["Indian/Cocos"] = new("+06:30", "+06:30"),
            ["Indian/Comoro"] = new("+03:00", "+03:00"),
            ["Indian/Kerguelen"] = new("+05:00", "+05:00"),
            ["Indian/Mahe"] = new("+04:00", "+04:00"),
            ["Indian/Maldives"] = new("+05:00", "+05:00"),
            ["Indian/Mauritius"] = new("+04:00", "+04:00"),
            ["Indian/Mayotte"] = new("+03:00", "+03:00"),
            ["Indian/Reunion"] = new("+04:00", "+04:00"),
            ["Iran"] = new("+03:30", "+03:30"),
            ["Israel"] = new("+02:00", "+03:00"),
            ["Jamaica"] = new("−05:00", "−05:00"),
            ["Japan"] = new("+09:00", "+09:00"),
            ["Kwajalein"] = new("+12:00", "+12:00"),
            ["Libya"] = new("+02:00", "+02:00"),
            ["MET"] = new("+01:00", "+02:00"),
            ["Mexico/BajaNorte"] = new("−08:00", "−07:00"),
            ["Mexico/BajaSur"] = new("−07:00", "−07:00"),
            ["Mexico/General"] = new("−06:00", "−06:00"),
            ["MST"] = new("−07:00", "−07:00"),
            ["MST7MDT"] = new("−07:00", "−06:00"),
            ["Navajo"] = new("−07:00", "−06:00"),
            ["NZ"] = new("+12:00", "+13:00"),
            ["NZ-CHAT"] = new("+12:45", "+13:45"),
            ["Pacific/Apia"] = new("+13:00", "+13:00"),
            ["Pacific/Auckland"] = new("+12:00", "+13:00"),
            ["Pacific/Bougainville"] = new("+11:00", "+11:00"),
            ["Pacific/Chatham"] = new("+12:45", "+13:45"),
            ["Pacific/Chuuk"] = new("+10:00", "+10:00"),
            ["Pacific/Easter"] = new("−06:00", "−05:00"),
            ["Pacific/Efate"] = new("+11:00", "+11:00"),
            ["Pacific/Enderbury"] = new("+13:00", "+13:00"),
            ["Pacific/Fakaofo"] = new("+13:00", "+13:00"),
            ["Pacific/Fiji"] = new("+12:00", "+12:00"),
            ["Pacific/Funafuti"] = new("+12:00", "+12:00"),
            ["Pacific/Galapagos"] = new("−06:00", "−06:00"),
            ["Pacific/Gambier"] = new("−09:00", "−09:00"),
            ["Pacific/Guadalcanal"] = new("+11:00", "+11:00"),
            ["Pacific/Guam"] = new("+10:00", "+10:00"),
            ["Pacific/Honolulu"] = new("−10:00", "−10:00"),
            ["Pacific/Johnston"] = new("−10:00", "−10:00"),
            ["Pacific/Kanton"] = new("+13:00", "+13:00"),
            ["Pacific/Kiritimati"] = new("+14:00", "+14:00"),
            ["Pacific/Kosrae"] = new("+11:00", "+11:00"),
            ["Pacific/Kwajalein"] = new("+12:00", "+12:00"),
            ["Pacific/Majuro"] = new("+12:00", "+12:00"),
            ["Pacific/Marquesas"] = new("−09:30", "−09:30"),
            ["Pacific/Midway"] = new("−11:00", "−11:00"),
            ["Pacific/Nauru"] = new("+12:00", "+12:00"),
            ["Pacific/Niue"] = new("−11:00", "−11:00"),
            ["Pacific/Norfolk"] = new("+11:00", "+12:00"),
            ["Pacific/Noumea"] = new("+11:00", "+11:00"),
            ["Pacific/Pago_Pago"] = new("−11:00", "−11:00"),
            ["Pacific/Palau"] = new("+09:00", "+09:00"),
            ["Pacific/Pitcairn"] = new("−08:00", "−08:00"),
            ["Pacific/Pohnpei"] = new("+11:00", "+11:00"),
            ["Pacific/Ponape"] = new("+11:00", "+11:00"),
            ["Pacific/Port_Moresby"] = new("+10:00", "+10:00"),
            ["Pacific/Rarotonga"] = new("−10:00", "−10:00"),
            ["Pacific/Saipan"] = new("+10:00", "+10:00"),
            ["Pacific/Samoa"] = new("−11:00", "−11:00"),
            ["Pacific/Tahiti"] = new("−10:00", "−10:00"),
            ["Pacific/Tarawa"] = new("+12:00", "+12:00"),
            ["Pacific/Tongatapu"] = new("+13:00", "+13:00"),
            ["Pacific/Truk"] = new("+10:00", "+10:00"),
            ["Pacific/Wake"] = new("+12:00", "+12:00"),
            ["Pacific/Wallis"] = new("+12:00", "+12:00"),
            ["Pacific/Yap"] = new("+10:00", "+10:00"),
            ["Poland"] = new("+01:00", "+02:00"),
            ["Portugal"] = new("+00:00", "+01:00"),
            ["PRC"] = new("+08:00", "+08:00"),
            ["PST8PDT"] = new("−08:00", "−07:00"),
            ["ROC"] = new("+08:00", "+08:00"),
            ["ROK"] = new("+09:00", "+09:00"),
            ["Singapore"] = new("+08:00", "+08:00"),
            ["Turkey"] = new("+03:00", "+03:00"),
            ["UCT"] = new("+00:00", "+00:00"),
            ["Universal"] = new("+00:00", "+00:00"),
            ["US/Alaska"] = new("−09:00", "−08:00"),
            ["US/Aleutian"] = new("−10:00", "−09:00"),
            ["US/Arizona"] = new("−07:00", "−07:00"),
            ["US/Central"] = new("−06:00", "−05:00"),
            ["US/Eastern"] = new("−05:00", "−04:00"),
            ["US/East-Indiana"] = new("−05:00", "−04:00"),
            ["US/Hawaii"] = new("−10:00", "−10:00"),
            ["US/Indiana-Starke"] = new("−06:00", "−05:00"),
            ["US/Michigan"] = new("−05:00", "−04:00"),
            ["US/Mountain"] = new("−07:00", "−06:00"),
            ["US/Pacific"] = new("−08:00", "−07:00"),
            ["US/Samoa"] = new("−11:00", "−11:00"),
            ["UTC"] = new("+00:00", "+00:00"),
            ["WET"] = new("+00:00", "+01:00"),
            ["W-SU"] = new("+03:00", "+03:00"),
            ["Zulu"] = new("+00:00", "+00:00"),
        };

    [GeneratedRegex("([A-Z])", RegexOptions.Compiled)]
    private static partial Regex UpperCaseRegex();
}