/*
 * BaseCorrelationCalibrator.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Base class of all the calibrators for base correlation term structures
  ///   <preliminary/>
  /// </summary>
  /// <exclude />
  [Serializable]
  public abstract class BaseCorrelationCalibrator : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    ///   Default constructor (needed for XML serilization)
    /// </summary>
    internal protected BaseCorrelationCalibrator() { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="cdoTerms">Tranche term</param>
    /// <param name="basket">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dates">Array of tenor dates</param>
    /// <param name="detachments">Array of detachments</param>
    /// <param name="runningPrems">Tranche running premiums assciated with the quotes</param>
    /// <param name="quotes">Tranche quotes by tenors and detachments</param>
    protected BaseCorrelationCalibrator(
      SyntheticCDO cdoTerms,
      BasketPricer basket,
      DiscountCurve discountCurve,
      Dt[] dates,
      double[] detachments,
      double[,] runningPrems,
      double[,] quotes
      )
    {
      cdoTerms_ = cdoTerms;
      basket_ = basket;
      discountCurve_ = discountCurve;
      dps_ = detachments;
      dates_ = dates;
      runningPrems_ = GetPremiums(runningPrems, detachments.Length, dates.Length);
      trancheQuotes_ = ToMarketQuotes(detachments.Length, dates.Length, quotes);
      return;
    }

    private static double[][] GetPremiums(double[,] runningPrems, int dps, int tenors)
    {
      double[][] results = new double[tenors][];
      if (runningPrems.GetLength(0) < dps || runningPrems.GetLength(1) < tenors)
      {
        // Need to set up a new array with the right size
        runningPrems = BaseCorrelationFactory.SetUpRunningPremiums(
          runningPrems, dps, tenors);
      }

      // we want the raw number, not in basis point
      for (int t = 0; t < tenors; ++t)
      {
        results[t] = new double[dps];
        for (int i = 0; i < dps; ++i)
          results[t][i] = runningPrems[i, t] / 10000;
      }
      return results;
    }

    private static MarketQuote[][] ToMarketQuotes(int nTranches, int nTenors, double[,] quotes)
    {
      if (quotes.GetLength(0) < nTranches)
        throw new ArgumentException("Number of quote rows should be no less than the number of tranches");
      if (quotes.GetLength(1) < nTenors)
        throw new ArgumentException("Number of quote columns should be no less than the number of tenors");
      MarketQuote[][] trancheQuotes = new MarketQuote[nTenors][];
      for (int i = 0; i < nTranches;++i)
        for (int j = 0; j < nTenors; ++j)
          if (!Double.IsNaN(quotes[i, j]))
          {
            if (trancheQuotes[j] == null)
              trancheQuotes[j] = new MarketQuote[nTranches];
            double q = quotes[i, j];
            if (Math.Abs(q) < 1.0)
            {
              trancheQuotes[j][i].Value = q;
              trancheQuotes[j][i].Type = QuotingConvention.Fee;
            }
            else
            {
              trancheQuotes[j][i].Value = q / 10000.0;
              trancheQuotes[j][i].Type = QuotingConvention.CreditSpread;
            }
          }
      return trancheQuotes;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationCalibrator obj = (BaseCorrelationCalibrator)base.Clone();
      obj.dates_ = CloneUtil.Clone(dates_);
      obj.dps_ = CloneUtil.Clone(dps_);
      obj.trancheQuotes_ = CloneUtil.Clone(trancheQuotes_);
      return obj;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Create a base correlation term structure from market quotes
    /// </summary>
    /// <param name="calibrationMethod">Calinration method</param>
    /// <param name="method">Base correlation method</param>
    /// <param name="strikeMethod">Strike method</param>
    /// <param name="strikeEvaluator">User supplied strike evaluator</param>
    /// <param name="strikeInterp">Strike interpolation method</param>
    /// <param name="strikeExtrap">Strike extrapolation method</param>
    /// <param name="tenorInterp">Tenor interpolation method</param>
    /// <param name="tenorExtrap">Tenor extrapolation method</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <returns>Base correlation term structure</returns>
    public BaseCorrelationTermStruct Fit(
      BaseCorrelationCalibrationMethod calibrationMethod,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseEntity.Toolkit.Base.BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
    {
      int nDps = dps_.Length;
      int nDates = dates_.Length;
      // Allow missing quotes for a tenor
      List<BaseEntity.Toolkit.Base.BaseCorrelation> bcs = new List<BaseEntity.Toolkit.Base.BaseCorrelation>();
      List<Dt> dts = new List<Dt>();
      for (int t = 0; t < nDates; ++t)
        if (trancheQuotes_[t] != null)
        {
          BaseEntity.Toolkit.Base.BaseCorrelation bc = new BaseEntity.Toolkit.Base.BaseCorrelation(
            method, strikeMethod, strikeEvaluator,
            new double[nDps], new double[nDps]);
          bc.Interp = InterpFactory.FromMethod(
            strikeInterp, strikeExtrap, min, max);
          bc.Detachments = dps_;
          bc.StrikeEvaluator = strikeEvaluator;
          bcs.Add(bc);
          dts.Add(dates_[t]);
        }
      BaseCorrelationTermStruct bct =
        new BaseCorrelationTermStruct(dts.ToArray(), bcs.ToArray());
      bct.Interp = InterpFactory.FromMethod(
        tenorInterp, tenorExtrap, min, max);
      bct.CalibrationMethod = calibrationMethod;
      bct.MinCorrelation = min;
      bct.MaxCorrelation = max;
      bct.Extended = (max > 1.0);

      Timer timer = new Timer();
      timer.start();
      FitFrom(bct, 0, 0, min, max);
      timer.stop();
      bct.CalibrationTime = timer.getElapsed();

      return bct;
    }

    /// <summary>
    ///   Fit base correlations from a specific tenor and detachment
    /// </summary>
    /// <param name="baseCorrelation">Base correlation term structure to fit</param>
    /// <param name="dateIndex">Index of the start tenor</param>
    /// <param name="dpIndex">Index of the start detachment</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    public abstract void FitFrom(
      BaseCorrelationTermStruct baseCorrelation,
      int dateIndex, int dpIndex, double min, double max);

    /// <summary>
    ///   Calculate the flat pv per unit notional of a tranche at given premium and fee
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Fee</param>
    /// <returns>Flat pv</returns>
    internal protected abstract double TranchePriceAt(int t, int d,
      double premium, double fee);

    /// <summary>
    ///   Calculate the flat pv per unit notional of a tranche at a given quote
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="quote">Quote</param>
    /// <returns>Flat pv</returns>
    internal protected double TranchePriceAt(int t, int d, MarketQuote quote)
    {
      QuotingConvention type = trancheQuotes_[t][d].Type;
      if (type != QuotingConvention.CreditSpread && type != QuotingConvention.Fee)
        return Double.NaN;
      if (type != QuotingConvention.CreditSpread)
        return TranchePriceAt(t, d, runningPrems_[t][d], quote.Value);
      else
        return TranchePriceAt(t, d, quote.Value, 0.0);
    }

    /// <summary>
    ///   Get tranche premium and fee for a tranche
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="premium">To receive premium</param>
    /// <param name="fee">To receive fee</param>
    /// <returns>True if valid premium and fee exit; false otherwise.</returns>
    internal protected bool GetTranchePremiumAndFee(int t, int d,
      ref double premium, ref double fee)
    {
      QuotingConvention type = trancheQuotes_[t][d].Type;
      if (type != QuotingConvention.CreditSpread && type != QuotingConvention.Fee)
        return false;
      if (type == QuotingConvention.Fee)
      {
        premium = runningPrems_[t][d];
        fee = trancheQuotes_[t][d].Value;
        return true;
      }
      else
      {
        premium = trancheQuotes_[t][d].Value;
        fee = 0;
        return true;
      }
      //return false;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Market quotes of index tranches by tenors
    /// </summary>
    public MarketQuote[][] TrancheQuotes
    {
      get { return trancheQuotes_; }
      set { trancheQuotes_ = value; }
    }

    /// <summary>
    ///  Get the index scaling calibrator
    /// </summary>
    /// <returns>IndexScalingCalibrator</returns>
  
    public IndexScalingCalibrator GetIndexTerm()
    {
      return IndexTerm;
    }

    /// <summary>
    ///   Running premia of index tranches by detachments
    /// </summary>
    public double[][] RunningPremiums
    {
      get { return runningPrems_; }
      set { runningPrems_ = value; }
    }

    /// <summary>
    ///   Tenor dates
    /// </summary>
    public Dt[] TenorDates
    {
      get { return dates_; }
    }

    /// <summary>
    ///  Detachments
    /// </summary>
    public double[] Detachments
    {
      get { return dps_; }
    }

    /// <summary>
    ///   Basket data
    /// </summary>
    public BasketPricer Basket
    {
      get { return basket_; }
      set { basket_ = value; }
    }

    /// <summary>
    ///   Discount curve
    /// </summary>
    internal protected DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; }
    }

    /// <summary>
    ///  Index term
    /// </summary>
    public IndexScalingCalibrator IndexTerm
    {
      get { return index_; }
      internal protected set { index_ = value; }
    }

    /// <summary>
    ///   Tranche term
    /// </summary>
    public SyntheticCDO TrancheTerm
    {
      get { return cdoTerms_; }
    }
    #endregion Properties

    #region Data
    private SyntheticCDO cdoTerms_;
    private MarketQuote[][] trancheQuotes_;
    private double[][] runningPrems_;

    private Dt[] dates_;
    private double[] dps_;

    // data for internal use
    private BasketPricer basket_;
    private DiscountCurve discountCurve_;
    private IndexScalingCalibrator index_;
    #endregion Data
  } // class BaseCorrelationCalibrator
}
