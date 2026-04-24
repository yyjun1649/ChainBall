using System;

public static class EnumExtension
{
    public static T ToEnum<T>(this string value) where T : Enum
    {
        return (T) Enum.Parse(typeof(T), value);
    }
    
    public static T ToEnum<T>(this int value) where T : Enum
    {
        return (T) Enum.ToObject(typeof(T), value);
    }
}