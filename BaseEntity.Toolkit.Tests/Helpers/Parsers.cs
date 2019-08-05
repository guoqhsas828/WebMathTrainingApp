//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges;
using BaseEntity.Toolkit.Numerics;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  public static class Parsers
  {
    #region Initialization

    private static readonly Dictionary<Type, Delegate> KnownParsers
      = new Dictionary<Type, Delegate>();

    private static void AddParser<T>(
      Dictionary<Type, Delegate> parsers,
      Func<string, T> parser)
    {
      parsers.Add(typeof(T), parser);
    }

    public static void Initialize()
    {
      var map = KnownParsers;
      AddParser(map, Tenor.Parse);
      AddParser(map, Calendar.Parse);
      AddParser(map, Parsers.ParseDt);
      AddParser(map, Parsers.ParseDouble);
      AddParser(map, Parsers.ParseInterpScheme);
      AddParser(map, Parsers.ParseInterp);
      AddParser(map, Parsers.ParseTable);
      AddParser(map, Parsers.ParseArray2D<double>);
      AddParser(map, Parsers.ParseArray2D<string>);
      AddParser(map, Parsers.ParseCurvePoints);
      AddParser(map, FxVolatilityQuoteTermParser.Parse);
    }

    public static T Parse<T>(string input)
    {
      if (KnownParsers.TryGetValue(typeof(T), out var f))
      {
        return ((Func<string, T>)f).Invoke(input);
      }

      return (T)Convert.ChangeType(input, typeof(T));
    }

    public static object Parse(Type type, string input)
    {
      if (type == typeof(string)) return input;

      if (KnownParsers.TryGetValue(type, out var f))
      {
        return f.DynamicInvoke(input);
      }

      if (type.IsEnum)
      {
        return Enum.Parse(type, input);
      }

      if (type.IsArray)
      {
        var elemType = type.GetElementType();
        switch (type.GetArrayRank())
        {
        case 1:
          return ParseArray1D(elemType, input);
        }
      }

      return Convert.ChangeType(input, type);
    }

    private static object ParseArray1D(Type elemType, string input)
    {
      if (string.IsNullOrEmpty(input))
        return Array.CreateInstance(elemType, 0);

      var items = input.Split(',');
      if (elemType == typeof(string)) return items;

      var a = Array.CreateInstance(elemType, items.Length);
      for (int i = 0; i < items.Length; ++i)
      {
        var v = Parse(elemType, items[i]);
        a.SetValue(v, i);
      }
      return a;
    }


    /// <summary>
    ///   Parse a comma delimited string
    /// </summary>
    /// <param name="types">Types of elements</param>
    /// <param name="commaDelimted">Comma delimited string</param>
    /// <returns>objects</returns>
    public static object[] Parse(Type[] types, string commaDelimted)
    {
      if (commaDelimted == null || commaDelimted.Length <= 0)
        return null;
      Regex regex = new Regex(@"\s*,\s*");
      string[] elems = regex.Split(commaDelimted);
      if (elems.Length != types.Length)
        throw new ArgumentException(String.Format(
          "Length of input data ({0}) differs from the required length ({1})",
          elems.Length, types.Length));
      object[] objs = new object[types.Length];
      for (int i = 0; i < types.Length; ++i)
        objs[i] = Parse(types[i], elems[i]);
      return objs;
    }

    #endregion

    /// <summary>
    /// Parses the two dimensional array of type T.
    /// </summary>
    /// <param name="input">The input.</param>
    public static T[,] ParseArray2D<T>(this string input)
    {
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input)) return null;
      var list = new List<string[]>();
      int begin = 0, end = 0, cols = 0;
      while ((begin = input.IndexOf('{', begin)) >= 0)
      {
        end = input.IndexOf('}', begin + 1);
        if (end < 0)
        {
          throw new Exception("Invalid array input");
        }
        var row = input.Substring(begin + 1, end - begin - 1).Split(',');
        if (cols < row.Length) cols = row.Length;
        list.Add(row);
        begin = end + 1;
      }
      if (cols <= 0 || list.Count == 0) return new T[0, 0];
      int rows = list.Count;
      var result = new T[rows, cols];
      for (int i = 0; i < rows; ++i)
      {
        var row = list[i];
        for (int j = 0; j < row.Length; ++j)
        {
          var s = row[j].Trim();
          if (!String.IsNullOrEmpty(s))
          {
            result[i, j] = Parse<T>(s);
          }
        }
      }
      return result;
    }

    /// <summary>
    ///  Method to Parse the date input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>Date</returns>
    /// <remarks></remarks>
    public static Dt ParseDt(this string input)
    {
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input)) return Dt.Empty;
      var fmt = GuessDtFormat(input);
      if (fmt != null) return Dt.FromStr(input, fmt);
      throw new Exception(String.Format("Unknown Dt format: {0}", input));
    }

    /// <summary>
    /// Parses the double value.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static double ParseDouble(this string input)
    {
      if (input != null)
      {
        input = input.Replace(",", "").Trim();
      }
      if (String.IsNullOrEmpty(input)) return 0.0;
      double scale = 1.0;
      int last = input.Length - 1;
      if (input[last] == '%')
      {
        scale = 100;
        input = input.Substring(0, last);
      }
      var value = Double.Parse(input);
      return value / scale;
    }

    /// <summary>
    /// Parses the interpolation scheme input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static InterpScheme ParseInterpScheme(this string input)
    {
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input)) return null;
      var list = input.Split(';');
      var interp = list[0].Trim();
      if (String.IsNullOrEmpty(interp)) return null;
      ExtrapMethod lo, hi;
      lo = hi = ExtrapMethod.None;
      if (list.Length > 1)
      {
        var extraps = list[1].Split('/');
        lo = hi = ParseEnum(extraps[0], ExtrapMethod.None);
        if (extraps.Length > 1)
          hi = ParseEnum(extraps[1], ExtrapMethod.None);
      }
      return InterpScheme.FromString(interp, hi, lo);
    }

    /// <summary>
    /// Parses the interpolation input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Interp ParseInterp(this string input)
    {
      var interpScheme = ParseInterpScheme(input);
      return interpScheme.ToInterp();
    }

    /// <summary>
    /// Parses a table of data, with rows delimited by new line ('\n')
    ///  and columns delimited by tab ('\t').
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static DataTable ParseTable(this string input)
    {
      var table = new DataTable();
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input))
      {
        return table;
      }
      var data = input.Split('\n')
        .Select(s => s.Trim()).Where(s => !String.IsNullOrEmpty(s))
        .Select(s => s.Split('\t').ToArray())
        .ToArray();
      var head = data[0];
      for (int j = 0; j < head.Length; ++j)
        table.Columns.Add(head[j], typeof(object));
      for (int i = 1; i < data.Length; ++i)
      {
        var tr = table.NewRow();
        var dr = data[i];
        int count = dr.Length < head.Length ? dr.Length : head.Length;
        for (int c = 0; c < count; ++c)
        {
          var s = dr[c];
          if (String.IsNullOrEmpty(s)) continue;
          if (IsNumber(s))
          {
            tr[c] = ParseDouble(s);
            continue;
          }
          var fmt = GuessDtFormat(s);
          if (fmt != null) tr[c] = Dt.FromStr(s, fmt);
          else tr[c] = s;
        }
        table.Rows.Add(tr);
      }
      return table;
    }

    public static CurvePoint[] ParseCurvePoints(string input)
    {
      var data = input.ParseArray2D<string>();
      if (data.GetLength(1) != 2)
      {
        throw new ArgumentException("Curve points must formated as n x 2 matrix.");
      }
      var m = data.GetLength(0);
      var points = new CurvePoint[m];
      for (int i = 0; i < m; ++i)
      {
        points[i] = new CurvePoint(ParseDt(data[i, 0]), ParseDouble(data[i, 1]));
      }
      return points;
    }

    public static bool IsNumber(string input)
    {
      return regexNumber_.IsMatch(input);
    }
    private static readonly Regex regexNumber_ = new Regex(
      @"^[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?%?$", RegexOptions.Compiled);

    public static string GuessDtFormat(string input)
    {
      var m = regexDt.Match(input);
      if (m.Success)
      {
        for (int i = 1; i <= 4; ++i)
          if (!String.IsNullOrEmpty(m.Groups[i].Value))
            return formatDt[i - 1];
      }
      return null;
    }
    private static readonly Regex regexDt = new Regex(
      @"^(?:(\d{1,2}-[a-zA-Z]+-\d{2,4})|(\d{1,2}/\d{1,2}/\d{2,4})|(\d{1,2}/\d{1,2}/\d{2,4} \d{1,2}:\d{1,2}:\d{1,2})|((?:19|20)\d\d(?:0\d|1[012])(?:[0-2]\d|3[01])))$",
      RegexOptions.Compiled);
    private static readonly string[] formatDt = new[]
      {
        "%d-%b-%Y", "%D", "%D %T", "%Y%m%d"
      };

    private static T ParseEnum<T>(string input, T defaultValue) where T : struct
    {
      input = input.Trim();
      if (String.IsNullOrEmpty(input)) return defaultValue;
      return (T)Enum.Parse(typeof(T), input);
    }
  }

  public static class FxVolatilityQuoteTermParser
  {
    static readonly string[] keys = new[]
    {
      "CURRENCY1", "CURRENCY2", "ATMSETTINGS", "DELTAPREMIUM",
      "DELTASTYLE", "RISKREVERSAL", "BUTTERFLY"
    };

    public static FxVolatilityQuoteTerm Parse(string input)
    {
      var terms = new string[keys.Length];
      var count = input.Split('\n', '\r', ';').Select(s => s.Trim())
        .Count(s => GetTerm(s, terms));
      if (count < 2 || terms[0] == null || terms[1] == null)
        throw new Exception("Must specify currency pair.");
      return new FxVolatilityQuoteTerm(
        (Currency)Enum.Parse(typeof(Currency), terms[0]),
        (Currency)Enum.Parse(typeof(Currency), terms[1]),
        terms[2] ?? "DeltaNeutral",
        terms[3] ?? "Excluded",
        terms[4] ?? "Spot",
        terms[5] ?? "Ccy1CallPut",
        terms[6] ?? "CallPutAverage");
    }

    private static bool GetTerm(string line, string[] terms)
    {
      var items = line.Split(new[] { ':' }, 2);
      if (items.Length < 2) return false;
      var key = Regex.Replace(items[0], @"\s+", "").ToUpperInvariant();
      var idx = Array.IndexOf(keys, key);
      if (idx < 0) return false;
      var term = items[1].Trim();
      if (!String.IsNullOrEmpty(term))
        terms[idx] = term;
      return true;
    }
  }



  public static class FilePaths
  {
    public static string GetFullPath(this string filename,
      bool mustExists = true)
    {
      if (File.Exists(filename))
        return Path.GetFullPath(filename);
      var qnroot = BaseEntityContext.InstallDir;
      if (string.IsNullOrEmpty(qnroot))
        qnroot = Environment.GetEnvironmentVariable("baseentity_install_dir");
      if (!string.IsNullOrEmpty(qnroot))
      {
        var path = Path.Combine(qnroot, filename);
        if (File.Exists(path)) return Path.GetFullPath(path);
        if (!mustExists)
        {
          var dir = Path.GetDirectoryName(path);
          if (dir != null && Directory.Exists(dir))
            return Path.GetFullPath(path);
        }
      }
      throw new FileNotFoundException($"Unable to locate file: {filename}");
    }
  }
}
