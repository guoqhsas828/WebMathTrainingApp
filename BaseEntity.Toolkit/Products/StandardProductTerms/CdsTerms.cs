//
// CdsTerms.cs
//   2015. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for market-standard <see cref="CDS"/>.
  /// </summary>
  /// <remarks>
  ///   <para>The terms of market-standard CDS are defined uniquely by the
  ///   <see cref="CdsTerms.TransactionType">ISDA Credit Derivative Transaction Type</see>.</para>
  ///   <para>Once the correct transaction type has been selected, the CDS can be created given the
  ///   pricing date, term, currency, premium, fee and cleared flag.</para>
  ///   <inheritdoc cref="CdsTerms.GetProduct(Dt, string, BaseEntity.Toolkit.Base.Currency, double, double, bool)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating a standard SNAC CDS.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var transactionType = CreditDerivativeTransactionType.StandardNorthAmericanCorporate;
  ///     var currency = Currency.USD;
  ///     var tenor = "5 Year"
  ///     var premium = 100;
  ///     var fee = 0;
  ///     var cleared = false;
  ///     // Look up product terms
  ///     var terms = StandardProductTermsUtil.GetCdsTerms(transactionType);
  ///     // Create cds
  ///     var cds = terms.GetProduct(asOf, tenor, currency, premium, fee, cleared);
  ///   </code>
  ///   <para>A convenience function is provided to simplify creating a standard product directly.
  ///   The following example demonstrates creating a CDS using this convenience function.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var transactionType = CreditDerivativeTransactionType.StandardNorthAmericanCorporate;
  ///     var currency = Currency.USD;
  ///     var tenor = "5 Year"
  ///     var premium = 100;
  ///     var fee = 0;
  ///     var cleared = false;
  ///     // Create cds
  ///     var cds = StandardProductTermsUtil.GetStandardCds(transactionType, asOf, tenor, currency, premium, fee, cleared);
  ///   </code>
  /// </example>
  /// </remarks>
  /// <seealso href="http://www.isda.org/c_and_a/Credit-Derivatives-Physical-Settlement-Matrix.html"/>
  /// <seealso cref="CDS"/>
  [DebuggerDisplay("CDS Terms")]
  [Serializable]
  public class CdsTerms : StandardProductTermsBase
  {
    #region Constructor

    /// <summary>
    /// Default constructor for property defaults. 
    /// </summary>
    /// <remarks>
    ///   <para>This is hear so there are dfeaults for for meta-data driven class construction such as reading from XML.</para>
    /// </remarks>
    private CdsTerms()
    {
      FeeSettleDays = 3;
      ClearedFeeSettleDays = 1;
      Frequency = Frequency.Quarterly;
      DayCount = DayCount.Actual360;
      BDConvention = BDConvention.Following;
      Standard = true;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="transactionType">CDS transaction type)</param>
    /// <param name="abreviation">Optional CDS transaction type abreviation (eg SNAC)</param>
    /// <param name="description">Description</param>
    /// <param name="feeSettleDays">Number of days to fee settlement for uncleared trade</param>
    /// <param name="clearedFeeSettleDays">Number of days to fee settlement for cleared trade</param>
    /// <param name="frequency">Premium frequency</param>
    /// <param name="dayCount">Premium daycount</param>
    /// <param name="bdConvention">Premium businessday convention</param>
    /// <param name="calendars">List of premium payment and fee settlement calendars by premium currency</param>
    /// <param name="standard">Is this a standard contract</param>
    public CdsTerms(CreditDerivativeTransactionType transactionType, string abreviation, string description,
      int feeSettleDays, int clearedFeeSettleDays, Frequency frequency, DayCount dayCount, BDConvention bdConvention,
      List<Tuple<Currency, string, string>> calendars, bool standard)
      : this()
    {
      TransactionType = transactionType;
      Abreviation = String.IsNullOrEmpty(abreviation) ? transactionType.ToString() : abreviation;
      Description = description;
      FeeSettleDays = feeSettleDays;
      ClearedFeeSettleDays = clearedFeeSettleDays;
      Frequency = frequency;
      DayCount = dayCount;
      BDConvention = bdConvention;
      Calendars = calendars;
      Standard = standard;
    }

    /// <summary>
    /// Simple Constructor
    /// </summary>
    /// <param name="transactionType">CDS transaction type)</param>
    /// <param name="abreviation">Optional CDS transaction type abreviation (eg SNAC)</param>
    /// <param name="description">Description</param>
    /// <param name="calendars">List of premium payment and fee settlement calendars by premium currency</param>
    public CdsTerms(CreditDerivativeTransactionType transactionType, string abreviation, string description,
      List<Tuple<Currency, string, string>> calendars)
      : this()
    {
      TransactionType = transactionType;
      Abreviation = String.IsNullOrEmpty(abreviation) ? transactionType.ToString() : abreviation;
      Description = description;
      Calendars = calendars;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key { get { return GetKey(TransactionType); } }

    /// <summary>
    ///   CDS Transaction type (eg StandardNorthAmericanCorporate, StandardEuropeanCorporate, etc)
    /// </summary>
    public CreditDerivativeTransactionType TransactionType { get; private set; }

    /// <summary>
    ///   Optional Abreviation for CDS Transaction type (eg SNAC, STEC)
    /// </summary>
    public string Abreviation { get; private set; }

    /// <summary>
    ///   Number of business days for fee settlement for uncleared trades
    /// </summary>
    public int FeeSettleDays { get; private set; }

    /// <summary>
    ///   Number of business days for fee settlement for cleared trades
    /// </summary>
    public int ClearedFeeSettleDays { get; private set; }

    /// <summary>
    /// Premium payment frequency
    /// </summary>
    public Frequency Frequency { get; private set; }

    /// <summary>
    /// Premium accrual daycount
    /// </summary>
    public DayCount DayCount { get; private set; }

    /// <summary>
    /// Premium business day convention (roll)
    /// </summary>
    public BDConvention BDConvention { get; private set; }

    /// <summary>
    /// Is this a standard CDS contract
    /// </summary>
    public bool Standard { get; private set; }

    /// <summary>
    ///   Premium and Fee settlement calendars by currency (primary currency first)
    /// </summary>
    public List<Tuple<Currency, string, string>> Calendars { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Validate terms
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if( TransactionType == CreditDerivativeTransactionType.None)
        InvalidValue.AddError(errors, this, "TransactionType", "TransactionType must be specified");
      if( FeeSettleDays < 0 )
        InvalidValue.AddError(errors, this, "FeeSettleDays", "Invalid number of days for fee settmement");
      if (ClearedFeeSettleDays < 0)
        InvalidValue.AddError(errors, this, "ClearedFeeSettleDays", "Invalid number of days for cleared fee settmement");
      if( Calendars == null || Calendars.Count <= 0 )
        InvalidValue.AddError(errors, this, "Calendars", "Calendar for each valid currency must be specified");
    }

    /// <summary>
    ///   Create a market-standard <see cref="CDS"/> from the CDS Terms
    /// </summary>
    /// <remarks>
    ///   <para>Given the CDS terms, the CDS is created as follows:</para>
    ///   <para>The premium and fee calendars are the calendar pairs matching the currency in the Terms <see cref="CdsTerms.Calendars"/>.</para>
    ///   <list type="table">
    ///     <listheader><term>CDS Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Effective"/></term>
    ///       <description>Set to the first business day after the last IMM date prior to the specified quoted <paramref name="asOf"/> date using the premium calendar.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Maturity"/></term>
    ///       <description>Set to the next IMM date after the quoted <paramref name="asOf"/> date plus the specified <paramref name="tenorName"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the specified currency.</description>
    ///     </item><item>
    ///       <term><see cref="CDS.Fee"/></term>
    ///       <description>Set to the specified fee.</description>
    ///     </item><item>
    ///       <term><see cref="CDS.Premium"/></term>
    ///       <description>Set to the specified premium.</description>
    ///     </item><item>
    ///       <term><see cref="CDS.DayCount"/></term>
    ///       <description>Set to the Terms <see cref="CdsTerms.DayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the Terms <see cref="CdsTerms.Frequency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Terms <see cref="CdsTerms.BDConvention"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Calendar"/></term>
    ///       <description>Set to the premium calendar.</description>
    ///     </item><item>
    ///       <term><see cref="CDS.FeeSettle"/></term>
    ///       <description>Set <see cref="CdsTerms.ClearedFeeSettleDays"/> business days after the quoted <paramref name="asOf"/> date if the Terms <paramref name="cleared"/>
    ///       flag is true otherwise is set to <see cref="CdsTerms.FeeSettleDays"/> business days after the quoted <paramref name="asOf"/> date.
    ///       The fee calendar is used for these calculations.</description>
    ///     </item><item>
    ///       <term><see cref="CDS.AccruedOnDefault"/></term>
    ///       <description>Set to true.</description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenorName">Tenor name</param>
    /// <param name="currency">Currency of premium. If None then use first (primary) currency for this transaction type</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Upfront fee</param>
    /// <param name="cleared">True if cleared</param>
    /// <returns>Created <see cref="CDS"/></returns>
    public CDS GetProduct(Dt asOf, string tenorName, Currency currency, double premium, double fee, bool cleared)
    {
      var calt = (currency == Currency.None) ? Calendars[0] : Calendars.FirstOrDefault(t => t.Item1 == currency);
      if (calt == null)
        throw new Exception(String.Format("Invalid currency {0} for CDS transaction type {1}", currency, TransactionType));
      var premCal = String.IsNullOrEmpty(calt.Item2) ? Calendar.None : new Calendar(calt.Item2); // Premium calendar
      var feeCal = String.IsNullOrEmpty(calt.Item3) ? Calendar.None : new Calendar(calt.Item3); // Fee calendar
      var effective = Dt.SNACFirstAccrualStart(asOf, premCal);
      var settle = Dt.Add(asOf, 1);
      var maturity = Dt.CDSMaturity(settle, tenorName);
      var feeSettleDays = (cleared) ? ClearedFeeSettleDays : FeeSettleDays;
      var cds = new CDS(effective, maturity, currency, premium, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, premCal)
        { AccruedOnDefault = true, Fee = fee };
      // Force calculation of the first premium payment date to improve performance
      cds.FirstPrem = cds.FirstPrem;
      // Set fee settlement
      cds.FeeSettle = Dt.AddDays(asOf, feeSettleDays, feeCal);
      return cds;
    }

    /// <summary>
    /// Create unique key for CDS Terms
    /// </summary>
    /// <param name="transactionType">Credit Derivative Transaction Type</param>
    /// <returns>Unique key</returns>
    public static string GetKey(CreditDerivativeTransactionType transactionType)
    {
      return String.Format("CdsTerms.{0}", transactionType);
    }

    #endregion
  }
}
