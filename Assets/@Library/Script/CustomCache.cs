using System;
using System.Collections.Generic;

public class CustomCache
{
    public static class EnumCache<T> where T : Enum
    {
        private static readonly Dictionary<T, string> dic = new();
        public static string GetString(T value)
        {
            if (!dic.TryGetValue(value, out var str))
            {
                str = value.ToString();
                dic[value] = str;
            }
            return str;
        }
    }
}
