//
// Copyright (c)    2018. All rights reserved.
//

using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class TestSwapLegCustomSchedule
  {
    #region Forward rate swap

    [Test]
    public static void ForwardRateSwap()
    {
      var terms = ToolkitCache.StandardProductTermsCache.Values
        .OfType<SwapTerms>()
        .First(s => s.Description == "USD Swap");
      var noncustomized = terms.GetProduct(new Dt(20160413), "30Y", 0.02);
      var customized = CreateSwapWithCustomSchedule(noncustomized);

      Dt asOf = new Dt(20160614);
      var discountCurve = new DiscountCurve(asOf, 0.01);
      var projectionCurve = new DiscountCurve(asOf, 0.02);

      var noncustomizedPricer = new SwapPricer(noncustomized,
        asOf, asOf, 1.0, discountCurve, projectionCurve,
        new RateResets(0.02, double.NaN));
      var customizedPricer = new SwapPricer(customized,
        asOf, asOf, 1.0, discountCurve, projectionCurve,
        new RateResets(0.02, double.NaN));
 
      // Check payments and PV
      AssertSamePaymentsAndPv(noncustomizedPricer, customizedPricer);
    }

    #endregion

    #region CMS rate swap

    [Test]
    public static void CmsSwapWithConvexity()
    {
      var noncustomizedPricer = CmsCapFloorTestData.Data
        .All[0].GetCmsSwapPricer(45);
      var customizedPricer = GetCmsSwapPricerWithCustomSchedule(
        0, 45);

      // Make sure we have convexity adjustment
      Assert.NotNull(customizedPricer.ReceiverSwapPricer.FwdRateModelParameters);

      // Check payments and PV
      AssertSamePaymentsAndPv(noncustomizedPricer, customizedPricer);
    }

    private static SwapPricer GetCmsSwapPricerWithCustomSchedule(
      int index, int dateShift)
    {
      var swap = CreateSwapWithCustomSchedule(
        CmsCapFloorTestData.Data.All[index]
          .GetCmsSwapPricer(dateShift).Swap);
      var pricer = CmsCapFloorTestData.Data.All[index]
        .GetCmsSwapPricer(dateShift);
      pricer.Product = swap;
      pricer.ReceiverSwapPricer.Product = swap.ReceiverLeg;
      pricer.PayerSwapPricer.Product = swap.PayerLeg;
      return pricer;
    }

    #endregion

    #region Utilities

    public static void AssertSamePaymentsAndPv(
      SwapPricer noncustomizedPricer, SwapPricer customizedPricer)
    {
      // Make sure one pricer has custom schedule
      // and the other has not.
      Assert.NotNull(customizedPricer.ReceiverSwapPricer.SwapLeg.CustomPaymentSchedule);
      Assert.NotNull(customizedPricer.PayerSwapPricer.SwapLeg.CustomPaymentSchedule);
      Assert.Null(noncustomizedPricer.ReceiverSwapPricer.SwapLeg.CustomPaymentSchedule);
      Assert.Null(noncustomizedPricer.PayerSwapPricer.SwapLeg.CustomPaymentSchedule);

      // Make sure both have the same payments
      var asOf = noncustomizedPricer.AsOf;
      var regularPayments = noncustomizedPricer.ReceiverSwapPricer
        .GetPaymentSchedule(null, asOf)
        .OfType<FloatingInterestPayment>()
        .ToArray();
      var customPayments = customizedPricer.ReceiverSwapPricer
        .GetPaymentSchedule(null, asOf)
        .OfType<FloatingInterestPayment>()
        .ToArray();
      var mismatch = ObjectStatesChecker.Compare(
        regularPayments, customPayments);
      Assert.IsNull(mismatch, "Payments");

      // Make sure both have the same PV
      Assert.AreEqual(noncustomizedPricer.Pv(),
        customizedPricer.Pv(), 1E-15, "PV");
    }

    private static Swap CreateSwapWithCustomSchedule(Swap swap)
    {
      // Custom payments are created with dummy curves,
      // using product effective as the pricing date.
      var dummyCurve = new DiscountCurve(Dt.MinValue, 0.0);
      Dt asOf = swap.Effective;
      var pricer = new SwapPricer(swap, asOf, asOf,
        1.0, dummyCurve, dummyCurve, null);

      swap = swap.CloneObjectGraph();
      swap.ReceiverLeg.CustomPaymentSchedule =
        pricer.ReceiverSwapPricer.GetPaymentSchedule(null, asOf);
      swap.PayerLeg.CustomPaymentSchedule =
        pricer.PayerSwapPricer.GetPaymentSchedule(null, asOf);

      foreach (var leg in new[] { swap.ReceiverLeg, swap.PayerLeg})
      {
        foreach (var floatingInterestPayment in leg.CustomPaymentSchedule.OfType<FloatingInterestPayment>())
        {
          floatingInterestPayment.RateProjector = null;
        }
      }

      return swap;
    }

    #endregion
  }
}
