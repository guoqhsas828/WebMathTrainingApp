//
// Conversion.cs
//  -2010. All rights reserved.
//
using System;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   String conversion helper class. Static methods convert strings to
  ///   values and objects.
  /// </summary>
  public static class Conversion
  {
    /// <summary>
    ///   Convert an array of strings to an array of doubles
    /// </summary>
    /// <param name="inputs">Input string</param>
    /// <param name="allowBlank">If true, blank strings are changed into the blank value that is given.;
    /// Otherwise, an exception is thrown when an input string is blank.</param>
    /// <param name="blankValue">Value to replace the blank string. Used when <paramref name="allowBlank"/>
    /// is true.</param>
    /// <param name="wasChanged">returned to indicate which array members were changed</param>
    /// <returns>Array of doubles converted from array of strings</returns>
    /// <example>
    /// <para>Suppose <c>quotes</c> is an array of strings representing quote
    /// values, where a missing data point is represented as an empty string.
    /// Then the following call converts it into an array of double values
    /// with missing quote represented as <c>NaN</c>.
    /// The changed array can be used later on to remove bad values from multiple arrays or to skip over values.
    /// </para>
    /// <code>
    ///   double[] quoteValues = Conversion.ToDouble(quotes, true, Double.NaN);
    /// </code>
    /// </example>
    public static double[] ToDouble(string[] inputs, bool allowBlank, double blankValue, ref bool[] wasChanged)
    {
      if (inputs == null) return null;

      double[] results = new double[inputs.Length];
      //keep track of altered values
      wasChanged = new bool[inputs.Length];
      for (int i = 0; i < inputs.Length; ++i)
        results[i] = ToDouble(inputs[i], allowBlank, blankValue, ref wasChanged[i]);

      return results;
    }

    /// <summary>
    ///  Convert a string to double
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="allowBlank">If true, blank strings are changed into the blank value that is given.;
    /// Otherwise, an exception is thrown when an input string is blank.</param>
    /// <param name="blankValue">Value to replace the blank string. Used when <paramref name="allowBlank"/>
    /// is true.</param>
    /// <param name="wasChanged">returned to indicate if values was changed</param>
    /// <returns>double converted from string</returns>
    public static double ToDouble(string input, bool allowBlank, double blankValue, ref bool wasChanged)
    {
      if (String.IsNullOrEmpty(input))
      {
        if (!allowBlank)
          throw new ArgumentNullException("input", @"input cannot be null");
        wasChanged = true;
        return blankValue;
      }
      return Double.Parse(input.Trim());
    }

    /// <summary>
    ///  Conert a string to double
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="allowBlank">If true, blank strings are changed into the blank value that is given.;
    /// Otherwise, an exception is thrown when an input string is blank.</param>
    /// <param name="blankValue">Value to replace the blank string. Used when <paramref name="allowBlank"/>
    /// is true.</param>
    /// <returns>double converted from string</returns>
    public static double ToDouble(string input, bool allowBlank, double blankValue)
    {
      bool wasChanged = false;
      return ToDouble(input, allowBlank, blankValue, ref wasChanged);
    }

    /// <summary>
    ///  Convert string array to double array
    /// </summary>
    /// <param name="inputs">Input string array</param>
    /// <param name="allowBlank">If true, blank strings are changed into the blank value that is given.;
    /// Otherwise, an exception is thrown when an input string is blank.</param>
    /// <param name="blankValue">Value to replace the blank string. Used when <paramref name="allowBlank"/>
    /// is true.</param>
    /// <returns>Array of doubles converted from array of strings</returns>
    public static double[,] ToDouble(string[,] inputs, bool allowBlank, double blankValue)
    {
      if (inputs == null) return null;
      int rows = inputs.GetLength(0);
      int cols = inputs.GetLength(1);
      double[,] results = new double[rows, cols];
      for (int i = 0; i < rows; ++i)
        for (int j = 0; j < cols; ++j)
          results[i, j] = ToDouble(inputs[i, j], allowBlank, blankValue);
      return results;
    }

    /// <summary>
    ///  Convert a string to enum
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <param name="input">Input string</param>
    /// <param name="allowBlank">Allow blank string</param>
    /// <param name="blankValue">Blank value if allow blank</param>
    /// <returns>Enum converted from string</returns>
    public static T ToEnum<T>(string input, bool allowBlank, T blankValue)
    {
      if (input == null)
      {
        if( !allowBlank )
          throw new ArgumentNullException("input", @"input cannot be null");
        return blankValue;
      }
      input = input.Trim();
      if (input.Length == 0 && allowBlank)
        return blankValue;
      return (T)Enum.Parse(typeof(T), input);
    }

    /// <summary>
    /// Convert string to generic object
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <param name="input">input string</param>
    /// <param name="allowBlank">Allow blank string</param>
    /// <param name="blankValue">Blank value if allow blank</param>
    /// <returns>Object converted from string</returns>
    public static T ToObject<T>(string input, bool allowBlank, T blankValue) where T : IConvertible
    {
      if (input == null)
      {
        if (!allowBlank)
          throw new ArgumentNullException("input", @"input cannot be null");
        return blankValue;
      }
      input = input.Trim();
      if (input.Length == 0)
      {
        if (!allowBlank)
          throw new ArgumentNullException("input", @"input cannot be empty");
        return blankValue;
      }
      return (T)Convert.ChangeType(input, typeof(T));
    }

    internal static int SetFlag(this int flags, int bits, bool set)
    {
      return set ? (flags | bits) : (flags & ~bits);
    }
  }
}

