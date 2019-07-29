/*
 * DiscountRateCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///  Interest rate curve calibration using an industry-standard bootstrap
  /// </summary>
  /// <remarks>
  ///  <para><b>Curve construction</b></para>
  ///  <para>The market standard method for constructing a LIBOR curve is as follows:</para>
  ///  <list type = "number">
  ///    <item><description>First LIBOR deposit rates are used to construct the short end of the curve.
  ///      The discount factor from each deposit rate is
  ///      <formula inline = "true">DF_i = 1/(1 + mm_i \times period_i)</formula>.
  ///      Where <formula inline = "true">mm_i</formula> is the <formula inline = "true">i^{\mbox{th}}</formula> LIBOR deposit rate and
  ///      <formula inline = "true">period_i</formula> is the compounding fraction of the year
  ///      for the ith LIBOR deposit rate</description></item>
  ///    <item><description>Convexity adjusted Eurodollar futures are used to construct the
  ///      middle of the curve. The discount factor for the starting date of the first
  ///      future contract is interpolated from the LIBOR deposit rates. The discount factor for each
  ///      futures expiration date is
  ///      <formula inline = "true">DF_i = DF_{i-1} / [1 + (100 - price_i) \times period_i]</formula>.
  ///      Where <formula inline = "true">price_i</formula> is the convexity adjusted Eurodollar
  ///      futures price.</description></item>
  ///    <item><description>The long end of the curve is constructed from par swap rates.
  ///      The discount factor for each swap rate is
  ///      <formula inline = "true">DF_i = (1 - swap_i \times \sum_{1}^{i} DF_j) / (1 + swap_i)</formula>
  ///      where <formula inline = "true">swap_i</formula> is the <formula inline = "true">i^{\mbox{th}}</formula> par swap rate.</description></item>
  ///  </list>
  ///
  ///  <para><b>Eurodollar Futures Convexity Adjustment</b></para>
  ///  <para>The futures prices need to be converted to forward prices for the purposes of
  ///  the curve construction. To do this an adjustment needs to be made based on some model
  ///  assumptions. There are several alternatives supported for this
  ///  <see cref = "BaseEntity.Toolkit.Base.FuturesCAMethod">'convexity' adjustment</see>.</para>
  ///
  ///  <para><b>End of Year Turn Effect</b></para>
  ///  <para>The end of year turn effect takes into account liquidity effects experienced
  ///  at the end of the financial year. The way the turn effect is implemented is to insert
  ///  a 'de-turned' rate before the end of the financial year and a 're-turned' rate at the
  ///  beginning of the financial year.</para>
  /// </remarks>
  /// <note>
  ///  <para>For a simpler wrapper for common curve construction needs
  ///  <see cref = "CalibratorUtil">Calibrator Utilities</see></para>
  /// </note>
  /// <example>
  ///  <para>The following example shows construction of an interest rate curve from
  ///  swap rates using the industry standard bootstrap method.</para>
  ///  <code language = "C#">
  ///    // Pricing is as of today and settlement is tomorrow.
  ///    Dt today = Dt.Today();
  ///    Dt settle = Dt.Add(today, 1);
  ///    // Set up the swap tenor points and quotes.
  ///    string [] swapTenors = new string [] { "1 Year", "5 Year", "7 Year", "10 year" };
  ///    double [] swapRates = new double [] { 0.04, 0.043, 0.045, 0.05 };
  ///
  ///    // Construct the discount curve
  ///    DiscountBootstrapCalibrator fit = new DiscountBootstrapCalibrator( today, settle );
  ///    DiscountCurve discountCurve = new DiscountCurve( fit );
  ///
  ///    // Add the swap tenor points
  ///    for( int i = 0; i &lt; swapTenors.Length; i++ )
  ///    {
  ///      discountCurve.AddSwap( swapTenors[i], Dt.Add(today, swapTenors[i]), swapRates[i],
  ///        DayCount.Actual360, Frequency.SemiAnnual, BDConvention.Modified,
  ///        Calendar.NYB );
  ///    }
  ///
  ///    // Fit the discount curve
  ///    discountCurve.Fit();
  ///  </code>
  /// </example>
  [Serializable]
  public partial class DiscountBootstrapCalibrator : DiscountCalibrator, IRateCurveCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (DiscountBootstrapCalibrator));

    #region Constructors

    /// <summary>
    ///   Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    ///   <para>Swap rate interpolation defaults to PCHIP/Const.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected DiscountBootstrapCalibrator(Dt asOf) : this(asOf, asOf)
    {}

    /// <summary>
    ///   Constructor given as-of and settlement dates
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    ///   <para>Swap rate interpolation defaults to PCHIP/Const.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    public DiscountBootstrapCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      SwapInterp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const);
      caMethod_ = FuturesCAMethod.None;
      swapCalibrationMethod_ = Settings.DiscountBootstrapCalibrator.SwapCalibrationMethod;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name = "curve">Discount curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    private void FitFromExtrap(CalibratedCurve curve, int fromIdx)
    {
      var discountCurve = new OverlayWrapper(curve);

      // Start from scratch each time as this is fast
      discountCurve.Clear();

      // Set up curve of swap rates to interpolate from
      var swapCurve = new Curve(AsOf);
      swapCurve.Interp = SwapInterp;
      Dt firstSwapDate = Dt.Empty;

      // Determine nr. of swap rates
      int noSwapTenors = 0;
      int idx = 0;
      var maxSwapTenor = new Tenor(35, TimeUnit.Years);
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (tenor.Product is SwapLeg)
        {
          idx++;
          Tenor t = Tenor.Parse(tenor.Name);
          if (t.CompareTo(maxSwapTenor) > 0) //current is greater than max
            maxSwapTenor = t;
        }
      }
      noSwapTenors = idx;

      // swap frequencies array
      var freq = new Frequency[noSwapTenors];
      var schedArr = new Schedule[noSwapTenors];
      for (int i = 0; i < noSwapTenors; ++i) freq[i] = Frequency.None;

      string swapEndTenor = maxSwapTenor.ToString();
      // extrapolate swap curve to 35 years, or the max that is given if > 35
      var swapDates = new Dt[noSwapTenors];
      DayCount swapDC = DayCount.None;
      Schedule sched = null;
      Dt matLastNonSwapProduct = Dt.Empty;

      CycleRule cycleRule;
      if (AsOf.Day == 31) cycleRule = CycleRule.EOM;
      else if (AsOf.Day == 30) cycleRule = CycleRule.Thirtieth;
      else if (AsOf.Day == 29) cycleRule = CycleRule.TwentyNinth;
      else cycleRule = CycleRule.None;

      const CashflowFlag schedFlags = CashflowFlag.RollLastPaymentDate | CashflowFlag.AccrueOnCycle;

      Dt swapEndTenorDt = Dt.Add(AsOf, swapEndTenor);

      idx = 0; // reset schedules array index
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (tenor.Product is SwapLeg)
        {
          var swap = (SwapLeg) tenor.Product;
          if (idx == noSwapTenors - 1) // if last swap tenor
            schedArr[idx] = new Schedule(AsOf, AsOf, Dt.Empty, swapEndTenorDt, swapEndTenorDt, swap.Freq,
                                         swap.BDConvention, swap.Calendar, cycleRule, schedFlags);
          else
            schedArr[idx] = new Schedule(AsOf, AsOf, Dt.Empty, swap.Maturity, swap.Maturity, swap.Freq,
                                         swap.BDConvention, swap.Calendar, cycleRule, schedFlags);
          swapDC = swap.DayCount;
          swapCurve.Add(swap.Maturity, swap.Coupon);
          if (!firstSwapDate.IsValid())
          {
            firstSwapDate = swap.Maturity;
            swapDates[0] = firstSwapDate;
          }
          if (freq[idx] == Frequency.None) freq[idx] = swap.Freq;

          if (idx == noSwapTenors - 1) // if last swap tenor
            swapDates[idx] = swapEndTenorDt;
          else swapDates[idx] = swap.Maturity;
          idx++;
          // else if( freq != swap.Freq )
          // throw new ToolkitException( String.Format("Swap rates have mismatched premium frequencies. Tenor {0} has a frequency of {1} which is not {2}", tenor.Maturity, swap.Freq, freq) );
        }
        // else if( !(tenor.Product is Note) )
        //	throw new ToolkitException( String.Format("Invalid product {0} for DiscountBootstrapCalibrator", typeof(tenor.Product)) );
      }

      logger.Debug(String.Format("First swap rate found has maturity {0}", firstSwapDate));

      // check for different frequencies
      bool hasDifferentFreq = false;
      for (int i = 1; i < freq.Length; ++i)
      {
        if (freq[i] != freq[i - 1])
        {
          hasDifferentFreq = true;
          break;
        }
      }

      if (hasDifferentFreq)
      {
        // Build concatenated schedule (with different frequencies)
        var periodDatesList = new List<Dt>();
        var paymentDatesList = new List<Dt>();

        // initialize schedule
        Dt firstCycleDate = schedArr[0].GetCycleStart(0);
        Dt firstAccrualDate = schedArr[0].GetPeriodStart(0);
        Dt lastCycleDate = schedArr[0].GetCycleEnd(schedArr[0].Count - 1);
        for (int j = 0; j < schedArr[0].Count; ++j)
        {
          periodDatesList.Add(schedArr[0].GetPeriodEnd(j));
          paymentDatesList.Add(schedArr[0].GetPaymentDate(j));
        }
        for (int k = 1; k < schedArr.Length; ++k)
        {
          if (schedArr[k].GetCycleStart(0) < firstCycleDate) firstCycleDate = schedArr[k].GetCycleStart(0);
          if (schedArr[k].GetPeriodStart(0) < firstAccrualDate) firstAccrualDate = schedArr[k].GetPeriodStart(0);
          if (schedArr[k].GetCycleEnd(0) > lastCycleDate)
            lastCycleDate = schedArr[k].GetCycleEnd(schedArr[k].Count - 1);
          for (int j = 0; j < schedArr[k].Count; ++j)
          {
            if (Dt.Cmp(schedArr[k].GetPeriodEnd(j), periodDatesList[periodDatesList.Count - 1]) > 0)
              periodDatesList.Add(schedArr[k].GetPeriodEnd(j));
            if (Dt.Cmp(schedArr[k].GetPaymentDate(j), paymentDatesList[paymentDatesList.Count - 1]) > 0)
              paymentDatesList.Add(schedArr[k].GetPaymentDate(j));
          }
        }
        sched = new Schedule(AsOf, firstCycleDate, firstAccrualDate, lastCycleDate, periodDatesList.ToArray(),
                             paymentDatesList.ToArray());
      }
      else if (noSwapTenors > 0) sched = schedArr[noSwapTenors - 1];

      // Fit initial money market rates up to first swap date
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (firstSwapDate.IsValid() && Dt.Cmp(tenor.Maturity, firstSwapDate) >= 0) break;
        if (tenor.Product is Note)
        {
          var note = (Note) tenor.Product;
          double df = RateCalc.PriceFromRate(note.Coupon, AsOf, note.Maturity, note.DayCount, note.Freq);
          logger.Debug(String.Format("Tenor {0}, MM rate={1}, df={2}", note.Maturity, note.Coupon, df));
          discountCurve.Add(tenor.Maturity, df);
          matLastNonSwapProduct = tenor.Maturity;
        }
      } // foreach

      // Overlay Eurodollar Futures up to first swap date
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (tenor.Product is StirFuture)
        {
          var fut = (StirFuture) tenor.Product;

          // Test if we are after first swap date
          if (firstSwapDate.IsValid() && Dt.Cmp(fut.DepositMaturity, firstSwapDate) >= 0) break;

          // Interpolate or exptrapolate discount factor to start of this ED Future
          if (discountCurve.Count < 1) throw new ToolkitException("Must specify some MM rates before first ED Future");
          double prevDf = discountCurve.DiscountFactor(fut.Maturity);

          // Set up curve for start of ED futures deposit period
          int nextIdx = discountCurve.After(fut.Maturity);
          // If futures date before last date in curve, need to clear remaining points
          if (nextIdx < discountCurve.Count) discountCurve.Shrink(nextIdx);

          // If futures date does not match exising point in curve then we need to add a point
          if ((discountCurve.Count <= 0) || (Dt.Cmp(fut.Maturity, discountCurve.GetDt(discountCurve.Count - 1)) != 0))
            discountCurve.Add(fut.Maturity, prevDf);

          // Add discount factor for maturity of this ED futures.
          double rate = 1.0 - tenor.MarketPv;
          // Implement convexity adjustment
          double caAdjustment = 0.0;
          if (caMethod_ == FuturesCAMethod.Manual)
          {
            IModelParameter caCurve;
            if (ModelParameters.TryGetValue(RateModelParameters.Process.Projection, RateModelParameters.Param.Custom, out caCurve))
              caAdjustment = ModelParameters.Interpolate(fut.Maturity, rate, RateModelParameters.Param.Custom,
                                                         RateModelParameters.Process.Projection);
            else
              caAdjustment = 0.0;
          }
          else if (caMethod_ == FuturesCAMethod.Hull)
          {
            if (VolatilityCurve == null)
              throw new ToolkitException("Attempting to do convexity adjustment for ED Futures without vols");
            double vol = VolatilityCurve.Volatility(fut.Maturity);
            double years = Dt.TimeInYears(AsOf, fut.Maturity);
            double term = Dt.TimeInYears(fut.Maturity, fut.DepositMaturity);
            caAdjustment = ConvexityAdjustments.EDFutures(rate, years, term, vol, caMethod_);
            logger.Debug(String.Format("Tenor {0}, CA vol {1}, years {2}, term {3}, adj {4}", fut.DepositMaturity, vol,
                                       years, term, caAdjustment));
          }
          double df = prevDf*
                      RateCalc.PriceFromRate(rate + caAdjustment, fut.Maturity, fut.DepositMaturity, fut.ReferenceIndex.DayCount,
                                           Frequency.None);
          logger.Debug(String.Format("Tenor {0}, EDFut rate={1}+{2}, df={3}", fut.DepositMaturity, rate, caAdjustment,
                                     df));
          discountCurve.Add(fut.DepositMaturity, df);
          matLastNonSwapProduct = fut.DepositMaturity;
        }
      }

      // Bootstrap swap rates
      // - Create the payments schedule.
      // - Generate out payment dates from settlement.
      if (firstSwapDate.IsValid())
      {
        // Should never be true 
        if (sched == null) throw new ToolkitException("sched should be defined with firstSwapDate");

        // Step through schedule 
        double sumDfDt = 0.0;
        double swapRateToAdd = 0;
        double df = 0;
        Frequency currentSwapFreq = freq[0];

        for (int i = 0; i < sched.Count; i++)
        {
          Dt start = sched.GetPeriodStart(i);
          Dt end = sched.GetPeriodEnd(i);

          double deltaT = (swapDC == DayCount.None ? (1.0/(double) currentSwapFreq) : sched.Fraction(i, swapDC));
          // Original, testing new conditional
          //if( (Dt.Cmp(end, matLastNonSwapProduct)) <= 0 && (curve.Count > 0) )
          if ((Dt.Cmp(end, firstSwapDate)) < 0 && (curve.Count > 0))
          {
            // Before first swap date so use discount factors from money market rates
            df = discountCurve.DiscountFactor(end);
            sumDfDt += df*deltaT;
          }
          else
          {
            // 'end' date is after maturity of last non-swap instrument.  
            // 'start' date should be BEFORE maturity of last non-swap instrument
            //  so we can calculate swap rate of swap with 'start' date maturity.
            // (we know all discount factors) and add it to swap curve.
            // I am assuming that sched.GetPeriodEnd(i) = sched.GetPeriodStart(i+1);
            if (Dt.Cmp(start, matLastNonSwapProduct) <= 0 && Dt.Cmp(end, swapCurve.GetDt(0)) < 0)
            {
              // Add swap rate to beginning of swap curve
              Dt[] DtArray;
              DtArray = new Dt[swapCurve.Count + 1];
              double[] DoubleArray;
              DoubleArray = new double[swapCurve.Count + 1];
              for (int j = 0; j < swapCurve.Count; j++)
              {
                DtArray[j + 1] = swapCurve.GetDt(j);
                DoubleArray[j + 1] = swapCurve.GetVal(j);
              }
              swapRateToAdd = (1 - discountCurve.DiscountFactor(start))/sumDfDt;
              DtArray[0] = start;
              DoubleArray[0] = swapRateToAdd;

              swapCurve.Clear();
              swapCurve.Add(DtArray, DoubleArray);
              //swapCurve.Add(start, swapRateToAdd);
            }

            // find swap index date
            idx = 0;
            while ((Dt.Cmp(swapDates[idx], end) < 0)) ++idx;

            double currentSumDfDt = sumDfDt;
            double currentDeltaT = deltaT;
            double currentDf = 0;
            // if swap frequency changed we start from scratch: 
            // we recalculate SumDfDt till we hit the firstSwap date and then
            // bootstrap the discount factors based on the 'new' swap frequency.
            // However we only add 'new' df's to the Discount Curve (i.e for dates > previos swap tenor)
            if (freq[idx] != currentSwapFreq && idx > 0)
            {
              // Reset values
              currentSwapFreq = freq[idx];
              currentSumDfDt = 0;
              var currentEnd = new Dt();
              for (int k = 0; k < schedArr[idx].Count; k++)
              {
                currentEnd = schedArr[idx].GetPeriodEnd(k);
                currentDeltaT = (swapDC == DayCount.None
                                   ? (1.0/(double) currentSwapFreq)
                                   : schedArr[idx].Fraction(k, swapDC));
                // if before first swap date
                if ((Dt.Cmp(currentEnd, firstSwapDate)) < 0 && (curve.Count > 0))
                {
                  // Before first swap date so use discount factors from money market rates
                  currentDf = discountCurve.DiscountFactor(currentEnd);
                  currentSumDfDt += currentDf*currentDeltaT;
                }
                else
                {
                  swapRateToAdd = swapCurve.Interpolate(currentEnd);
                  df = (1.0 - (swapRateToAdd*currentSumDfDt))/(1.0 + swapRateToAdd*currentDeltaT);

                  // make sure 'currentEnd' date is after the last date on the discount curve
                  bool bflag = true;
                  for (int j = 0; j < discountCurve.Count; j++)
                  {
                    // check if we have a point on the curve on the 'end' date; if yes, keep it
                    if (Dt.Cmp(discountCurve.GetDt(j), currentEnd) == 0 && discountCurve.GetVal(j) > 0) bflag = false;
                  }
                  if (bflag) discountCurve.Add(currentEnd, df);

                  currentSumDfDt += df*currentDeltaT;
                }
              }
              sumDfDt = currentSumDfDt;
              end = currentEnd;

              // find the index schedIdx of the superschedule that returns currentEnd and 
              // update/reset the current superschedule index i; 
              int schedIdx = 0;
              while (Dt.Cmp(sched.GetPeriodEnd(schedIdx), currentEnd) < 0) schedIdx++;
              i = schedIdx;
            }
            else //if freqs don't change
            {
              swapRateToAdd = swapCurve.Interpolate(end);
              df = (1.0 - (swapRateToAdd*sumDfDt))/(1.0 + swapRateToAdd*deltaT);
              sumDfDt += df*deltaT;
            }

            if (logger.IsDebugEnabled)
            {
              logger.DebugFormat("Tenor {0}, Swap rate={1}, sumDfDt={2}, df={3}", end, swapRateToAdd, sumDfDt, df);
              logger.Debug(discountCurve.ToString());
            }

            // make sure 'end' date is after the last date on the discount curve
            bool flag = true;
            for (int j = 0; j < discountCurve.Count; j++)
            {
              // check if we have a point on the curve on the 'end' date; if yes, keep it
              if (Dt.Cmp(discountCurve.GetDt(j), end) == 0 && discountCurve.GetVal(j) > 0) flag = false;
            }
            if (flag) discountCurve.Add(end, df);
            if (logger.IsDebugEnabled) logger.Debug(discountCurve.ToString());
            //sumDfDt += df * deltaT;
          }
        }
      }
      return;
    }

    // Fit()

    /// <summary>
    ///   Create a pricer equal to the one used for the discount curve calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">Interest rate product</param>
    /// <returns>Instantianted pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var dCurve = (DiscountCurve) curve;
      if (product is Note)
      {
        var note = (Note) product;
        var pricer = new NotePricer(note, AsOf, AsOf, 1.0, (DiscountCurve) curve);
        return pricer;
      }
      else if (product is StirFuture)
      {
        var future = (StirFuture) product;
        RateModelParameters fwdModelParameters = null;
        if (caMethod_ == FuturesCAMethod.Hull && VolatilityCurve != null)
          fwdModelParameters = new RateModelParameters(RateModelParameters.Model.Hull, new[] {RateModelParameters.Param.Sigma},
            new IModelParameter[] {VolatilityCurve}, future.ReferenceIndex.IndexTenor, future.Ccy);
        var pricer = new StirFuturePricer(future, AsOf, AsOf, 1.0 / future.ContractSize, (DiscountCurve)curve, null) {RateModelParameters = fwdModelParameters};
        pricer.Validate();
        return pricer;
      }
      else if (product is SwapLeg)
      {
        //create a plain vanilla swap pricer. 
        var payer = (SwapLeg) product;

        //This is a mistake. To be consistent with the past, asOf is assumed to be equal to Settle
        var payerPricer = new SwapLegPricer(payer, AsOf, AsOf, -1.0, (DiscountCurve) curve, null, null, null,
                                            null, null);

        var index = InterestRateIndex ?? (ReferenceIndex)
                                  new InterestRateIndex("libor", new Tenor(payer.Freq), payer.Ccy,
                                                        payer.DayCount, payer.Calendar, payer.BDConvention, 2);
        var receiver = new SwapLeg(payer.Effective, payer.Maturity, payer.Ccy, 0, index.DayCount,
                                   index.IndexTenor.ToFrequency(), payer.BDConvention, payer.Calendar, false,
                                   index.IndexTenor, index.IndexName);
        var receiverPricer = new SwapLegPricer(receiver, AsOf, AsOf, 1.0, (DiscountCurve) curve, index, curve,
                                               new RateResets(0, 0), null, null);
        var vanillaSwapPricer = new SwapPricer(receiverPricer, payerPricer);
        vanillaSwapPricer.Validate();
        return vanillaSwapPricer;
      }
      else if (product is FRA)
      {
        var fra = (FRA) product;
        var fraPricer = new FRAPricer(fra, AsOf, fra.Effective, (DiscountCurve) curve, (DiscountCurve) curve, 1);
        fraPricer.Validate();
        return fraPricer;
      }
      else if (product is Bond)
      {
        var bond = (Bond) product;
        var bondPricer = new BondPricer(bond, AsOf, AsOf, (DiscountCurve) curve, null, 0, TimeUnit.None, 0.0);
        bondPricer.Validate();
        return bondPricer;
      }
      else
      {
        throw new ArgumentException("Product not supported", product.ToString());
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Swap Interpolation method
    /// </summary>
    public Interp SwapInterp
    {
      get { return swapInterp_; }
      set { swapInterp_ = value; }
    }

    /// <summary>
    ///   Convexity adjustment method for Eurodollar Futures
    /// </summary>
    public FuturesCAMethod FuturesCAMethod
    {
      get { return caMethod_; }
      set { caMethod_ = value; }
    }

    /// <summary>
    ///   Rate index, used to define floating leg terms for IR swap hedges
    /// </summary>
    public ReferenceIndex InterestRateIndex { get; set; }

    #endregion Properties

    #region Data

    private FuturesCAMethod caMethod_;
    private Interp swapInterp_;

    #endregion Data

    #region IRateCurveCalibrator members

    DiscountCurve IRateCurveCalibrator.DiscountCurve
    {
      get { return null; }
    }

    ReferenceIndex IRateCurveCalibrator.ReferenceIndex
    {
      get { return InterestRateIndex; }
    }

    #endregion
  }
}