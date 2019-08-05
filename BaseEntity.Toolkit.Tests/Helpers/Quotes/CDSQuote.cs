using System;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  [Serializable]
  public class CdsDefaultInfo
  {
    public int DefaultDate;
    public int SettleDate;
    public bool NotSettled;
  }


  [Serializable]
  public partial class CDSQuote : IComparable<CDSQuote>
  {
    [Serializable]
    public class RefinanceInfo
    {
      public double AnnualRate;
      public double Correlation;
    }

    public string Ticker;
    public int Date;
    public string Currency;
    public double Recovery;
    public Quote[] Quotes;
    public RefinanceInfo Refinance;
    public CdsDefaultInfo DefaultInfo;

    private CDSQuote() { }

    public override string ToString()
    {
      if (Quotes == null || Quotes.Length == 0)
        return Ticker + (DefaultInfo == null ? "[missing]" : "[defaulted]");
      string result= Ticker + '@' + Date 
        + '[' + Quotes[0].Tenor + ':' + Quotes[0].Value;
      for (int i = 1; i < Quotes.Length; ++i)
        result += ',' + Quotes[i].Tenor + ':' + Quotes[i].Value;
      result += ']';
      return result;
    }


    #region IComparable<CDSQuote> Members

    public int CompareTo(CDSQuote other)
    {
      return Ticker.CompareTo(other.Ticker);
    }

    #endregion
  } // class CDSQuote
}
