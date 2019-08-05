using System;
using System.Xml;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  /// <summary>
  ///   Represent an index quote
  /// </summary>
  [Serializable]
  public class IndexQuote
  {
    /// <summary>
    ///   Index id string
    /// </summary>
    public string TradeId;

    /// <summary>
    ///   Quote date
    /// </summary>
    public int Date;

    /// <summary>
    ///   Price quote
    /// </summary>
    public double Price;

    /// <summary>
    ///   Spread quote
    /// </summary>
    public double Spread;

    /// <summary>
    ///   Maturity date
    /// </summary>
    public int Maturity;

    /// <summary>
    ///   Tenor name
    /// </summary>
    public string Tenor;

    private IndexQuote() { }

    internal IndexQuote(string id)
    {
      TradeId = id;
      Date = Maturity = 0;
      Price = Spread = Double.NaN;
      Tenor = null;
    }

    internal IndexQuote(XmlNode node)
    {
      TradeId = XmlUtil.GetElementAsText(node, "IndexID", null);
      Date = XmlUtil.GetElementAsDate(node, "Date", 0);
      Price = XmlUtil.GetElementAsDouble(node, "ModelPrice", Double.NaN);
      Spread = XmlUtil.GetElementAsDouble(node, "ModelSpread", Double.NaN);
      Maturity = XmlUtil.GetElementAsDate(node, "Maturity", 0);
      Tenor = XmlUtil.GetElementAsText(node, "Term", null);
    }

    /// <summary>
    ///   Load CDS quotes of a ticker from an XML tree
    /// </summary>
    /// <param name="doc">XML tree</param>
    /// <param name="tradeId">Index ID</param>
    /// <returns>Index Quote</returns>
    public static IndexQuote Load(XmlNode doc, string tradeId)
    {
      XmlNode node = doc.SelectSingleNode("//row[child::IndexID=\'" + tradeId + "\']");
      if (node == null) return new IndexQuote(tradeId);
      return new IndexQuote(node);
    }

    public override string ToString()
    {
      if (Double.IsNaN(Price) && Double.IsNaN(Spread))
        return TradeId + "[missing]";
      string result = TradeId + '@' + Date + '[';
      if (!Double.IsNaN(Spread))
      {
        if (!Double.IsNaN(Price))
          result += "Spread:" + Spread + ",Price:" + Price;
        else
          result += "Spread:" + Spread;
      }
      else
        result += "Price:" + Price;
      result += ']';
      return result;
    }
  } // class IndexQuote
}
