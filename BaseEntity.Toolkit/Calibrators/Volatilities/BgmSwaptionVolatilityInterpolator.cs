/*
 * BgmSwaptionVolatilityInterpolator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using Correlation=BaseEntity.Toolkit.Models.BGM.BgmCorrelation;
using CashflowAdapter = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Interface IForwardVolatilityInfo
  /// </summary>
  public interface IForwardVolatilityInfo
  {
    /// <summary>
    /// Gets the forward volatility curves.
    /// </summary>
    /// <value>The forward volatility curves.</value>
    VolatilityCurve[] ForwardVolatilityCurves { get; }
    /// <summary>
    /// Gets the type of the distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    DistributionType DistributionType { get; }
    /// <summary>
    /// Gets the correlation.
    /// </summary>
    /// <value>The correlation.</value>
    BgmCorrelation Correlation { get; }
    /// <summary>
    /// Gets the reset dates.
    /// </summary>
    /// <value>The reset dates.</value>
    Dt[] ResetDates { get; }
  }

  /// <summary>
  ///  Interpolate the swaption volatility.
  /// </summary>
  internal class BgmSwaptionVolatilityInterpolator : IForwardVolatilityInterpolator
  {
    private readonly Swaption swaption_; // not used, yet.
    private readonly DiscountCurve rateCurve_;
    private readonly IForwardVolatilityInfo volatilities_;
    private readonly VolatilityCurve[] fwdVolCurves_;

    /// <summary>
    /// Initializes a new instance of the <see cref="BgmSwaptionVolatilityInterpolator"/> class.
    /// </summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="rateCurve">The rate curve.</param>
    /// <param name="volatilities">The calibrated volatilities.</param>
    /// <param name="fwdVolCurves">The forward volatility curves.</param>
    public BgmSwaptionVolatilityInterpolator(
      Swaption swaption,
      DiscountCurve rateCurve,
      IForwardVolatilityInfo volatilities,
      VolatilityCurve[] fwdVolCurves)
    {
      swaption_=swaption;
      rateCurve_ = rateCurve;
      volatilities_ = volatilities;
      fwdVolCurves_ = fwdVolCurves;
    }

    #region IForwardVolatilityInterpolator Members

    /// <summary>
    /// Interpolates the volatility from a forward start date
    /// to the expiry date for an option with the specified strike.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    /// The volatility at the given date and strike.
    /// </returns>
    public double Interpolate(Dt start, double strike)
    {
      Dt expiry = swaption_.Expiration,
        maturity = swaption_.UnderlyingFixedLeg.Maturity;
      var swap = swaption_.UnderlyingFixedLeg;
      var zeroDiscountCurve = new DiscountCurve(start, 0.0);
      var ps = (!swap.Amortizes)? null : new SwapLegPricer(swap, 
        swap.Effective, swap.Effective, 1.0, zeroDiscountCurve, null, null, 
        null, null, null).GetPaymentSchedule(null, start);

      return Interpolate(start, expiry, maturity, new CashflowAdapter(ps));
    }
 
    internal double Interpolate(Dt start, Dt expiry, Dt maturity, CashflowAdapter cf)
    {
      var resets = volatilities_.ResetDates;

      int loIdx = -1, hiIdx = -1;
      {
        int count = resets.Length - 1;

        // No volatility if the maturity is not after the expiry or
        // if the expiration is before the first reset.
        if (start >= expiry || expiry >= maturity) return 0.0;

        if (expiry <= resets[0])
        {
          loIdx = 0;
        }
        else
        {
          // Now find two indices bracket the 
          for (int i = -1; ++i < count;)
          {
            int cmp = Dt.Cmp(resets[i], expiry);
            // Go to the next date if resets[i] < expiry
            if (cmp < 0) continue;
            // Make sure resets[loIdx] <= expiry
            loIdx = cmp == 0 ? i : (i - 1);
            Debug.Assert(loIdx >= 0);
            break;
          }
        }
        if (loIdx >= 0) // loIdx is set.
        {
          for (int i = loIdx; ++i < count;)
          {
            if (resets[i] < maturity) continue;
            hiIdx = i;
            break;
          }
          if (hiIdx < 0) hiIdx = count;
        }
        else
        {
          // only the last rate is active.
          hiIdx = count;
          loIdx = hiIdx - 1;
        }
      } // end loIdx and hiIdx

      // Find the dimension
      int dim = hiIdx - loIdx;
      var volCurves = new Curve[dim];
      var balanceCurve = GetBalanceCurve(cf);
      var rates = new double[dim];
      var fractions = new double[dim];
      var corr = new double[dim, dim];
      {
        var fullCurves = fwdVolCurves_;
        var fullCorrelation = volatilities_.Correlation;

        Dt dt0 = expiry;
        double df0 = rateCurve_.DiscountFactor(dt0);
        for (int i = 0; i < dim; ++i)
        {
          int row = i + loIdx;
          volCurves[i] = fullCurves[row];
          Dt dt = resets[row + 1]; // the next date.
          if (dt > maturity) dt = maturity;
          double df = rateCurve_.DiscountFactor(dt);
          double frac = (dt - dt0) / 365.0;
          fractions[i] = frac * df * (balanceCurve == null
            ? 1.0 : Average(balanceCurve, dt0, dt));
          rates[i] = (df0 / df - 1) / frac;
          corr[i, i] = 1.0;
          for (int j = 0; j < i; ++j)
          {
            corr[i, j] = corr[j, i] = fullCorrelation[row, j + loIdx];
          }
          dt0 = dt;
          df0 = df;
        }
      }

      double vol = BgmCalibrations.SwaptionVolatility(
        volatilities_.DistributionType == DistributionType.Normal,
        start, expiry, rates, fractions, volCurves,
        Correlation.CreateBgmCorrelation(BgmCorrelationType.CorrelationMatrix,
          dim, corr));
      return vol;
    }

    private static Curve GetBalanceCurve(CashflowAdapter cfa)
    {
      if (cfa.IsNullOrEmpty())
        return null;
      int count = cfa.Count;
      if (count > 0)
      {
        var effective = cfa.Effective;
        //- we build a cumulative balance curve.  Let <m>b(t)</m> be the balance
        //- at time t, then the cumulated balance is <m>a(t) = \int_0^t b(s) ds</m>.
        var curve = new Curve(effective, DayCount.None, Frequency.None)
        {
          // For stepwise constant balance, Linear is exact for accumulation.
          Interp = new Linear(new Const(), new Const())
        };
        // By construction, the balance always starts with 1.0;
        Dt lastDate = effective;
        double lastBalance = 1.0, sumBalance = 0;
        curve.Add(lastDate, sumBalance);
        for (int i = 0; i < count; ++i)
        {
          Dt date = cfa.GetStartDt(i);
          if (date <= lastDate) continue;
          sumBalance += (date - lastDate)*lastBalance;
          curve.Add(lastDate = date, sumBalance);
          lastBalance = cfa.GetPrincipalAt(i);
        }
        if (lastBalance > 0) {
          Dt date = cfa.GetEndDt(count-1);
          if (date > lastDate)
          {
            sumBalance += (date - lastDate) * lastBalance;
            curve.Add(date, sumBalance);
          }
        }
        return curve;
      }
      return null;
    }

    private static double Average(Curve curve, Dt start, Dt end)
    {
      if (start == end) end = Dt.Add(start, 1);
      return (curve.Interpolate(end) - curve.Interpolate(start))/(end - start);
    }
    #endregion
  }
}
