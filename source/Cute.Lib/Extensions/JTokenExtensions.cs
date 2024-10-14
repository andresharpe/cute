using System.Text;
using Newtonsoft.Json.Linq;

public static class JTokenExtensions
{
    public static string ToUserFriendlyString(this JToken token, int indentLevel = 0)
    {
        var sb = new StringBuilder();
        WriteYaml(token, sb, indentLevel);
        return sb.ToString();
    }

    private static void WriteYaml(JToken? token, StringBuilder sb, int indentLevel)
    {
        if (token == null) return;
        switch (token.Type)
        {
            case JTokenType.Object:
                foreach (var property in (JObject)token)
                {
                    AppendIndent(sb, indentLevel);
                    sb.Append(property.Key).Append(":");
                    if (property.Value is JObject || property.Value is JArray)
                    {
                        sb.AppendLine();
                        WriteYaml(property.Value, sb, indentLevel + 1);
                    }
                    else
                    {
                        sb.Append(" ");
                        WriteYaml(property.Value, sb, indentLevel);
                    }
                }
                break;

            case JTokenType.Array:
                foreach (var item in (JArray)token)
                {
                    AppendIndent(sb, indentLevel);
                    sb.AppendLine("-");
                    WriteYaml(item, sb, indentLevel + 1);
                }
                break;

            default:
                sb.AppendLine(token.ToString());
                break;
        }
    }

    private static void AppendIndent(StringBuilder sb, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 2));
    }
}