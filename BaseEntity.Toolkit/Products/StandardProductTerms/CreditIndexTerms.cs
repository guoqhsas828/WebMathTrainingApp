//
// CreditIndexTerms.cs
//   2015. All rights reserved.
//
using System;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Standard Credit Index terms
  /// </summary>
  /// <remarks>
  ///   <para>The terms of market-standard Credit Index are defined uniquely by the index name.</para>
  ///   <para>Once the correct transaction type has been selected, the CDS can be created given the
  ///   pricing date and term.</para>
  ///   <inheritdoc cref="GetProduct(Dt,string)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating a standard Credit Index.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var name = "iTraxx Europe";
  ///     var tenor = "5 Year"
  ///     // Look up product terms
  ///     var terms = StandardProductTermsUtil.GetCreditIndexTerms(name);
  ///     // Create credit index
  ///     var creditIndex = terms.GetProduct(asOf, tenor);
  ///   </code>
  ///   <para>A convenience function is provided to simplify creating a standard product directly.
  ///   The following example demonstrates creating a Credit Index using this convenience function.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var name = "iTraxx Europe";
  ///     var tenor = "5 Year"
  ///     // Create credit index
  ///     var terms = StandardProductTermsUtil.GetStandardCreditIndex(name, asOf, tenor);
  ///   </code>
  /// </example>
  /// </remarks>
  /// <seealso cref="CDX"/>
  [Serializable]
  public class CreditIndexTerms : StandardProductTermsBase
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="name">Index subseries name</param>
    /// <param name="ccy">Currency of index</param>
    /// <param name="entities">Number of index constituents</param>
    /// <param name="startDate">Issue date of index</param>
    /// <param name="premium">Premium in basis points</param>
    /// <param name="calendar">Premium payment calendar</param>
    /// <param name="recovery">Contractual recovery rate</param>
    /// <param name="quoteInPrice">Whether quoting in price</param>
    internal CreditIndexTerms(string name, Currency ccy, int entities,
      int startDate, int premium, Calendar calendar,
      double recovery, bool quoteInPrice)
      : base(name)
    {
      SubSeriesName = name;
      Currency = ccy;
      Entities = entities;
      StartDate = startDate;
      Premium = premium;
      Calendar = calendar;
      Recovery = recovery;
      QuoteInPrice = quoteInPrice;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key { get { return GetKey(SubSeriesName); } }

    /// <summary>RED Sub-series name</summary>
    public string SubSeriesName { get; private set; }

    /// <summary>Currency</summary>
    public Currency Currency { get; private set; }

    /// <summary>Number of entities</summary>
    public int Entities { get; private set; }

    /// <summary>Series start date (the year/month/date of series 1)</summary>
    public int StartDate { get; private set; }

    /// <summary>Premium in basis point</summary>
    public int Premium { get; private set; }

    /// <summary>Business day calendar</summary>
    public Calendar Calendar { get; private set; }

    /// <summary>Contractual recovery rate</summary>
    public double Recovery { get; private set; }

    /// <summary>Whether quoted in price</summary>
    public bool QuoteInPrice { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Create standard product given a date and a tenor
    /// </summary>
    /// <remarks>
    ///   <para>The name identifies the credit index series, version and tenor in the
    ///   format Series name.Version.Tenor. E.g. CDX.NA.HY-V23.5Y.</para>
    /// </remarks>
    /// <param name="name">Credit Index name</param>
    /// <returns>Standard <see cref="CDX"/></returns>
    [ProductBuilder]
    public CDX GetProduct(string name)
    {
      var key = Regex.Replace(name, @"\s+", " ");
      var match = Regex.Match(key, @"\.(\d+)(?:-V\d+)?\.(\d+)Y$", RegexOptions.IgnoreCase);
      if (!match.Success)
        throw new ArgumentException(String.Format("Unexpected series version and/or tenor name"));
      var series = Int32.Parse(match.Groups[1].Value);
      var years = Int32.Parse(match.Groups[2].Value);
      return GetProduct(match.Groups[0].Value, series, years);
    }

    /// <summary>
    ///   Create standard product given a index name, series and maturity
    /// </summary>
    /// <param name="index">Credit index name</param>
    /// <param name="series">Series</param>
    /// <param name="tenor">Tenor</param>
    /// <returns>Standard <see cref="CDX"/></returns>
    [ProductBuilder]
    public CDX GetProduct(string index, int series, Tenor tenor)
    {
      var name = index;
      var dayCount = DayCount.Actual360;
      var frequency = Frequency.Quarterly;
      var roll = BDConvention.Following;
      var effective = Dt.AddMonth(new Dt(StartDate), (series - 1) * 6, false);
      var maturity = Dt.AddMonth(effective, (int)tenor.Months + 3, false);
      return new CDX(effective, maturity, Currency, Premium / 10000.0, dayCount, frequency, roll, Calendar)
      {
        Description = name
      };
    }

    /// <summary>
    ///   Create standard product given a index name, series and maturity
    /// </summary>
    /// <param name="index">Credit index name</param>
    /// <param name="series">Series</param>
    /// <param name="years">Tenor</param>
    /// <returns>Standard <see cref="CDX"/></returns>
    [ProductBuilder]
    public CDX GetProduct(string index, int series, int years)
    {
      var name = index;
      var dayCount = DayCount.Actual360;
      var frequency = Frequency.Quarterly;
      var roll = BDConvention.Following;
      var effective = Dt.AddMonth(new Dt(StartDate), (series - 1) * 6, false);
      var maturity = Dt.AddMonth(effective, years * 12 + 3, false);
      return new CDX(effective, maturity, Currency, Premium / 10000.0, dayCount, frequency, roll, Calendar)
      {
        Description = name
      };
    }

    /// <summary>
    /// Create unique key for Credit Index Terms
    /// </summary>
    /// <param name="name">Index subseries name</param>
    /// <returns>Unique key</returns>
    public static string GetKey(string name)
    {
      return $"CreditIndexTerms.{name}";
    }

    #endregion
  }
}
