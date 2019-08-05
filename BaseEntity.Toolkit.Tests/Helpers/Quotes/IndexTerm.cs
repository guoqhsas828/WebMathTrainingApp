using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace BaseEntity.Toolkit.Tests.Helpers.Quotes
{
  /// <summary>
  ///   Index term
  /// </summary>
  [Serializable]
  public class IndexTerm
  {
    #region Types
    /// <summary>
    ///   Infomation of individual component
    /// </summary>
    [Serializable]
    public class Component
    {
      /// <summary>
      ///   Enitity name
      /// </summary>
      public string Name;

      /// <summary>
      ///   Ticker
      /// </summary>
      public string Ticker;

      /// <summary>
      ///   Red code
      /// </summary>
      public string RedCode;

      public string DocClause;
      public string Country;
      public string Region;
      public string Type;
      public double Weight;

      internal Component(XmlNode refentity)
      {
        Name = XmlUtil.GetElementAsText(refentity, "name", null);
        Ticker = XmlUtil.GetElementAsText(refentity, "ticker", null);
        RedCode = XmlUtil.GetElementAsText(refentity, "red", null);
        DocClause = XmlUtil.GetElementAsText(refentity, "docclause", null);
        Country = XmlUtil.GetElementAsText(refentity, "country", null);
        Region = XmlUtil.GetElementAsText(refentity, "region", null);
        Type = XmlUtil.GetElementAsText(refentity, "type", null);
        Weight = XmlUtil.GetElementAsDouble(refentity, "weight", Double.NaN);
      }

      private Component() { }
    }

    /// <summary>
    ///   Term infomation
    /// </summary>
    [Serializable]
    public class Term
    {
      /// <summary>
      ///   Tenor name (6M, 1Y, 3Y, ...)
      /// </summary>
      public string TenorName;

      /// <summary>
      ///   Effective date
      /// </summary>
      public int Effective;

      /// <summary>
      ///   First premium date
      /// </summary>
      public int FirstPrem;

      /// <summary>
      ///   Frequency label in tenor format (3M, 6M, etc)
      /// </summary>
      public string Frequency;

      /// <summary>
      ///  Maurity date
      /// </summary>
      public int Maturity;

      /// <summary>
      ///   Deal Premium
      /// </summary>
      public double DealPremium;

      /// <summary>
      ///   Trade Id
      /// </summary>
      public string TradeId;

      internal Term(XmlNode termnode)
      {
        TenorName = XmlUtil.GetElementAsTenor(termnode, "period", null);
        Frequency = XmlUtil.GetElementAsText(termnode, "frequency", null);
        DealPremium = XmlUtil.GetElementAsDouble(termnode, "fixedrate", Double.NaN);
        Effective = XmlUtil.GetElementAsDate(termnode, "effective", 0);
        FirstPrem = XmlUtil.GetElementAsDate(termnode, "firstpayment", 0);
        Maturity = XmlUtil.GetElementAsDate(termnode, "maturity", 0);
        TradeId = XmlUtil.GetElementAsText(termnode, "tradeid", null);
      }

      internal Term(string tenorname, double premium)
      {
        TenorName = tenorname;
        DealPremium = premium;
      }

      private Term() { }
    }
    #endregion Types

    #region Read only data
    /// <summary>
    ///   Index id string
    /// </summary>
    public string IndexName;

    /// <summary>
    ///   Red code
    /// </summary>
    public string RedCode;

    /// <summary>
    ///   Index factor
    /// </summary>
    public double IndexFactor;

    /// <summary>
    ///   An array of index terms
    /// </summary>
    public Term[] Terms;

    /// <summary>
    ///   An array of reference entities
    /// </summary>
    public Component[] Components;

    /// <summary>
    ///   Currency
    /// </summary>
    public string Currency;

    public int AnnexDate;

    #endregion Read only data

    #region methods
    /// <summary>
    ///   Load index terms from a file
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="fileName">File name</param>
    /// <returns>An InDexTerm object</returns>
    public static IndexTerm Load(string indexName, string fileName)
    {
      FileStream stream = new FileStream(fileName, FileMode.Open);
      return Load(indexName, stream);
    }

    /// <summary>
    ///   Load index terms from a stream
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="stream">Stream of xml dateFile name</param>
    /// <returns>An InDexTerm object</returns>
    public static IndexTerm Load(string indexName, Stream stream)
    {
      XmlDocument doc = XmlUtil.LoadDocument(stream);
      XmlNode indexnode = doc.SelectSingleNode(
          "(descendant::*[(self::row or self::index) and child::indexname=\'" + indexName
          + "\'])");
      if (indexnode == null) return null;
      return new IndexTerm(indexnode);
    }

    internal IndexTerm(XmlNode indexnode)
    {
      IndexName = XmlUtil.GetElementAsText(indexnode, "indexname", null);
      RedCode = XmlUtil.GetElementAsText(indexnode, "red", null);
      IndexFactor = XmlUtil.GetElementAsDouble(indexnode, "indexfactor", Double.NaN);
      Currency = XmlUtil.GetElementAsText(indexnode, "ccy", null);
      AnnexDate = XmlUtil.GetElementAsDate(indexnode, "annex", 0);
      Terms = GetTerms(indexnode);
      Components = GetEndtities(indexnode);
    }

    internal IndexTerm(string indexname)
    {
      IndexName = indexname;
      RedCode = null;
      IndexFactor = 1;
    }

    private IndexTerm() { }

    private static Term[] GetTerms(XmlNode indexnode)
    {
      XmlNodeList list = indexnode.SelectNodes("child::terms");
      if (list == null || list.Count <= 0) return null;
      List<Term> terms = new List<Term>();
      foreach (XmlNode node in list)
        terms.Add(new Term(node));
      return terms.ToArray();
    }

    private static Component[] GetEndtities(XmlNode indexnode)
    {
      XmlNodeList list = indexnode.SelectNodes("component/refentity");
      if (list == null || list.Count <= 0) return null;
      List<Component> entities = new List<Component>();
      foreach (XmlNode node in list)
        entities.Add(new Component(node));
      return entities.ToArray();
    }
    #endregion Static methods
  }
}
