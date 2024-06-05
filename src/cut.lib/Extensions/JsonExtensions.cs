using Newtonsoft.Json.Linq;

namespace cut.lib.Extensions;

public static class JsonExtensions
{
    public static IDictionary<string, object?> ToDictionary(this JObject jsonObject)
    {
        var result = new Dictionary<string, object?>();

        foreach (var token in jsonObject.SelectTokens("$..*"))
        {
            // store arrays as a single entry with object[]
            if (token is JArray jsonArray && jsonArray.Count > 0 && jsonArray[0] is JValue)
            {
                var values = jsonArray.Children()
                    .Select(t => t.ToObject<object>())
                    .ToArray();

                result.Add(token.Path + "[]", values);

                continue;
            }

            // skip other non-primitive json types
            if (token.HasValues)
            {
                continue;
            }

            // skip previously saved array elements

            if (token.Path.EndsWith(']'))
            {
                continue;
            }

            result.Add(token.Path, token.ToObject<object>());
        }

        return result;
    }

    public static JToken RemoveEmptyChildren(this JToken token)
    {
        if (token.Type == JTokenType.Object)
        {
            var copy = new JObject();
            foreach (JProperty prop in token.Children<JProperty>())
            {
                JToken child = prop.Value;
                if (child.HasValues)
                {
                    child = RemoveEmptyChildren(child);
                }
                if (!IsEmpty(child))
                {
                    copy.Add(prop.Name, child);
                }
            }
            return copy;
        }
        else if (token.Type == JTokenType.Array)
        {
            var copy = new JArray();
            foreach (JToken item in token.Children())
            {
                JToken child = item;
                if (child.HasValues)
                {
                    child = RemoveEmptyChildren(child);
                }
                if (!IsEmpty(child))
                {
                    copy.Add(child);
                }
            }
            return copy;
        }
        return token;
    }

    public static bool IsEmpty(this JToken token)
    {
        return (token.Type == JTokenType.Null);
    }
}