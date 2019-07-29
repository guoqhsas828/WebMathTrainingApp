/*
 * CalibratorUtil.cs
 *
 *   2006-2011. All rights reserved.
 *
 */

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibration utility methods.
  /// </summary>
  /// <remarks>
  ///   <para>Collection of utility classes for curve and calibration construction.
  ///   This is separated from the Calibrator class for simplification.</para>
  /// </remarks>
  public static class CalibratorUtil
  {
    /// <summary>
    ///   Build a discount curve using a standard interest rate bootstrap from money market and swap rates.
    /// </summary>
    /// <remarks>
    ///   <para>Simple wrapper for constructing a
    ///   <see cref = "DiscountBootstrapCalibrator">Discount Bootstrap calibrator</see>
    ///   fitted from a series of swap rates.</para>
    ///
    ///   <para>Note that the returned discount curve has been generated (fitted).</para>
    /// </remarks>
    /// <param name = "asOf">Pricing as-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "mmDayCount">Daycount of money market rates</param>
    /// <param name = "mmTenors">Tenors for money market rates</param>
    /// <param name = "mmRates">Money market rates</param>
    /// <param name = "swapDayCount">Daycount of swap rates</param>
    /// <param name = "swapFrequency">Payment frequency of swap rates</param>
    /// <param name = "swapTenors">Tenors for swap rates</param>
    /// <param name = "swapRates">Swap rates</param>
    /// <example>
    ///   Construct an interest rate curve bootstrapped from swap rates
    ///   <code language = "C#">
    ///   Dt today = new Dt(15,1,2000);
    ///   string [] tenors = new string []    { "1 Yr", "5 Yr", "7 Yr", "10 Yr" }; // Tenors
    ///   double [] swapRates = new double [] {  0.04,   0.043,  0.045,  0.05   }; // Swap rates
    ///
    ///   DiscountCurve dc =
    ///     CalibratorUtil.BuildDiscountBoostrapCurve( today, today,
    ///     DayCount.None, null, null,
    ///     DayCount.Actual360, Frequency.SemiAnnual, tenors, swapRates );
    ///   </code>
    /// </example>
    public static DiscountCurve BuildBootstrapDiscountCurve(Dt asOf, Dt settle, DayCount mmDayCount, string[] mmTenors,
                                                            double[] mmRates, DayCount swapDayCount,
                                                            Frequency swapFrequency, string[] swapTenors,
                                                            double[] swapRates)
    {
      // Construct the discount curve
      var fit = new DiscountBootstrapCalibrator(asOf, settle);
      fit.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
      var discountCurve = new DiscountCurve(fit);
      discountCurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);

      // Add the MM tenor points
      if (mmTenors != null)
        for (int i = 0; i < mmTenors.Length; i++)
          discountCurve.AddMoneyMarket(mmTenors[i], Dt.Add(asOf, mmTenors[i]), mmRates[i], mmDayCount);

      // Add the swap tenor points
      for (int i = 0; i < swapTenors.Length; i++)
        discountCurve.AddSwap(swapTenors[i], Dt.Add(asOf, swapTenors[i]), swapRates[i], swapDayCount, swapFrequency,
                              BDConvention.None, Calendar.None);

      // Fit the discount curve
      discountCurve.Fit();

      // Retun constructed curve
      return discountCurve;
    }

    /// <summary>
    ///   Build a survival curve using a standard market fit from a term structure of CDS rates.
    /// </summary>
    /// <remarks>
    ///   <para>Simple wrapper for constructing a
    ///   <see cref = "SurvivalFitCalibrator">Survival fit calibrator</see>
    ///   fitted from a series of CDS rates.</para>
    ///
    ///   <para>Note that the returned survival curve has been generated (fitted).</para>
    /// </remarks>
    /// <param name = "asOf">Pricing as-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "dayCount">Daycount of CDS rates</param>
    /// <param name = "frequency">Payment frequency of CDS rates</param>
    /// <param name = "bdConvention">Busines day convention for CDS premium payments</param>
    /// <param name = "calendar">Calendar for CDS premium payments</param>
    /// <param name = "tenors">Tenors for rates</param>
    /// <param name = "rates">CDS rates</param>
    /// <param name = "recoveryRate">Recovery rate</param>
    /// <param name = "discountCurve">Discount curve</param>
    /// <example>
    ///   Construct a survival curve from CDS quotes.
    ///   <code language = "C#">
    ///   Dt today = new Dt(15,1,2000);
    ///   string [] tenors = new string []    { "1 Yr", "5 Yr", "7 Yr", "10 Yr" };  // Tenors
    ///   double [] cdsRates = new double []  {  0.003,  0.004,  0.005,  0.006   }; // CDS rates
    ///
    ///   // Build discount curve
    ///   DiscountCurve discountCurve = .../
    ///
    ///   // Build survival curve
    ///   SurvivalCurve sc = CalibratorUtil.BuildSurvivalFitCurve( today, today, DayCount.Actual360,
    ///     Frequency.SemiAnnual, BDConvention.Following, Calendar.USD, tenors, swapRates,
    ///     recoveryRate, discountCurve );
    ///   </code>
    /// </example>
    public static SurvivalCurve BuildCDSSurvivalCurve(Dt asOf, Dt settle, DayCount dayCount, Frequency frequency,
                                                      BDConvention bdConvention, Calendar calendar, string[] tenors,
                                                      double[] rates, double recoveryRate, DiscountCurve discountCurve)
    {
      // Create survival calibrator
      var calibrator = new SurvivalFitCalibrator(asOf, settle, recoveryRate, discountCurve);

      // Create survival curve
      var survivalCurve = new SurvivalCurve(calibrator);

      // Add CDS to calibration
      for (int i = 0; i < tenors.Length; i++)
        survivalCurve.AddCDS(tenors[i], Dt.CDSMaturity(settle, tenors[i]), rates[i], dayCount, frequency, bdConvention,
                             calendar);

      // Fit survival curve
      survivalCurve.Fit();

      // Return constructed survival curve
      return survivalCurve;
    }

    /// <summary>
    ///   Build a survival curve using a standard market fit from a term structure of Corporate CDS rates.
    /// </summary>
    /// <remarks>
    ///   <para>Simple wrapper for constructing a
    ///   <see cref = "SurvivalFitCalibrator">Survival fit calibrator</see>
    ///   fitted from a series of CDS rates.</para>
    ///
    ///   <para>Defaults are:</para>
    ///   <list type = "bullet">
    ///     <item><description>Settlement is T+1</description></item>
    ///     <item><description>Premium DayCount of Actual360</description></item>
    ///     <item><description>Premium payment frequency of Quarterly</description></item>
    ///     <item><description>Premium payment business day convention of Following</description></item>
    ///   </list>
    ///
    ///   <para>Equivalent to: <code language = "C#">BuildCDSSurvivalCurve(asOf, Dt.Add(asOf, 1), DayCount.Actual360,
    ///   Frequency.Quarterly, BDConvention.Following, calendar, tenors, rates, recoveryRate, discountCurve);</code></para>
    /// 
    ///   <para>Note that the returned survival curve has been generated (fitted).</para>
    /// </remarks>
    /// <param name = "asOf">Pricing as-of date</param>
    /// <param name = "calendar">Calendar for CDS premium payments</param>
    /// <param name = "tenors">Tenors for rates</param>
    /// <param name = "rates">CDS rates</param>
    /// <param name = "recoveryRate">Recovery rate</param>
    /// <param name = "discountCurve">Discount curve</param>
    /// <example>
    ///   Construct a survival curve from CDS quotes.
    ///   <code language = "C#">
    ///   Dt today = new Dt(15,1,2000);
    ///   string [] tenors = new string []    { "1 Yr", "5 Yr", "7 Yr", "10 Yr" };  // Tenors
    ///   double [] cdsRates = new double []  {  0.003,  0.004,  0.005,  0.006   }; // CDS rates
    ///
    ///   // Build discount curve
    ///   DiscountCurve discountCurve = .../
    ///
    ///   // Build survival curve
    ///   SurvivalCurve sc = CalibratorUtil.BuildCorpCDSSurvivalCurve( today,
    ///   Calendar.NYB, tenors, swapRates, recoveryRate, discountCurve );
    ///
    ///   </code>
    /// </example>
    public static SurvivalCurve BuildCorpCDSSurvivalCurve(Dt asOf, Calendar calendar, string[] tenors, double[] rates,
                                                          double recoveryRate, DiscountCurve discountCurve)
    {
      return BuildCDSSurvivalCurve(asOf, Dt.Add(asOf, 1), DayCount.Actual360, Frequency.Quarterly,
                                   BDConvention.Following, calendar, tenors, rates, recoveryRate, discountCurve);
    }
  }
}