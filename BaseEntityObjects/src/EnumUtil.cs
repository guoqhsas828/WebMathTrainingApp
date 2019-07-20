using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  /// 
  /// </summary>
  public static class EnumUtil
  {
    internal static bool HasFlags(Type type)
    {
      return type.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
    }

    internal static bool AreAllEnumsDefined<T>(T value) where T : struct
    {
      if (HasFlags(typeof(T)))
      {
        return IsFlagsEnumDefined(value);
      }
      return Enum.IsDefined(typeof(T), value);
    }

    internal static bool IsFlagsEnumDefined<T>(T value) where T : struct
    {
      ulong flagsset = System.Convert.ToUInt64(value);
      Array values = Enum.GetValues(typeof(T));
      int flagno = values.Length - 1;
      ulong initialflags = flagsset;
      ulong flag = 0;

      while (flagno >= 0)
      {
        flag = System.Convert.ToUInt64(values.GetValue(flagno));
        if ((flagno == 0) && (flag == 0))
        {
          break;
        }

        //if the flags set contain this flag
        if ((flagsset & flag) == flag)
        {
          //unset this flag
          flagsset -= flag;
          if (flagsset == 0)
            return true;
        }
        flagno--;
      }
      if (flagsset != 0)
      {
        return false;
      }
      if (initialflags != 0 || flag == 0)
      {
        return true;
      }
      return false;
    }

    /// <summary>
    /// Parses the string representation of an arbitrary object into an Enum value
    /// </summary>
    /// <typeparam name="T">The type of Enum to expect from the object</typeparam>
    /// <param name="obj">The object to parse</param>
    /// <param name="name">The name or description of what the value represents. This will be reported back to the user if
    /// any error occurs during parsing.</param>
    /// <returns>The parsed <typeparamref name="T"/></returns>
    public static T Parse<T>(Object obj, String name) where T : struct // Enum
    {
      if (obj == null)
      {
        throw new ArgumentNullException(String.Format("No {0} specified", name));
      }
      return Parse<T>(obj.ToString(), name);
    }

    /// <summary>
    /// Parses text into an Enum value
    /// </summary>
    /// <typeparam name="T">The type of Enum to expect from the text</typeparam>
    /// <param name="text">The text to parse</param>
    /// <returns>The parsed <typeparamref name="T"/></returns>
    public static T Parse<T>(String text) where T : struct // Enum
    {
      return Parse<T>(text, "enumeration", false);
    }

    /// <summary>
    /// Parses text into an Enum value
    /// </summary>
    /// <typeparam name="T">The type of Enum to expect from the text</typeparam>
    /// <param name="text">The text to parse</param>
    /// <param name="ignoreCase">if set to <c>true</c> [ignore case].</param>
    /// <returns>The parsed <typeparamref name="T"/></returns>
    public static T Parse<T>(String text, bool ignoreCase) where T : struct // Enum
    {
      return Parse<T>(text, "enumeration", ignoreCase);
    }

    /// <summary>
    /// Parses text into an Enum value
    /// </summary>
    /// <typeparam name="T">The type of Enum to expect from the text</typeparam>
    /// <param name="text">The text to parse</param>
    /// <param name="name">The name or description of what the value represents. This will be reported back to the user if
    /// any error occurs during parsing.</param>
    /// <returns>The parsed <typeparamref name="T"/></returns>
    public static T Parse<T>(String text, String name) where T : struct // Enum
    {
      return Parse<T>(text, name, false);
    }

    /// <summary>
    /// Parses text into an Enum value
    /// </summary>
    /// <typeparam name="T">The type of Enum to expect from the text</typeparam>
    /// <param name="text">The text to parse</param>
    /// <param name="name">The name or description of what the value represents. This will be reported back to the user if
    /// any error occurs during parsing.</param>
    /// <param name="ignoreCase">true to ignore case</param>
    /// <returns>The parsed <typeparamref name="T"/></returns>
    public static T Parse<T>(String text, String name, Boolean ignoreCase) where T : struct // Enum
    {
      Boolean isDefined;
      T value = default(T);
      try
      {
        value = (T)Enum.Parse(typeof(T), text.Trim(), ignoreCase);
        isDefined = AreAllEnumsDefined(value);
      }
      catch (ArgumentException)
      {
        isDefined = false;
      }

      if (!isDefined)
      {
        throw new FormatException(
          String.Format("Invalid {0} [{1}] for {2}.\nValid options are: {3}",
                        typeof(T).Name,
                        text,
                        name,
                        String.Join(",", Enum.GetValues(typeof(T))
                                             .Cast<T>()
                                             .Select(e => e.ToString())
                                             .ToArray())));
      }
      return value;
    }
  }
}
