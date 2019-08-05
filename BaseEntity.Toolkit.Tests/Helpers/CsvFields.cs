//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  internal class CsvFields
  {
    private readonly Dictionary<string, int> header_;
    private readonly string[] line_;
    public CsvFields(Dictionary<string, int> header, string[] line)
    {
      header_ = header;
      line_ = line;
    }

    public string GetString(string s)
    {
      int idx;
      if (!header_.TryGetValue(s, out idx))
        throw new Exception(String.Format("Column [{0}] not exists", s));
      return line_.Length <= idx ? "" : line_[idx];
    }

    public double GetDouble(string s)
    {
      s = GetString(s);
      return s.Length == 0 ? Double.NaN : Double.Parse(s);
    }

    public Calendar GetCalendar(string s)
    {
      s = GetString(s);
      return new Calendar(s);
    }

    public T GetEnum<T>(string s) where T : struct
    {
      s = GetString(s);
      return (T)Enum.Parse(typeof(T), s);
    }

    public Dt GetExcelDate(string s)
    {
      s = GetString(s);
      return s.Length == 0 ? Dt.Empty : Dt.FromExcelDate(Int32.Parse(s));
    }
  }
}
