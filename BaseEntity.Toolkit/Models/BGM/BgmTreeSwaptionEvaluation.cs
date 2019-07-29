using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Models.BGM.Native;
using Tree = BaseEntity.Toolkit.Models.BGM.BgmBinomialTree;

namespace BaseEntity.Toolkit.Models.BGM
{
  #region Config

  /// <exclude/>
  [Serializable]
  public class BermudanSwaption
  {
    /// <exclude/>
    [ToolkitConfig("Use the arbitrage free tree model if the model is explicitly specified by the user")]
    public readonly bool EnableArbitrageFreeTreeModel = false;
  }

  #endregion Config

  public struct CallInfo
  {
    /// <summary>
    /// The price of zero coupon bond paying $1 on the specified date
    /// </summary>
    public double ZeroPrice;
    /// <summary>
    /// The price of the claim paying $1 in the call state 
    /// on the specified date
    /// </summary>
    public double StatePrice;
    /// <summary>
    /// The probability of a call made on the specified date
    /// </summary>
    public double Probability;
  }

  public static class BgmTreeSwaptionEvaluation
  {
    #region Evaluate Probability

    public static double CalculateBermudanPvWithCallProbabilities(
      SwaptionInfo[] swpns,
      RateSystem tree,
      CallInfo[] callInfos)
    {
      if (callInfos.Length != swpns.Length)
      {
        throw new ArgumentNullException(String.Format(
          "Size of call info array ({0}) not match swpns ({1})", 
          callInfos.Length, swpns.Length));
      }
      bool[][] callStatus = new bool[swpns.Length + 1][];
      double[] fwdValues;
#if DEBUG
      double[] swpValues;
#endif
      int latsRateIndex = swpns.Length;
      {
        int rateIndex = latsRateIndex;
        int stateCount = tree.GetStateCount(rateIndex);
        Debug.Assert(stateCount > 0);
        fwdValues = new double[stateCount];
        var called = callStatus[rateIndex] = new bool[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        int sign = swpns[rateIndex - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[rateIndex - 1].Coupon;
        double frac = tree.GetFraction(rateIndex);
        for (int s = 0; s < stateCount; ++s)
        {
          double r = (tree.GetRate(rateIndex, rateIndex, s) - k)*sign;
          double v = r*frac*tree.GetAnnuity(rateIndex, rateIndex, s);
          if (v > 0)
          {
            fwdValues[s] = v;
            called[s] = true;
          }
#if DEBUG
          swpValues[s] = v;
#endif
        }
      }
      for (int d = swpns.Length; --d > 0;)
      {
        int sign = swpns[d - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[d - 1].Coupon;
        double frac = tree.GetFraction(d);
        int stateCount = tree.GetStateCount(d);
        var values = new double[stateCount];
        var called = callStatus[d] = new bool[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        for (int s = 0; s < stateCount; ++s)
        {
          double a = 0;
          for (int i = latsRateIndex; i >= d; --i)
            a += tree.GetFraction(i)*tree.GetAnnuity(i, d, s);
          a *= k;
          double r = tree.GetRate(d, d, s);
          double v = ((1 + r*frac)*tree.GetAnnuity(d, d, s)
            - tree.GetAnnuity(latsRateIndex, d, s) - a)*sign;
          double c = tree.CalculateExpectation(d, s, d + 1, fwdValues);
          if (v > c)
          {
            values[s] = v;
            called[s] = true;
          }
          else
          {
            values[s] = c;
          }
#if DEBUG
          swpValues[s] = v;
#endif
        }
        fwdValues = values;
      }

      double bmdValue = tree.CalculateExpectation(0, 0, 1, fwdValues);
#if DEBUG
      for (int i = 0; i < swpValues.Length; ++i)
      {
        swpValues[i] = Math.Max(0, swpValues[i]);
      }
      double swpnValue = tree.CalculateExpectation(0, 0, 1, swpValues);
#endif

      CalculateCallProbabilities(tree, callStatus, callInfos);
      return bmdValue;
    }

    static void CalculateCallProbabilities(
      RateSystem tree,
      bool[][] callStatus,
      CallInfo[] callInfos)
    {
      var maxStateCount = tree.GetStateCount(callStatus.Length - 1);
      var fwdProbs = new double[maxStateCount];
      var lastProbs = new double[maxStateCount];
      lastProbs[0] = 1;
      callStatus[0] = new bool[1];
      for (int d = 1; d < callStatus.Length; ++d)
      {
        double price = 0.0, statePrice = 0.0, prob = 0.0;
        var called = callStatus[d];
        var lastCalled = callStatus[d - 1];
        int stateCount = tree.GetStateCount(d);
        for (int s = 0; s < stateCount; ++s)
        {
          var a = tree.GetAnnuity(d, d, s);
          var p = 0.0;
          for (int i = 0; i < lastCalled.Length; ++i)
          {
            if (lastCalled[i]) continue;
            p += lastProbs[i]*tree.GetConditionalProbability(
              d, s, d - 1, i);
          }
          fwdProbs[s] = p;
          if (called[s])
          {
            prob += p;
            statePrice += a*p;
          }
          price += a*tree.GetProbability(d, s);
        }
        callInfos[d - 1] = new CallInfo
        {
          ZeroPrice = price,
          StatePrice = statePrice,
          Probability = prob
        };

        var tmp = lastProbs;
        lastProbs = fwdProbs;
        fwdProbs = tmp;
      }
    }

    #endregion

    #region Evaluate Pv

    internal static bool IsArbitrageFreeTreeEnabled(
      this BgmTreeOptions treeOptions)
    {
      return (treeOptions != null && (treeOptions.ArbitrageFreeTree == true
        || (treeOptions.ArbitrageFreeTree == null && ToolkitConfigurator
          .Settings.BermudanSwaption.EnableArbitrageFreeTreeModel)));
    }

    internal static double CalculateBermudanPv(
      SwaptionInfo[] swpns,
      Dt asOf, Dt maturity,
      DistributionType distributionType,
      bool convertNormalToLognormal,
      bool isAmericanOption,
      BgmTreeOptions treeOptions)
    {
      if (IsArbitrageFreeTreeEnabled(treeOptions))
      {
        return Trees.RateCalculations.CalculateBermudanPv(
          swpns, asOf, maturity, distributionType, isAmericanOption);
      }
      var flags = distributionType == DistributionType.Normal
          ? (convertNormalToLognormal
            ? BgmBinomialTree.NormalToLogNormal
            : BgmBinomialTree.Normal)
          : BgmBinomialTree.LogNormal;
      if (isAmericanOption) flags |= 16;
      return CalculateBermudanPv(swpns, asOf, maturity, flags, null);
    }

    internal static double CalculateBermudanPv(
      SwaptionInfo[] swpns,
      Dt asOf, Dt maturity, 
      DistributionType distributionType,
      CallInfo[] callInfos = null,
      bool convertNormalToLognormal = false,
      bool isAmericanOption = false)
    {
      var flags = distributionType == DistributionType.Normal
          ? (convertNormalToLognormal
            ? BgmBinomialTree.NormalToLogNormal
            : BgmBinomialTree.Normal)
          : BgmBinomialTree.LogNormal;
      if (isAmericanOption) flags |= 16;
      return CalculateBermudanPv(swpns, asOf, maturity, flags, callInfos);
    }

    internal static SwaptionInfo[] CheckTreeOptions(
      this SwaptionInfo[] swpns,
      Dt asOf,
      DistributionType distributionType,
      BgmTreeOptions treeOpt)
    {
      var hasAccuracy = treeOpt != null && treeOpt.CalibrationTolerance > 0;
      if (!(swpns[0].Accuracy > 0))
      {
        swpns[0].Accuracy = hasAccuracy ? treeOpt.CalibrationTolerance : 1E-6;
      }
      if (!(swpns[0].Steps > 0))
      {
        if (treeOpt != null && treeOpt.InitialSteps > 0)
        {
          swpns[0].Steps = treeOpt.InitialSteps;
        }
        else if (treeOpt != null && treeOpt.Adaptive)
        {
          // Set the initial steps to a sensible value
          double v = swpns[0].Volatility, t = (swpns[0].Date - asOf) / 365.0;
          if (distributionType == DistributionType.Normal)
          {
            v /= Math.Max(swpns[0].Rate, 0.005);
          }
          int required = (int)(v * v / 0.04 * t);
          const int max = 100;
          if (required > max) swpns[0].Steps = max;
          else if (required > 10) swpns[0].Steps = required;
        }
      }

      var hasMiddleSteps = treeOpt != null && treeOpt.MiddleSteps > 0;
      for (int i = 1, n = swpns.Length; i < n; ++i)
      {
        if (!(swpns[i].Accuracy > 0))
        {
          swpns[i].Accuracy = hasAccuracy ? treeOpt.CalibrationTolerance : 1E-6;
        }
        if (hasMiddleSteps && !(swpns[i].Steps > 0))
        {
          swpns[i].Steps = treeOpt.MiddleSteps;
        }
      }

      return swpns;
    }

    private static double CalculateBermudanPv(
      SwaptionInfo[] swpns,
      Dt asOf, Dt maturity, int distribution,
      CallInfo[] callInfos)
    {
      var tree = new RateSystem();
      Native.BgmBinomialTree.calibrateCoTerminalSwaptions(
        asOf, maturity, swpns, 1E-8, distribution, tree);
      return (distribution & BgmBinomialTree.AmericanOption) != 0
        ? CalculateAmericanPv(swpns, tree)
        : (callInfos == null
          ? CalculateBermudanPv(swpns, tree)
          : CalculateBermudanPvWithCallProbabilities(swpns, tree, callInfos));
    }

    private static double CalculateBermudanPv(
      SwaptionInfo[] swpns,
      RateSystem tree)
    {
      double[] fwdValues;
#if DEBUG
      double[] swpValues;
#endif
      int latsRateIndex = swpns.Length;
      {
        int rateIndex = latsRateIndex;
        int stateCount = tree.GetStateCount(rateIndex);
        Debug.Assert(stateCount > 0);
        fwdValues = new double[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        int sign = swpns[rateIndex - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[rateIndex - 1].Coupon;
        double frac = tree.GetFraction(rateIndex);
        for (int s = 0; s < stateCount; ++s)
        {
          double r = (tree.GetRate(rateIndex, rateIndex, s) - k)*sign;
          double v = r*frac*tree.GetAnnuity(rateIndex, rateIndex, s);
          if (v > 0)
          {
            fwdValues[s] = v;
          }
#if DEBUG
          swpValues[s] = v;
#endif
        }
      }
      for (int d = swpns.Length; --d > 0;)
      {
        int sign = swpns[d - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[d - 1].Coupon;
        double frac = tree.GetFraction(d);
        int stateCount = tree.GetStateCount(d);
        var values = new double[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        for (int s = 0; s < stateCount; ++s)
        {
          double a = 0;
          for (int i = latsRateIndex; i >= d; --i)
            a += tree.GetFraction(i)*tree.GetAnnuity(i, d, s);
          a *= k;
          double r = tree.GetRate(d, d, s);
          double v = ((1 + r*frac)*tree.GetAnnuity(d, d, s)
            - tree.GetAnnuity(latsRateIndex, d, s) - a)*sign;
          double c = tree.CalculateExpectation(d, s, d + 1, fwdValues);
          values[s] = Math.Max(v, c);
#if DEBUG
          swpValues[s] = v;
#endif
        }
        fwdValues = values;
      }

      double bmdValue = tree.CalculateExpectation(0, 0, 1, fwdValues);
#if DEBUG
      for (int i = 0; i < swpValues.Length; ++i)
      {
        swpValues[i] = Math.Max(0, swpValues[i]);
      }
      double swpnValue = tree.CalculateExpectation(0, 0, 1, swpValues);
#endif

      return bmdValue;
    }

    private static double CalculateAmericanPv(
      SwaptionInfo[] swpns,
      RateSystem tree)
    {
      double[] fwdValues;
#if DEBUG
      double[] swpValues;
#endif
      int lastRateIndex = swpns.Length,
        lastDateIndex = tree.DateCount - 1,
        dateIndex = lastDateIndex;
      {
        int stateCount = tree.GetStateCount(dateIndex);
        Debug.Assert(stateCount > 0);

        int rateIndex = tree.GetLastResetIndex(dateIndex);
        Debug.Assert(rateIndex == lastRateIndex);

        fwdValues = new double[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        int sign = swpns[rateIndex - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[rateIndex - 1].Coupon;
        double frac = tree.GetFraction(rateIndex);
        for (int s = 0; s < stateCount; ++s)
        {
          double r = (tree.GetRate(rateIndex, dateIndex, s) - k)*sign;
          double v = r*frac*tree.GetAnnuity(rateIndex, dateIndex, s);
          if (v > 0)
          {
            fwdValues[s] = v;
          }
#if DEBUG
          swpValues[s] = v;
#endif
        }
      }


      while (--dateIndex > 0)
      {
        // ri is the index of the first active rate.
        int ri = tree.GetLastResetIndex(dateIndex);
        if (ri == 0)
        {
          ++dateIndex;
          break;
        }
        bool isResetDate = ri != tree.GetLastResetIndex(dateIndex - 1);
        if (!isResetDate) ++ri;

        // Calculate the remaining option value at the date.
        int sign = swpns[ri - 1].OptionType == OptionType.Call ? 1 : (-1);
        double k = swpns[ri - 1].Coupon;
        double frac = tree.GetFraction(ri);
        int stateCount = tree.GetStateCount(dateIndex);
        var values = new double[stateCount];
#if DEBUG
        swpValues = new double[stateCount];
#endif
        for (int s = 0; s < stateCount; ++s)
        {
          double a = 0;
          for (int i = lastRateIndex; i >= ri; --i)
            a += tree.GetFraction(i)*tree.GetAnnuity(i, dateIndex, s);
          a *= k;
          double r = tree.GetRate(ri, dateIndex, s);
          double v = ((1 + r*frac)*tree.GetAnnuity(ri, dateIndex, s)
            - tree.GetAnnuity(lastRateIndex, dateIndex, s) - a)*sign;
          double c = tree.CalculateExpectation(dateIndex, s, dateIndex + 1, fwdValues);
          values[s] = Math.Max(v, c);
#if DEBUG
          swpValues[s] = v;
#endif
        }
        fwdValues = values;
      }

      double bmdValue = tree.CalculateExpectation(0, 0, dateIndex, fwdValues);
#if DEBUG
      for (int i = 0; i < swpValues.Length; ++i)
      {
        swpValues[i] = Math.Max(0, swpValues[i]);
      }
      double swpnValue = tree.CalculateExpectation(0, 0, dateIndex, swpValues);
#endif

      return bmdValue;
    }

    #endregion
  }
}
