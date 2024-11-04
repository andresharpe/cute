namespace Cute.Lib.Extensions
{
    public static class DictionaryExtensions
    {
        public static HashSet<TKey> MergeWith<TKey, TValue>(this IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> source) where TKey : notnull
        {
            HashSet<TKey> conflictingKeys = new HashSet<TKey>();
            foreach (var kvp in source)
            {
                if (!target.ContainsKey(kvp.Key) || target[kvp.Key] is null)
                {
                    target[kvp.Key] = kvp.Value;
                }
                else if (target[kvp.Key] is not null && kvp.Value is not null && !target[kvp.Key]!.Equals(kvp.Value))
                {
                    conflictingKeys.Add(kvp.Key);
                }
            }

            return conflictingKeys;
        }
    }
}
