using System;

internal static class Argument
{
    public static void NotNullOrEmpty(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(paramName);
        }
    }

    public static void Ensure<T>(bool condition, string message)
        where T : Exception
    {
        if (!condition)
        {
            throw (T)Activator.CreateInstance(typeof(T), message);
        }
    }

    public static T EnsureType<T>(object obj, string message, string paramName)
    {
        if (!(obj is T tObj))
        {
            throw new ArgumentException(message, paramName);
        }

        return tObj;
    }

    public static void Ensure(bool condition, string message, string paramName)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    public static void NotNull(object value, string paramName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}