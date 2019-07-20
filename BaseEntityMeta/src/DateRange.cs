// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Object to represent a date range for a control or query.
  /// </summary>
  public class DateRange
  {
    #region Data

    private DateTime _fromDate;
    private DateTime _toDate;

    #endregion

    /// <summary>
    /// Constructor1
    /// </summary>
    public DateRange()
    {
      _fromDate = DateTime.MinValue;
      _toDate = DateTime.MaxValue;
    }

    /// <summary>
    /// Constructor2
    /// </summary>
    /// <param name="fromDate"></param>
    /// <param name="toDate"></param>
    public DateRange(DateTime fromDate, DateTime toDate)
    {
      _fromDate = fromDate;
      _toDate = toDate;
    }

    /// <summary>
    /// Beginning date of the range. DateTime.MinValue means no beginning
    /// </summary>
    public DateTime FromDate
    {
      get { return _fromDate; }
      set { _fromDate = value; }
    }

    /// <summary>
    /// Ending date of the range. DateTime.MaxValue indicates no end
    /// </summary>
    public DateTime ToDate
    {
      get { return _toDate; }
      set { _toDate = value; }
    }

    /// <summary>
    /// Convert values to a string representation
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      if (_fromDate == DateTime.MinValue && _toDate == DateTime.MaxValue)
        return "";

      if (_fromDate.Month == _toDate.Month && _fromDate.Year == _toDate.Year && _fromDate.Day == _toDate.Day)
      {
        return _fromDate.ToShortDateString();
      }

      if (_fromDate == DateTime.MinValue)
        return "-" + _toDate.ToShortDateString();

      if (_toDate == DateTime.MaxValue)
        return _fromDate.ToShortDateString() + "-";

      return _fromDate.ToShortDateString() + "-" + _toDate.ToShortDateString();
    }

    /// <summary>
    /// Parse a text string into a range object
    /// </summary>
    /// <param name="text"></param>
    /// <param name="range"></param>
    /// <returns></returns>
    public static bool TryParse(string text, out DateRange range)
    {
      int idx = text.Trim().IndexOf('-');
      DateTime temp;

      if (idx == -1)
      {
        // Single date typed in 
        if (DateTime.TryParse(text, out temp))
        {
          // If we can parse it then thats the date (ignore time)
          range = new DateRange(new DateTime(temp.Year, temp.Month, temp.Day),
            new DateTime(temp.Year, temp.Month, temp.Day));
          return true;
        }
      }
      else if (idx == 0)
      {
        // Before
        if (DateTime.TryParse(text.Trim().Substring(1), out temp))
        {
          range = new DateRange(DateTime.MinValue, new DateTime(temp.Year, temp.Month, temp.Day));
          return true;
        }
      }
      else if (idx == text.Trim().Length - 1)
      {
        // After
        if (DateTime.TryParse(text.Trim().Substring(0, text.Trim().Length - 1), out temp))
        {
          range = new DateRange(new DateTime(temp.Year, temp.Month, temp.Day), DateTime.MaxValue);
          return true;
        }
      }
      else if (idx > 0 && idx < text.Trim().Length - 1)
      {
        // Between
        string first = text.Trim().Substring(0, idx);
        string second = text.Trim().Substring(idx + 1);

        DateTime temp1;
        DateTime temp2;

        if (DateTime.TryParse(first, out temp1) && DateTime.TryParse(second, out temp2))
        {
          // If we can parse it then thats the date (ignore time)
          range = new DateRange(new DateTime(temp1.Year, temp1.Month, temp1.Day),
            new DateTime(temp2.Year, temp2.Month, temp2.Day));

          return true;
        }
      }

      range = new DateRange();
      return false;
    }
  }
}