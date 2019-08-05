using System;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  [Serializable]
  public class Quote : IComparable<Quote>
  {
    public string Tenor;
    public double Value;
    public string Type;
    private int days_ = -1;

    private Quote() { }

    internal Quote(string tenor, double value, string type)
    {
      Tenor = tenor; Value = value; Type = type;
      return;
    }

    #region IComparable<Quote> Members

    public int CompareTo(Quote other)
    {
      if (other == null) return 1;
      return Days.CompareTo(other.Days);
    }

    private int Days
    {
      get
      {
        if (days_ < 0)
        {
          if (Tenor == null || Tenor.Length <= 1) days_ = 0;
          else
          {
            int last = Tenor.Length - 1;
            int unit = Tenor[last] == 'Y' ? 360 : (Tenor[last] == 'M' ? 30 :
              (Tenor[last] == 'W' ? 7 : 1));
            int len = Int32.Parse(Tenor.Substring(0, last));
            days_ = len * unit;
          }
        }
        return days_;
      }
    }
    #endregion
  } // class Quote

}
