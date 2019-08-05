using System;
using System.Collections.Generic;
using System.Xml;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  [Serializable]
  public class IndexTrancheQuote : IComparable<IndexTrancheQuote>
  {
    public string IndexName;
    public string Tenor;
    public int Maturity;
    public int Date;
    public double Attachment;
    public double Detachment;
    public double Bid;
    public double Ask;
    public double Mid;
    public string QuoteType;

    public bool Missing => QuoteType == null;

    private IndexTrancheQuote() { }

    internal IndexTrancheQuote(string name, string tenor, double ap, double dp)
    {
      IndexName = name; Tenor = tenor;
      Attachment = ap; Detachment = dp;
    }

    internal IndexTrancheQuote(XmlNode node)
    {
      IndexName = XmlUtil.GetElementAsText(node, "IndexName", null);
      Tenor = XmlUtil.GetElementAsTenor(node, "IndexTerm", null);
      Maturity = XmlUtil.GetElementAsDate(node, "IndexMaturity", 0);
      Date = XmlUtil.GetElementAsDate(node, "Date", 0);
      Attachment = XmlUtil.GetElementAsDouble(node, "Attachment", Double.NaN);
      Detachment = XmlUtil.GetElementAsDouble(node, "Detachment", Double.NaN);
      string type = "UpFront";
      double bid = XmlUtil.GetElementAsDouble(node, "TrancheUpfrontBid", Double.NaN);
      double ask = XmlUtil.GetElementAsDouble(node, "TrancheUpfrontAsk", Double.NaN);
      double mid = XmlUtil.GetElementAsDouble(node, "TrancheUpfrontMid", Double.NaN);
      if (Double.IsNaN(bid) && Double.IsNaN(ask) && Double.IsNaN(mid))
      {
        type = "Spread";
        bid = XmlUtil.GetElementAsDouble(node, "TrancheSpreadBid", Double.NaN);
        ask = XmlUtil.GetElementAsDouble(node, "TrancheSpreadAsk", Double.NaN);
        mid = XmlUtil.GetElementAsDouble(node, "TrancheSpreadMid", Double.NaN);
      }
      Bid = bid; Ask = ask; Mid = mid;
      if (!(Double.IsNaN(bid) && Double.IsNaN(ask) && Double.IsNaN(mid)))
        QuoteType = type;
      return;
    }

    public static IndexTrancheQuote[] Load(XmlNode doc, string indexName, string tenorName)
    {
      string xpath = "//row[child::IndexName=\'" + indexName + ' ' + tenorName + "']";
      XmlNodeList nodelist = doc.SelectNodes(xpath);
      if (nodelist == null || nodelist.Count == 0) return null;
      List<IndexTrancheQuote> quotes = new List<IndexTrancheQuote>();
      foreach (XmlNode node in nodelist)
        quotes.Add(new IndexTrancheQuote(node));
      quotes.Sort();
      return quotes.ToArray();
    }


    #region IComparable<IndexTrancheQuote> Members

    public int CompareTo(IndexTrancheQuote other)
    {
      return Attachment.CompareTo(other.Attachment);
    }

    #endregion
  }
}
