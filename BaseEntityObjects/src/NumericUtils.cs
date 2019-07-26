/*
 * StringUtil.cs
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  ///   Utility class for numerical conversions
  /// </summary>
  public static class NumericUtils
  {
    /// <summary>
    ///   Convert an object to double value
    /// </summary>
    ///
    /// <param name="obj">The object to convert</param>
    /// <param name="blankValue">The value used when the object is null or a blank string</param>
    /// <param name="throwException">If true, throw an excpetion when conversion fails; otherwise, return NaN</param>
    ///
    /// <returns>The value as a double</returns>
    ///
    public static double ToDouble(object obj, double blankValue, bool throwException)
    {
      if (obj == null)
        return blankValue;
      if (obj is double)
        return (double)obj;
      string s = obj.ToString().Trim();
      if (s.Length == 0)
        return blankValue;
      if (throwException)
        return Double.Parse(s);
      double result;
      if( Double.TryParse(s, out result) )
        return result;
      else
        return Double.NaN;
    }

		/// <summary>
		/// Scales each of the numbers by the sum of all of the numbers.
		/// </summary>
		/// 
		/// <remarks>The array of doubles should be all positive as no checks are made that the sum is 0.</remarks>
		/// 
		/// <param name="numbers">Array of Double</param>
		/// 
		public static void Scale(double[] numbers)
		{
			if(numbers == null || numbers.Length == 0)
				return;

			// Get Sum
			double total = 0;
			for (int i = 0; i < numbers.Length; i++)
				total += numbers[i];

			// Scale
			for (int i = 0; i < numbers.Length; i++)
				numbers[i] /= total;
		}

    /// <summary>
    /// Excels the date to date time.
    /// </summary>
    /// <param name="excelDate">The excel date.</param>
    /// <returns></returns>
    public static DateTime ExcelDateToDateTime(double excelDate)
    {
      if (excelDate <= 367)
      {
        throw new ArgumentOutOfRangeException("excelDate", "dates prior to 1/1/1901 not supported");
      }
      var dateOfReference = new DateTime(1900, 1, 1);
      return dateOfReference.AddDays(excelDate - 2);
    }
	}
}
