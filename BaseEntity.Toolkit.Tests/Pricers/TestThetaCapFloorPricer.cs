//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestThetaCapFloorPricer : ToolkitTestBase
  {
    [OneTimeSetUp]
    public void Initialize()
    {
      asOf_ = new Dt(16, 12, 2010);
      settle_ = new Dt(20,12,2010);
      logNormalVols_ = new double[] { 0.926, 0.775, 0.684, 0.6435, 0.61, 0.5555, 0.5075, 0.4545, 0.4385 };
      capMaturities_ = new Dt[]{new Dt(20,12,2011),new Dt(20,12,2012),new Dt(20,12,2013),new Dt(20,12,2014),new Dt(20,12,2015),new Dt(20,12,2017),
      new Dt(20,12,2020),new Dt(20,12,2025),new Dt(20,12,2030)};
      capStrikes_ = CapStrikes();
      capVols_ = CapVolatilities();
      lambdasCaps_ = CapLambdas();

    }

    /// <summary>
    /// Tests if the Theta for a 1Y cap matches with the manually calculated values  
    /// </summary>
    [Test]
    public void TestTheta1Y()
    {
      var toAsOf = new Dt(17, 12, 2010);
      var toSettle = new Dt(21, 12, 2010);
      var cap = new Cap(settle_, new Dt(20, 12, 2011), Currency.USD, CapFloorType.Cap, 0.01, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Toolkit.Base.Calendar.NYB)
                  {
                    AccrueOnCycle = true,
                    CycleRule = CycleRule.None
                  };
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, capMaturities_, logNormalVols_, VolatilityType.LogNormal,cap.RateIndex);
      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");
      var pricer = new CapFloorPricer(cap, asOf_, settle_, discountCurve, discountCurve, cube);
      pricer.Resets.Add(new RateReset(new Dt(16,12,2010),discountCurve.F(asOf_,Dt.Add(asOf_,"3M"),Toolkit.Base.DayCount.Actual360,Frequency.None)));
      var theta = Sensitivities.Theta(pricer, "Pv", toAsOf, toSettle, ThetaFlags.Clean, SensitivityRescaleStrikes.No);

      //Now compute a manual theta 
      var basePv = pricer.ProductPv();
      pricer.AsOf = toAsOf;
      pricer.Settle = toSettle;
      pricer.Reset();
      discountCurve.AsOf = toAsOf;
      cube.AsOf = toAsOf;
      var newPv = pricer.ProductPv();
      var manualTheta = newPv - basePv;
      Assert.AreEqual(theta, manualTheta,String.Format("Theta does not match manual theta {0} != {1}", theta, manualTheta));

    }

    [Test]
    public void TestTheta1YSabr()
    {
      var toAsOf = new Dt(17, 12, 2010);
      var toSettle = new Dt(21, 12, 2010);
      var cap = new Cap(settle_, new Dt(20, 12, 2011), Currency.USD, CapFloorType.Cap, 0.01, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Toolkit.Base.Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");
      
      double[,] capVolatilities = new double[capMaturities_.Length,capStrikes_.Length];
      int idx = 0;
      for(int i=0;i<capStrikes_.Length;i++)
      {
        for (int j = 0; j < capMaturities_.Length; j++)
          capVolatilities[j,i] = capVols_[idx++];
      }


      var sabrCalibrator = new RateVolatilityCapSabrCalibrator(asOf_, settle_, discountCurve, dt=>cap.RateIndex, dt=>discountCurve, VolatilityType.LogNormal,
                                                               new Dt[] {}, new double[] {}, new double[] {},
                                                               null, null, capMaturities_, capStrikes_, capVolatilities,
                                                               new double[] {}, lambdasCaps_,
                                                               VolatilityBootstrapMethod.PiecewiseQuadratic,
                                                               new double[] {0.001, -0.9, 0.001},
                                                               new double[] {0.1, 0.5, 0.7}, new Curve(settle_, 0.35),
                                                               null, null, null);
      var cube = new RateVolatilityCube(sabrCalibrator);
      cube.Fit();

      var pricer = new CapFloorPricer(cap, asOf_, settle_, discountCurve, discountCurve, cube);
      pricer.Resets.Add(new RateReset(new Dt(16, 12, 2010), discountCurve.F(asOf_, Dt.Add(asOf_, "3M"), Toolkit.Base.DayCount.Actual360, Frequency.None)));
      var theta = Sensitivities.Theta(pricer, "Pv", toAsOf, toSettle, ThetaFlags.Clean, SensitivityRescaleStrikes.No);

      //Now compute a manual theta 
      var basePv = pricer.ProductPv();
      pricer.AsOf = toAsOf;
      pricer.Settle = toSettle;
      pricer.Reset();
      discountCurve.AsOf = toAsOf;
      cube.AsOf = toAsOf;
      cube.RateVolatilityCalibrator.AsOf = toAsOf;
      var newPv = pricer.ProductPv();
      var manualTheta = newPv - basePv;
      Assert.AreEqual(theta, manualTheta, String.Format("Theta does not match manual theta {0} != {1}", theta, manualTheta));
    }

    /// <summary>
    /// Test for a 10 Y 3% cap. Check if the Theta is the same 
    /// </summary>
    [Test]
    public void TestTheta10YSabr()
    {
      var toAsOf = new Dt(17, 12, 2010);
      var toSettle = new Dt(21, 12, 2010);
      var cap = new Cap(settle_, new Dt(20, 12, 2020), Currency.USD, CapFloorType.Cap, 0.03, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Toolkit.Base.Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");

      double[,] capVolatilities = new double[capMaturities_.Length, capStrikes_.Length];
      int idx = 0;
      for (int i = 0; i < capStrikes_.Length; i++)
      {
        for (int j = 0; j < capMaturities_.Length; j++)
          capVolatilities[j, i] = capVols_[idx++];
      }


      var sabrCalibrator = new RateVolatilityCapSabrCalibrator(asOf_, settle_, discountCurve, dt=>cap.RateIndex, dt=>discountCurve, VolatilityType.LogNormal,
                                                               new Dt[] { }, new double[] { }, new double[] { },
                                                               null, null, capMaturities_, capStrikes_, capVolatilities,
                                                               new double[] { }, lambdasCaps_,
                                                               VolatilityBootstrapMethod.PiecewiseQuadratic,
                                                               new double[] { 0.001, -0.9, 0.001 },
                                                               new double[] { 0.1, 0.5, 0.7 }, new Curve(settle_, 0.35),
                                                               null, null, null);
      var cube = new RateVolatilityCube(sabrCalibrator);
      cube.Fit();

      var pricer = new CapFloorPricer(cap, asOf_, settle_, discountCurve, discountCurve, cube);
      pricer.Resets.Add(new RateReset(new Dt(16, 12, 2010), discountCurve.F(asOf_, Dt.Add(asOf_, "3M"), Toolkit.Base.DayCount.Actual360, Frequency.None)));
      var theta = Sensitivities.Theta(pricer, "Pv", toAsOf, toSettle, ThetaFlags.Clean, SensitivityRescaleStrikes.No);

      //Now compute a manual theta 
      var basePv = pricer.ProductPv();
      pricer.AsOf = toAsOf;
      pricer.Settle = toSettle;
      pricer.Reset();
      discountCurve.AsOf = toAsOf;
      cube.AsOf = toAsOf;
      cube.RateVolatilityCalibrator.AsOf = toAsOf;
      var newPv = pricer.ProductPv();
      var manualTheta = newPv - basePv;
      Assert.AreEqual(theta, manualTheta, String.Format("Theta does not match manual theta {0} != {1}", theta, manualTheta));
    }

    [Test]
    public void TestTheta10YBootstrap()
    {
      var toAsOf = new Dt(17, 12, 2010);
      var toSettle = new Dt(21, 12, 2010);
      var cap = new Cap(settle_, new Dt(20, 12, 2020), Currency.USD, CapFloorType.Cap, 0.03, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Toolkit.Base.Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");

      double[,] capVolatilities = new double[capMaturities_.Length, capStrikes_.Length];
      int idx = 0;
      for (int i = 0; i < capStrikes_.Length; i++)
      {
        for (int j = 0; j < capMaturities_.Length; j++)
          capVolatilities[j, i] = capVols_[idx++];
      }

      var bootstrapCalibrator = new RateVolatilityCapBootstrapCalibrator(asOf_, settle_, discountCurve, dt => cap.RateIndex, dt=>discountCurve,
                                                                          VolatilityType.LogNormal,
                                                                          new Dt[] { }, new double[] { }, new double[] { },
                                                                          null, null, capMaturities_, capStrikes_,
                                                                          capVolatilities, new double[] { }, lambdasCaps_,
                                                                          VolatilityBootstrapMethod.PiecewiseQuadratic);

      var cube = new RateVolatilityCube(bootstrapCalibrator);
      cube.Fit();

      var pricer = new CapFloorPricer(cap, asOf_, settle_, discountCurve, discountCurve, cube);
      pricer.Resets.Add(new RateReset(new Dt(16, 12, 2010), discountCurve.F(asOf_, Dt.Add(asOf_, "3M"), Toolkit.Base.DayCount.Actual360, Frequency.None)));
      var theta = Sensitivities.Theta(pricer, "Pv", toAsOf, toSettle, ThetaFlags.Clean, SensitivityRescaleStrikes.No);

      //Now compute a manual theta 
      var basePv = pricer.ProductPv();
      pricer.AsOf = toAsOf;
      pricer.Settle = toSettle;
      pricer.Reset();
      discountCurve.AsOf = toAsOf;
      cube.AsOf = toAsOf;
      cube.RateVolatilityCalibrator.AsOf = toAsOf;
      var newPv = pricer.ProductPv();
      var manualTheta = newPv - basePv;
      Assert.AreEqual(theta, manualTheta, String.Format("Theta does not match manual theta {0} != {1}", theta, manualTheta));
    }

    [Test]
    public void TestTheta1YBootstrap()
    {
      var toAsOf = new Dt(17, 12, 2010);
      var toSettle = new Dt(21, 12, 2010);
      var cap = new Cap(settle_, new Dt(20, 12, 2011), Currency.USD, CapFloorType.Cap, 0.01, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Toolkit.Base.Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");

      double[,] capVolatilities = new double[capMaturities_.Length, capStrikes_.Length];
      int idx = 0;
      for (int i = 0; i < capStrikes_.Length; i++)
      {
        for (int j = 0; j < capMaturities_.Length; j++)
          capVolatilities[j, i] = capVols_[idx++];
      }

      var bootstrapCalibrator = new RateVolatilityCapBootstrapCalibrator(asOf_, settle_, discountCurve,dt => cap.RateIndex, dt => discountCurve,
                                                                          VolatilityType.LogNormal,
                                                                          new Dt[] {}, new double[] {}, new double[] {},
                                                                          null, null, capMaturities_, capStrikes_,
                                                                          capVolatilities, new double[] {},
                                                                          lambdasCaps_,
                                                                          VolatilityBootstrapMethod.PiecewiseQuadratic);

      var cube = new RateVolatilityCube(bootstrapCalibrator);
      cube.Fit();

      var pricer = new CapFloorPricer(cap, asOf_, settle_, discountCurve, discountCurve, cube);
      pricer.Resets.Add(new RateReset(new Dt(16, 12, 2010), discountCurve.F(asOf_, Dt.Add(asOf_, "3M"), Toolkit.Base.DayCount.Actual360, Frequency.None)));
      var theta = Sensitivities.Theta(pricer, "Pv", toAsOf, toSettle, ThetaFlags.Clean, SensitivityRescaleStrikes.No);

      //Now compute a manual theta 
      var basePv = pricer.ProductPv();
      pricer.AsOf = toAsOf;
      pricer.Settle = toSettle;
      pricer.Reset();
      discountCurve.AsOf = toAsOf;
      cube.AsOf = toAsOf;
      cube.RateVolatilityCalibrator.AsOf = toAsOf;
      var newPv = pricer.ProductPv();
      var manualTheta = newPv - basePv;
      Assert.AreEqual(theta, manualTheta, String.Format("Theta does not match manual theta {0} != {1}", theta, manualTheta));
    }

    public static double[] CapVolatilities()
    {
      var  capVols = new double[]{0.926,0.775,0.684,0.6435,0.61,0.5555,0.5075,0.4545,0.4385,0.8985,0.7515,
                                   0.6425,0.58,0.5415,0.486,0.4415,0.394,0.37,0.879,0.7385,0.6135,0.532,0.49,0.4345,0.393,0.35,0.3285,
                                   0.852,0.7245,0.5745,0.465,0.419,0.3625,0.3265,0.2895,0.2735,0.842,0.7205,0.56,0.4425,0.3955,0.338,
                                   0.3035,0.269,0.2535, 0.8335,0.717,0.548,0.426,0.378,0.32,0.2865,0.253,0.2385,0.819,0.712,0.529,
                                   0.405,0.358,0.2975,0.2645,0.2315,0.218,0.8075,0.708,0.5145,0.394,0.35,0.288,0.254,0.2215,0.2075,
                                   0.798,0.705,0.503,0.3885,0.348,0.286,0.251,0.218,0.2035,0.7895,0.7025,0.4935,0.386,0.349,0.2875,
                                   0.252,0.218,0.201};
      return capVols;
    }

    public static double[] CapLambdas()
    {
      return new double[] { 1.50, 1.50, 1.50, 0.50, 0.25, 0.50, 0.50, 0.50, 0.50, 0.50 };
    }

    public static double[] CapStrikes()
    {
      return new double[] { 0.01, 0.015, 0.02, 0.03, 0.035, 0.04, 0.05, 0.06, 0.07, 0.08 };
    }

    private double[] logNormalVols_;
    private Dt[] capMaturities_;
    
    private Dt asOf_;
    private Dt settle_;
    
    private double[] capStrikes_;
    private double[] capVols_;
    private double[] lambdasCaps_;
  }
}
