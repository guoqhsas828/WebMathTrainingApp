/*
 * StringUtil.cs
 *
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BaseEntity.Shared
{
  /// <summary>
  /// </summary>
  public static class StringUtil
  {
    private const string CONST_FILESYSTEM_SPECIALCHARS = @"[\\\/:\*\?""<>|]"; 
 
    /// <summary>
    /// Check if a string is either null, empty, or blank
    /// </summary>
    public static bool HasValue(string strValue)
    {
#if FASTER_WAY
      return string.IsNullOrWhiteSpace(strValue); // RD Jul'18
#else
      if (strValue == null || strValue.Length == 0 || strValue.Trim().Length == 0)
        return false;

      // Has something
      return true;
#endif
    }

    /// <summary>
    /// Handles the special characters in a sql string parameter.
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string GetSqlSafeString(string str)
    {
      if (str == null)
        return "null";
      else
        return ("'" + str.Replace("'", "''") + "'");
    }

    /// <summary>
    ///   Returns an empty string if null
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string GetNullSafeString(string str)
    {
      return (String.IsNullOrEmpty(str)) ? String.Empty : str;
    }

		/// <summary>
		/// Handles single quotes in a string and escapes them to be double quotes for correct processing.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string GetEscapedString(string str)
		{
			if (str == null)
				return null;
			else
				return str.Replace("'", "''");
		}

    /// <summary>
    /// Compares 2 strings using DOS standard wildcards.
    /// </summary>
    /// <param name="target">The string to check for a match.</param>
    /// <param name="pattern">The pattern to check the target string against.</param>
    /// <returns>Whether the target matches the pattern.</returns>
    public static bool Like(string target, string pattern)
    {
      //adjust pattern to use in regex
      pattern = pattern.Replace("*", ".*");
      pattern = pattern.Replace("?", ".{1}");
      pattern = pattern.Replace("+", @"\+");

      //compare and return
      return Regex.Match(target, pattern).Success;
    }

    /// <summary>
    /// remove junk from a numeric string
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static double CorrectNumericText(object s)
    {
      return CorrectNumericText(s as string);
    }

    /// <summary>
    /// remove junk from a numeric string
    /// </summary>
    /// <param name="temp"></param>
    /// <returns></returns>
    public static double CorrectNumericText(string temp)
    {
      string result = "";
      double resultd = 0;
      int i = 0;
      bool isNegative = false;
      string DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

      bool foundPeriod = false;
      while (temp != null && i < temp.Length)
      {
        if (i == 0 && temp[i].ToString().Equals(CultureInfo.CurrentCulture.NumberFormat.NegativeSign))
        {
          //this is separate to prevent parse errors on one char strings like "-"
          isNegative = true;
        }

        if ((Char.IsDigit(temp[i]) || temp[i].ToString().Equals(DecimalSeparator)))
        {
          //one period allowed
          if (temp[i].ToString().Equals(DecimalSeparator))
          {
            if (!foundPeriod)
            {
              result += temp[i];
              foundPeriod = true;
            }
          }
          else
          {
            result += temp[i];
          }
        }
        i++;
      }

      if (HasValue(result) && !result.Equals(DecimalSeparator))
      {
        Double.TryParse(result, out resultd);
        resultd *= (isNegative ? -1 : 1);
      }

      return resultd;
    }


    /// <summary>
    /// Function will clean the provided filename of chars that will be problems for the filesystem.
    /// will stripe out  <![CDATA[\/:*?"|<>]]>
    /// 
    /// DO NOT PASS PATH
    /// </summary>
    /// <param name="filename">filename</param>
    /// <returns></returns>
    public static string CleanFileName(this string filename)
    {
      return filename.CleanFileName(String.Empty);
    }

    /// <summary>
    /// Function will clean the provided filename of chars that will be problems for the filesystem.
    /// will stripe out  <![CDATA[\/:*?"|<>]]>
    /// replace with
    ///      
    /// DO NOT PASS PATH
    /// </summary>
    /// <param name="filename">filename</param>
    /// <param name="replacementChar">Char</param>
    /// <returns>clean filename string</returns>
    public static string CleanFileName(this string filename, string replacementChar)
    {
      // Internet says that this is actutally batter than using static Regex.Replace method. 
      // possible re-use savings.
      // Either way they do the same thing.
      return new Regex(CONST_FILESYSTEM_SPECIALCHARS, RegexOptions.None).Replace(filename, replacementChar);
    }

    /// <summary>
    /// Computes the Levenshtein Distance between 2 strings.
    /// </summary>
    /// 
    /// <remarks>
    /// Algorithm obtained from the linked site and translated from Visual Basic 
    /// to C#. See <a href="http://www.merriampark.com/ld.htm#FLAVORS">this site</a> 
    /// for more information.
    /// </remarks>
    /// 
    /// <param name="s">String 1</param>
    /// <param name="t">String 2</param>
    ///  
    /// <returns>Integer</returns>
    /// 
    public static int LevenshteinDistance(string s, string t)
    {
      int[,] d;
      // matrix
      int m;
      // length of t
      int n;
      // length of s
      int i;
      // iterates through s
      int j;
      // iterates through t
      string s_i;
      // ith character of s
      string t_j;
      // jth character of t
      int cost;
      // cost

      // Step 1
      n = (s == null ? 0 : s.Length);
      m = (t == null ? 0 : t.Length);
      if (n == 0)
        return m;
      if (m == 0)
        return n;

      // Step 2
      d = new int[n+1, m+1];
      for (i = 0; i <= n; i++)
        d[i, 0] = i;
      for (j = 0; j <= m; j++)
        d[0, j] = j;

      // Step 3
      for (i = 1; i <= n; i++)
      {
        s_i = s.Substring(i-1, 1);

        // Step 4
        for (j = 1; j <= m; j++)
        {

          t_j = t.Substring(j-1, 1);

          // Step 5
          if (s_i == t_j)
            cost = 0;
          else
            cost = 1;

          // Step 6
          int temp = Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1);
          d[i, j] =  Math.Min(temp, d[i - 1, j - 1] + cost);
        }
      }

      // Step 7
      return d[n, m];
    }

  	/// <summary>
  	/// Utility method to convert a string of format a+b+c or a;b;c to a list or null
  	/// </summary>
  	/// <param name="s"></param>
  	/// <returns></returns>
  	public static List<string> StringToList(string s)
  	{
  		return String.IsNullOrEmpty(s) ? null : s.Split(new[] { '+', ';' }).ToList();
  	}

    /// <summary>
    /// Given a full path of a riskview get the folder portion of the path and the file name portion
    /// foldername has slashes trimmed off.
    /// Assumes viewname is last bit after last slash
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="folderName">path without file name. Already trimmed slashes</param>
    /// <param name="fileName">filename without path</param>
    public static void SplitFolderAndFileName(string fullPath, out string folderName, out string fileName)
    {
      if (String.IsNullOrEmpty(fullPath))
      {
        folderName = null;
        fileName = null;
        return;
      }

      var tokens = fullPath.Replace('/', '\\').Trim(new[] { '\\' }).Split(new[] { '\\' });
      var lastTokenIdx = tokens.Length - 1;

      if (lastTokenIdx < 0)
      {
        folderName = null;
        fileName = null;
        return;
      }

      if (lastTokenIdx == 0)
      {
        fileName = tokens[0];
        folderName = null;
        return;
      }

      folderName = fullPath.Substring(0, fullPath.Length - tokens[lastTokenIdx].Length).Trim(new[] { '\\' });

      fileName = tokens[lastTokenIdx];
    }
  }
}
