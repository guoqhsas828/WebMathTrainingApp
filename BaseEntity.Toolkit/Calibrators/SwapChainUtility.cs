// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///  A wrapper of chained swaps
  /// </summary>
  public class SwapChain : Swap
  {
    internal readonly IList<Swap> Chain;
    internal readonly int Count;

    public SwapChain(IList<Swap> swaps, int count)
      : base(swaps[0].ReceiverLeg, swaps[0].PayerLeg)
    {
      Chain = swaps;
      Count = count;
    }
  }

  /// <summary>
  ///   Extension methods related to swap chain manipulation
  /// </summary>
  public static class SwapChainUtility
  {
    private static log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof (DiscountCalibrator));

    #region Compose swap chain

    internal static CurveTenorCollection ComposeSwapChain(
      this IEnumerable<CurveTenor> tenors,
      ReferenceIndex targetIndex,
      IList<ReferenceIndex> knownIndices = null,
      bool chainedSwapApproach = true
      )
    {
      return ComposeSwapChain(tenors, targetIndex, null, knownIndices, chainedSwapApproach);
    }

    internal static CurveTenorCollection ComposeSwapChain(
      this IEnumerable<CurveTenor> tenors,
      ReferenceIndex targetIndex,
      ReferenceIndex targetIndexLong,
      IList<ReferenceIndex> knownIndices,
      bool chainedSwapApproach)
    {
      var targetIndices = new List<ReferenceIndex>() { targetIndex };
      if (targetIndexLong != null)
        targetIndices.Add(targetIndexLong);
      return chainedSwapApproach || (!chainedSwapApproach && (knownIndices != null && knownIndices.Count > 0))
        ? ComposeSwapChains(tenors, targetIndices, knownIndices, chainedSwapApproach)
        : ComposeSwaps(tenors, targetIndices); // discount curves without chained swap approach
    }

    private static CurveTenorCollection ComposeSwapChains(
      this IEnumerable<CurveTenor> tenors,
      IList<ReferenceIndex> targetIndices,
      IList<ReferenceIndex> knownIndices,
      bool chainedSwapApproach)
    { 
      var retVal = new CurveTenorCollection();
      var swapList = new SortedMultiMap<Dt, CurveTenor>();
      foreach (var tenor in tenors)
      {
        if (tenor.Product is Swap)
          swapList.Add(tenor.Maturity, tenor);
        else if (targetIndices.Any(o =>tenor.HasReferenceIndex(o)))
          retVal.Add(tenor);
      }
      foreach (var o in swapList)
      {
        var swaps = o.Value.Select(t => (Swap) t.Product).ToList();
        var count = FindChain(swaps, targetIndices, knownIndices);
        if (count == 0)
        {
          if (logger.IsInfoEnabled)
          {
            logger.Info(String.Format(
              "No swap chain found at {0} for target index {1}",
              o.Key, string.Join(",", targetIndices.Select(i => i.IndexName).ToArray())));
          }
          continue;
        }
        retVal.Add(new CurveTenor(swaps[0].Description, count == 1 || !chainedSwapApproach
          ? swaps[0] : new SwapChain(swaps, count), 0.0, 0.0, 0.0, 1.0));
      }
      if (retVal.Count == 0)
      {
        throw new ToolkitException(String.Format(
          "No eligible tenor to calibrate curve with target index {0}",
          string.Join(",", targetIndices.Select(i => i.IndexName).ToArray())));
      }
      return retVal;
    }

    public static int FindChain(
      this IList<Swap> swaps,
      ReferenceIndex targetIndex)
    {
      return swaps.FindChain(new List<ReferenceIndex>() { targetIndex }, null);
    }

    public static int FindChain(
      this IList<Swap> swaps,
      IList<ReferenceIndex> targetIndices,
      IEnumerable<ReferenceIndex> knownIndices = null)
    {
      Func<Swap, ReferenceIndex>
        receiverIndex = s => s.ReceiverLeg.ReferenceIndex,
        payerIndex = s => s.PayerLeg.ReferenceIndex;
      
      var count = FindChain(swaps, receiverIndex, payerIndex,
        0, targetIndices, null);
      if (count > 0 || knownIndices == null)
        return count;

      foreach (var knownIndex in knownIndices)
      {
        count = FindChain(swaps, receiverIndex, payerIndex,
          0, targetIndices, knownIndex);
        if (count > 0) return count;
      }
      return 0;
    }

    public static int FindChain<T>(
      this IList<T> swaps,
      Func<T, ReferenceIndex> getReceiverIndex,
      Func<T, ReferenceIndex> getPayerIndex,
      ReferenceIndex targetIndex,
      IEnumerable<ReferenceIndex> knownIndices = null)
    {
      var count = FindChain(swaps, getReceiverIndex, getPayerIndex,
        0, new List<ReferenceIndex>() { targetIndex }, null);
      if (count > 0 || knownIndices == null)
        return count;

      foreach (var knownIndex in knownIndices)
      {
        count = FindChain(swaps, getReceiverIndex, getPayerIndex,
          0, new List<ReferenceIndex>() { targetIndex }, knownIndex);
        if (count > 0) return count;
      }
      return 0;
    }

    /// <summary>
    ///   Find a chain of swaps starting with the target index and
    ///   ending with the known index
    /// </summary>
    /// <param name="swaps">A list of swaps to pick from</param>
    /// <param name="getReceiverIndex">A function to get the index of the receiver leg</param>
    /// <param name="getPayerIndex">A function to get the index of the payer leg</param>
    /// <param name="first">The start position to pick, i.e.,
    ///  only swaps in the range [first..end] are available,
    ///  while those in the range [0..first) have already been picked.</param>
    /// <param name="targetIndices">The target indices</param>
    /// <param name="knownIndex">The known index (null means the fixed swap leg)</param>
    /// <returns>The number of swaps in the chain.</returns>
    /// <remark>
    ///   Let <c>count</c> the returned value.  Then the range [0..count] of the list
    ///   contains the swaps which form a proper chain,
    ///   where the first element has one leg with the target index,
    ///   and the last element has one leg fixed or with index in the known indices.
    /// </remark>
    public static int FindChain<T>(IList<T> swaps,
      Func<T, ReferenceIndex> getReceiverIndex,
      Func<T, ReferenceIndex> getPayerIndex,
      int first,
      IList<ReferenceIndex> targetIndices, ReferenceIndex knownIndex)
    {
      Debug.Assert(targetIndices != null);

      for (int i = swaps.Count; --i >= first;)
      {
        var swap = swaps[i];

        ReferenceIndex otherIndex;
        if (targetIndices.Any(o => MatchIndex(getReceiverIndex(swap), o)))
          otherIndex = getPayerIndex(swap);
        else if (targetIndices.Any(o => MatchIndex(getPayerIndex(swap), o)))
          otherIndex = getReceiverIndex(swap);
        else continue;

        // Now add this swap to chain and check if it ends with the start index.
        swaps.Exchange(first, i);
        if (MatchIndex(knownIndex, otherIndex)) return first + 1;

        int count = FindChain(swaps, getReceiverIndex, getPayerIndex,
          first + 1, new List<ReferenceIndex>() { otherIndex }, knownIndex);
        if (count > 0) return count;

        // Restore the original order
        swaps.Exchange(first, i);
      }

      // No chain found.
      return 0;
    }

    #endregion

    #region compose swaps (old style)

    private static CurveTenorCollection ComposeSwaps(IEnumerable<CurveTenor> tenors,
  IList<ReferenceIndex> targetIndices)
    {
      var retVal = new CurveTenorCollection();
      var swapList = new SortedMultiMap<Dt, CurveTenor>();
      foreach (CurveTenor tenor in tenors)
      {
        if (tenor.Product is Swap)
          swapList.Add(tenor.Maturity, tenor);
        else
          retVal.Add(tenor);
      }
      foreach (var o in swapList)
      {
        var swaps = o.Value.Select(t => (Swap)t.Product).ToList();
        if (swaps.Count == 1)
        {
          var swap = swaps.First();
          if (swap.IsFixedAndFloating &&
              ((swap.IsPayerFixed && swap.ReceiverLeg.ReferenceIndex.IsEqualToAnyOf(targetIndices))) ||
               (swap.IsReceiverFixed && swap.PayerLeg.ReferenceIndex.IsEqualToAnyOf(targetIndices)))
            retVal.Add(o.Value.First());
          continue;
        }
        PaymentSchedule fixedPayments = null;
        var fixedFloatingIndex = swaps.FindIndex(s => s.IsFixedAndFloating);
        if (fixedFloatingIndex < 0)
          throw new ArgumentException(String.Format(
            "One fixed-floating swap is required to build synthetic fixed-floating {0} swap.", targetIndices.First().IndexName));
        var fixedFloating = swaps[fixedFloatingIndex];
        swaps.RemoveAt(fixedFloatingIndex);
        SwapLeg targetFloater = null;
        if (GetFixedPayments(swaps, fixedFloating.IsReceiverFixed ? fixedFloating.ReceiverLeg : fixedFloating.PayerLeg, 1,
                             fixedFloating.IsReceiverFixed ? fixedFloating.PayerLeg.ReferenceIndex : fixedFloating.ReceiverLeg.ReferenceIndex, targetIndices,
                             ref fixedPayments, ref targetFloater) && targetFloater != null)
        {
          var compositeLeg = new SwapLeg(targetFloater.Effective, targetFloater.Maturity,
                                         targetFloater.Ccy, 0.0, DayCount.None, Frequency.None, BDConvention.None,
                                         Calendar.None, false)
          {
            CustomPaymentSchedule = fixedPayments
          };
          var swap = new Swap(targetFloater, compositeLeg); //Floating leg at receiver
          retVal.Add(new CurveTenor(targetFloater.Description, swap, 0.0, 0.0, 0.0, 1.0));
        }
        else
          logger.DebugFormat("One or more offsetting SwapLegs are missing for Tenor {0}. Synthetic {1} fixed-floating swap cannot be formed. " +
                            "BasisSwap quotes for Tenor {0} will be excluded.", o.Key, targetIndices.First());
      }
      return retVal;
    }

    private static bool GetFixedPayments(List<Swap> swaps, SwapLeg swapLeg, int sign,
      ReferenceIndex referenceIndex, IList<ReferenceIndex> targetIndices,
      ref PaymentSchedule fixedPayments, ref SwapLeg targetFloater)
    {
      if (fixedPayments == null)
        fixedPayments = new PaymentSchedule();
      if (!swapLeg.ReferenceIndex.IsEqualToAnyOf(targetIndices)) //otherwise goes in floating leg
        fixedPayments.AddPayments(PaymentScheduleUtils.FixedRatePaymentSchedule(swapLeg.Effective, Dt.Empty,
                                                                                swapLeg.Ccy, swapLeg.Schedule,
                                                                                swapLeg.CashflowFlag, sign * swapLeg.Coupon,
                                                                                swapLeg.CouponSchedule,
                                                                                swapLeg.Notional,
                                                                                swapLeg.AmortizationSchedule,
                                                                                swapLeg.Amortizes && swapLeg.IntermediateExchange,
                                                                                swapLeg.DayCount,
                                                                                swapLeg.CompoundingFrequency,
                                                                                null, false, Dt.Empty, Dt.Empty, null, null));
      if (swaps.Count == 0)
        return true;
      var swapIndex = swaps.FindIndex(s => s.ReceiverLeg.ReferenceIndex.IsEqual(referenceIndex) || s.PayerLeg.ReferenceIndex.IsEqual(referenceIndex));
      if (swapIndex < 0)
        return false;
      var swap = swaps[swapIndex];
      referenceIndex = swap.ReceiverLeg.ReferenceIndex.IsEqual(referenceIndex) ? swap.PayerLeg.ReferenceIndex : swap.ReceiverLeg.ReferenceIndex;
      swaps.RemoveAt(swapIndex);
      if (swap.ReceiverLeg.ReferenceIndex.IsEqualToAnyOf(targetIndices))
      {
        targetFloater = swap.ReceiverLeg;
        swaps.Clear(); //cycle is complete
      }
      else if (swap.PayerLeg.ReferenceIndex.IsEqualToAnyOf(targetIndices))
      {
        targetFloater = swap.PayerLeg;
        swaps.Clear(); //cycle is complete
      }
      if (swap.IsSpreadOnReceiver)
      {
        swapLeg = swap.ReceiverLeg;
        if (swap.ReceiverLeg.ReferenceIndex.IsEqual(referenceIndex))
          sign = -sign;
      }
      else if (swap.IsSpreadOnPayer)
      {
        swapLeg = swap.PayerLeg;
        if (swap.PayerLeg.ReferenceIndex.IsEqual(referenceIndex))
          sign = -sign;
      }
      else
        throw new ArgumentException(String.Format("Cannot create synthetic fixed-floating {0} swap from given quotes", targetIndices.First().IndexName));
      return GetFixedPayments(swaps, swapLeg, sign, referenceIndex, targetIndices, ref fixedPayments, ref targetFloater);
    }

    #endregion

    #region Get the payments on both sides (receiver and payer)

    public static List<Payment>[] GetSwapChainPayments(
      this IList<Swap> chained, int count,
      ReferenceIndex targetIndex,
      DiscountCurve discountCurve,
      IList<CalibratedCurve> projectionCurves,
      CalibratorSettings fitSettings)
    {
      var payments = new[] {new List<Payment>(), new List<Payment>()};

      // We make use of the fact that the chain is well formed
      // and the first one is the target index vs something else.
      ReferenceIndex lhsIndex = targetIndex;
      for (int i = 0; i < count; ++i)
      {
        var swap = chained[i];
        SwapLeg lhs, rhs;
        if (MatchIndex(swap.ReceiverLeg, lhsIndex))
        {
          lhs = swap.ReceiverLeg;
          rhs = swap.PayerLeg;
        }
        else if (MatchIndex(swap.PayerLeg, lhsIndex))
        {
          lhs = swap.PayerLeg;
          rhs = swap.ReceiverLeg;
        }
        else
        {
          throw new ToolkitException("Invalid swap chain");
        }
        AppendPayments(payments[0], lhs, discountCurve,
          projectionCurves, fitSettings);
        AppendPayments(payments[1], rhs, discountCurve,
          projectionCurves, fitSettings);
        lhsIndex = rhs.ReferenceIndex;
      }

      return payments;
    }


    /// <summary>
    ///   Append swap leg payments to the specified payment list
    /// </summary>
    private static void AppendPayments(List<Payment> paymentList,
      SwapLeg swapLeg, DiscountCurve discountCurve,
      IList<CalibratedCurve> projectionCurves,
      CalibratorSettings fitSettings)
    {
      var curveAsOf = discountCurve.AsOf;
      CalibratedCurve projectCurve = null;
      if (MatchIndex(swapLeg, discountCurve))
      {
        projectCurve = discountCurve;
      }
      else if (swapLeg.Floating && (projectionCurves == null
        || (projectCurve = projectionCurves.FirstOrDefault(
          c => MatchIndex(swapLeg, c))) == null))
      {
        swapLeg = (SwapLeg) swapLeg.ShallowCopy();
        swapLeg.Index = null;
      }
      // Create a pricer with proper curve
      var pricer = new SwapLegPricer(swapLeg, curveAsOf, swapLeg.Effective,
        1.0, discountCurve, swapLeg.ReferenceIndex, projectCurve,
        new RateResets(0.0, 0.0),
        fitSettings.FwdModelParameters, null)
      {
        ApproximateForFastCalculation = fitSettings.ApproximateRateProjection
      };
      paymentList.AddRange(pricer.GetPaymentSchedule(null, curveAsOf));
    }

    #endregion

    #region Helpers

    private static bool MatchIndex(SwapLeg swapLeg, CalibratedCurve curve)
    {
      var rateCurve = curve as DiscountCurve;
      return rateCurve != null && curve.ReferenceIndex != null
        && curve.ReferenceIndex.IsEqual(swapLeg.ReferenceIndex);
    }

    private static bool MatchIndex(SwapLeg swapLeg, ReferenceIndex index)
    {
      return index == null ? (!swapLeg.Floating)
        : index.IsEqual(swapLeg.ReferenceIndex);
    }

    private static bool MatchIndex(ReferenceIndex swapLegIndex,
      ReferenceIndex index)
    {
      return index == null ? swapLegIndex == null
        : index.IsEqual(swapLegIndex);
    }

    private static bool HasReferenceIndex(
      this CurveTenor tenor, ReferenceIndex index)
    {
      if (tenor == null) return false;
      if (tenor.ReferenceIndex == null || index == null)
        return true;
      return MatchIndex(tenor.ReferenceIndex, index);
    }

    #endregion
  }
}
