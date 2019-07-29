//
// SurvivalCurve.cs
//  -2014. All rights reserved.
// TBD: Review if some of the curve attributes should be moved to the calibration.
// TBD: Support default dates in the future. RTD Apr'06
// TBD: Merge FitCDSQuotes and FitCDXQuotes. RTD Dec'07
//
#define NEW_DEFAULTED_BEHAVIOR

using System;
using System.Linq;
using System.ComponentModel;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using CDSQuoteHandler = BaseEntity.Toolkit.Curves.CurveTenorQuoteHandlers.CDSQuoteHandler;
using CDSConverter = BaseEntity.Toolkit.Models.ISDACDSModel.SNACCDSConverter;

namespace BaseEntity.Toolkit.Curves
{
  
  /// <summary>
	///   Survival curve default state
	/// </summary>
  /// <remarks>
  ///   <para>All normal curves are marked as <c>NotDefaulted</c>.
  ///   When a curve is marked
  ///   as <c>HasDefaulted</c>, the default is assumed happens before the pricing period, 
  ///   the notional associated with this curve is excluded and appropriate adjustments are
  ///   made accordingly.
  ///   If a curve is marked as <c>WillDefault</c>, then the default is assumed
  ///   happens exactly at the begining of the pricing period and the full loss
  ///   is included in pricing.
  ///   </para>
  /// </remarks>
	public enum Defaulted
	{
		/// <summary>Not defaulted</summary>
		NotDefaulted,
		/// <summary>Has defaulted</summary>
		HasDefaulted,
		/// <summary>Will default</summary>
		WillDefault
	}

  /// <summary>
  ///   A term structure of credit
  /// </summary>
  /// <remarks>
  ///   <para>Contains a term structure of credit. The interface is in terms
  ///   of survival probabilities.</para>
  ///   <para>Survival curves can be created by directly specifying survival probabilities
  ///   or by calibrating to market data. Calibration is performed by
  ///   <see cref="SurvivalCalibrator"/>s</para>
  /// </remarks>
  [Serializable]
  public class SurvivalCurve : CalibratedCurve
  {
		// Logger
		//private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(SurvivalCurve));

    #region Constructors

    /// <summary>
		///   Constructor given just the asOf date.
		/// </summary>
		///
	  /// <remarks>
		///   <para>Interpolation defaults to flat continuously compounded forward rates.
		///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
		///   Compounding frequency is Continuous.</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		///
		public
		SurvivalCurve( Dt asOf )
			: base( asOf )
		{}


		/// <summary>
		///   Constructor for a flat hazard rate curve
		/// </summary>
		///
	  /// <remarks>
		///   <para>Constructs a simple survival curve based on a constant hazard
		///   rate.</para>
		///
		///   <para>Interpolation defaults to flat continuously compounded forward rates.
		///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
		///   Compounding frequency is Continuous.</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="hazardRate">Single continuously compounded hazard rate</param>
		///
		/// <example>
		/// <code language="C#">
		///   // Pricing is as of today
		///   Dt today = Dt.today();
		///   // Constant hazard rate is 4 percent.
		///   double hazardRate = 0.04;
		///
		///   // Construct the survival curve using a constant hazard rate.
		///   SurvivalCurve survivalCurve = new SurvivalCurve( today, hazardRate );
		/// </code>
		/// </example>
		///
		public
		SurvivalCurve( Dt asOf, double hazardRate )
			: base( asOf, hazardRate )
		{}


		/// <summary>
		///   Constructor give calibrator
		/// </summary>
		///
	  /// <remarks>
		///   <para>Interpolation defaults to flat continuously compounded forward hazard rates.
		///   Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
		///   Compounding frequency is Continuous.</para>
		/// </remarks>
		///
		/// <param name="calibrator">Calibrator</param>
		///
		/// <example>
		/// <code language="C#">
		///   // Pricing is as of today and settlement is tomorrow.
		///   Dt today = Dt.today();
		///   Dt settle = Dt.add(today, 1);
		///   // Risk free discount rate is 4 percent.
		///   double riskFreeRate = 0.04;
		///   // Recovery is 40 percent of notional.
		///   double recoveryRate = 0.40;
		///   // Set up the cds tenor points and quotes.
		///   string [] cdsTenors = new string [] { "1 Year", "5 Year", "7 Year", "10 year" };
		///   double [] cdsRates = new double [] { 0.001, 0.0015, 0.002, 0.0025 };
		///
		///   // Construct the survival curve
		///   DiscountCurve discountCurve = new DiscountCurve( today, riskFreeRate );
		///   SurvivalFitCalibrator fit = new SurvivalFitCalibrator( today, settle, recoveryRate, discountCurve );
		///   SurvivalCurve survivalCurve = new SurvivalCurve( fit );
		///
		///   // Add the CDS tenor points
		///   for( int i = 0; i &lt; cdsTenors.Length; i++ )
		///   {
		///     survivalCurve.AddCDS( cdsTenors[i], Dt.cdsMaturity(today, cdsTenors[i]), cdsRates[i],
		///                         DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
		///                         Calendar.NYB );
		///   }
		///
		///   // Fit the survival curve
		///   survivalCurve.Fit();
		/// </code>
		/// </example>
		///
		public
		SurvivalCurve( SurvivalCalibrator calibrator )
			: base( calibrator )
		{}


		/// <summary>
		///   Constructor given calibrator and interpolation details
		/// </summary>
	  ///
		/// <param name="calibrator">Calibrator</param>
    /// <param name="interp">Interpolation method</param>
    /// <param name="dc">Daycount for interpolation</param>
    /// <param name="freq">Compounding frequency for interpolation</param>
		///
		public
		SurvivalCurve( SurvivalCalibrator calibrator, Interp interp, DayCount dc, Frequency freq )
			: base( calibrator, interp, dc, freq )
		{}

    #endregion // Constructors

    #region Static_Constructors

    /// <summary>
    ///   Create a Survival Curve from a set of specified survival probabilities
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Entries where the survival probability is &lt;= 0 are ignored</para>
    ///
    ///   <para>This function returns a calibrated curve, which can be used in sensitivity analysis.
    ///   To make such a curve, it creates
    ///   a fictitious risky zero coupon bond for each pair of maturity date and survival probability.
    ///   The bond is counstructed using <c>DayCount.Actual365Fixed</c> as rate day count,
    ///   <c>Frequency.Continuous</c> as rate compounding frequency, and zero recovery rate
    ///   if none of them is supplied.
    ///   In this case, the rate of the risky bond is equivalent to the hazard rate of
    ///   the corresponding period.</para>
    ///
    ///   <para>Alternaltively the user can supply day counts, frequencies and recovery rates used
    ///   to construct the bonds.  He can also supply an array of tenor names in addition to the
    ///   maurities dates.
    ///   </para>
    ///
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="maturities">Maturity dates</param>
    /// <param name="survivalProbabilities">Survival probabilities</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="rateDaycounts">Rate daycounts (vector or single value)</param>
    /// <param name="rateFreqs">Rate compounding frequencies (vector or single value)</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent</param>
    /// <param name="supportSpreadBump">If true, the curve supports bumping/scaling in credit spreads;
    ///  otherwise, spread bumping is not supported</param>
    ///
    /// <returns>constructed survival curve.</returns>
    public static SurvivalCurve FromProbabilitiesWithBond(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      Dt[] maturities,
      double[] survivalProbabilities,
      string[] tenorNames,
      DayCount[] rateDaycounts,
      Frequency[] rateFreqs,
      double[] recoveries,
      double recoveryDispersion,
      bool supportSpreadBump = false
      )
    {
      // Sanity checks
      if (tenorNames != null && tenorNames.Length == 0)
        tenorNames = null;
      if (tenorNames != null && tenorNames.Length != maturities.Length)
        throw new ArgumentException("Number of tenor names must match number of tenor dates");
      if (survivalProbabilities == null)
        throw new ArgumentException("survivalProbabilities cannot bu null");
      if (maturities == null)
        throw new ArgumentException("maturities cannot bu null");
      if (survivalProbabilities.Length != maturities.Length)
        throw new ArgumentException("Number of survival probabilities must match number of maturities");

      // check day counts
      if (rateDaycounts == null || rateDaycounts.Length == 0)
        rateDaycounts = new DayCount[] { DayCount.Actual365Fixed };
      else if (rateDaycounts.Length != 1 && rateDaycounts.Length != maturities.Length)
        throw new ArgumentException("Number of daycounts must be one or match number of tenors");

      // check freqs
      if (rateFreqs == null || rateFreqs.Length == 0)
        rateFreqs = new Frequency[] { Frequency.Continuous };
      else if (rateFreqs.Length != 1 && rateFreqs.Length != maturities.Length)
        throw new ArgumentException("Number of compounding frequencies must be one or match number of tenors");

      // Discount curve and recovery curves
      DiscountCurve dfCurve = new DiscountCurve(asOfDate, 0.0); // no discounting
      if (recoveries == null || recoveries.Length == 0)
        recoveries = new double[] { 0.0 }; // no recovery
      RecoveryCurve rcCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, maturities, recoveries, recoveryDispersion);

      // Survival curve
      var calibrator = new SurvivalRateCalibrator(asOfDate, asOfDate, rcCurve, dfCurve);
      if (calibrator.UseNaturalSettlement && !supportSpreadBump)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Ccy = currency;
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Category = category ?? "None";

      for (int i = 0; i < maturities.Length; i++)
        if (survivalProbabilities[i] > 0.0)
        {
          curve.Add(maturities[i], survivalProbabilities[i]);
          curve.AddZeroYield(tenorNames != null ? tenorNames[i] : null,
            maturities[i], 0.0,
            (rateDaycounts.Length == 1) ? rateDaycounts[0] : rateDaycounts[i],
            (rateFreqs.Length == 1) ? rateFreqs[0] : rateFreqs[i]);
        }

      if (supportSpreadBump)
      {
        calibrator.FitSpotRates = true;
        var quoteHandler = new SurvivalRateQuoteHandler(dfCurve);
        for (int i = 0, n = curve.Count; i < n; ++i)
        {
          var tenor = curve.Tenors[i];
          SurvivalRateQuoteHandler.SetCouponFromSurvivalProbability(
            tenor, curve.GetVal(i), rcCurve, dfCurve);
          tenor.QuoteHandler = quoteHandler;
          var q = tenor.CurrentQuote;
          tenor.OriginalQuote = new MarketQuote(q.Value, q.Type);
        }
      }
      else
      {
        // Calculate rdf curve
        Curve rdfCurve = new Curve(asOfDate);
        BaseEntity.Toolkit.Models.Bootstrap.SurvivalToRdf(curve, dfCurve, rcCurve, rdfCurve);

        // set up survival rates
        int nTenors = rdfCurve.Count;
        if (nTenors != curve.Count)
          throw new ToolkitException("Rdf curve and survival curve not match!");
        for (int i = 0; i < nTenors; ++i)
        {
          double rate = RateCalc.RateFromPrice(rdfCurve.GetVal(i), asOfDate,
            rdfCurve.GetDt(i),
            rateDaycounts.Length == 1 ? rateDaycounts[0] : rateDaycounts[i],
            rateFreqs.Length == 1 ? rateFreqs[0] : rateFreqs[i]);
          ((Note)curve.Tenors[i].Product).Coupon = rate;
        }
      }

      // This is not really needed.  Included here for consistency check
      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Create a Survival Curve from a set of specified survival probabilities
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Entries where the survival probability is &lt;= 0 are ignored</para>
    ///
    ///   <para>This function returns a calibrated curve, which can be used in sensitivity analysis.
    ///   To make such a curve, it creates
    ///   a fictitious risky zero coupon bond for each pair of maturity date and survival probability.
    ///   The bond is constructed using <c>DayCount.Actual365Fixed</c> as rate day count,
    ///   <c>Frequency.Continuous</c> as rate compounding frequency, and zero recovery rate
    ///   if none of them is supplied.
    ///   In this case, the rate of the risky bond is equivalent to the hazard rate of
    ///   the corresponding period.</para>
    ///
    ///   <para>Alternatively the user can supply day counts, frequencies and recovery rates used
    ///   to construct the bonds.  He can also supply an array of tenor names in addition to the
    ///   maturities dates.
    ///   </para>
    ///
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="maturities">Maturity dates</param>
    /// <param name="survivalProbabilities">Survival probabilities</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="cdsDayCounts">Day count of CDS premium accrual</param>
    /// <param name="cdsFreqs">Frequency of CDS premium payments</param>
    /// <param name="cdsRolls">Business day (roll) convention for CDS premium payments</param>
    /// <param name="cdsCals">Calendar for CDS premium payments</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent</param>
    /// <param name="exactDate">If true, the curve dates should be exactly the maturity dates; otherwise, calibrator may adjust curve dates</param>
    /// <param name="refitTolerance">Tolerance used to refit the probabilities from CDS spreads.  Ignored if it is not positive.</param>
    ///
    /// <returns>constructed survival curve.</returns>
    public static SurvivalCurve FromProbabilitiesWithCDS(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      Dt[] maturities,
      double[] survivalProbabilities,
      string[] tenorNames,
      DayCount[] cdsDayCounts,
      Frequency[] cdsFreqs,
      BDConvention[] cdsRolls,
      Calendar[] cdsCals,
      double[] recoveries,
      double recoveryDispersion,
      bool exactDate = false,
      double refitTolerance = 0)
    {
      // Sanity checks
      if (tenorNames != null && tenorNames.Length == 0)
        tenorNames = null;
      if (tenorNames != null && tenorNames.Length != maturities.Length)
        throw new ArgumentException("Number of tenor names must match number of tenor dates");
      if (survivalProbabilities == null)
        throw new ArgumentException("survivalProbabilities cannot bu null");
      if (maturities == null)
        throw new ArgumentException("maturities cannot be null");
      if (survivalProbabilities.Length != maturities.Length)
        throw new ArgumentException("Number of survival probabilities must match number of maturities");

      // check day counts
      if (cdsDayCounts == null || cdsDayCounts.Length == 0)
        cdsDayCounts = new DayCount[] { DayCount.Actual360 };
      else if (cdsDayCounts.Length != 1 && cdsDayCounts.Length != maturities.Length)
        throw new ArgumentException("Number of day counts must be one or match number of tenors");

      // check freqs
      if (cdsFreqs == null || cdsFreqs.Length == 0)
        cdsFreqs = new Frequency[] { Frequency.Quarterly };
      else if (cdsFreqs.Length != 1 && cdsFreqs.Length != maturities.Length)
        throw new ArgumentException("Number of compounding frequencies must be one or match number of tenors");

      // check rolls
      if (cdsRolls == null || cdsRolls.Length == 0)
        cdsRolls = new[] {exactDate ? BDConvention.None : BDConvention.Following};
      else if (cdsRolls.Length != 1 && cdsRolls.Length != maturities.Length)
        throw new ArgumentException("Number of roll conventions must be one or match number of tenors");

      // check calendars
      if (cdsCals == null || cdsCals.Length == 0)
        cdsCals = new Calendar[] { Calendar.NYB };
      else if (cdsCals.Length != 1 && cdsCals.Length != maturities.Length)
        throw new ArgumentException("Number of calendars must be one or match number of tenors");

      // Discount curve and recovery curves
      var dfCurve = new DiscountCurve(asOfDate, 0.0); // no discounting
      if (recoveries == null || recoveries.Length == 0)
        recoveries = new double[] { 0.0 }; // no recovery
      RecoveryCurve rcCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, maturities, recoveries, recoveryDispersion);

      // Survival curve
      var calibrator = new SurvivalFitCalibrator(asOfDate, asOfDate, rcCurve, dfCurve);
      if (refitTolerance > 0)
        calibrator.ToleranceX = calibrator.ToleranceF = refitTolerance;
      if (calibrator.UseNaturalSettlement)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      var curve = new SurvivalCurve(calibrator);
      curve.Ccy = currency;
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Category = category ?? "None";

      for (int i = 0; i < maturities.Length; i++)
        if (survivalProbabilities[i] > 0.0)
        {
          curve.Add(maturities[i], survivalProbabilities[i]);
          curve.AddCDS(tenorNames != null ? tenorNames[i] : null,
            exactDate ? (maturities[i] - 1) : maturities[i], 0.0,
            cdsDayCounts.Length == 1 ? cdsDayCounts[0] : cdsDayCounts[i],
            cdsFreqs.Length == 1 ? cdsFreqs[0] : cdsFreqs[i],
            cdsRolls.Length == 1 ? cdsRolls[0] : cdsRolls[i],
            cdsCals.Length == 1 ? cdsCals[0] : cdsCals[i]);
        }

      // set up survival rates
      int nTenors = curve.Tenors.Count;
      if (nTenors != curve.Count)
        throw new ToolkitException("Curve tenors and curve points not match!");
      for (int i = 0; i < nTenors; ++i)
      {
        var tenor = curve.Tenors[i];
        var pricer = (CDSCashflowPricer) (exactDate
          ? tenor.GetPricer(curve, calibrator)
          : calibrator.GetPricer(curve, tenor.Product));
        double spread = pricer.BreakEvenPremium();
        tenor.SetQuote(QuotingConvention.CreditSpread, spread);
        pricer.Reset();
        curve.Tenors[i].MarketPv = pricer.Pv();
      }

      // This is not really needed.  Included here for consistency check
      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a set of risky zero coupon bond yields
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Each risky zero coupon bond is assumed to pay face at maturity
    ///   if no default occurs or a percentage of face (recovery) if default
    ///   occurs before maturity.</para>
    ///
    ///   <para>The Zero coupon bond yields are expressed in terms of simple yields.</para>
    ///
    ///   <para>Entries where the rate is &lt;= 0 are ignored.</para>
    ///
    ///   <para>The daycounts and frequencies are arrays or a single value for all tenors.</para>
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor (zero coupon bond maturity) dates. If not specified, these are calculated from the tenors</param>
    /// <param name="rates">rates</param>
    /// <param name="rateDaycounts">Rate daycounts (vector or single value)</param>
    /// <param name="rateFreqs">Rate compounding frequencies (vector or single value)</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent</param>
    ///
    /// <returns>constructed Survival Curve.</returns>
    public static SurvivalCurve FromBondRates(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] rates,
      DayCount[] rateDaycounts,
      Frequency[] rateFreqs,
      double[] recoveries,
      double recoveryDispersion
      )
    {
      Dt settle = asOfDate;
      SurvivalRateCalibrator calibrator = new SurvivalRateCalibrator(asOfDate, settle);
      if (calibrator.UseNaturalSettlement)
        settle = calibrator.Settle = Dt.Add(asOfDate, 1);

      // Fill out dates from tenors if necessary
      if (tenorDates == null || tenorDates.Length == 0)
      {
        if (tenorNames == null || tenorNames.Length == 0)
          throw new ArgumentException("TenorNames and TenorDates cannot be both null");
        tenorDates = new Dt[tenorNames.Length];
        for (int i = 0; i < tenorNames.Length; i++)
          tenorDates[i] = Dt.Add(settle, tenorNames[i]);
      }

      // Validate
      if (tenorNames != null && tenorNames.Length == 0)
        tenorNames = null;
      if (tenorNames != null && tenorNames.Length != tenorDates.Length)
        throw new ArgumentException("Number of tenor names must match number of tenor dates");
      if (rates == null || rates.Length != tenorDates.Length)
        throw new ArgumentException("Number of rates must match number of tenors");
      if (rateDaycounts == null)
        rateDaycounts = new DayCount[] { DayCount.Actual360 };
      else if (rateDaycounts.Length != 1 && rateDaycounts.Length != tenorDates.Length)
        throw new ArgumentException("Number of daycounts must be one or match number of tenors");
      if (rateFreqs == null)
        rateFreqs = new Frequency[] { Frequency.Quarterly };
      else if (rateFreqs.Length != 1 && rateFreqs.Length != tenorDates.Length)
        throw new ArgumentException("Number of compounding frequencies must be one or match number of tenors");

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, tenorDates, recoveries, recoveryDispersion);

      calibrator.DiscountCurve = discountCurve;
      calibrator.RecoveryCurve = recoveryCurve;
      calibrator.NegSPTreatment = nspTreatment;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Ccy = currency;
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Category = category ?? "None";

      for (int i = 0; i < tenorDates.Length; i++)
        if (rates[i] > 0.0)
          curve.AddZeroYield(tenorNames != null ? tenorNames[i] : null,
            tenorDates[i], rates[i],
            rateDaycounts.Length == 1 ? rateDaycounts[0] : rateDaycounts[i],
            rateFreqs.Length == 1 ? rateFreqs[0] : rateFreqs[i]);

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a term structure of CDS rates using a bootstrap method.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Credit curve bootstrap using flat forward hazard rates.</para>
    ///
    ///   <para>Entries where the CDS premium is &lt;= 0 are ignored.</para>
    /// </remarks>
    ///
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="cdsDayCount">Daycount of CDS premium accrual</param>
    /// <param name="cdsFrequency">Frequency of CDS premium payments</param>
    /// <param name="cdsRoll">Business day (roll) convention for CDS premium payments</param>
    /// <param name="cdsCalendar">Calendar for CDS premium payments</param>
    /// <param name="cdsInterp">Interpolation method for CDS rates</param>
    /// <param name="cdsExtrap">Extrapolation method for CDS rates</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="premiums">CDS premiums (in basis points)</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    ///
    /// <returns>constructed Survival Curve.</returns>
    public static SurvivalCurve Bootstrap(
      Dt asOf,
      Currency currency,
      string category,
      DayCount cdsDayCount,
      Frequency cdsFrequency,
      BDConvention cdsRoll,
      Calendar cdsCalendar,
      InterpMethod cdsInterp,
      ExtrapMethod cdsExtrap,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] premiums,
      double[] recoveries,
      double recoveryDisp
      )
    {
      Dt settle = asOf;
      SurvivalBootstrapCalibrator calibrator = new SurvivalBootstrapCalibrator(
        asOf, settle, cdsDayCount, cdsFrequency, cdsCalendar, cdsRoll,
        null, discountCurve);
      if (calibrator.UseNaturalSettlement)
        settle = calibrator.Settle = Dt.Add(asOf, 1);

      // Fill out dates from tenors if necessary
      if (tenorDates == null || tenorDates.Length == 0)
      {
        if (tenorNames == null || tenorNames.Length == 0)
          throw new ArgumentException("TenorNames and TenorDates cannot be both null");
        tenorDates = new Dt[tenorNames.Length];
        for (int i = 0; i < tenorNames.Length; i++)
          tenorDates[i] = Dt.CDSMaturity(settle, tenorNames[i]);
      }

      // Validate
      if (tenorNames != null && tenorNames.Length == 0)
        tenorNames = null;
      if (tenorNames != null && tenorNames.Length != tenorDates.Length)
        throw new ArgumentException("Number of tenor names must match number of tenor dates");
      if (premiums == null || premiums.Length != tenorDates.Length)
        throw new ArgumentException("Number of CDS premiums must match number of CDS maturities");

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(asOf, tenorDates, recoveries, recoveryDisp);

      calibrator.RecoveryCurve = recoveryCurve;
      calibrator.NegSPTreatment = nspTreatment;
      calibrator.CDSInterp = InterpFactory.FromMethod(cdsInterp, cdsExtrap);

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = currency;
      curve.Category = category ?? "None";

      for (int i = 0; i < tenorDates.Length; i++)
        if (premiums[i] > 0.0)
          // Assume in basis points
          curve.AddCDS(tenorNames != null ? tenorNames[i] : null,
            tenorDates[i], premiums[i] / 10000.0, cdsDayCount,
            cdsFrequency, cdsRoll, cdsCalendar);

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a term structure of CDS using the Fit method
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a survival curve based on a term structure of CDS quotes
    ///   by backing out the implied survival
    ///   probability to the maturity of each CDS in sequence.
    ///   There are various options in terms of the form of the
    ///   survival probability which allow for alternate forms of the
    ///   hazard rate such as flat, smooth or time-weighted.</para>
    ///
    ///   <para>Entries where the CDS premium is &lt;= 0 are ignored.</para>
    /// </remarks>
    ///
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="ccy">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="cdsDayCount">Daycount of CDS premium accrual</param>
    /// <param name="cdsFrequency">Frequency of CDS premium payments</param>
    /// <param name="cdsRoll">Business day (roll) convention for CDS premium payments</param>
    /// <param name="cdsCalendar">Calendar for CDS premium payments</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="fees">Vector of CDS up-front fees or single fees for all CDS (percent)</param>
    /// <param name="premiums">CDS premiums (bps)</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="forceFit">Set to true to allow bumped curves to have their quotes "flattened" when unable to fit naturally</param>
    /// <param name="eventDates">Optionally default date and recovery settle date</param>
    ///
    /// <returns>Constructed survival curve</returns>
    public static SurvivalCurve FitCDSQuotes(
      Dt asOf,
      Currency ccy,
      string category,
      DayCount cdsDayCount,
      Frequency cdsFrequency,
      BDConvention cdsRoll,
      Calendar cdsCalendar,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] fees,
      double[] premiums,
      double[] recoveries,
      double recoveryDisp,
      bool forceFit,
      params Dt[] eventDates
      )
    {
      //this is fine for CDS because the LCDS fit is the same as CDS when you leave off the refi curve
      return FitLCDSQuotes(asOf, ccy, category, cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
        interpMethod, extrapMethod, nspTreatment, discountCurve, tenorNames, tenorDates, fees,
        premiums, recoveries, recoveryDisp, forceFit, eventDates, null, 0);
    }

    private static bool BuildQuotes(Dt[] tenorDates, ref double[] fees, ref double[] premiums)
    {
      bool ignoreZeroFees = false;
      // We check that both fees and premiums are not null or of legnth 0
      if ((fees == null || fees.Length == 0) && (premiums == null || premiums.Length == 0))
      {
        throw new ArgumentException("Fees and Premiums cannot be both null");
      }

      if ((premiums == null || premiums.Length == 0) || (premiums.Length == 1 && Double.IsNaN(premiums[0])))
      {
        premiums = new double[] { 0.0 };
      }

      if (fees == null || fees.Length == 0)
      {
        fees = new double[] { 0.0 };
        //quotes are all running, should not ignore zero fees
      }else if (fees.Length == 1)
      {
        if(Double.IsNaN(fees[0]))
          fees = new double[] { 0.0 };

        if(fees[0] != 0.0 && premiums.Length > 1)
          throw new ArgumentException("Invalid Upfront Fee, provide one for each valid tenor");
      }else
      {
        foreach(double d in fees)
        {
          //existence of non-zero fee implies all quotes could be upfront, 
          //must check: if only a single premium is present then all quotes are certainly upfront
          if (d != 0.0 && premiums.Length <= 1)
            ignoreZeroFees = true;
        }
      }
  
      if (fees.Length != 1 && fees.Length != tenorDates.Length)
        throw new ArgumentException("Number of CDS up-front fees must be one or match number of CDS maturities");
      if (premiums.Length != 1 && premiums.Length != tenorDates.Length)
        throw new ArgumentException("Number of CDS running premiums must be one or match number of CDS maturities");
      return ignoreZeroFees;
    }

    /// <summary>
    ///   Calibrate a survival curve to CDX quotes
    /// </summary>
    /// 
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Pricing settle date (or None)</param>
    /// <param name="ccy">Currency of curve</param>
    /// <param name="effective">Index effective date</param>
    /// <param name="firstPrem">Index first premium date</param>
    /// <param name="dayCount">Index daycount of premium accrual</param>
    /// <param name="freq">Index frequency of premium payments</param>
    /// <param name="roll">Business day (roll) convention for premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="dealPremiums">Deal premiums</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="quotes">Index market quotes by tenors</param>
    /// <param name="quoteIsPrice">Indiccate if the index quotes should be prices or premium</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="forceFit">Set to true to allow bumped curves to have their quotes "flattened" when unable to fit naturally</param>
    /// 
    /// <returns>Market survival curve</returns>
    public static SurvivalCurve FitCDXQuotes(
      Dt asOf,
      Dt settle,
      Currency ccy,
      Dt effective,
      Dt firstPrem,
      DayCount dayCount,
      Frequency freq,
      BDConvention roll,
      Calendar calendar,
      double[] dealPremiums,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] quotes,
      bool quoteIsPrice,
      double[] recoveries,
      double recoveryDisp,
      bool forceFit
      )
    {
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle, null, discountCurve);

      //
      // Find the first premium date
      //
      if (calibrator.UseNaturalSettlement)
      {
        // Manually find the first premium date after the settle.
        // Note: Cannot rely on SurvivalCurve.AddCDS() to determine the first premium date.
        //       For example, when effective is 27/3/2007 and settle is 22/5/2007,
        //       SurvivalCurve.AddCDS() gives the first premium on 20/9/2007 instead of 20/6/2007.  HJ 21May07
        Dt maturity = Dt.Add(effective, "40Y");
        while (firstPrem <= settle)
          firstPrem = Dt.CDSRoll(firstPrem, false);
        if (firstPrem > maturity)
          firstPrem = maturity;
      }
      else
        firstPrem = Dt.Empty;

      //
      // Check tenor dates, names, fees, premiums only when the curve is not defaulted on as-of
      //
      double[] spreads;
      double[] fees = null;
      {
        // Fill out dates from tenors if necessary
        if (tenorDates == null || tenorDates.Length == 0)
        {
          if (tenorNames == null || tenorNames.Length == 0)
            throw new ArgumentException("TenorNames and TenorDates cannot be both null");
          tenorDates = new Dt[tenorNames.Length];
          for (int i = 0; i < tenorNames.Length; i++)
            tenorDates[i] = Dt.CDSMaturity(settle, tenorNames[i]);
        }

        // Validate
        if (tenorNames != null && tenorNames.Length == 0)
          tenorNames = null;
        if (tenorNames != null && tenorNames.Length != tenorDates.Length)
          throw new ArgumentException("Number of tenor names must match number of tenor dates");
        if (quotes == null || quotes.Length <= 0)
          throw new ArgumentException("Must specify at least one CDX quote");
        if (quotes.Length != tenorDates.Length)
          throw new ArgumentException("Number of index quotes must match number of index maturities");

        if (quoteIsPrice)
        {
          if (dealPremiums == null || dealPremiums.Length <= 0)
            throw new ArgumentException("Must specify all seal premiums with price quotes");
          else if (dealPremiums.Length != tenorDates.Length)
            throw new ArgumentException("Number of tenor dates and deal premiums not match");
          fees = new double[quotes.Length];
          for (int i = 0; i < quotes.Length; ++i)
            fees[i] = (100 - quotes[i]) / 100;
          spreads = dealPremiums;
        }
        else
          spreads = quotes;
      }

      // Create a market level curve using the standard 40pc recovery if no recoveries sepcified
      RecoveryCurve recoveryCurve = recoveries == null || recoveries.Length == 0 ?
        new RecoveryCurve(asOf, 0.4) :
        CurveUtil.GetRecoveryCurve(asOf, tenorDates, recoveries, recoveryDisp);

      // Survival calibrator
      calibrator.RecoveryCurve = recoveryCurve;
      calibrator.ForceFit = forceFit;
      calibrator.NegSPTreatment = nspTreatment;

      // Survival curve
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = ccy;
      curve.Category = category ?? "None";

      // add quotes
      for (int i = 0; i < tenorDates.Length; ++i)
        if (!Double.IsNaN(quotes[i]))
        {
          curve.AddCDS(
            tenorNames != null ? tenorNames[i] : null,
            tenorDates[i],
            fees == null ? 0.0 : fees[i],
            firstPrem,
            spreads[i] / 10000,
            dayCount,
            freq,
            roll,
            calendar);
        }

      curve.Fit();
      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a term structure of CDS using the Fit method
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a survival curve based on a term structure of CDS quotes
    ///   by backing out the implied survival
    ///   probability to the maturity of each CDS in sequence.
    ///   There are various options in terms of the form of the
    ///   survival probability which allow for alternate forms of the
    ///   hazard rate such as flat, smooth or time-weighted.</para>
    ///
    ///   <para>Entries where the CDS premium is &lt;= 0 are ignored.</para>
    /// </remarks>
    ///
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="ccy">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="cdsDayCount">Daycount of CDS premium accrual</param>
    /// <param name="cdsFrequency">Frequency of CDS premium payments</param>
    /// <param name="cdsRoll">Business day (roll) convention for CDS premium payments</param>
    /// <param name="cdsCalendar">Calendar for CDS premium payments</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="fees">Vector of CDS up-front fees or single fees for all CDS (percent)</param>
    /// <param name="premiums">CDS premiums (bps)</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="forceFit">Set to true to allow bumped curves to have their quotes "flattened" when unable to fit naturally</param>
    /// <param name="eventDates">Optionally default date and recovery settle date</param>
    /// <param name="refinanceCurve">Refinancing curve</param>
    /// <param name="corr">Correlation btw refinance and default</param>
    ///
    /// <returns>Constructed survival curve</returns>
    public static SurvivalCurve FitLCDSQuotes(
      Dt asOf,
      Currency ccy,
      string category,
      DayCount cdsDayCount,
      Frequency cdsFrequency,
      BDConvention cdsRoll,
      Calendar cdsCalendar,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] fees,
      double[] premiums,
      double[] recoveries,
      double recoveryDisp,
      bool forceFit,
      Dt[] eventDates,
      SurvivalCurve refinanceCurve,
      double corr
      )
    {
      Dt settle = asOf;
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle, null, discountCurve);
      if (calibrator.UseNaturalSettlement)
        settle = calibrator.Settle = Dt.Add(asOf, 1);

      Dt defaultDate, dfltSettle;
      bool ignoreZeroFees = false;
      GetDefaultDates(eventDates, settle, out defaultDate, out dfltSettle);
      bool notDefaultedOnAsOf = defaultDate.IsEmpty() || (defaultDate > asOf);

      // We check tenor dates, names, fees, premiums only when the curve is not defaulted on as-of
      if (notDefaultedOnAsOf)
      {
        BuildTenorDates(asOf,ref tenorNames,ref  tenorDates);

        ignoreZeroFees = BuildQuotes(tenorDates, ref fees, ref premiums);
      }

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(asOf, tenorDates, recoveries, recoveryDisp);

      calibrator.RecoveryCurve = recoveryCurve;
      calibrator.ForceFit = forceFit;
      calibrator.NegSPTreatment = nspTreatment;
      if (refinanceCurve != null)
      {
        calibrator.CounterpartyCurve = refinanceCurve;
        calibrator.CounterpartyCorrelation = corr;
      }
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = ccy;
      curve.Category = category ?? "None";

      if (notDefaultedOnAsOf)
      {
        bool addedCDS = false;
        for (int i = 0; i < tenorDates.Length; i++)
        {
          double prem = premiums.Length == 1 ? premiums[0] : premiums[i];
          double fee = fees.Length == 1 ? fees[0] : fees[i];

          //never allow an NaN quote on the curve
          if (Double.IsNaN(prem) || Double.IsNaN(fee))
          {
            //when two full arrays are given, if one side is blank throw error.
            if (premiums.Length == fees.Length && fees.Length > 1)
            {
              //blank on both arrays, so ignore
              if (Double.IsNaN(prem) && Double.IsNaN(fee))
                continue;
              throw new ArgumentException("Quote Missing at Tenor: " +tenorDates[i]);
            }

            continue;
          }

          //ignore zero fees is used when you have missing fee quotes that don't actually represent quotes to be calibrated
          if ((fee != 0 || prem > 0.0) && (fee != 0 || !ignoreZeroFees))
          {
            curve.AddCDS(tenorNames[i], tenorDates[i],
                         fee, prem/10000.0, cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar);
            curve.Tenors.Last().QuoteKey = String.Format("{0}.{1}.{2}", curve.Id, curve.Tenors.Count,
              String.IsNullOrEmpty(tenorNames[i]) ? "T" : tenorNames[i]);
            addedCDS = true;
          }
        }
        if(!addedCDS)
          throw new ArgumentException("No Valid Quotes were provided");

        curve.Fit();

        ConvertTenorCdsQuotes(curve);
      }

      // set default date
      if (defaultDate.IsValid())
      {
        curve.SetDefaulted(defaultDate, true);
        if (dfltSettle.IsValid())
          curve.SurvivalCalibrator.RecoveryCurve.JumpDate = dfltSettle;
      }

      return curve;
    }

    /// <summary>
    ///    Fit CDS/LCDS quotes with the new types of constracts and quotes
    /// </summary>
    /// 
    /// <param name="name">Name for curve</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settle date (it can be empty, in which case
    ///   the settle date is constructed default based on UseNaturalSettlement flag)</param>
    /// <param name="ccy">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="cdsQuoteType">CDS quote type.</param>
    /// <param name="runningPremium">The running premium.</param>
    /// <param name="parameters">Survival curve parameters object</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="quotes">CDS quotes, the interpretation of which is based on parameters.CdsQuoteType.</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="eventDates">Optionally default date and recovery settle date</param>
    /// <param name="refinanceCurve">Refinancing curve</param>
    /// <param name="corr">Correlation btw refinance and default</param>
    /// <param name="forcefit">if set to <c>true</c> do force fit.</param>
    /// <returns>Constructed survival curve</returns>
    /// <returns>Survival Curve.</returns>
    public static SurvivalCurve FitCDSQuotes(
      string name,
      Dt asOf,
      Dt settle,
      Currency ccy,
      string category,
      CDSQuoteType cdsQuoteType,
      double runningPremium,
      SurvivalCurveParameters parameters,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      double[] quotes,
      double[] recoveries,
      double recoveryDisp,
      Dt[] eventDates,
      SurvivalCurve refinanceCurve,
      double corr,
      bool forcefit
      )
    {
      return FitCDSQuotes(name, asOf, settle, ccy, category,
        false, cdsQuoteType, runningPremium, parameters,
        discountCurve, tenorNames, tenorDates, quotes,
        recoveries, recoveryDisp,
        eventDates, refinanceCurve, corr, Double.NaN, null, forcefit);
    }

    /// <summary>
    ///    Fit CDS/LCDS quotes with the new types of constracts and quotes
    /// </summary>
    /// 
    /// <param name="name">Name for curve</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settle date (it can be empty, in which case
    ///   the settle date is constructed default based on UseNaturalSettlement flag)</param>
    /// <param name="ccy">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="isStandardContract">CDS Standard Contract.</param>
    /// <param name="cdsQuoteType">CDS quote type.</param>
    /// <param name="runningPremium">The running premium.</param>
    /// <param name="parameters">Survival curve parameters object</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="quotes">CDS quotes, the interpretation of which is based on parameters.CdsQuoteType.</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDisp">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="eventDates">Optionally default date and recovery settle date</param>
    /// <param name="refinanceCurve">Refinancing curve</param>
    /// <param name="corr">Correlation btw refinance and default</param>
    /// <param name="convRecoveryRate">The recovery rate for conventional spread.</param>
    /// <param name="convDiscountCurve">The discount curve for conventional spread.</param>
    /// <param name="forcefit">if set to <c>true</c> do force fit.</param>
    /// <returns>A fitted survival curve.</returns>
    public static SurvivalCurve FitCDSQuotes( 
      string name, 
      Dt asOf, 
      Dt settle, 
      Currency ccy, 
      string category,
      bool isStandardContract,
      CDSQuoteType cdsQuoteType,
      double runningPremium,
      SurvivalCurveParameters parameters, 
      DiscountCurve discountCurve, 
      string[] tenorNames, 
      Dt[] tenorDates, 
      double[] quotes,
      double[] recoveries, 
      double recoveryDisp, 
      Dt[] eventDates, 
      SurvivalCurve refinanceCurve, 
      double corr,
      double convRecoveryRate,
      DiscountCurve convDiscountCurve,
      bool forcefit
      )
    {
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(
        asOf, settle, null, discountCurve);
      if (settle.IsEmpty())
      {
        settle = calibrator.Settle =
          calibrator.UseNaturalSettlement ? Dt.Add(asOf, 1) : asOf;
      }
      calibrator.ValueDate = asOf;

      Dt defaultDate, dfltSettle;
      GetDefaultDates(eventDates, settle, out defaultDate, out dfltSettle);
      bool notDefaultedOnAsOf = defaultDate.IsEmpty() || (defaultDate > asOf);

      // We check tenor dates and tenor names
      if (notDefaultedOnAsOf)
        BuildTenorDates(asOf, settle, ref tenorNames, ref  tenorDates);

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(asOf, tenorDates, recoveries, recoveryDisp);

      calibrator.RecoveryCurve = recoveryCurve;
      calibrator.ForceFit = forcefit;
      calibrator.NegSPTreatment = parameters.NegSPTreatment;
      calibrator.ForbidNegativeHazardRates = parameters.ForbidNegativeHazardRates;
      calibrator.AllowNegativeCDSSpreads = parameters.AllowNegativeCdsSpreads;  // This will be passed false by default, if not explicitly set.
      if (refinanceCurve != null)
      {
        calibrator.CounterpartyCurve = refinanceCurve;
        calibrator.CounterpartyCorrelation = corr;
      }
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(parameters.InterpMethod, parameters.ExtrapMethod);
      curve.Ccy = ccy;
      curve.Category = category ?? "None";
      curve.Name = name;
      curve.Stressed = parameters.Stressed;

      if (notDefaultedOnAsOf)
      {
        if (tenorDates.Length != quotes.Length)
        {
          throw new ArgumentException(String.Format("Quotes ({0}) and tenors ({1}) not match",
            tenorDates.Length, quotes.Length));
        }

        if (isStandardContract)
        {
          if (cdsQuoteType == CDSQuoteType.ConvSpread && Double.IsNaN(convRecoveryRate))
            throw new ArgumentException(String.Format("Conventional Recovery Rate is not provided"));
          if (convDiscountCurve == null)
            convDiscountCurve = discountCurve;
        }

        int idxTenor = 0;
        for (int i = 0; i < tenorDates.Length; i++)
        {
          if (!Double.IsNaN(quotes[i]))
          {
            double prem, fee;
            CurveTenorQuoteHandlers.CDSQuoteHandler qh;
            QuotingConvention qc;
            switch (cdsQuoteType)
            {
              case CDSQuoteType.Upfront:
                fee = quotes[i];
                prem = runningPremium;
                qc = isStandardContract ? QuotingConvention.CreditConventionalUpfront : QuotingConvention.Fee;
                qh = new CurveTenorQuoteHandlers.CDSQuoteHandler(fee, qc,
                  convDiscountCurve, convRecoveryRate);
                break;
              case CDSQuoteType.ConvSpread:
                prem = runningPremium;
                qh = new CurveTenorQuoteHandlers.CDSQuoteHandler(quotes[i] / 10000.0,
                  QuotingConvention.CreditConventionalSpread,
                  convDiscountCurve, convRecoveryRate);
                fee = 1 - CDSConverter.FromSpread(asOf,
                  tenorDates[i], convDiscountCurve, prem / 10000,
                  quotes[i] / 10000, convRecoveryRate, 1.0)
                  .CleanPrice / 100;
                break;
              default:
                fee = 0;
                prem = runningPremium > 0 ? runningPremium : quotes[i];
                qh = new CurveTenorQuoteHandlers.CDSQuoteHandler(quotes[i] / 10000.0,
                  QuotingConvention.CreditSpread,
                  convDiscountCurve, convRecoveryRate);
                break;
            }
            CDS cds = CreateCDS(tenorNames[i], asOf, settle, tenorDates[i],
              ccy, prem / 10000.0, fee, isStandardContract, parameters);
            curve.Add(cds, 0.0, 0.0, 0.0, 1.0);
            curve.Tenors[idxTenor].QuoteHandler = qh;
            curve.Tenors[idxTenor].OriginalQuote =
              qh.GetCurrentQuote(curve.Tenors[idxTenor]);
            curve.Tenors[idxTenor].QuoteKey = String.Format("{0}.{1}",
              name ?? curve.Id.ToString(), tenorNames[i]);
            ++idxTenor;
          }
        }
        if (idxTenor == 0)
          throw new ArgumentException("No Valid Quotes were provided");

        curve.Fit();

        if (cdsQuoteType == CDSQuoteType.Upfront)
        {
          // For backward compatibility, we convert the upfront quotes
          // into the par spreads.
          // Note that conventional spread quotes are not converted.
          curve.UpdateQuotes(QuotingConvention.CreditSpread);
        }
        else if (cdsQuoteType == CDSQuoteType.ParSpread && runningPremium > 0)
        {
          // Calculate and set up the implied upfront/fee for the hedge product.
          QuotingConvention quotingConvention = isStandardContract
                                                  ? QuotingConvention.CreditConventionalUpfront
                                                  : QuotingConvention.Fee;
          double[] fees = curve.GetQuotes(quotingConvention).ToArray();
          for (int i = 0; i < fees.Length; ++i)
            ((CDS)curve.Tenors[i].Product).Fee = fees[i];
        }
      }

      // set default date
      if (defaultDate.IsValid())
      {
        curve.SetDefaulted(defaultDate, true);
        if (dfltSettle.IsValid())
          curve.SurvivalCalibrator.RecoveryCurve.JumpDate= dfltSettle;
      }

      return curve;
    }

    /// <summary>
    ///   Creates a CDS product based on the given parameters.
    /// </summary>
    /// <param name="name">The name of the product.</param>
    /// <param name="asOf">Today.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="ccy">Currency.</param>
    /// <param name="premium">The deal premium.</param>
    /// <param name="fee">The upfront fee.</param>
    /// <param name="isStandardContract">CDS Standard Contract.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>CDS product</returns>
    private static CDS CreateCDS(
      string name, Dt asOf, Dt settle, Dt maturity,
      Currency ccy, double premium, double fee,
      bool isStandardContract,
      SurvivalCurveParameters parameters)
    {
      Dt effective = isStandardContract ? Dt.SNACFirstAccrualStart(asOf, parameters.Calendar) : settle;
      CDS cds = new CDS(effective, maturity, ccy, premium, parameters.DayCount,
        parameters.Frequency, parameters.Roll, parameters.Calendar);
      cds.AccruedOnDefault = true;
      cds.Description = name;
      // fix the first premium date
      cds.FirstPrem = cds.FirstPrem;
      cds.Fee = fee;
      if (isStandardContract)
      {
        // We use T+3 as the value date.
        cds.FeeSettle = Dt.AddDays(asOf, 3, Calendar.None);
      }
      return cds;
    }

    /// <summary>
    ///   Record orginal tenor quotes and Convert tenor products to have all-running spreads
    /// </summary>
    /// <param name="curve">A survival curve calbrated with CDS/LCDS quotes</param>
    private static void ConvertTenorCdsQuotes(SurvivalCurve curve)
    {
      // since this is an internal function, we assume the curve
      // has non-null calibrator and tenors and all the tenor products
      // are cds/lcds.
      SurvivalFitCalibrator calibrator = (SurvivalFitCalibrator)curve.Calibrator;
      foreach (CurveTenor tenor in curve.Tenors)
      {
        double fee = -tenor.MarketPv; // cds Fee is recorded here;
        CDS cds = (CDS)tenor.Product;
        tenor.OriginalQuote = new CurveTenor.UpfrontFeeQuote(fee, cds.Premium);
        if (fee != 0.0)
        {
          cds.Fee = 0;
          ICDSPricer pricer = (ICDSPricer)calibrator.GetPricer(curve, cds);
          cds.Premium = pricer.BreakEvenPremium();
          tenor.MarketPv = 0.0; // indicate zero fee
        }
        tenor.QuoteHandler = CurveTenorQuoteHandlers.DefaultHandler;
      }
      return;
    }


    /// <summary>
    ///   Build tenor dates from tenor names
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">As-of date</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates</param>
    /// <returns>Tenor names</returns>
    public static void BuildTenorDates(Dt asOf, Dt settle,
      ref string[] tenorNames, ref Dt[] tenorDates)
    {
      if(settle.IsEmpty())
        settle = ToolkitConfigurator
          .Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(asOf, 1) : asOf;

      if (tenorDates == null || tenorDates.Length == 0)
      {
        if (tenorNames == null || tenorNames.Length == 0)
          throw new ArgumentException("TenorNames and TenorDates cannot be both null");
        //figure out the dates from namses
        tenorDates = new Dt[tenorNames.Length];
        for (int i = 0; i < tenorNames.Length; i++)
        {
          tenorDates[i] = Dt.CDSMaturity(settle, tenorNames[i]);
        }
      }

      if (tenorNames == null || tenorNames.Length == 0)
      {
        //provide dummy names
        tenorNames = new string[tenorDates.Length];
        for (int i = 0; i < tenorNames.Length; i++)
          tenorNames[i] = null;
      }

      if (tenorNames != null && tenorNames.Length != tenorDates.Length)
        throw new ArgumentException("Number of tenor names must match number of tenor dates");

    }

    /// <summary>
    /// Builds the tenor dates.
    /// </summary>
    /// <param name="asOf">As-of date.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="tenorDates">The tenor dates.</param>
    public static void BuildTenorDates(Dt asOf, ref string[] tenorNames, ref Dt[] tenorDates)
    {
      BuildTenorDates(asOf, Dt.Empty, ref tenorNames, ref tenorDates);
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a set of products (CDS/Bond/etc) using the BDT Fit method
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calibrate a survival curve using the BDT fit algorithm.</para>
    ///
    ///   <details>
    ///   <para>Constructs a survival curve based on a set of products
    ///   by backing out the implied survival
    ///   probability to the maturity of each product in sequence.
    ///   There are various options in terms of the form of the
    ///   survival probability which allow for alternate forms of the
    ///   hazard rate such as flat, smooth or time-weighted.</para>
    ///   </details>
    ///
    ///   <para>Entries with no products are ignored.</para>
    ///
    ///   <para>The financing spreads may be a vector or a single value for all tenors.</para>
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volatilityCurve">Spot rate volatility curve for BDT</param>
    /// <param name="products">Products in maturity order</param>
    /// <param name="prices">Market (full) prices of products</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="finSpreads">Product financing spreads in bps (default is 0)</param>
    ///
    /// <returns>Constructed survival curve</returns>
    public static SurvivalCurve FitBDT(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      VolatilityCurve volatilityCurve,
      IProduct[] products,
      double[] prices,
      double[] recoveries,
      double recoveryDispersion,
      double[] finSpreads
      )
    {
      if (products == null || products.Length <= 0)
        throw new ArgumentException("Must specify at least one product to fit");
      if (prices == null || prices.Length != products.Length)
        throw new ArgumentException("Number of prices must match number of products");
      if (finSpreads == null || finSpreads.Length == 0)
        finSpreads = new double[] { 0.0 };
      if (finSpreads.Length > 1 && finSpreads.Length != products.Length)
        throw new ArgumentException("Number of financing spreads must be one or match number of maturities");

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, products, recoveries, recoveryDispersion);

      SurvivalBDTCalibrator calibrator = new SurvivalBDTCalibrator(asOfDate, asOfDate, recoveryCurve, discountCurve, volatilityCurve);
      if (calibrator.UseNaturalSettlement)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = nspTreatment;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = currency;
      curve.Category = category ?? "None";

      for (int i = 0; i < products.Length; i++)
      {
        if (products[i] != null)
          curve.Add(products[i], prices[i] / 100.0, 0.0,
            finSpreads.Length == 1 ? (finSpreads[0] / 10000.0) : (finSpreads[i] / 10000.0),
            1.0);
      }

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a set of products using an Affine hazard rate process
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calibrate a survival curve assuming a general affine process
    ///   for the hazard rate. Calibration is done by running an
    ///   optimizer over pricing of the whole curve.</para>
    ///
    ///   <details>
    ///   <para>Constructs a survival curve
    ///   based on a set of products by running an optimizer over the
    ///   pricing of all of the tenors assuming an affine process for
    ///   the hazard rate.</para>
    ///
    ///   <para>The hazard rate process has a mean-reverting drift with
    ///   volatility proportional to
    ///   the square root of <formula inline="true">lambda</formula>, plus jumps.</para>
    ///
    ///   <para>The dynamic of <formula inline="true">lambda</formula> is described by:
    ///   <formula>
    ///   dx_t = \kappa_x[\theta - \lambda_t]dt + \sigma_\lambda\sqrt{\lambda_t} dW_{\lambda_t} + \Delta J(t)
    ///   </formula></para>
    ///
    ///   <para>Here <formula inline="true">\Delta J(t)</formula> refers to the increments
    ///   at time <formula inline="true">t</formula> of an
    ///   independent pure jump process with independently and identically
    ///   distributed exponential jumps with mean size <formula inline="true">\mu_\lambda</formula>
    ///   and arrival intensity <formula inline="true">\ell_\lambda</formula></para>
    ///
    ///   <para>The objective function for fit is
    ///   <formula inline="true">\sum_{i=1}^n (P_i - M_i)^2 * W_i</formula>
    ///   where P is the market full price and M is the calculated model price
    ///   and W is the weighting.</para>
    ///   </details>
    ///
    ///   <para>Any product that can be priced using the credit-contingent
    ///   cashflow pricing model can be used to calibrate.</para>
    ///
    ///   <para>The financing spreads and weights may be a vector or a
    ///   single value for all tenors.</para>
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="products">Products in maturity order</param>
    /// <param name="prices">Market (full) prices of products</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="weights">Weights to assign each product for calibration (default is 1)</param>
    /// <param name="finSpreads">Product financing spreads in bps (default is 0)</param>
    ///
    /// <returns>Constructed survival curve</returns>
    ///
    /// <exclude />
    public static SurvivalCurve FitAffine(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      IProduct[] products,
      double[] prices,
      double[] recoveries,
      double recoveryDispersion,
      double[] weights,
      double[] finSpreads
      )
    {
      if (products == null || products.Length <= 0)
        throw new ArgumentException("Must specify at least one product to fit");
      if (prices == null || prices.Length != products.Length)
        throw new ArgumentException("Number of prices must match number of products");
      if (finSpreads == null || finSpreads.Length == 0)
        finSpreads = new double[] { 0.0 };
      if (finSpreads.Length > 1 && finSpreads.Length != products.Length)
        throw new ArgumentException("Number of financing spreads must be one or match number of products");
      if (weights == null || weights.Length == 0)
        weights = new double[] { 1.0 };
      if (weights.Length > 1 && weights.Length != products.Length)
        throw new ArgumentException("Number of weights must be one or match number of products");

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, products, recoveries, recoveryDispersion);

      SurvivalAffineCalibrator calibrator = new SurvivalAffineCalibrator(asOfDate, asOfDate, recoveryCurve, discountCurve);
      if (calibrator.UseNaturalSettlement)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = nspTreatment;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Ccy = currency;
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Category = category ?? "None";

      for (int i = 0; i < products.Length; i++)
      {
        if (products[i] != null)
          curve.Add(products[i], prices[i] / 100.0, 0.0,
                    finSpreads.Length == 1 ? finSpreads[0] / 10000.0 : finSpreads[i] / 10000.0,
                    weights.Length == 1 ? weights[0] : weights[i]);
      }

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Calibrate a Survival Curve from a set of products using a CIR hazard rate process
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calibrate a survival curve assuming a CIR process
    ///   for the hazard rate. Calibration is done by running an
    ///   optimizer over pricing of the whole curve.</para>
    ///
    ///   <details>
    ///   <para>Constructs a survival curve
    ///   based on a set of products by running an optimizer over the
    ///   pricing of all of the tenors assuming a CIR process for
    ///   the hazard rate.</para>
    ///
    ///   <para>The hazard rate process has a mean-reverting drift with
    ///   volatility proportional to
    ///   the square root of <formula inline="true">lambda</formula>.</para>
    ///
    ///   <para>The dynamic of <formula inline="true">lambda</formula> is described by:
    ///   <formula>
    ///   dx_t = \kappa_x[\theta - \lambda_t]dt + \sigma_\lambda\sqrt{\lambda_t} dW_{\lambda_t}
    ///   </formula></para>
    ///
    ///   <para>The objective function for fit is
    ///   <formula inline="true">\sum_{i=1}^n (P_i - M_i)^2 * W_i</formula>
    ///   where P is the market full price and M is the calculated model price
    ///   and W is the weighting.</para>
    ///   </details>
    ///
    ///   <para>Any product that can be priced using the credit-contingent
    ///   cashflow pricing model can be used to calibrate.</para>
    ///
    ///   <para>The financing spreads and weights may be a vector or a
    ///   single value for all tenors.</para>
    /// </remarks>
    ///
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="currency">Currency of curve</param>
    /// <param name="category">Category for curve</param>
    /// <param name="interpMethod">Interpolation method for survival probabilities</param>
    /// <param name="extrapMethod">Extrapolation method for survival probabilities</param>
    /// <param name="nspTreatment">Treatment of negative survival probabilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="products">Products in maturity order</param>
    /// <param name="prices">Market (full) prices of products</param>
    /// <param name="recoveries">Single recovery rate or recovery rates matching rates in percent (eg. 0.4)</param>
    /// <param name="recoveryDispersion">Dispersion of recovery rates in percent (default is zero)</param>
    /// <param name="weights">Weights to assign each product for calibration (default is 1)</param>
    /// <param name="finSpreads">Product financing spreads in bps (default is 0)</param>
    ///
    /// <returns>Constructed survival curve</returns>
    ///
    /// <exclude />
    public static SurvivalCurve FitCIR(
      Dt asOfDate,
      Currency currency,
      string category,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      DiscountCurve discountCurve,
      IProduct[] products,
      double[] prices,
      double[] recoveries,
      double recoveryDispersion,
      double[] weights,
      double[] finSpreads
      )
    {
      if (products == null || products.Length <= 0)
        throw new ArgumentException("Must specify at least one product to fit");
      if (prices.Length != products.Length)
        throw new ArgumentException("Number of prices must match number of products");
      if (finSpreads == null || finSpreads.Length == 0)
        finSpreads = new double[] { 0.0 };
      if (finSpreads.Length > 1 && finSpreads.Length != products.Length)
        throw new ArgumentException("Number of financing spreads must be one or match number of products");
      if (weights == null || weights.Length == 0)
        weights = new double[] { 1.0 };
      if (weights.Length > 1 && weights.Length != products.Length)
        throw new ArgumentException("Number of weights must be one or match number of products");

      RecoveryCurve recoveryCurve = CurveUtil.GetRecoveryCurve(
        asOfDate, products, recoveries, recoveryDispersion);

      SurvivalCIRCalibrator calibrator = new SurvivalCIRCalibrator(asOfDate, asOfDate, recoveryCurve, discountCurve);
      if (calibrator.UseNaturalSettlement)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = nspTreatment;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = currency;
      curve.Category = category ?? "None";

      for (int i = 0; i < products.Length; i++)
      {
        if (products[i] != null)
          curve.Add(products[i], prices[i] / 100.0, 0.0,
            finSpreads.Length == 1 ? finSpreads[0] / 10000.0 : finSpreads[i] / 10000.0,
            weights.Length == 1 ? weights[0] : weights[i]);
      }

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///   Constructs a scaled credit curve based on an existing credit curve and specified scaling factors
    /// </summary>
    /// <remarks>
    ///   <para>Scaled credit curves are typically used to match existing credit curves to the traded value
    ///   of credit baskets for the pricing of components of that basket.</para>
    /// </remarks>
    /// <param name="survivalCurve">Unscaled survival curve</param>
    /// <param name="tenorNames">List of tenors to scale (default is all)</param>
    /// <param name="scalingFactors">Scaling factors for each tenor</param>
    /// <param name="relative">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <returns>Scaled survival curve</returns>
    public static SurvivalCurve Scale(
      SurvivalCurve survivalCurve,
      string[] tenorNames,
      double[] scalingFactors,
      bool relative
      )
    {
      if (survivalCurve.SurvivalCalibrator == null)
        throw new ArgumentException(String.Format("Credit curve {0} has not been calibrated", survivalCurve.Name));
      if (scalingFactors == null || scalingFactors.Length < 1)
        throw new ArgumentException("Must specify at lease one scaling factor");
      if (tenorNames != null && tenorNames.Length == 0)
        tenorNames = null;
      if (tenorNames != null && tenorNames.Length != scalingFactors.Length)
        throw new ArgumentException(String.Format("Number of tenors ({0}) must be none or match number of scaling factors ({1})", tenorNames.Length, scalingFactors.Length));

      Dt defaultDate = survivalCurve.DefaultDate;
      if (defaultDate.IsValid() && (defaultDate <= survivalCurve.AsOf))
        return (SurvivalCurve)survivalCurve.Clone();

      // Copy original survival curve
      Dt asOfDate = survivalCurve.AsOf;
      DiscountCurve discountCurve = survivalCurve.SurvivalCalibrator.DiscountCurve;
      RecoveryCurve recoveryCurve = survivalCurve.SurvivalCalibrator.RecoveryCurve;

      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOfDate, asOfDate, recoveryCurve, discountCurve);
      if (calibrator.UseNaturalSettlement)
        calibrator.Settle = Dt.Add(asOfDate, 1);
      calibrator.NegSPTreatment = survivalCurve.SurvivalCalibrator.NegSPTreatment;
      calibrator.CounterpartyCurve = survivalCurve.SurvivalCalibrator.CounterpartyCurve;
      calibrator.CounterpartyCorrelation = survivalCurve.SurvivalCalibrator.CounterpartyCorrelation;
      if (survivalCurve.SurvivalCalibrator is SurvivalFitCalibrator)
      {
        calibrator.ForceFit = ((SurvivalFitCalibrator)survivalCurve.SurvivalCalibrator).ForceFit;
      }
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = survivalCurve.Interp.clone();
      curve.Ccy = survivalCurve.Ccy;
      curve.Category = survivalCurve.Category;

      foreach (CurveTenor t in survivalCurve.Tenors)
      {
        CurveTenor tenor = (CurveTenor)t.Clone();
        curve.Tenors.Add(tenor);
      }

      // Be sure to fit the curve first, otherwise the bump might do the wrong thing.
      curve.Fit();

      // Scale tenors
      CurveUtil.CurveBump(new SurvivalCurve[] { curve }, tenorNames, scalingFactors, true, relative, true, null);

      // Set default date if any
      if (defaultDate.IsValid())
        curve.SetDefaulted(defaultDate, true);

      return curve;
    }

    /// <summary>
    /// Constructs a credit curve based on a mixture of existing credit curves
    /// </summary>
    /// <remarks>
    ///   <para>Create a survival curve by mixing par CDS levels CDS from existing credit curves.</para>
    ///   <para>Each CDS is calculated as:</para>
    ///   <math>CDS_{i} = \sum_{j=1}^{n} CDS_{i}^{j} * \beta_{j} + spread</math>
    ///   <para>Where:</para>
    ///   <list type="bullet">
    ///     <item><description><m>CDS_{i}</m> is the calulated ith CDS</description></item>
    ///     <item><description><m>CDS_{i}^{j}</m> is the implied par CDS at the ith tenor from the jth survivalCurve</description></item>
    ///     <item><description><m>\beta_{j}</m> is the jth factor</description></item>
    ///     <item><description><m>spread</m> is the spread</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date or pricing date of first survival curve if not specified</param>
    /// <param name="settle">Effective date or effective date of first survival curve if not specified</param>
    /// <param name="ccy">Currency of curve or currency of first survival curve if not specified</param>
    /// <param name="category">Category for curve</param>
    /// <param name="discountCurve">Discount curve ir discount curve of first survival curve if not specified</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="scalingFactors">Scaling factors to apply to each survival curve par CDS</param>
    /// <param name="spread">Final spread to add to mixed curves in basis points</param>
    /// <param name="recovery">Recovery rate or recovery rate of first survival curve if not specified</param>
    /// <returns>Mixed survival curve</returns>
    public static SurvivalCurve Mixed(
      Dt asOf,
      Dt settle,
      Currency ccy,
      string category,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      SurvivalCurve[] survivalCurves,
      double[] scalingFactors,
      double spread,
      double recovery
      )
    {
      if( survivalCurves == null || survivalCurves.Length < 1 )
        throw new ArgumentException("Must specify at lease one survival curve");
      if( scalingFactors == null || scalingFactors.Length != survivalCurves.Length )
        throw new ArgumentException("Number of scaling factors must match number of survival curves");
      if (tenorDates != null && (tenorNames != null && tenorDates.Length != tenorNames.Length))
        throw new ArgumentException("Number of tenor dates not consistent with number of tenor names");
      var calibrator = survivalCurves[0].Calibrator as SurvivalFitCalibrator;
      if( calibrator == null )
        throw new ArgumentException("Survival curves must be Calibrated");
      // Get defaults
      if (asOf.IsEmpty())
        asOf = survivalCurves[0].AsOf;
      if (settle.IsEmpty())
        settle = survivalCurves[0].Calibrator.Settle;
      if (discountCurve == null)
        discountCurve = calibrator.DiscountCurve;
      if (recovery < 0.0)
        recovery = calibrator.RecoveryCurve.RecoveryRate(settle);

      if (tenorNames == null || tenorNames.Length == 0)
      {
        if (tenorDates == null || tenorDates.Length == 0)
        {
          // No tenors specified, so use all tenors from first curve
          tenorNames = new string[survivalCurves[0].Tenors.Count];
          tenorDates = new Dt[survivalCurves[0].Tenors.Count];
          for (var i = 0; i < survivalCurves[0].Tenors.Count; i++)
          {
            tenorNames[i] = survivalCurves[0].Tenors[i].Name;
            tenorDates[i] = survivalCurves[0].Tenors[i].Maturity;
          }
        }
        else
        {
          tenorNames = new string[tenorDates.Length];
          for (int i = 0; i < tenorDates.Length; ++i)
          {
            tenorNames[i] = tenorDates[i].ToString();
          }
        }
      }
      else if (tenorDates == null || tenorDates.Length == 0)
      {
        // Tenor names specified but not tenor dates specified so imply from tenor names and first curve asOf date
        tenorDates = new Dt[tenorNames.Length];
        for (var i = 0; i < tenorNames.Length; i++)
          tenorDates[i] = Dt.CDSMaturity(survivalCurves[0].AsOf, tenorNames[i]);
      }

      // Calculate CDS levels
      var cds = new double[tenorDates.Length];
      for (var i = 0; i < tenorDates.Length; i++)
      {
        cds[i] = spread;
        for( var j = 0; j < survivalCurves.Length; j++ )
          cds[i] += survivalCurves[j].ImpliedSpread(tenorDates[i]) * 10000.0 * scalingFactors[j];
      }
      // Create curve
      var param = SurvivalCurveParameters.GetDefaultParameters();
      var curve = SurvivalCurve.FitCDSQuotes(String.Empty, asOf, settle, ccy, category, false,
                                                       CDSQuoteType.ParSpread, Double.NaN, param, discountCurve, tenorNames,
                                                       tenorDates, cds, new double[] { recovery }, 0, null, null,
                                                       0, Double.NaN, null, false);
      return curve;
    }

    public static SurvivalCurve FromHazardRate(
      Dt asOf, DiscountCurve discountCurve,
      string cdsTenor, double hazardRate, double recoveryRate, bool refit)
    {
      var cal = new SurvivalFitCalibrator(asOf, asOf,
        recoveryRate, discountCurve ?? new DiscountCurve(asOf, 0.0));
      var curve = new SurvivalCurve(asOf, hazardRate)
      {
        Calibrator = cal
      };
      if (String.IsNullOrEmpty(cdsTenor))
        cdsTenor = "5Y";
      Dt maturity = Dt.CDSMaturity(asOf, cdsTenor);
      double sp = curve.SurvivalProb(maturity);
      curve.Clear();
      curve.Add(maturity, sp);
      curve.AddCDS(cdsTenor, maturity, 0, 0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      var pricer = (CDSCashflowPricer)curve.Tenors[0].GetPricer(curve, curve.Calibrator);
      pricer.CDS.Premium = pricer.BreakEvenPremium();
      curve.Tenors[0].Product = pricer.CDS;
      pricer = (CDSCashflowPricer)curve.Tenors[0].GetPricer(curve, curve.Calibrator);
      curve.Tenors[0].MarketPv = pricer.Pv();
      if(refit) curve.Fit();
      return curve;
    }
    #endregion // Static_Constructors

    #region Methods

    /// <summary>
		///   Add cds tenor to Survival Calibration
		/// </summary>
		///
		/// <remarks>
		///   Useful convenience function for adding a standard CDS tenor point
		/// </remarks>
		///
		/// <param name="description">Description of CDS (for tenor) or null to use maturity date</param>
		/// <param name="maturity">Maturity of cds tenor</param>
		/// <param name="fee">Up-front fee (.001 = 10bp)</param>
		/// <param name="premium">Premium (.001 = 10bp)</param>
		/// <param name="dayCount">Daycount of premium accrual</param>
		/// <param name="frequency">Frequency of premium payment</param>
		/// <param name="roll">Business day convention for premium payment</param>
		/// <param name="calendar">Calendar for premium payment</param>
    ///
		/// <returns>New curve tenor</returns>
		///
		public CurveTenor
		AddCDS( string description, Dt maturity, double fee, double premium, DayCount dayCount,
						Frequency frequency, BDConvention roll, Calendar calendar )
		{
      CDS cds = CreateCDS(description, FindEffectiveDate(), maturity,
        Dt.Empty, premium, dayCount, frequency, roll, calendar);
			// Fee is treated as a price as the cashflow routines skip payments on the settlement
			// date by default.
			cds.Fee = 0.0;
			return Add(cds, -fee, 0.0, 0.0, 1.0);
		}

   


		/// <summary>
		///   Add cds tenor to Survival Calibration
		/// </summary>
		///
		/// <remarks>
		///   Useful convenience function for adding a standard CDS tenor point
		/// </remarks>
		///
		/// <param name="description">Description of CDS (for tenor) or null to use maturity date</param>
		/// <param name="maturity">Maturity of cds tenor</param>
		/// <param name="fee">Up-front fee (.001 = 10bp)</param>
		/// <param name="firstPrem">First Premium Date (.001 = 10bp)</param>
		/// <param name="premium">Premium (.001 = 10bp)</param>
		/// <param name="dayCount">Day count of premium accrual</param>
		/// <param name="frequency">Frequency of premium payment</param>
		/// <param name="roll">Business day convention for premium payment</param>
		/// <param name="calendar">Calendar for premium payment</param>
		///
		public void
		AddCDS( string description, Dt maturity, double fee, Dt firstPrem, double premium, DayCount dayCount,
			Frequency frequency, BDConvention roll, Calendar calendar )
		{
      CDS cds = CreateCDS(description, FindEffectiveDate(), maturity,
        firstPrem, premium, dayCount, frequency, roll, calendar);
			// Fee is treated as a price as the cashflow routines skip payments on the settlement
			// date by default.
			cds.Fee = 0.0;
			Add(cds, -fee, 0.0, 0.0, 1.0);
			return;
		}


		/// <summary>
		///   Add cds tenor to Survival Calibration
		/// </summary>
		///
		/// <remarks>
		///   Useful convenience function for adding a standard CDS tenor point
		/// </remarks>
		///
		/// <param name="maturity">Maturity of cds tenor</param>
		/// <param name="fee">Up-front fee (.001 = 10bp)</param>
		/// <param name="premium">Premium (.001 = 10bp)</param>
		/// <param name="dayCount">Daycount of premium accrual</param>
		/// <param name="frequency">Frequency of premium payment</param>
		/// <param name="roll">Business day convention for premium payment</param>
		/// <param name="calendar">Calendar for premium payment</param>
    ///
		/// <returns>New curve tenor</returns>
		///
		public CurveTenor
		AddCDS( Dt maturity, double fee, double premium, DayCount dayCount,
						Frequency frequency, BDConvention roll, Calendar calendar )
		{
			return AddCDS( null, maturity, fee, premium, dayCount, frequency, roll, calendar);
		}


		/// <summary>
		///   Add cds tenor to Survival Calibration
		/// </summary>
		///
		/// <remarks>
		///   Useful convenience function for adding a standard CDS tenor point
		/// </remarks>
		///
		/// <param name="description">Description of CDS (for tenor) or null to use maturity date</param>
		/// <param name="maturity">Maturity of cds tenor</param>
		/// <param name="premium">Premium (.001 = 10bp)</param>
		/// <param name="dayCount">Daycount of premium accrual</param>
		/// <param name="frequency">Frequency of premium payment</param>
		/// <param name="roll">Business day convention for premium payment</param>
		/// <param name="calendar">Calendar for premium payment</param>
    ///
		/// <returns>New curve tenor</returns>
		///
		public CurveTenor
		AddCDS( string description, Dt maturity, double premium, DayCount dayCount, Frequency frequency,
						BDConvention roll, Calendar calendar )
		{
			return AddCDS(description, maturity, 0.0, premium, dayCount, frequency, roll, calendar);
		}


		/// <summary>
		///   Add cds tenor to Survival Calibration
		/// </summary>
		///
		/// <remarks>
		///   Useful convenience function for adding a standard CDS tenor point
		/// </remarks>
		///
		/// <param name="maturity">Maturity of cds tenor</param>
		/// <param name="premium">Premium (.001 = 10bp)</param>
		/// <param name="dayCount">Daycount of premium accrual</param>
		/// <param name="frequency">Frequency of premium payment</param>
		/// <param name="roll">Business day convention for premium payment</param>
		/// <param name="calendar">Calendar for premium payment</param>
    ///
		/// <returns>New curve tenor</returns>
		///
		public CurveTenor
		AddCDS( Dt maturity, double premium, DayCount dayCount, Frequency frequency,
						BDConvention roll, Calendar calendar )
		{
			return AddCDS(null, maturity, 0.0, premium, dayCount, frequency, roll, calendar);
		}

    
    /// <summary>
		///  Add risky zero coupon bond to calibration
		/// </summary>
		///
		/// <remarks>
		///   <para>Each risky zero coupon bond is assumed to pay face at maturity
		///   if no default occurs or a percentage of face (recovery) if default occurs
	  ///   before maturity.</para>
	  ///
	  ///   <para>The bonds are quoted in terms of a simple yield.</para>
		/// </remarks>
		///
		/// <param name="description">Description of zero coupon bond or null to use maturity date</param>
		/// <param name="maturity">Maturity date of zero coupon bond</param>
		/// <param name="yield">Zero coupon bond yield</param>
		/// <param name="dayCount">Daycount of yield</param>
		/// <param name="freq">Compounding frequency of yield</param>
    ///
		/// <returns>New curve tenor</returns>
		///
    public CurveTenor
		AddZeroYield( string description, Dt maturity, double yield, DayCount dayCount, Frequency freq)
		{
			// Validate
			if( !maturity.IsValid() )
				throw new ArgumentException(String.Format("Invalid maturity. Must be valid date, not {0}.", maturity));
			if( yield < 0.0 || yield > 20.0 )
			  throw new ArgumentOutOfRangeException("yield", "Invalid yield. Must be between 0 and 20.0");

			//logger.Debug( String.Format("Adding rate {0}, asOf={1}, maturity={2}, dayCount={3}, freq={4}", quote, AsOf, maturity, dayCount, freq) );
      Note note = new Note(FindEffectiveDate(), maturity, Ccy, yield, dayCount, freq, BDConvention.None, Calendar.None);
			note.Description = description;
			return Add(note, 1.0, 0.0, 0.0, 1.0);
		}


    /// <summary>
		///  Add risky zero coupon bond to calibration
		/// </summary>
		///
		/// <remarks>
		///   <para>Each risky zero coupon bond is assumed to pay face at maturity
		///   if no default occurs or a percentage of face (recovery) if default occurs
	  ///   before maturity.</para>
	  ///
	  ///   <para>The bonds are quoted in terms of a simple yield.</para>
		/// </remarks>
		///
		/// <param name="maturity">Maturity date of zero coupon bond</param>
		/// <param name="yield">Zero coupon bond yield</param>
		/// <param name="dayCount">Daycount of yield</param>
		/// <param name="freq">Compounding frequency of yield</param>
    ///
		/// <returns>New curve tenor</returns>
		///
    public CurveTenor
		AddZeroYield( Dt maturity, double yield, DayCount dayCount, Frequency freq)
		{
			return AddZeroYield( null, maturity, yield, dayCount, freq );
		}


		/// <summary>
		///   Get implied default date given survival probability.
    /// </summary>
		///
    /// <param name="survivalProbability">Probability of survival.</param>
		///
    /// <returns>Implied default date matching survival probability.</returns>
		///
		public Dt
		DefaultDt(double survivalProbability)
		{
			return Solve(survivalProbability, 1e-6, 1000);
		}


    /// <summary>
    ///   Get the survival probability given a date
    /// </summary>
		///
		/// <remarks>
		///   Interpolates the survival probability from a survival curve given a date.
		/// </remarks>
		///
		/// <param name="date">Date to interpolate for</param>
		///
    /// <returns>Survival probability matching date</returns>
		///
		public double
		SurvivalProb(Dt date)
		{
			return Interpolate(date);
		}
    /// <summary>
    ///  Interpolate survival probability between two dates
    /// </summary>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>Survival probability</returns>
    public override double Interpolate(Dt start, Dt end)
    {
      const double tiny_probability = 1E-9;
      double p = Interpolate(start);
    
      // If survival probability at the starting date is zero,
      // the curve is defaulted after the starting date.
      // To count the default as in the period, we need to assume
      // the curve exists (is not defaulted) at the begining.
      if (p <= tiny_probability)
        return Dt.Cmp(start,end) >= 0 ? 1.0 : 0.0;

      // Compute the conditional survival
      p = Interpolate(end)/p;
      if (p > 1)
        p = 1;
      else if (p < 0)
        p = 0;
      return p;      
    }

    /// <summary>
    ///   Get forward (conditional) survival probability between start and end date
    /// </summary>
		///
    /// <param name="start">Date conditional on reaching</param>
    /// <param name="end">Date probability of default to.</param>
		///
    /// <returns>Survival probability to end date conditional on surviving to start date.</returns>
		///
    public double
		SurvivalProb(Dt start, Dt end)
		{
			return( Interpolate(start, end) );
		}


    /// <summary>
    ///   Get default probability given date
    /// </summary>
		///
    /// <param name="date">Date to interpolate for</param>
		///
    /// <returns>Default probability matching date</returns>
		///
    public double
		DefaultProb(Dt date)
		{
			return( 1.0 - Interpolate(date) );
		}


    /// <summary>
    ///   Get forward (conditional) default probability between start and end date
    /// </summary>
		///
    /// <param name="start">Date conditional on reaching</param>
    /// <param name="end">Date probability of default to</param>
		///
		/// <remarks>
    /// The recovery dispersion input determines the distribution of recovery rates.
		/// Using zero as input will treat all recovery rates as constants, while any other positive
		/// number will lead to employing a Beta distribution with variance that is proportional to
		/// the level of dispersion indicated.  Levels of dispersion between 0.4 and 0.6 do a
		/// reasonable job of matching empirically observed historical recovery rates as reported
		/// by Moodys and S&amp;P.
    /// </remarks>
		///
    /// <returns>Default probability to end date conditional on surviving to start date.</returns>
		///
		public double
		DefaultProb(Dt start, Dt end)
		{
			return( 1.0 - Interpolate(start, end) );
		}


    /// <summary>
    ///   Get hazard rate given date
    /// </summary>
		///
    /// <param name="date">Date hazard rate to</param>
		///
    /// <returns>Implied constant hazard rate from curve as-of date to specified date.</returns>
		///
    public double
		HazardRate(Dt date)
		{
			return( R(date) );
		}


    /// <summary>
    ///   Get forward hazard rate between start and end date
    /// </summary>
		///
    /// <param name="start">Date conditional on reaching</param>
    /// <param name="end">Date probability of default to.</param>
		///
    /// <returns>Implied constant hazard rate between start and end dates.</returns>
		///
		public double
		HazardRate(Dt start, Dt end)
		{
			return( F(start, end) );
		}


    /// <summary>
        /// Initializes the coefficients of the system of equations satisfied respectively by the gradient and hessian of each of the curve ordinates w.r.t the market quotes of the products in 
        /// tenors_, used for calibration of the curve. In particular, let <m>q_i</m> be the <m>i^th</m> quote, the, once the curve is calibrated the following holds:
        /// <m>q_i = f_i(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))</m>, where the functions <m>f_i, i = 0,\dots,n </m> are known 
        /// (for instance, if <m>q_i</m> is the market spread of a swap on Libor, then
        /// <m>\\</m>
        ///  <m>f_i = \frac{FloatingLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}{FixedLeg(y_0(q_0,q_1, \dots, q_n), y_1(q_0,q_1, \dots, q_n), \dots, y_n(q_0,q_1, \dots, q_n))}</m> 
        /// <m>\\</m>
        /// <m>y_i(q_0,q_1, \dots, q_n)</m> are forward libor rates, that are function of the quotes since they have been calibrated to them. Taking derivatives, we obtain the following <m>n</m> systems, one for every <m>j</m>, 
        /// of n equations for <m>\partial_{q_j} y_i, i = 1, \dots,n :</m>
        /// <m>\\</m>
        /// <m>\partial_{q_j} q_i = \sum_k \partial_{y_k} f_i \partial_{q_j}y_k, i = 1,\dots,n.   </m>
        /// <m>\\</m>
        /// By differentiating again , we obtain the following <m>n(n+1)/2</m> systems of <m>n</m> equations for <m> \partial_{q_jq_k}y_i, i = 1, \dots, n</m>
        /// <m>
        /// \\
        /// </m>
        /// <m>\partial_{q_m q_n} q_i = \sum_{k,j} \partial_{y_k y_j} f_i (\partial_{q_m}y_k \partial_{q_n}y_j) + \sum_k \partial_{y_k}f_i \partial_{q_m q_n}y_k , i = 1,\dots,n. </m> 
        /// </summary>
        /// <param name="gradients">the vectors gradients[i] are filled with <m>\partial_{y_k}f_i \, k = 0,\dots,n</m>  </param>
        /// <param name="hessians">the vectors hessians[i] are filled with <m>\partial_{y_k y_j} f_i</m> with hessians[i][k*(k+1)/2 + j] = \partial_{y_k y_j} f_i </param>
        public override void InitializeDerivativesWrtQuotes(double[][] gradients, double[][] hessians)
        {
            for(int i = 0; i < Tenors.Count; i++)
            {
                if (Tenors[i].Product is CDS)
                {
                    var pricer = Calibrator.GetPricer(this, Tenors[i].Product);
                    if (pricer is CDSCashflowPricer)
                    {
                        CDSCashflowPricer p = (CDSCashflowPricer) pricer;
                        QuotingConvention conv = Tenors[i].CurrentQuote.Type;
                        if(conv == QuotingConvention.Fee)
                        {
                            p.PvDerivatives(gradients[i], hessians[i]);
                        }
                        else
                        {
                            p.ParCouponDerivatives(gradients[i], hessians[i]);
                        }
                    }
                    else
                    {
                        throw new ToolkitException(
                            "Semi-analytic sensitivities are currently supported only on CashflowPricers"); 
                    }
                }
                else if (Tenors[i].Product is LCDS)
                {
                    var pricer = Calibrator.GetPricer(this, Tenors[i].Product);
                    QuotingConvention conv = Tenors[i].CurrentQuote.Type;
                    if (pricer is LCDSCashflowPricer)
                    {
                        LCDSCashflowPricer p = (LCDSCashflowPricer) pricer;
                        if (conv == QuotingConvention.Fee)
                        {
                            p.PvDerivatives(gradients[i], hessians[i]);
                        }
                        else
                        {
                            p.ParCouponDerivatives(gradients[i], hessians[i]);
                        }
                    }
                    else
                    {
                        throw new ToolkitException(
                            "Semi-analytic sensitivities are currently supported only on CashflowPricers"); 
                    }
                }
                else throw new ToolkitException("This product does not support semi-analytic sensitivities yet");
            }
        }
      
    /// <summary>
    ///   Set the curve to be defaulted since a specific date
    /// </summary>
    /// 
    /// <remarks>
    /// <para>If the parameter <c>deterministic</c> is true, 
    /// the survival probability is set to 1 before the default date.  It becomes 0 on and after the date.
    /// In other word, the curve defaults exactly on the given date.
    /// </para>
    /// 
    /// <para>If the parameter <c>deterministic</c> is false, the curve is assumed defaulted for sure
    /// on and after the date given.  Before the date, however, the curve may default at any time and
    /// the survival probability is the conditional probabilitiy calculated from the original curve,
    /// conditioning on the event that the curve does not survive beyond the given date.</para>
    /// </remarks>
    /// 
    /// <param name="date">The default date</param>
    /// <param name="deterministic">Make the curve deterministic</param>
    /// 
    /// <exclude />
    public void SetDefaulted(Dt date, bool deterministic)
    {
      const double tiny = 1.0E-14;
      deterministic_ = deterministic;

      // If defaulted on/before the as-of date
      if (date <= AsOf)
      {
        Clear();

        // Set DayCount and Interp such that it simply return the tiny value
        DayCount = DayCount.None;
        Interp = InterpFactory.FromMethod(InterpMethod.Flat, ExtrapMethod.Const);

        // need at least one date point
        Add(AsOf, tiny);

        defaulted_ = Defaulted.HasDefaulted;
        JumpDate = date;
        return;
      }

      // If default exactly on the date
      if (deterministic)
      {
        // Clear the curve
        Clear();

        // Set DayCount and Interp such that it simply return the above values
        DayCount = DayCount.None;
        Interp = InterpFactory.FromMethod(InterpMethod.Flat, ExtrapMethod.Const);

        // The survival probability before the date is 1
        Add(AsOf, 1);
        if (Dt.Diff(AsOf, date) > 1)
        {
          // For safety in case Interp changed, which is possible because Curve.Set() does not copy Interp method
          Add(Dt.Add(date, -1), 1);
        }
        Add(date, tiny);

        // Set the flag and date
        defaulted_ = Defaulted.WillDefault;
        JumpDate = date;

        return;
      }

      // In the following codes we have to keep the conditional survival probabilities.
      //-
      // First we call Curve.clone() to duplicate the curve only (not the calibrator)
      Curve curve = clone();

      // Clear the curve
      Clear();

      // Now we should honor the original spread,
      // which has been cleared by the Clear() function
      Spread = curve.Spread;

      // Probability of the event that the curve defaults before the date
      double probDefault = 1 - curve.Interpolate(date);
      if (probDefault <= tiny || probDefault > 1)
        // In this pathetic case, we do not do any conditioning
        probDefault = 1;

      // Check all dates and set the correct conditional suvival probabilities
      int count = curve.Count;
      for (int i = 0; i < count; ++i)
      {
        Dt dt = curve.GetDt(i);
        if (dt < date)
        {
          double sp = curve.GetVal(i);
          sp = 1 - (1 - sp) / probDefault;
          Add(dt, sp);
        }
        else
        {
          Add(date, tiny);
          defaulted_ = Defaulted.WillDefault;
          JumpDate = date;
          return;
        }
      }

      // If we are here, the default date is after the last curve date
      Add(date, tiny);
      defaulted_ = Defaulted.WillDefault;
      JumpDate = date;
      return;
    }

    /// <summary>
    ///   Copy all data from another curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function makes a shallow copy of both Tenors and Cailbrator,
    ///   in addition to Curve.Set(), when the other curve is a Calibrated curve.
    ///   It also makes sure to copy the properties Defaulted and Deterministic
    ///   when the source curve is a survival curve.
    ///   </para>
    /// </remarks>
    ///
    /// <param name="curve">Curve to copy</param>
    ///
    /// <exclude />
    public override void Copy(Curve curve)
    {
      base.Copy(curve);
      if (curve is SurvivalCurve)
      {
        this.defaulted_ = ((SurvivalCurve)curve).defaulted_;
        this.deterministic_ = ((SurvivalCurve)curve).deterministic_;
      }
      return;
    }

    /// <summary>
    ///   Create a CDS product compatible with the calibration settings
    /// </summary>
    /// <param name="name">Name of the product</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="firstPremium">The first premium date (empty to use the default setting)</param>
    /// <param name="premium">Premium</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="frequency">Frequency</param>
    /// <param name="roll">Roll</param>
    /// <param name="calendar">Calendar</param>
    /// <returns>CDS</returns>
    internal protected CDS CreateCDS(string name, Dt maturity,
      Dt firstPremium, double premium, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar)
    {
      Dt effective = Dt.Empty;
      if (!FixCdsDates(maturity, ref effective, ref firstPremium))
        effective = FindEffectiveDate();
      return CreateCDS(name, effective, maturity, firstPremium,
        premium, dayCount, frequency, roll, calendar);
    }

    #region Helpers
    /// <summary>
    /// Creates a CDS product.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="effective">The effective.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="firstPremium">The first premium.</param>
    /// <param name="premium">The premium.</param>
    /// <param name="dayCount">The day count.</param>
    /// <param name="frequency">The frequency.</param>
    /// <param name="roll">The roll.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>CDS product</returns>
    private CDS CreateCDS(string name, Dt effective, Dt maturity,
      Dt firstPremium, double premium, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDS cds = new CDS(effective, maturity, Ccy, firstPremium,
        premium, dayCount, frequency, roll, calendar);
      cds.AccruedOnDefault = true;
      cds.Description = name;
      return cds;
    }

    /// <summary>
    /// Fixes the CDS effective and first premium dates based the existing tenors.
    /// </summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="effective">The effective to fix.</param>
    /// <param name="firstPrem">The first premium date to fix.</param>
    /// <returns>True if the dates are fixed; otherwise, false.</returns>
    /// <remarks>
    /// If the curve contains at least one tenor CDS product,
    /// we use the effective date of the first CDS tenor with
    /// the maturity closest to, preferably later than, the
    /// required maturity date.  This will ensure that
    /// CurveUtil.ImpliedSpread() and related functions perform
    /// all the calculations based on the correct dates.
    /// </remarks>
    private bool FixCdsDates(Dt maturity, ref Dt effective, ref Dt firstPrem)
    {
      if (Tenors == null || Tenors.Count == 0)
        return false;

      CDS closestCDS = null;
      foreach (CurveTenor tenor in Tenors)
      {
        if (tenor != null)
        {
          CDS cds = tenor.Product as CDS;
          if (cds == null) continue;
          closestCDS = cds;
          if (cds.Maturity >= maturity)
            break;
        }
      }
      if (closestCDS == null) return false;

      // Fix the effective and first premium dates
      effective = closestCDS.Effective;
      if (firstPrem.IsEmpty())
      {
        Dt date = closestCDS.FirstPrem;
        if (date < maturity)
          firstPrem = date;
      }
      return true;
    }

    /// <summary>
    ///   Find an effective date consistent with calibrator
    /// </summary>
    /// <returns>effective date</returns>
    private Dt FindEffectiveDate()
    {
      // Otherwise, use the calibrator settle or as-of date,
      // depending on UseNaturalSettlement flag.
      if (SurvivalCalibrator.UseNaturalSettlement)
      {
        if (Calibrator != null && Calibrator.Settle.IsValid())
          return Calibrator.Settle;
        else
          throw new ArgumentException("Calibrator and Calibrator.Settle must set before adding products");
      }
      // Old behavior
      // Note:  This is WRONG when Calibrator.Settle <> this.AsOf.
      //        An example is in the method CDXPricer.GetEquivalentCDSPricer(),
      //        which leads to incorrect market values.
      return AsOf;
    }

    private static void GetDefaultDates(Dt[] eventDates, Dt settle, out Dt defaultDate, out Dt dfltSettle)
    {
      if (eventDates == null || eventDates.Length == 0)
      {
        defaultDate = new Dt(); dfltSettle = new Dt();
        return;
      }
      else if (eventDates.Length == 1)
      {
        defaultDate = eventDates[0]; dfltSettle = new Dt();
        return;
      }
      else if (eventDates.Length == 2)
      {
        defaultDate = eventDates[0]; dfltSettle = eventDates[1];
        if (dfltSettle.IsEmpty())
        {
          // If the name is defaulted on or before pricer settle
          //  and the default settle is empty, we assume that the
          //  default is settled on the pricer settle plus one day.
          if (!defaultDate.IsEmpty() && defaultDate <= settle)
            dfltSettle = Dt.Add(settle, 1);
          return;
        }
        else if (defaultDate <= dfltSettle && !defaultDate.IsEmpty())
        {
          // Nonempty default settle date and the name if defaulted
          //  on or before that date.
          return;
        }
        throw new ArgumentException("Default date must set to a date no later than the recovery settle date");
      }
      throw new ArgumentException("eventDates must not have more than two dates");
    }
    #endregion Helpers

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Calibrator for this curve
    /// </summary>
    [Category("Base")]
    public SurvivalCalibrator SurvivalCalibrator
    {
      get { return (SurvivalCalibrator)Calibrator; }
    }

		/// <summary>
		///   Default state of this survival curve
		/// </summary>
		[Category("Base")]
		public Defaulted Defaulted
		{
			get { return defaulted_; }
			set
			{
        if (defaulted_ == Defaulted.NotDefaulted)
        {
          defaulted_ = value;
          if (defaulted_ == Defaulted.WillDefault)
            SetDefaulted(Dt.Add(AsOf, 1), true);
          else if (defaulted_ == Defaulted.HasDefaulted)
            SetDefaulted(Dt.Add(AsOf, -1), true);
        }
        else
        {
          defaulted_ = value;
        }
      }
		}

		/// <summary>
		///   Date curve has defaulted (for case where Defaulted=HasDefaulted).
		/// </summary>
		[Category("Base")]
		public Dt DefaultDate
		{
			get { return JumpDate; }
			set { SetDefaulted(value, true); }
		}

		/// <summary>
		///   If set, this curve represent a deterministic event
		/// </summary>
    /// <exclude />
    [Category("Base")]
    public bool Deterministic
    {
      get { return deterministic_; }
      set { deterministic_ = value; }
    }
		#endregion

		#region Data

    private Defaulted defaulted_;
    private bool deterministic_;
    

    #endregion
   
  } // class SurvivalCurve

  internal static class SurvivalUtility
  {
    internal static void GetDefaultDates(this SurvivalCurve sc,
      RecoveryCurve rc,
      out Dt defaultDate, out Dt defaultSettle, out double recoveryRate)
    {
      defaultDate = defaultSettle = Dt.Empty;
      recoveryRate = 0.0;
      if (sc == null) return;

      defaultDate = sc.DefaultDate;
      if (defaultDate.IsEmpty()) return;

      if (rc == null && sc.SurvivalCalibrator != null)
      {
        rc = sc.SurvivalCalibrator.RecoveryCurve;
      }
      if (rc == null) return;
      defaultSettle = rc.DefaultSettlementDate;
      recoveryRate = rc.Interpolate(defaultSettle.IsEmpty()
        ? defaultDate : defaultSettle);
    }
  } // class SurvivalUtilit
}