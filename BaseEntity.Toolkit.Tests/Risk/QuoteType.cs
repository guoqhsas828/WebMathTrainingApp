using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   A Quote type indicates the particular type of quote
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The Quote type dictates the display format and data
  ///   validation</para>
  /// </remarks>
  ///
  [Serializable]
  [Entity(EntityId = 201, AuditPolicy = AuditPolicy.History, Description = "A Quote type indicates the particular type of quote")]
  public sealed class QuoteType : AuditedObject
  {
    #region Properties

    /// <summary>
    ///   Name of quote type
    /// </summary>
    [StringProperty(MaxLength = 128, IsKey = true)]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Description of quote type
    /// </summary>
    [StringProperty(MaxLength = 256)]
    public string Description
    {
      get { return description_; }
      set { description_ = value; }
    }

    /// <summary>
    ///   Quoting convention
    /// </summary>
    [EnumProperty]
    public QuotingConvention QuotingConvention
    {
      get { return quotingConvention_; }
      set { quotingConvention_ = value; }
    }

    #endregion

    #region Data

    private string name_;
    private string description_;
    private QuotingConvention quotingConvention_;

    #endregion

  } // class QuoteType

  /// <summary>
  ///   Helper classes for QuoteType
  /// </summary>
  public static class QuoteTypeUtil
  {
    #region Methods

    /// <summary>
    ///   Get by id
    /// </summary>
    public static QuoteType FindById(Int64 id)
    {
      return (QuoteType)Session.Get(typeof(QuoteType), id);
    }

    /// <summary>
    ///   Get by name
    /// </summary>
    public static QuoteType FindByName(string name)
    {
      return _cache.GetOrAdd(name, LoadByName);
    }

    private static QuoteType LoadByName(string name)
    {
      var quoteType = Session.CreateCriteria(typeof(QuoteType)).Add(Restrictions.Eq("Name", name)).UniqueResult<QuoteType>();
      if (quoteType != null)
      {
        MetaUtility.ResolveAll(quoteType);
      }
      return quoteType;
    }

    /// <summary>
		///   Get all
		/// </summary>
		public static IList FindAll()
    {
      return Session.CreateCriteria(typeof(QuoteType)).List();
    }

    #endregion

    #region FormatInformation

    /// <summary>
    /// How to display or allow edit formatting of a particular quotetype
    /// Currently necessary for TradedLevel on Trade
    /// </summary>
    public enum FormatType
    {
      /// <summary>
      /// No special formatting
      /// </summary>
      None,

      /// <summary>
      /// Percentage including % symbol where applicable (.01 stored but 1% displayed)
      /// </summary>
      Percent,

      /// <summary>
      /// Basis Points including 'bps' suffix (.01 stored but 1000 bps displayed)
      /// </summary>
      BasisPoints,

      /// <summary>
      /// Percentage but dont show % symbol (.01 stored but 1 displayed)
      /// </summary>
      PricePar100,

      /// <summary>
      /// Basis Points but dont show 'bps' suffix (.01 stored but 1000 displayed)
      /// </summary>
      CentsPer100
    }

    /// <summary>
    /// Given a quotetype find out how to format the value for display
    /// We may be able to use QuotingConvention at some point but 
    /// we dont have time before 9.2 release to verify that we can do this and change upgrade process
    /// to populate this field correctly.
    /// </summary>
    /// <param name="qt">quote type</param>
    /// <returns>how to format this value</returns>
		static public FormatType GetFormatType(QuoteType qt)
    {
      // Allows us to pass in directly a property that may be null like Trade.TradedLevelType for example
      if (qt == null)
        return FormatType.None;

      return GetFormatType(qt.Name);
    }

    /// <summary>
    /// When 0 is entered in the traded level field, display the actual 0 or an empty field.
    /// We return true for CDS Upfront below since 0 (or even a negative value) is a legitimate traded level in this case.
    /// </summary>
    public static bool DisplayZeroInTradedLevel(QuoteType qt)
    {
      if (qt == null) return false;
      if (qt.Name == "CDS Upfront") return true;
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly Dictionary<string, FormatType> Formats = new Dictionary<string, FormatType>()
    {
      {"Asset Swap Spread", FormatType.BasisPoints},
      {"Base Correlation", FormatType.Percent},
      {"Base Correlation Strike", FormatType.None},
      {"Bill Price", FormatType.PricePar100},
      {"Bond Price", FormatType.PricePar100},
      {"Bond Full Price", FormatType.PricePar100},
      {"Bond Recovery Rate", FormatType.Percent},
      {"Bond Spread", FormatType.BasisPoints},
      {"Bond Yield", FormatType.Percent},
      {"CDO Fee", FormatType.Percent},
      {"CDS Conv Spread", FormatType.BasisPoints},
      {"Tranche Conv Spread", FormatType.BasisPoints},
      {"CDS Spread", FormatType.BasisPoints},
      {"CDS Upfront", FormatType.Percent},
      {"CDX Fee", FormatType.Percent},
      {"CDX Price", FormatType.Percent},
      {"CDX Scaling Factor", FormatType.Percent},
      {"CDX Spread", FormatType.BasisPoints},
      {"Discount Margin", FormatType.BasisPoints},
      {"Discount Rate", FormatType.Percent},
      {"ED Future", FormatType.PricePar100},
      {"ED Volatility", FormatType.Percent},
      {"Full Price", FormatType.None},
      {"FX Rate", FormatType.Percent},
      {"LCDS Factor", FormatType.None},
      {"LCDS Spread", FormatType.BasisPoints},
      {"LCDS Upfront", FormatType.Percent},
      {"LCDX Price", FormatType.Percent},
      {"LCDX Scaling Factor", FormatType.Percent},
      {"LCDX Spread", FormatType.BasisPoints},
      {"Loan Price", FormatType.None},
      {"Market Value", FormatType.None},
      {"MM Rate", FormatType.Percent},
      {"Recovery Rate", FormatType.Percent},
      {"RSpread", FormatType.BasisPoints},
      {"Single Factor Correlation", FormatType.Percent},
      {"Stock Option Price", FormatType.None},
      {"Stock Option Vol", FormatType.Percent},
      {"Bond Option Volatility", FormatType.Percent},
      {"Stock Price", FormatType.None},
      {"Dividend Yield", FormatType.Percent},
      {"Swap Rate", FormatType.Percent},
      {"Tranche Fee", FormatType.Percent},
      {"Tranche Premium", FormatType.BasisPoints},
      {"Volatility", FormatType.Percent},
      {"Yield", FormatType.Percent},
      {"ZSpread", FormatType.BasisPoints},
      {"CDX Option Price", FormatType.CentsPer100}
    };

    /// <summary>
    /// Gets the format type.
    /// </summary>
    /// <param name="quoteTypeName">Name of the quote type.</param>
    /// <returns></returns>
    public static FormatType GetFormatType(string quoteTypeName)
    {
      FormatType ft;
      return !string.IsNullOrEmpty(quoteTypeName) && Formats.TryGetValue(quoteTypeName, out ft) ? ft : FormatType.None;
    }


    /// <summary>
    /// Get a formatted display value for a double based on its quote type
    /// </summary>
    /// <param name="value"></param>
    /// <param name="quoteTypeName"></param>
    /// <returns></returns>
    public static string ValueToString(double value, string quoteTypeName)
    {
      var formatType = GetFormatType(quoteTypeName);

      switch (formatType)
      {
        case FormatType.Percent:
          return (value * 100.0) + " %";

        case FormatType.PricePar100:
          return (value * 100.0).ToString();

        case FormatType.BasisPoints:
          return (value * 10000.0) + " bps";

        case FormatType.CentsPer100:
          return (value * 10000.0).ToString();

        default:
          return value.ToString();
      }
    }

    /// <summary>
    /// Get a double value for a quote based on its display text and its quote type
    /// </summary>
    /// <param name="text"></param>
    /// <param name="quoteTypeName"></param>
    /// <returns></returns>
    public static double? StringToValue(string text, string quoteTypeName)
    {
      if (string.IsNullOrEmpty(text))
        return null;

      var cleanText = text.Trim(new[] { ' ', '%' });
      cleanText = cleanText.ToLower().Replace("bps", "");

      double val;

      if (!double.TryParse(cleanText, out val))
        return null;

      var formatType = GetFormatType(quoteTypeName);

      switch (formatType)
      {
        case FormatType.Percent:
          return val / 100.0;

        case FormatType.PricePar100:
          return val / 100.0;

        case FormatType.BasisPoints:
          return val / 10000.0;

        case FormatType.CentsPer100:
          return val / 10000.0;

        default:
          return val;
      }
    }

    #endregion

    #region Factory

    /// <summary>
    ///   Create instance of QuoteType
    /// </summary>
    public static QuoteType CreateInstance()
    {
      return (QuoteType)Entity.CreateInstance();
    }

    /// <summary>
    ///   Create instance of QuoteType with specified name and description
    /// </summary>
    public static QuoteType CreateInstance(string name, string description)
    {
      QuoteType quoteType = CreateInstance();
      quoteType.Name = name;
      quoteType.Description = description;
      return quoteType;
    }

    /// <summary>
    ///   Get meta information
    /// </summary>
    public static ClassMeta Entity
    {
      get { return LazyEntity.Value; }
    }

    #endregion

    #region Data

    private static readonly Lazy<ClassMeta> LazyEntity = new Lazy<ClassMeta>(() => ClassCache.Find("QuoteType"));

    private static volatile ConcurrentDictionary<string, QuoteType> _cache =
      new ConcurrentDictionary<string, QuoteType>();

    #endregion
  }
}
