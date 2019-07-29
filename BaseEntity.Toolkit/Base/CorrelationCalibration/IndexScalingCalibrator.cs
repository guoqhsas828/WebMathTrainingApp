//
// IndexScalingCalibrator.cs
//  -2008. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.ComponentModel;

using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

using BaseEntity.Toolkit.Pricers.Baskets;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Enum type of actions taken when tenors are inadequate
  /// </summary>
  public enum ActionOnInadequateTenors
  {
    /// <summary></summary>
    AddCurveTenors,
    /// <summary></summary>
    DropFirstIndexTenor,
    /// <summary></summary>
    Fail,
  }

  /// <summary>
  ///   Index scaling calibrator
  /// </summary>
  [Serializable]
  public class IndexScalingCalibrator : BaseEntityObject
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SpreadScaling));
    #region Constructors
    /// <summary>
    ///   Default constructor (for XML serialization)
    /// </summary>
    IndexScalingCalibrator()
    {
    }

    /// <summary>
    ///   Constructor an index scaling calculator
    /// </summary>
    /// 
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotes">Market quotes for each CDX</param>
    /// <param name="quotesArePrices">Use TRUE for price quotes (100 based) and FALSE for spreads</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="scaleOnHazardRates">Bump hazard rate (true) or spread (false)</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="wantscale">Array of booleans indicating whether a name need to scale (null means to scale all curves)</param>
    /// <param name="marketRecoveryRate">Recovery rate to use in Model method appraoch</param>
    public IndexScalingCalibrator(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotes,
      bool quotesArePrices,
      CDXScalingMethod[] scalingMethods,
      bool relativeScaling,
      bool scaleOnHazardRates,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool[] wantscale,
      double marketRecoveryRate)
      : this(asOf, settle, cdx, tenors, quotes, quotesArePrices, scalingMethods,
        relativeScaling, scaleOnHazardRates, discountCurve, survivalCurves, wantscale,
        marketRecoveryRate, false)
    {
    }

    /// <summary>
    ///   Constructor an index scaling calculator using flag iterative
    /// </summary>
    /// 
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotes">Market quotes for each CDX</param>
    /// <param name="quotesArePrices">Use TRUE for price quotes (100 based) and FALSE for spreads</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="scaleOnHazardRates">Bump hazard rate (true) or spread (false)</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="wantscale">Array of booleans indicating whether a name need to scale (null means to scale all curves)</param>
    /// <param name="marketRecoveryRate">Recovery rate to use in Model method appraoch</param>
    /// <param name="useIterative">Boolean using iterative scaling</param>
    public IndexScalingCalibrator(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotes,
      bool quotesArePrices,
      CDXScalingMethod[] scalingMethods,
      bool relativeScaling,
      bool scaleOnHazardRates,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool[] wantscale,
      double marketRecoveryRate,
      bool useIterative)
    {
      asOf_ = asOf;
      settle_ = settle;
      cdx_ = cdx;
      tenors_ = tenors;
      scaleOnHazardRates_ = scaleOnHazardRates;
      discountCurve_ = discountCurve;
      survivalCurves_ = survivalCurves;
      marketRecoveryRate_ = marketRecoveryRate;

      // What curves need to scale?
      wantScales_ = wantscale;
      if (wantScales_ == null || wantScales_.Length == 0)
        wantScales_ = ArrayUtil.NewArray<bool>(survivalCurves.Length, true);
      else if (wantScales_.Length != survivalCurves.Length)
      {
        throw new ArgumentException(String.Format(
          "Survival curves (n={0}) and want scales (n={1}) not match",
          survivalCurves.Length, wantScales_.Length));
      }

      scalingType_ = relativeScaling ? BumpMethod.Relative : BumpMethod.Absolute;
      quotes_ = Array.ConvertAll<double, MarketQuote>(quotes,
        delegate(double q)
        {
          return new MarketQuote(q, q <= 0 ? QuotingConvention.None
          : (quotesArePrices ? QuotingConvention.FlatPrice : QuotingConvention.CreditSpread));
        });
      foreach (CDX note in cdx_)
        if (note != null && (note.Description == null || note.Description.Trim() == String.Empty))
          note.Description = Utils.ToTenorName(note.Effective, note.Maturity, true);
      iterative_ = useIterative;

      useTenors_ = ArrayUtil.Generate<bool>(quotes.Length,
        delegate(int i) { return quotes[i] > 0 && cdx[i] != null && cdx[i].Maturity > settle; });
      scalingMethods_ = new CDXScalingMethod[] { CheckScalingMethods(scalingMethods, useTenors_) };

      return;
    }

    private static CDXScalingMethod CheckScalingMethods(
      CDXScalingMethod[] scalingMethods, bool[] useTenors)
    {
      CDXScalingMethod method = CDXScalingMethod.None;
      for (int i = 0; i < scalingMethods.Length; ++i)
        if (scalingMethods[i] == CDXScalingMethod.Model
          || scalingMethods[i] == CDXScalingMethod.Duration
          || scalingMethods[i] == CDXScalingMethod.Spread)
        {
          if (method == CDXScalingMethod.None)
            method = scalingMethods[i];
          else if (method != scalingMethods[i])
            throw new ArgumentException(String.Format("Cannot mix scaling methods {0} and {1}",
              method.ToString(), scalingMethods[i].ToString()));
        }
        else if (scalingMethods.Length == useTenors.Length)
        {
          // This equality signals the old interface, in which
          // we do not use this tenor since there is no "real"
          // scaling method specified and the medthod is simply
          // "Next", "Previous" or "None".
          //
          // In the new interface we have more rigid restrictions
          // than the old one: any tenor not used in scaling
          // is either "Next" (when it is before the last tenor
          // in use) or "Previous" (when it is after the last
          // tenor in use).  This makes fully backward compatibility
          // impossible.  We just do whatever is possible here.
          useTenors[i] = false;
        }
      return method != CDXScalingMethod.None ? method : CDXScalingMethod.Model;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      IndexScalingCalibrator obj = (IndexScalingCalibrator)base.Clone();
      obj.cdx_ = CloneUtil.Clone(cdx_);
      obj.tenors_ = CloneUtil.Clone(tenors_);
      obj.survivalCurves_ = CloneUtil.Clone(survivalCurves_);
      obj.wantScales_ = CloneUtil.Clone(wantScales_);
      obj.scaledSurvivalCurves_ = CloneUtil.Clone(scaledSurvivalCurves_);
      obj.scalingFactors_ = CloneUtil.Clone(scalingFactors_);
      obj.scalingFactorsWithZeros_ = CloneUtil.Clone(scalingFactorsWithZeros_);
      obj.scalingMethods_ = CloneUtil.Clone(scalingMethods_);
      obj.includes_ = CloneUtil.Clone(includes_);
      return obj;
    }

    #endregion Constrcutors

    #region Methods
    /// <summary>
    ///   Create a scaled curve
    /// </summary>
    /// <param name="curve">Original curve to scale</param>
    /// <param name="dates">Array of index tenor dates</param>
    /// <param name="factors">Array of scalings by index tenors</param>
    /// <param name="relative">Relative or absolute scaling</param>
    /// <param name="scaleHazardRates">Scale hazard rates instead of spread quotes</param>
    /// <param name="failOnInadequateTenors">If true, it fails when not enough curve tenors to match index quotes; otherwise, add required curve tenor when neccessay.</param>
    /// <returns>Scaled curve</returns>
    public static SurvivalCurve Scale(SurvivalCurve curve, Dt[] dates, double[] factors,
      bool relative, bool scaleHazardRates, bool failOnInadequateTenors)
    {
      SurvivalCurve curveClone = (SurvivalCurve)curve.Clone();
      if (curve.Defaulted == Defaulted.HasDefaulted)
        return curveClone;

      ScaledSurvivalCurveBuilder builder =
        new ScaledSurvivalCurveBuilder(curveClone, dates, failOnInadequateTenors);
      curveClone.Tenors = builder.Tenors;

      SurvivalCurveScaling scaling = scaleHazardRates
        ? ((SurvivalCurveScaling)new HazardRateScaling(curveClone, builder.CurvePoints, builder.LastStartIndex))
        : ((SurvivalCurveScaling)new SpreadScaling(curveClone, builder.CurvePoints, builder.LastStartIndex));
      Dt asOf = curveClone.AsOf;
      for (int i = 0; i < dates.Length; ++i)
        if (dates[i] > asOf && !Double.IsNaN(factors[i]))
        {
          scaling.SetBumpRange(dates[i]);
          scaling.Bump(factors[i], relative, false);
        }
      if (scaleHazardRates)
        ScaledSurvivalCurveBuilder.SynchronizeCurveQuotes(scaling.ScaledCurve);
      else
        scaling.ScaledCurve.Fit();
      return scaling.ScaledCurve;
    }

    /// <summary>
    ///   Line up the sacling factors with the CDS curve tenors
    /// </summary>
    /// <param name="curve">A representative CDS curve</param>
    /// <param name="dates">Array of CDX scaling maturities</param>
    /// <param name="factors">Array of scaling factors, same size of dates</param>
    /// <param name="failOnInadequateTenors">False if added tenors needed</param>
    /// <param name="curveFactors">Array of Dt curve tenors that need filled up</param>
    /// <param name="curveTenors">Array of scaling factors that need filled up</param>
    /// <returns></returns>
    public static bool MatchScalingFactors(
      SurvivalCurve curve, Dt[] dates, double[] factors,
      bool failOnInadequateTenors, out Dt[] curveTenors, out double[] curveFactors)
    {
      if (curve == null)
        throw new ArgumentException("Null unscaled survival curve passed in.");
      if (dates == null || dates.Length == 0 || factors == null || factors.Length == 0)
        throw new ArgumentException("dates and factors must have values.");
      if (dates.Length != factors.Length)
        throw new ArgumentException("dates and factors must have same dimension");

      curve = (SurvivalCurve)curve.Clone();
      int numCurveTenors = curve.Tenors.Count;

      ScaledSurvivalCurveBuilder builder =
        new ScaledSurvivalCurveBuilder(curve, dates, failOnInadequateTenors);
      int numCurveNewTenors = builder.Tenors.Count;

      bool addedTenors = numCurveTenors < numCurveNewTenors;

      curve.Tenors = builder.Tenors;
      SurvivalCurveScaling scaling = new SurvivalCurveScaling(
        curve, builder.CurvePoints, builder.LastStartIndex);

      Dt asOf = curve.AsOf;

      curveTenors = new Dt[numCurveNewTenors];
      curveFactors = new double[numCurveNewTenors];
      int k = 0;
      for (int i = 0; i < dates.Length; ++i)
        if (dates[i] > asOf)
        {
          foreach (int idx in scaling.GetBumpIndices(dates[i]))
          {
            curveTenors[idx] = curve.Tenors[idx].Maturity;
            curveFactors[idx] = factors[i];
            k++;
          }
        }
      // Assign remained curve tenors to curveTenors
      for (; k < numCurveNewTenors; ++k)
        curveTenors[k] = curve.Tenors[k].Maturity;
      return addedTenors;
    }

    /// <summary>
    ///   Line up the scaling factors with the CDS curve tenors
    /// </summary>
    /// <param name="curve">A representative CDS curve</param>
    /// <param name="dates">Array of CDX scaling maturities</param>
    /// <param name="factors">Array of scaling factors, same size of dates</param>
    /// <param name="failOnInadequateTenors">False if added tenors needed</param>
    /// <param name="curveFactors">Array of double-typed curve tenor dates that need filled up</param>
    /// <param name="curveTenors"> Array of scaling factors that need filled up</param>
    /// <returns></returns>
    public static bool MatchScalingFactors(
      SurvivalCurve curve, Dt[] dates, double[] factors, bool failOnInadequateTenors,
      out double[] curveTenors, out double[] curveFactors)
    {
      Dt[] curveTenorDates = null;
      bool addedTenors = MatchScalingFactors(
        curve, dates, factors, failOnInadequateTenors, out curveTenorDates, out curveFactors);
      curveTenors = Array.ConvertAll<Dt, double>
        (curveTenorDates, delegate(Dt dt) { return Dt.Cmp(dt, Dt.Empty) == 0 ? Double.NaN : Dt.ToExcelDate(dt); });
      return addedTenors;
    }

    /// <summary>
    ///   Scale a given set of survival curves using the calculated scaling factors
    /// </summary>
    /// <param name="curves">Curves to scale</param>
    /// <returns>Scaled curves</returns>
    public SurvivalCurve[] Scale(SurvivalCurve[] curves)
    {
      if (quotes_ == null || cdx_ == null)
        return curves;

      if (curves == null || curves.Length == 0)
        return null;

      if (scalingFactors_ == null)
      {
        CalcScalings();
      }
      return ScaleCurves(curves, null);
    }

    /// <summary>
    ///   Re-Scale a given set of survival curves 
    /// </summary>
    /// <param name="curves">Curves to scale</param>
    /// <returns>Scaled curves</returns>
    public SurvivalCurve[] ReScale(SurvivalCurve[] curves)
    {
      if (quotes_ == null || cdx_ == null)
        return curves;

      if (curves == null || curves.Length == 0)
        return null;

      CalcScalings();
      return ScaleCurves(curves, includes_);
    }

    private void ScaleCurves()
    {
      if (quotes_ == null || cdx_ == null
        || survivalCurves_==null||survivalCurves_.Length==0)
      {
        return;
      }

      if (scalingFactors_ == null)
      {
        CalcScalings();
      }
      
      if (scaledSurvivalCurves_ == null)
      {
        if (includes_ == null)
          includes_ = ArrayUtil.Generate<bool>(survivalCurves_.Length,
            delegate(int i)
            {
              return wantScales_[i] && survivalCurves_[i].Defaulted != Defaulted.HasDefaulted;
            });
        scaledSurvivalCurves_ = ScaleCurves(survivalCurves_, includes_);
      }
      return;
    }
    private SurvivalCurve[] ScaleCurves(SurvivalCurve[] curves, bool[] includes)
    {
      // Check the use tenors
      CheckUseTenors();

      // Do the scaling using scaling factors
      Dt[] dates = ArrayUtil.GenerateIf<Dt>(useTenors_.Length,
        delegate(int i) { return useTenors_[i]; },
        delegate(int i) { return cdx_[i].Maturity; });
      double[] factors = ArrayUtil.GenerateIf<double>(useTenors_.Length,
        delegate(int i) { return useTenors_[i]; },
        delegate(int i) { return scalingFactors_[i]; });

      bool failOnInadequateTenors = actionOnInadequateTenors_ != ActionOnInadequateTenors.AddCurveTenors;
      bool relative = scalingType_ == BumpMethod.Relative;
      if (includes == null)
      {
        return Array.ConvertAll<SurvivalCurve, SurvivalCurve>(
          curves, delegate(SurvivalCurve sc)
          {
            return Scale(sc, dates, factors, relative,
              scaleOnHazardRates_, failOnInadequateTenors);
          });
      }
      return ArrayUtil.Generate<SurvivalCurve>(
        curves.Length, delegate(int i)
        {
          if (i < includes.Length && includes[i])
          {
            return Scale(curves[i], dates, factors, relative,
                         scaleOnHazardRates_, failOnInadequateTenors);
          }
          else if (curves[i]!=null)
              return (SurvivalCurve) curves[i].Clone();
          return null;
    });
    }

    internal void Reset()
    {
      scalingFactors_ = null;
      scalingFactorsWithZeros_ = null;
      scaledSurvivalCurves_ = null;
      includes_ = null;
    }

    private void CalcScalings()
    {
      // Save force fit flags and set force-fit to true for all curves
      bool[] savedForceFitFlags = Array.ConvertAll<SurvivalCurve, bool>(survivalCurves_,
        delegate(SurvivalCurve sc)
        {
          var cal = sc.SurvivalCalibrator;
          if (cal != null)
          {
            bool flag = cal.ForceFit;
            cal.ForceFit = true;
            return flag;
          }
          return false;
        });

      try
      {
        DoScaling(savedForceFitFlags,
          actionOnInadequateTenors_ != ActionOnInadequateTenors.AddCurveTenors);
      }
      catch (Exception e)
      {
        // For safety, initialize the output variables
        // to indicate that scaling factors are not ready.
        Reset();

        throw;
      }
      finally
      {
        CheckAndRestoreForceFit(survivalCurves_, savedForceFitFlags, false);
      }
      NormalizeFactors(scalingFactors_);
      scalingFactorsWithZeros_ = CreateScalingFactorsWithZeros(scalingFactors_, useTenors_);
      return;
    }

    private void DoScaling(bool[] forceFits, bool failOnInadequateTenors)
    {
      // Initialize the includes array.
      includes_ = ArrayUtil.Generate<bool>(survivalCurves_.Length,
        delegate(int i)
        {
          return wantScales_[i] && survivalCurves_[i].Defaulted != Defaulted.HasDefaulted;
        });
      int[] badCurveIdx = null;

      // For iterative scaling, the times we try never exceeds
      // the number of curves.
      for (int k = 0; k < forceFits.Length; ++k)
      {
        CDXScaling.Scaling(asOf_, settle_, cdx_, quotes_, useTenors_, marketRecoveryRate_,
          scalingType_ == BumpMethod.Relative, discountCurve_, survivalCurves_,
          scaleOnHazardRates_, failOnInadequateTenors, scalingMethods_[0], includes_,
          out scalingFactors_, out scaledSurvivalCurves_);
        badCurveIdx = CheckAndRestoreForceFit(
          scaledSurvivalCurves_, forceFits, !scaleOnHazardRates_);

        // If no bad curve, we are done.
        if (badCurveIdx == null) return;

        // If not iterative, we don't try again.
        if (!iterative_) break;

        // Iterative is true and we need to try again.
        // First remove the bad curves from scaling.
        foreach (int i in badCurveIdx)
          includes_[i] = false;
      }

      if (badCurveIdx != null)
      {
        // Since some curves without force-fit flags need
        // force fit. We tell the user what are these curves.
        string msg = "Curve ";
        foreach (int i in badCurveIdx)
          msg += survivalCurves_[i].Name + " ";
        msg += "need force fit";
        throw new ToolkitException(msg);
      }

      return;
    }

    private static int[] CheckAndRestoreForceFit(
      SurvivalCurve[] curves, bool[] forceFits, bool doFit)
    {
      List<int> badCurves = new List<int>();
      for (int i = 0; i < forceFits.Length; ++i)
        if (!forceFits[i])
        {
          var cal = curves[i].SurvivalCalibrator;
          if (cal == null) continue;
          if (doFit) curves[i].Fit();
          cal.ForceFit = false;
          if (cal.FitWasForced)
            badCurves.Add(i);
        }
      return badCurves.Count == 0 ? null : badCurves.ToArray();
    }

    /// <summary>
    ///   Modify trailing NAs to the last real factor in use
    /// </summary>
    /// <param name="factors"></param>
    private static void NormalizeFactors(double[] factors)
    {
      if (factors == null) return;
      int idxLastFactor = factors.Length - 1;
      for (; idxLastFactor >= 0; --idxLastFactor)
      {
        if (!Double.IsNaN(factors[idxLastFactor]))
          break;
      }
      if (idxLastFactor >= 0)
      {
        double lastFactor = factors[idxLastFactor];
        for (int i = idxLastFactor + 1; i < factors.Length; ++i)
          factors[i] = lastFactor;
      }
      return;
    }

    private static double[] CreateScalingFactorsWithZeros(
      double[] scalingFactors, bool[] useTenors)
    {
      scalingFactors = CloneUtil.Clone(scalingFactors);
      // Just zeros the factors for the tenors not used
      for (int i = 0; i < useTenors.Length; ++i)
        if (!useTenors[i])
        {
          scalingFactors[i] = 0;
        }
      return scalingFactors;
    }

    /// <summary>
    ///  Check and update useTenors_ in case the first two index 
    ///  maturities are  earlier than the first curve tenor
    /// </summary>
    public void CheckUseTenors()
    {
      // Nothing to do if
      //  (a) we don't drop tenor; or
      //  (b) there is less than 2 index tenor; or
      //  (c) the first index tenor is already dropped.
      if (actionOnInadequateTenors_ != ActionOnInadequateTenors.DropFirstIndexTenor
        || cdx_.Length < 2 || !useTenors_[0])
      {
        return;
      }

      // Find the second Index tenor and check the need to drop
      Dt secondCdxTenorDate = cdx_[1].Maturity;
      for (int i = 0; i < survivalCurves_.Length; ++i)
        if (survivalCurves_[i].GetDt(0) >= secondCdxTenorDate)
        {
          useTenors_[0] = false;
          logger.DebugFormat("The first index tenor at {0} is drooped", secondCdxTenorDate);
          return;
        }

      return;
    }

    /// <summary>
    /// Get scaling factors
    /// </summary>
    /// <returns></returns>
    public double[] GetScalingFactors()
    {
      if (scalingFactors_ == null)
        CalcScalings();
      return scalingFactors_;
    }

    /// <summary>
    /// Set scaling factors
    /// </summary>
    /// <param name="factors">factors to set</param>
    public void SetScalingFactors(double[] factors)
    {
      if (UseTenors.Length != factors.Length)
        throw new ArgumentException(String.Format(
          "The number of scaling factors ({0}) must match the number of cdx ({1})",
          factors.Length, UseTenors.Length));
      scalingFactors_ = CloneUtil.Clone(factors);
      scalingFactorsWithZeros_ = CreateScalingFactorsWithZeros(scalingFactors_, UseTenors);
    }

    /// <summary>
    /// Get scaling factors with zeros
    /// </summary>
    /// <returns></returns>
    public double[] GetScalingFactorsWithZeros()
    {
      if (scalingFactorsWithZeros_ == null)
        CalcScalings();
      return scalingFactorsWithZeros_;
    }

    /// <summary>
    /// Get scaled survival curves
    /// </summary>
    /// <returns></returns>
    public SurvivalCurve[] GetScaleSurvivalCurves()
    {
      if (scaledSurvivalCurves_ == null)
      {
        if (scalingFactors_ == null)
        {
          // Need to calculate both the factors and the scaled curves.
          CalcScalings();
        }
        else
        {
          // We have the factors ready, calculate the scaled curves only.
          ScaleCurves();
        }
      }
      return scaledSurvivalCurves_;
    }

    /// <summary>
    /// Set scaled survival curves
    /// </summary>
    /// <param name="curves"></param>
    public void SetScaledSurvivalCurves(SurvivalCurve[] curves)
    {
      scaledSurvivalCurves_ = curves;
    }


    #endregion Methods

    #region Properties

    /// <summary>
    ///   Scaling tenor dates
    /// </summary>
    [Browsable(false)]
    public Dt[] ScalingDates
    {
      get
      {
        return cdx_ == null ? null : Array.ConvertAll<CDX, Dt>(
          cdx_, delegate(CDX cdx) { return cdx.Maturity; });
      }
    }

    /// <summary>
    ///   Booleans indicating what names are included in scaling
    /// </summary>
    [Browsable(false)]
    public bool[] ScalingIncludes
    {
      get
      {
        if (scalingFactors_ == null)
          CalcScalings();
        return includes_;
      }
    }

    /// <summary>
    ///   Boolean variable: TRUE to use iterative way to scale, meaning to skip bad curves 
    /// </summary>
    [Category("Base")]
    public bool Iterative
    {
      get { return iterative_; }
      set { iterative_ = value; }
    }

    /// <summary>
    ///   Market quotes of index
    /// </summary>
    [Category("Base")]
    public MarketQuote[] Quotes
    {
      get { return quotes_; }
      set { quotes_ = value; }
    }

    /// <summary>
    ///   Market values by index tenors
    /// </summary>
    [Browsable(false)]
    public double[] MarketValues
    {
      get
      {
        if (Indexes == null) return null;
        return ArrayUtil.ConvertAll<CDX, MarketQuote, double>(Indexes, Quotes,
          delegate(CDX cdx, MarketQuote quote)
          {
            // Note: here we use the unscaled credit curves because they do not
            //   participate in the calculation except for indicating how many
            //   names are defaluted.
            return cdx == null ? Double.NaN : CDXScaling.MarketValue(cdx, quote,
              asOf_, settle_, discountCurve_, survivalCurves_, marketRecoveryRate_);
          });
      }
    }

    /// <summary>
    ///   Indicate whethe the scaling factors have been already calculated
    /// </summary>
    [Category("Base")]
    public bool IsScalingFactorsReady
    {
      get { return (scalingFactors_ != null); }
    }

    /// <summary>
    ///   Name of the index
    /// </summary>
    [Category("Base")]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Pricing date
    /// </summary>
    [Category("Base")]
    public Dt AsOf
    {
      get { return asOf_; }
    }

    /// <summary>
    ///   Settle date
    /// </summary>
    [Category("Base")]
    public Dt Settle
    {
      get { return settle_; }
    }

    /// <summary>
    ///   Name of the index
    /// </summary>
    [Category("Base")]
    public string[] TenorNames
    {
      get { return tenors_; }
    }

    /// <summary>
    ///   Indexes by tenors
    /// </summary>
    [Category("Base")]
    public CDX[] Indexes
    {
      get { return cdx_; }
    }

    /// <summary>
    ///   Override the curve recovery
    /// </summary>
    [Category("Base")]
    public double MarketRecoveryRate
    {
      get { return marketRecoveryRate_; }
    }

    /// <summary>
    ///   Array of survival curves by names
    /// </summary>
    [Category("Base")]
    public SurvivalCurve[] SurvivalCurves
    {
      get { return survivalCurves_; }
    }

    /// <summary>
    ///   Discount curve
    /// </summary>
    [Category("Base")]
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
    }

    /// <summary>
    ///   Array of booleans indicating whether a curve need to scale
    /// </summary>
    [Category("Base")]
    public bool[] WantScales
    {
      get { return wantScales_; }
    }

    /// <summary>
    ///   Array of scaling methods by tenors
    /// </summary>
    [Category("Base")]
    private CDXScalingMethod[] ScalingMethods
    {
      get { return scalingMethods_; }
    }

    /// <summary>
    ///   Scaling type (relative or absolute)
    /// </summary>
    [Category("Base")]
    public BumpMethod ScalingType
    {
      get { return scalingType_; }
      set { scalingType_ = value; }
    }

    /// <summary>
    ///   Array of override factors by tenors
    /// </summary>
    [Category("Base")]
    public bool ScaleHazardRates
    {
      get { return scaleOnHazardRates_; }
      set { scaleOnHazardRates_ = value; }
    }

    /// <summary>
    ///   If false, no tenor will be added and it fails when
    ///   there are not enough tenors on any curve to match
    ///   the index tenors.  If it is true, it adds the required 
    ///   tenors when neccessary.
    /// </summary>
    [Category("Base")]
    public ActionOnInadequateTenors ActionOnInadequateTenors
    {
      get { return actionOnInadequateTenors_; }
      set { actionOnInadequateTenors_ = value; }
    }

    /// <summary>
    ///   CDX scaling methods: Model or Duration
    /// </summary>
    [Category("Base")]
    public CDXScalingMethod[] CdxScaleMethod
    {
      get { return scalingMethods_; }
      set { scalingMethods_ = value; }
    }

    /// <summary>
    ///   Array of boolean indicating using the corresponding tenor or not
    /// </summary>
    [Category("Base")]
    public bool[] UseTenors
    {
      get { return useTenors_; }
    }

    #endregion Properties

    #region Data

    private string name_;
    private Dt asOf_;
    private Dt settle_;
    private CDX[] cdx_ = null;
    private string[] tenors_ = null;
    private MarketQuote[] quotes_ = null;
    CDXScalingMethod[] scalingMethods_ = null;
    private BumpMethod scalingType_ = BumpMethod.Relative;
    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve[] survivalCurves_ = null;
    private bool[] wantScales_ = null;
    private bool iterative_ = false;
    private double marketRecoveryRate_ = 0.4;

    private bool scaleOnHazardRates_ = true;
    private ActionOnInadequateTenors actionOnInadequateTenors_ = ActionOnInadequateTenors.DropFirstIndexTenor;

    // itermediate results
    private bool[] includes_ = null;
    private double[] scalingFactors_;
    private double[] scalingFactorsWithZeros_;
    private SurvivalCurve[] scaledSurvivalCurves_;
    private bool[] useTenors_;
    #endregion Data
  }
}