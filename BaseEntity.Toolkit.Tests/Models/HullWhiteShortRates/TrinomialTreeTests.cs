//
// Copyright (c)    2002-2018. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using static BaseEntity.Toolkit.Tests.Pricers.Bonds.CallableBondFactory;

namespace BaseEntity.Toolkit.Tests.Models.HullWhiteShortRates
{
  using NUnit.Framework;

  [TestFixture]
  public class TrinomialTreeTests
  {
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Price(int caseNo)
    {
      var data = Cases[caseNo];
      int n = data.MaxDays;
      var prices = new double[n+1];
      for (int i = 0; i <= n; ++i)
      {
        var pricer = CreateCallableBondPricer(data.Coupon);
        pricer.Bond.CallSchedule.Add(Call(new Dt(20170915), 1.026));
        pricer.Bond.NotificationDays = i;
        prices[i] = pricer.FullModelPrice();
      }
      Assert.That(prices, Is.EqualTo(data.Expects).Within(1E-7));
      return;
    }

    private static BondPricer CreateCallableBondPricer(double coupon)
    {
      Dt effective = new Dt(20140210),
        maturity = new Dt(20210315),
        firstCoupon = new Dt(20140315),
        asOf = new Dt(20170213),
        settle = new Dt(20170216);
      var bond = new Bond(
        effective, maturity, Currency.USD, BondType.USCorp, coupon,
        DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual,
        BDConvention.Following, Calendar.NYB)
      { FirstCoupon = firstCoupon };
      return new BondPricer(bond, asOf, settle,
        new DiscountCurve(asOf, 0.04), null, 0, TimeUnit.None, 0,
        CallableBondPricingMethod.BlackKarasinski)
      {
        MeanReversion = 0.1,
        Sigma = 0.3
      };
    }

    private struct Data
    {
      public double Coupon;
      public int MaxDays;
      public double[] Expects;
    }

    private static readonly Data[] Cases =
    {
      new Data
      {
        Coupon = 0.0525,
        MaxDays = 30,
        Expects = new[]
        {
          1.0480571807190249,
          1.0480650839750163,
          1.0480729765422838,
          1.048080858449052,
          1.0480887297234762,
          1.048112280033243,
          1.0481201090585133,
          1.0481279275912174,
          1.0481357356591117,
          1.0481668638354447,
          1.0481746199933639,
          1.0481823658516516,
          1.0481901014376138,
          1.048197826778493,
          1.0482209416021226,
          1.0482286262338474,
          1.048236300755768,
          1.0482439651947557,
          1.04825161957762,
          1.0482745226566714,
          1.0482821370819402,
          1.0482897415842365,
          1.0482973361900114,
          1.048304920925659,
          1.0483276161748991,
          1.0483351616928112,
          1.0483426974716956,
          1.0483502235376012,
          1.04835773991652,
          1.0483802311904422,
          1.0483877090802218,
        }
      },
      new Data
      {
        Coupon = 0.0425,
        MaxDays = 30,
        Expects = new[]
        {
          1.0220087263005557,
          1.0220146000125023,
          1.0220204634474344,
          1.0220263166242853,
          1.0220321595619133,
          1.022049627126941,
          1.0220554292947892,
          1.0220612213166027,
          1.0220670032107997,
          1.0220900298772313,
          1.0220957614070689,
          1.0221014829182744,
          1.0221071944287583,
          1.0221128959563619,
          1.0221299408192166,
          1.0221356025922712,
          1.0221412544705772,
          1.022146896471549,
          1.02215252861253,
          1.0221693660479108,
          1.0221749589209608,
          1.0221805420196866,
          1.0221861153610119,
          1.0221916789617922,
          1.0222083114883724,
          1.0222138362941382,
          1.0222193514425972,
          1.0222248569501913,
          1.0222303528332946,
          1.0222467828983681,
          1.0222522404458783,
        }
      },
      new Data
      {
        Coupon = 0.0325,
        MaxDays = 30,
        Expects = new[]
        {
          0.98397488320263771,
          0.98397488320263782,
          0.98397488320263793,
          0.98397488320263782,
          0.98397488320263771,
          0.9839748832026376,
          0.98397488320263782,
          0.98397488320263771,
          0.983974883202638,
          0.98397488320263782,
          0.98397488320263771,
          0.98397488320263793,
          0.983974883202638,
          0.98397488320263748,
          0.98397488320263782,
          0.98397488320263771,
          0.98397488320263793,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263793,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263771,
          0.98397488320263793,
          0.98397488320263782,
          0.98397488320263782,
          0.98397488320263782,
        }
      },
      new Data
      {
        Coupon = 0.0125,
        MaxDays = 30,
        Expects = new[]
        {
          0.90106956879465872,
          0.90106956879465872,
          0.9010695687946586,
          0.90106956879465872,
          0.90106956879465872,
          0.90106956879465849,
          0.90106956879465883,
          0.9010695687946586,
          0.90106956879465872,
          0.90106956879465872,
          0.9010695687946586,
          0.90106956879465849,
          0.90106956879465849,
          0.90106956879465872,
          0.90106956879465872,
          0.90106956879465849,
          0.90106956879465849,
          0.90106956879465883,
          0.90106956879465849,
          0.9010695687946586,
          0.9010695687946586,
          0.90106956879465872,
          0.90106956879465872,
          0.90106956879465894,
          0.9010695687946586,
          0.90106956879465849,
          0.90106956879465872,
          0.9010695687946586,
          0.90106956879465849,
          0.90106956879465872,
          0.9010695687946586,
        }
      }
    };
  }
}
