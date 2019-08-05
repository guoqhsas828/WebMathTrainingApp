//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Pricers.CmsCapFloorTestData;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture(Name.SimpleSurfaceFlat, -10)]
  [TestFixture(Name.RateCubeFlat, -10)]
  [TestFixture(Name.SwaptionMarketCube, -10)]
  [TestFixture(Name.SimpleSurfaceFlat, 10)]
  [TestFixture(Name.RateCubeFlat, 10)]
  [TestFixture(Name.SwaptionMarketCube, 10)]
  public class TestCmsCapFloorConsistency
  {
    #region Set up and tear down

    public TestCmsCapFloorConsistency(Name name, int shift)
    {
      _data = Data.All.Single(d => d.Name == name);
      _asOfShift = shift;
    }

    [OneTimeSetUp]
    public void SetUp()
    {
      _pricer = _data.GetPricer(_asOfShift);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      _pricer = null;
    }


    private readonly Data _data;
    private readonly int _asOfShift;
    private CmsCapFloorPricer _pricer;

    #endregion

    #region RoundtripVolatility tests

    [TestCase(VolatilityType.LogNormal, Category = "Smoke")]
    [TestCase(VolatilityType.Normal, Category = "Smoke")]
    public void RoundtripVolatility(VolatilityType vt)
    {
      var pricer = _pricer;
      pricer.ToleranceX = 1E-8;
      pricer.ToleranceF = 1E-10;
      var pv0 = pricer.ProductPv();
      pv0.IsExpected(To.Be.GreaterThan(0.0));
      var vol = pricer.ImpliedVolatility(vt, pv0);
      var flat = GetFlatVolatility(vt, pricer.VolatilityCube);
      if (flat.HasValue)
      {
        vol.IsExpected(To.Match(flat.Value).Within(1E-7),
          "Implied Volatility");
      }

      var flatVolObj = new FlatVolatility
      {
        Volatility = vol,
        DistributionType = vt == VolatilityType.LogNormal
          ? DistributionType.LogNormal : DistributionType.Normal,
      };
      var newPricer = CapFloorPricerBase.CreatePricer(
        pricer.Cap, pricer.AsOf, pricer.Settle,
        pricer.Resets, pricer.ReferenceCurve, pricer.DiscountCurve,
        flatVolObj, pricer.ConvexityParameters);
      newPricer.ToleranceX = 1E-5;
      var pv1 = newPricer.ProductPv();
      pv1.IsExpected(To.Match(pv0).Within(1E-9),"RoundTrip PV");
    }

    private static double? GetFlatVolatility(
      VolatilityType type, IVolatilityObject o)
    {
      var distribution = type == VolatilityType.LogNormal
        ? DistributionType.LogNormal
        : DistributionType.Normal;
      var flat = o as FlatVolatility;
      if (flat != null)
      {
        if (flat.DistributionType == distribution) return flat.Volatility;
        return null;
      }
      var cube = o as RateVolatilityCube;
      if (cube != null)
      {
        if (cube.IsFlat() && cube.DistributionType == distribution)
          return cube.FwdVols.GetVolatility(0, 0, 0);
        return null;
      }
      return null;
    }

    #endregion

    #region Cap/floor parity tests

    [TestCase(PricerKind.CapFloor)]
    [TestCase(PricerKind.CmsCapFloor)]
    public void CallPutParity(PricerKind kind)
    {
      var strike = _data.Strike;
      var swap = kind == PricerKind.CmsCapFloor
         ? _data.GetCmsSwapPricer(_asOfShift)
         : _data.GetSwapPricer(_asOfShift);
      Dt asOf = swap.AsOf, settle = swap.Settle;

      // Get all the floating payments and calculate rates, fractions, dfs, ...
      var floatPayments = swap.ReceiverSwapPricer.GetPaymentSchedule(
        null, swap.Settle).OfType<FloatingInterestPayment>().ToArray();
      var dates = Array.ConvertAll(floatPayments, p=>p.PayDt);
      var rates = Array.ConvertAll(floatPayments, p => p.EffectiveRate);
      var fracs = Array.ConvertAll(floatPayments, p => p.AccrualFactor);
      var dfs = dates.Select(d => swap.DiscountCurve.DiscountFactor(asOf, d))
        .ToArray();
      var swaps = rates.Select((r, i) => (r - strike)*fracs[i]*dfs[i]).ToArray();
      var swapValue = swaps.Sum();
      // Do we have consistent payments?
      swap.ProductPv().IsExpected(To.Match(swapValue).Within(1E-15));

      // Create a Cap and check each caplet payments
      var cap = kind == PricerKind.CmsCapFloor
        ? _data.GetCmsCapFloorPricer(CapFloorType.Cap, _asOfShift)
        : (CapFloorPricerBase) _data.GetCapFloorPricer(CapFloorType.Cap, _asOfShift);
      var caplets = cap.Caplets.OfType<CapletPayment>()
        .Where(p => p.PayDt > settle).ToArray();
      // Check that the dates, rates and fractions match the floating payments
      {
        Array.ConvertAll(caplets, p => p.PayDt)
          .IsExpected(To.Match(dates),"Caplet dates");
        Array.ConvertAll(caplets, cap.ForwardRate)
          .IsExpected(To.Match(rates).Within(1E-15), "Caplet rates");
        Array.ConvertAll(caplets, p => p.PeriodFraction)
          .IsExpected(To.Match(fracs).Within(1E-15), "Caplet fractions");
      }

      // Create a Floor and check each caplet payments
      var floor = kind == PricerKind.CmsCapFloor
        ? _data.GetCmsCapFloorPricer(CapFloorType.Floor, _asOfShift)
        : (CapFloorPricerBase) _data.GetCapFloorPricer(CapFloorType.Floor, _asOfShift);
      var floorlets = floor.Caplets.OfType<CapletPayment>()
        .Where(p => p.PayDt > settle).ToArray();
      // Check that the dates, rates and fractions match the floating payments
      {
        Array.ConvertAll(floorlets, p => p.PayDt)
          .IsExpected(To.Match(dates), "Floorlet dates");
        Array.ConvertAll(floorlets, floor.ForwardRate)
          .IsExpected(To.Match(rates).Within(1E-15), "Floorlet rates");
        Array.ConvertAll(floorlets, p => p.PeriodFraction)
          .IsExpected(To.Match(fracs).Within(1E-15), "Floorlet fractions");
      }

      // Check the call-put parity for each caplet
      caplets.Zip(floorlets, (c, f) => cap.CapletPv(c) - floor.CapletPv(f))
        .ToArray()
        .IsExpected(To.Match(swaps).Within(1E-7), "Swaplets");

      // Finally, check call-put parity for the full trade
      var capValue = cap.ProductPv();
      var floorValue = floor.ProductPv();
      (capValue - floorValue).IsExpected(To.Match(swapValue).Within(1E-6));
    }

    public enum PricerKind
    {
      CapFloor, CmsCapFloor,
    }
    #endregion
  }
}
