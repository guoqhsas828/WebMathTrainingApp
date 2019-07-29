using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Compute the exposure to the counterparty, given netted pvs and prescribed netting sets 
  /// </summary>
  public class PathWiseExposure
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(PathWiseExposure));

    #region RiskyParty

    /// <summary>
    /// Risky party
    /// </summary>
    internal enum RiskyParty
    {
      /// <summary>
      /// Booking entity
      /// </summary>
      BookingEntity,

      /// <summary>
      /// Counterparty
      /// </summary>
      Counterparty,

      /// <summary>
      /// No risky entity
      /// </summary>
      None, 

    }

    #endregion

    #region Data

    /// <summary>
    /// Collateral Agreement.
    /// </summary>
    private readonly Tuple<int, ICollateralMap>[] collateral_;

    /// <summary>
    /// Number of netting super groups.
    /// </summary>
    private readonly int count_;

    /// <summary>
    /// Exposure dates
    /// </summary>
    private readonly Dt[] exposureDates_;

    /// <summary>
    /// nettingRule_.item1 = netting group index, nettingRule_.item2 = netting super group index. 
    /// </summary>
    /// <remarks>Only netting groups with the same netting super group index are netted with each other.</remarks>
    private readonly Tuple<int, int>[] nettingRule_;

    /// <summary>
    /// Risky party, can be BookingEntity, Counterparty, or None.
    /// </summary>
    private readonly RiskyParty riskyParty_;

    private readonly bool _modelOvercollateralization; 
    #endregion

    #region Constructor

    /// <exclude></exclude>
    internal PathWiseExposure(
      Dt[] exposureDates,
      Dictionary<string, int> nettingMap,
      Netting netting,
      RiskyParty riskyParty, 
      bool modelOvercollateralization = false)
    {
      int idx;
      exposureDates_ = exposureDates;
      riskyParty_ = riskyParty;
      nettingRule_ = new Tuple<int, int>[netting.NettingGroups.Length];
      count_ = 0;
      _modelOvercollateralization = modelOvercollateralization; 
      var ssets = new List<string>();
      for (int i = 0; i < netting.NettingGroups.Length; ++i)
      {
        string id = netting.NettingGroups[i];
        if (nettingMap.TryGetValue(id, out idx))
        {
          string sid = netting.NettingSuperGroups[i];
          int sidx = ssets.IndexOf(sid);
          if (sidx < 0)
          {
            ssets.Add(sid);
            sidx = count_++;
          }
          nettingRule_[i] = new Tuple<int, int>
            (
            idx,
            sidx
            );
        }
        else
        {
          throw new ArgumentException(
            String.Format("Netting group {0} does not belong to the calculation environment", id));
        }
      }
      if (netting.CollateralMaps != null)
      {
        var collateral = new Dictionary<string, Tuple<int, ICollateralMap>>();
        foreach (ICollateralMap map in netting.CollateralMaps)
        {
          int index;
          if (map == null || (index = Array.IndexOf(netting.NettingGroups, map.NettingGroup)) < 0)
            continue;
          collateral[map.NettingGroup] = new Tuple<int, ICollateralMap>(index, map);
        }
        if (collateral.Count > 0)
          collateral_ = collateral.Values.ToArray();
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calculate the exposure.
    /// </summary>
    /// <param name="x">Mark to market value. Sign is from booking entity's perspective.</param>
    /// <returns><para>If risky party is booking entity, return <m>max(-x,0).</m> </para>
    /// <para>If risky party is counterparty, return <m>max(x,0).</m></para> 
    /// <para>If risky party is None, return <m>x</m>.</para></returns>
    internal double ExposureFn(double x)
    {
      return (riskyParty_ == RiskyParty.BookingEntity)
               ? Math.Max(-x, 0.0)
               : (riskyParty_ == RiskyParty.Counterparty) ? Math.Max(x, 0.0) : x;
    }

    /// <summary>
    /// Calculate the spread.
    /// </summary>
    /// <param name="path">Simulation path.</param>
    /// <param name="dateIndex">Date index.</param>
    /// <returns><para>If risky party is booking entity, return own spread.</para>
    /// <para>If risky party is counterparty, return cpty spread.</para> 
    /// <para>If risky party is None, return <m>0</m>.</para></returns>
    private double SpreadFn(SimulatedPathValues path, int dateIndex)
    {
      return (riskyParty_ == RiskyParty.BookingEntity)
               ? path.GetOwnSpread(dateIndex)
               : (riskyParty_ == RiskyParty.Counterparty) ? path.GetCptySpread(dateIndex) : 0.0;
    }

    /// <summary>
    /// Interpolation function.
    /// </summary>
    /// <param name="h"><m>x_1 - x_0</m></param>
    /// <param name="y0"><m>y_0 = f(x_0)</m></param>
    /// <param name="y1"><m>y_1 = f(x_1)</m></param>
    /// <param name="x"><m>x_2 - x_0</m></param>
    /// <returns>Given the value of <m>f(x_0)</m> and <m>f(x_1)</m>, this function returns the linear interpolated value for <m>f(x_2)</m>: 
    /// <math evn="align*">\frac{x}{h}y_1 + (1 - \frac{x}{h})y_0</math></returns>
    private static double Interpolate(double h, double y0, double y1, double x)
    {
      if (h <= 0.0)
        return y0;
      return x/h*y1 + (1.0 - x/h)*y0;
    }

    /// <summary>
    /// Determine whether a risky party has is out of the money, i.e. need to deliver some collateral.
    /// </summary>
    /// <param name="mtm">Mark to market value. Sign is from booking entity's perspective.</param>
    /// <returns><para>If risky party is booking entity and <m>mtm \lt 0</m>, return true.</para>
    /// <para>If risky party is counterparty and <m>mtm \gt 0</m>, return true.</para> 
    /// <para>In other cases, return false.</para></returns>
    private bool HasRiskyPartyExposure(double mtm)
    {
      return (mtm > 0 && riskyParty_ == RiskyParty.Counterparty) || (mtm < 0 && riskyParty_ == RiskyParty.BookingEntity);
    }

    
    /// <summary>
    /// Compute the collateral amount for the netting group.
    /// </summary>
    /// <param name="mtmPath">Mark to market path</param>
    /// <param name="spreadPath">Spread path.</param>
    /// <param name="dateIdx">Date index</param>
    /// <param name="mtm">Mark to market value of the netting groups.</param>
    /// <param name="tradeAllocations">Trade Allocations.</param>
    /// <returns>Collateral amounts for the netting groups.</returns>
    /// <remarks>
    /// <para>Suppose we have <m>N</m> netting groups with index <m>0, 1, \cdots N - 1</m>.</para>
    /// <para>This function will return an array <m>collateralAmounts</m> with <m>N</m> elements, such that <m>collateralAmounts[i]</m> 
    /// represents the collateral for the <m>i</m>-th netting group at the given simulation date.</para>
    /// </remarks>
    internal CollateralPoint[] ComputeCollateralAmounts(ISimulatedPathValues mtmPath, SimulatedPathValues spreadPath, int dateIdx,
                                              double[] mtm, double[][] tradeAllocations)
    {
      if (collateral_ == null || collateral_.Length == 0)
        return new CollateralPoint[mtm.Length];
      var collateralAmounts = new CollateralPoint[collateral_.Length];
      for (int i = 0; i < collateral_.Length; i++)
      {
        var collateral = collateral_[i];
        int index = collateral.Item1;
        int nettingSet = nettingRule_[index].Item1;

        // if (i >= N)
        if (nettingSet >= mtmPath.NettingCount)  
        {
          continue;
        }

        ICollateralMap map = collateral.Item2;

        // test whether the risky party is out of the money.
        if (!HasRiskyPartyExposure(mtm[index]) && !_modelOvercollateralization) 
        {
          continue;
        }

        // for the case that Remargin Period = 0.
        double vm;
        double ia;
        double totalCollateral;
        if (map.MarginPeriodOfRisk.IsEmpty || map.MarginPeriodOfRisk.N == 0)
        {
          vm = map.VariationMargin(mtm[index], SpreadFn(spreadPath, dateIdx), exposureDates_[dateIdx]);
          ia = map.IndependentAmount(mtm[index], vm);
        }
        else
        {
          // Calculate postingDt = current time - margin period.
          Dt postingDt = Dt.Add(exposureDates_[dateIdx], -map.MarginPeriodOfRisk.N, map.MarginPeriodOfRisk.Units);
          // For the case that postingDt <= T_0
          if (Dt.Cmp(postingDt, exposureDates_[0]) <= 0)
          {
            // collateral should only reduce exposures
            // so if sign is different, zero out computed collateral
            var initialMTM = mtmPath.GetPortfolioValue(0, nettingSet);
            vm = map.VariationMargin(initialMTM, SpreadFn(spreadPath, 0), postingDt);
            ia = map.IndependentAmount(mtm[index], vm);
          }
          else
          {
            // t represents the index of postingDt in the array exposureDates_ = {T_0, ..., T_n}, if value is found.
            // If postingDt is not found and T_{i-1} < postingDt < T_i, return -i.
            // If postingDt is not found and T_n < postingDt, return - (n+ 1). 
            int t = Array.BinarySearch(exposureDates_, postingDt);
            // For the case that postingDt is found in the array exposureDates_.
            double prevMTM;
            if (t >= 0)
            {
              // collateral should only reduce exposures
              // so if sign is different, zero out computed collateral
              prevMTM = mtmPath.GetPortfolioValue(t, nettingSet);
              vm = map.VariationMargin(prevMTM, SpreadFn(spreadPath, t), postingDt);
              ia = map.IndependentAmount(mtm[index], vm);
            }
            else
            {
              // For the case that postingDt is not found in exposureDates_, apply linear interpolation.
              t = ~t;
              double h = Dt.FractDiff(exposureDates_[t - 1], exposureDates_[t]);
              double x = Dt.FractDiff(exposureDates_[t - 1], postingDt);
              prevMTM = Interpolate(h, mtmPath.GetPortfolioValue(t - 1, nettingSet),
                mtmPath.GetPortfolioValue(t, nettingSet), x);
              double spread = Interpolate(h, SpreadFn(spreadPath, t - 1), SpreadFn(spreadPath, t), x);

              vm = map.VariationMargin(prevMTM, spread, postingDt);
              ia = map.IndependentAmount(mtm[index], vm);
            }
          }
        }

        collateralAmounts[index].IndependentAmount = ia;
        collateralAmounts[index].VariationMargin = vm;
        totalCollateral = ia + vm;

        if (!totalCollateral.ApproximatelyEqualsTo(0.0))
        {
          double collateralizedMTM = mtm[index] - totalCollateral;
          var overcollateralized = Math.Sign(mtm[index]) != Math.Sign(totalCollateral) ||
                                               (mtm[index] < 0 && totalCollateral < mtm[index]) ||
                                               (mtm[index] > 0 && totalCollateral > mtm[index]);
          if (overcollateralized && !_modelOvercollateralization)
          {
            if (logger.IsVerboseEnabled())
            {
              var msg = String.Format("Estimated collateral {0} exceeds mtm {1}, setting collateralizedMTM to 0 for dt {2}", collateralAmounts[index],
                mtm[index], dateIdx);
              logger.Verbose(msg);
            }
            collateralizedMTM = 0.0;
            collateralAmounts[index].VariationMargin = mtm[index] - collateralAmounts[index].IndependentAmount; // don't overcollateralize
          }

          if (tradeAllocations != null)
          {
            var totalWeight = 0.0;
            for (int tradeIdx = 0; tradeIdx < tradeAllocations[nettingSet].Length; tradeIdx++)
            {
              var weight = tradeAllocations[nettingSet][tradeIdx] / mtm[index];
              tradeAllocations[nettingSet][tradeIdx] = collateralizedMTM * weight;
              totalWeight += weight;
            }
            if (logger.IsVerboseEnabled() && !totalWeight.AlmostEquals(1.00))
            {
              logger.VerboseFormat("total marginal weights do not sum to 1 on dt {0}. Total is {1}", dateIdx, totalWeight);
            }
          }

        }
      }
      return collateralAmounts;
    }

    /// <summary>
    /// Compute pathwise exposure by applying netting and collateral.
    /// </summary>
    /// <param name="path">Simulation path.</param>
    /// <param name="dateIdx">Date index.</param>
    /// <returns>exposure by applying netting and collateral.</returns>
    internal double Compute(SimulatedPathValues path, int dateIdx)
    {
      return Compute(path, dateIdx, null).Item1;
    }

    /// <summary>
    /// Compute the pathwise exposure and the difference between collateralised exposure and uncollateralised exposure.
    /// </summary>
    /// <param name="path">Simulation path</param>
    /// <param name="dateIdx">Date index</param>
    /// <param name="tradeAllocations">Trade allocations.</param>
    /// <returns>Item 1 is pathwise exposure. Item 2 is the difference between collateralised exposure and uncollateralised exposure.</returns>
    /// <remarks>
    /// <para>Suppose we have <m>N</m> netting groups with index <m>0, 1, \cdots N - 1</m>, and <m>M</m> netting super groups with index <m>0, 1, \cdots, M-1</m>, where <m>N \geq M.</m></para>
    /// <para>For netting group <m>i</m>, denote the corresponding netting super group index by <m>S_i</m>, where <m>0 \leq S_i \leq M.</m> Only netting groups belonging to the same 
    /// super group are netted with each other.</para>
    /// <para>Let <m>workspace</m> be an array with <m>N</m> elements, such that <m>workspace[i]</m> represents the mark to market of the <m>i</m>-th netting group at the given simulation date.</para>
    /// <para>Let <m>collateralWorkspace</m> be an array with <m>N</m> elements, such that <m>collateralWorkspace[i]</m> represents the collateral of the <m>i</m>-th netting group at the given simulation date.</para>
    /// <para>Let <m>mtm</m> be an array with <m>M</m> elements, such that <m>mtm[j]</m> represents the mark to market of the <m>j</m>-th netting super group at the given simulation date.</para>
    /// <para>Let <m>collateral</m> be an array with <m>M</m> elements, such that <m>collateral[j]</m> represents the collateral of the <m>j</m>-th netting group at the given simulation date.</para> 
    /// For <m>j = 0, 1, \cdots, M-1</m>, one have:
    /// <math env="align*">
    /// mtm[j] &amp; = \sum_{i=0}^{N-1} \mathbb{1}_{j}(S_i) \cdot workspace[i], \\\\
    /// collateral[j] &amp; = \sum_{i=0}^{N-1} \mathbb{1}_{j}(S_i) \cdot collateralWorkspace[i], 
    /// </math>
    /// where <m>\mathbb{1}_{j}(S_i) = 1</m> if <m>S_i = j</m>, and <m>\mathbb{1}_{j}(S_i) = 0</m> if <m>S_i \neq j</m>.
    /// <para>
    /// <para>Denote the pathwise netted collateralised exposure of the portfolio by <m>totMtM</m>, and 
    /// the pathwise netted uncollateralised exposure of the portfolio by <m>totNoColl</m>. </para>
    /// <para>Then depending on the type of the risky party, we have the following three cases. </para>
    /// </para>
    /// <list type="bullet">
    /// <item><description>If the risky party is Counterparty, then 
    /// <math env="align*">
    /// totMtM &amp;= \sum_{j=0}^{M-1} \max \Big(mtm[j]-collateral[j], 0\Big) \\\\
    /// totNoColl &amp;= \sum_{j=0}^{M-1} \max \Big(mtm[j], 0\Big) 
    /// </math>
    /// </description></item>
    /// <item><description>If the risky party is Booking entity, then 
    /// <math env="align*">
    /// totMtM &amp;= \sum_{j=0}^{M-1} \max \Big(- \big( mtm[j]-collateral[j] \big), 0\Big) \\\\
    /// totNoColl &amp;= \sum_{j=0}^{M-1} \max \Big(- mtm[j], 0\Big) 
    /// </math>
    /// </description></item>
    /// <item><description>If the risky party is None, then 
    /// <math env="align*">
    /// totMtM &amp;= \sum_{j=0}^{M-1} \Big( mtm[j]-collateral[j] \Big) \\\\
    /// totNoColl &amp;= \sum_{j=0}^{M-1} mtm[j]
    /// </math>
    /// </description></item>
    /// </list>
    /// </remarks>
    internal Tuple<double,double> Compute(SimulatedPathValues path, int dateIdx, double[][] tradeAllocations)
    {
      var mtm = new double[count_];
      var collateral = new double[count_];
      var workspace = new double[nettingRule_.Length];
      for (int i = 0; i < nettingRule_.Length; ++i)
        workspace[i] = path.GetPortfolioValue(dateIdx, nettingRule_[i].Item1);
      var collateralWorkspace = ComputeCollateralAmounts(path, path, dateIdx, workspace, tradeAllocations);
      // Apply collateral and net super groups
      for (int i = 0; i < nettingRule_.Length; ++i)
      {
        mtm[nettingRule_[i].Item2] += workspace[i];
        collateral[nettingRule_[i].Item2] += collateralWorkspace[i].IndependentAmount + collateralWorkspace[i].VariationMargin;
      }
      // If super group netted exposure is 0, zero out allocations
      if (tradeAllocations != null)
      {
        for (int i = 0; i < nettingRule_.Length; ++i)
        {
          if (ExposureFn(mtm[nettingRule_[i].Item2] - collateral[nettingRule_[i].Item2]).ApproximatelyEqualsTo(0.0))
          {
            for (int j = 0; j < tradeAllocations[nettingRule_[i].Item1].Length; j++)
            {
              tradeAllocations[nettingRule_[i].Item1][j] = 0.0;
            }
          }
        }
      }
      double totMtM = 0.0;
      double totNoColl = 0.0;
      for (int i = 0; i < mtm.Length; i++)
      {
        double v = mtm[i];
        double c = collateral[i];
        double e = ExposureFn(v-c);
        double nce = ExposureFn(v);
        totMtM += e;
        totNoColl += nce; 
      }
      return new Tuple<double, double>(totMtM, totNoColl - totMtM);
    }

    
    /// <summary>
    /// Simplified collateral computation without allocation 
    /// </summary>
    internal CollateralPoint[] ComputeCollateral(SimulatedPathValues path, double[] mtmAtExposureDt, double[] mtmAtCollateralDt, int dateIdx)
    {
      if (collateral_ == null || collateral_.Length == 0)
        return new CollateralPoint[mtmAtExposureDt.Length];
      var collateralAmounts = new CollateralPoint[collateral_.Length];
      for (int i = 0; i < collateral_.Length; i++)
      {
        var collateral = collateral_[i];
        int index = collateral.Item1;
        int nettingSet = nettingRule_[index].Item1;

        // if (i >= N)
        if (nettingSet >= path.NettingCount)
        {
          collateralAmounts[index].VariationMargin = 0.0;
          collateralAmounts[index].IndependentAmount = 0.0;
          continue;
        }
        
        ICollateralMap map = collateral.Item2;

        // test whether the risky party is out of the money.
        if (!HasRiskyPartyExposure(mtmAtExposureDt[index]) && !_modelOvercollateralization)
        {
          collateralAmounts[index].VariationMargin = 0.0;
          collateralAmounts[index].IndependentAmount = 0.0;
          continue;
        }

        // test whether the risky party could have been owed collateral at call date
        if (!HasRiskyPartyExposure(mtmAtCollateralDt[index]) && !_modelOvercollateralization)
        {
          collateralAmounts[index].VariationMargin = 0.0;
          collateralAmounts[index].IndependentAmount = 0.0;
          continue;
        }

        // Calculate posingDt = current time - margin period.
        Dt postingDt = map.MarginPeriodOfRisk.IsEmpty ? exposureDates_[dateIdx] : Dt.Add(exposureDates_[dateIdx], -map.MarginPeriodOfRisk.N, map.MarginPeriodOfRisk.Units);

        var vm = map.VariationMargin(mtmAtCollateralDt[index], SpreadFn(path, dateIdx), postingDt);
        var ia = map.IndependentAmount(mtmAtExposureDt[index], vm);
        collateralAmounts[index].VariationMargin = vm;
        collateralAmounts[index].IndependentAmount = ia;


        var totalCollateral = ia + vm; 

        if (!totalCollateral.ApproximatelyEqualsTo(0.0))
        {
          var mtm = mtmAtExposureDt[index];
          var overcollateralized = Math.Sign(mtm) != Math.Sign(totalCollateral) ||
                                   mtm < 0 && totalCollateral < mtm ||
                                   mtm > 0 && totalCollateral > mtm;

          if (overcollateralized && !_modelOvercollateralization)
          {
            if (logger.IsVerboseEnabled())
            {
              var msg = String.Format("Estimated collateral {0} exceeds mtm {1}, setting collateralizedMTM to 0 for dt {2}", collateralAmounts[index],
                mtmAtExposureDt[index], dateIdx);
              logger.Verbose(msg);
            }
            collateralAmounts[index].VariationMargin = mtmAtExposureDt[index] - collateralAmounts[index].IndependentAmount; // don't overcollateralize
          }
        }
      }
      return collateralAmounts;
    }

    internal ExposurePoint CapAndSumExposures(double[] nettingSetExposures, CollateralPoint[] nettingSetCollateral)
    {
      var mtm = new double[count_];
      var reusableCollateral = new double[count_];
      var segregatedCollateral = new double[count_];
      // Apply collateral and net super groups
      for (int i = 0; i < nettingRule_.Length; ++i)
      {
        mtm[nettingRule_[i].Item2] += nettingSetExposures[i];
        var map = collateral_[i].Item2;
        if(map.ReusePermitted)
          reusableCollateral[nettingRule_[i].Item2] += nettingSetCollateral[i].VariationMargin;
        else
          segregatedCollateral[nettingRule_[i].Item2] += nettingSetCollateral[i].VariationMargin;

        if (map.IndependentAmountSegregated || !map.ReusePermitted)
          segregatedCollateral[nettingRule_[i].Item2] += nettingSetCollateral[i].IndependentAmount;
        else
          reusableCollateral[nettingRule_[i].Item2] += nettingSetCollateral[i].IndependentAmount;
      }

      double totMtM = 0.0;
      double totNoColl = 0.0;
      double fundingMtM = 0.0;
      for (int i = 0; i < mtm.Length; i++)
      {
        double v = mtm[i];
        double c = reusableCollateral[i] + segregatedCollateral[i];
        double e = ExposureFn(v - c);
        double fe = ExposureFn(v - reusableCollateral[i]);
        double nce = ExposureFn(v);
        totMtM += e;
        totNoColl += nce;
        fundingMtM += fe;
      }
      return new ExposurePoint() { Exposure = totMtM, Collateral = totNoColl - totMtM, FundingExposure = fundingMtM };
    }

    internal double[] CollateralizedTotals(double[] nettingSetExposures, double[] nettingSetCollateral)
    {
      var mtm = new double[count_];
      var collateral = new double[count_];

      var collateralizedExposures = new double[nettingSetExposures.Length];

      for (int i = 0; i < nettingSetExposures.Length; ++i)
      {
        collateralizedExposures[i] = nettingSetExposures[i] - nettingSetCollateral[i];
      }

      // Apply collateral and net super groups
      for (int i = 0; i < nettingRule_.Length; ++i)
      {
        mtm[nettingRule_[i].Item2] += nettingSetExposures[i];
        collateral[nettingRule_[i].Item2] += nettingSetCollateral[i];
      }
      // If super group netted exposure is 0, zero out allocations
      for (int n = 0; n < nettingRule_.Length; ++n)
      {
        if (ExposureFn(mtm[nettingRule_[n].Item2] - collateral[nettingRule_[n].Item2]).ApproximatelyEqualsTo(0.0))
        {
          collateralizedExposures[n] = 0.0;
        }
      }
      return collateralizedExposures;
    }


    internal double ComputeIncremental(SimulatedPathValues path, int dateIdx)
    {
      var mtm = new double[count_];
      var workspace = new double[nettingRule_.Length];
      for (int i = 0; i < nettingRule_.Length; ++i)
        workspace[i] = path.GetPortfolioValue(dateIdx, nettingRule_[i].Item1);
      var collateral = ComputeCollateralAmounts(path, path, dateIdx, workspace, null);
      // Apply collateral and net supergroups
      for (int i = 0; i < nettingRule_.Length; ++i)
        mtm[nettingRule_[i].Item2] += workspace[i] - (collateral[i].IndependentAmount + collateral[i].VariationMargin);
      double totMtM = 0.0;
      foreach (double d in mtm)
        totMtM += ExposureFn(d);
      if (path.OldPath == null)
        return totMtM;
      //Incremental calculations
      ISimulatedPathValues oldPath = path.OldPath;
      for (int i = 0; i < nettingRule_.Length; ++i)
        workspace[i] = (nettingRule_[i].Item1 >= oldPath.NettingCount)
                         ? 0.0
                         : oldPath.GetPortfolioValue(dateIdx, nettingRule_[i].Item1);
      collateral = ComputeCollateralAmounts(path.OldPath, path, dateIdx, workspace, null);
      for (int i = 0; i < mtm.Length; ++i)
        mtm[i] = 0.0;
      for (int i = 0; i < nettingRule_.Length; ++i)
        mtm[nettingRule_[i].Item2] += workspace[i] - (collateral[i].IndependentAmount + collateral[i].VariationMargin);
      foreach (double d in mtm)
        totMtM -= ExposureFn(d);
      return totMtM;
    }


    internal double ComputeOld(ISimulatedPathValues oldPath, SimulatedPathValues spreadPath, int dateIdx)
    {
      var mtm = new double[count_];
      var workspace = new double[nettingRule_.Length];
      for (int i = 0; i < nettingRule_.Length; ++i)
        workspace[i] = (nettingRule_[i].Item1 >= oldPath.NettingCount)
                         ? 0.0
                         : oldPath.GetPortfolioValue(dateIdx, nettingRule_[i].Item1);
      var collateral = ComputeCollateralAmounts(oldPath, spreadPath, dateIdx, workspace, null);
      // Apply collateral and net supergroups
      for (int i = 0; i < mtm.Length; ++i)
        mtm[i] = 0.0;
      for (int i = 0; i < nettingRule_.Length; ++i)
        mtm[nettingRule_[i].Item2] += workspace[i] - (collateral[i].IndependentAmount + collateral[i].VariationMargin);
      double totMtM = 0.0;
      foreach (double d in mtm)
        totMtM += ExposureFn(d);
      return totMtM;
    }

    #endregion

    /// <summary>
    /// Represents IA and VM posting at a particular point 
    /// </summary>
    internal struct CollateralPoint
    {
      /// <summary>
      /// Total amount of VM posted
      /// </summary>
      public double VariationMargin { get; set; }
      /// <summary>
      /// Total Independent Amount
      /// </summary>
      public double IndependentAmount { get; set; }

      /// <summary>
      /// </summary>
      public override string ToString()
      {
        return $"VM {VariationMargin}, IA {IndependentAmount}";
      }
    }

    /// <summary>
    /// Represents Exposure and Collateral data at a particular point
    /// </summary>
    internal struct ExposurePoint
    {
      public SimulatedPathValues Path { get; set; }
      public int PathIdx { get; set; }
      public int DateIdx { get; set; }
      public double Exposure { get; set; }
      public double Collateral { get; set; }
      public double FundingExposure { get; set; }

    }
  }

  


}