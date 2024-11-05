namespace Cute.Lib.Extensions
{
    public static class DictionaryExtensions
    {
        public static HashSet<TKey> MergeWith<TKey, TValue>(this IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> source) where TKey : notnull
        {
            HashSet<TKey> conflictingKeys = new HashSet<TKey>();
            foreach (var kvp in source)
            {
                if (target.TryGetValue(kvp.Key, out var existingValue))
                {
                    if (existingValue is not null && kvp.Value is not null && !existingValue.Equals(kvp.Value))
                    {
                        conflictingKeys.Add(kvp.Key);
                    }
                }
                else
                {
                    target[kvp.Key] = kvp.Value;
                }
            }

            return conflictingKeys;
        }
    }
}
