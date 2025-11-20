using System;
using System.Collections.Generic;

public static class Extensions
{
    public static IEnumerable<int> AllIndexesOf(this string str, string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("the string to find may not be empty", "value");
            for (int i = 0; ; i += 1)
            {
                i = str.IndexOf(value, i);
                if (i == -1) break;
                yield return i;
            }
        }
}