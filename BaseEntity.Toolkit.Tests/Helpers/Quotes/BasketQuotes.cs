using System;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  [Serializable]
  public partial class BasketQuotes
  {
    public IndexTerm Index;
    public CDSQuote[] CdsQuotes;
    public IndexQuote[] IndexQuotes;
    public IndexTrancheQuote[][] TrancheQuotes;

    private BasketQuotes() { }

  } // class BasketQuotes
}
