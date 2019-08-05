// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators
{

  [TestFixture]
  public class TestRateVolatilityCurveBuilder : ToolkitTestBase
  {
    [OneTimeSetUp]
    public void Initialize()
    {
      logNormalVols_ = new double[] {0.926, 0.775, 0.684, 0.6435, 0.61, 0.5555, 0.5075, 0.4545, 0.4385};
      normalVols_ = new double[]
                      {
                        0.007498185, 0.007653817, 0.008097386, 0.008642528, 0.00901909, 0.009405608, 0.009673912,
                        0.009419667, 0.009219514
                      };
      Dt settle = Cap.StandardSettle(new Dt(16, 12, 2010), 2, Calendar.NYB);
      string[]tenors = new string[]{"1Y","2Y","3Y","4Y","5Y","7Y","10Y","15Y","20Y"};
      maturities_ = new Dt[tenors.Length];
      for(int i=0;i<tenors.Length;i++)
        maturities_[i] = Dt.Add(settle, tenors[i]);
      capStrikes_ = new double[]{0.01,0.015,0.02,0.03,0.035,0.04,0.05,0.06,0.07,0.08};
    }

    /// <summary>
    /// Test 
    /// </summary>
    [Test]
    public void TestCapPvsLogNormal()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      double[] pvs;
      double[] vegas;
      //Create a rate option param collection 
      TestRateVolatilityCurveBuilderUtil.CalculateCapMarketPvs(asof,logNormalVols_,maturities_,curve,cap,VolatilityType.LogNormal,out pvs,out vegas);
      double[] testPvs = CalculateCapPvs(curve, logNormalVols_, maturities_, cap, asof, settle, VolatilityType.LogNormal);
      for(int i=0;i<testPvs.Length;i++)
      {
        Assert.AreEqual(testPvs[i], pvs[i], 1e-6, String.Format("Pvs don't match for maturity {0}", maturities_[i]));
      }

    }

    [Test]
    public void TestCapPvsNormal()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };

      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      double[] pvs;
      double[] vegas;
      //Create a rate option param collection 
      TestRateVolatilityCurveBuilderUtil.CalculateCapMarketPvs(asof, normalVols_, maturities_, curve, cap, VolatilityType.Normal, out pvs, out vegas);
      double[] testPvs = CalculateCapPvs(curve, normalVols_, maturities_, cap, asof, settle, VolatilityType.Normal);
      for (int i = 0; i < testPvs.Length; i++)
      {
        Assert.AreEqual(testPvs[i], pvs[i], 1e-6, String.Format("Pvs don't match for maturity {0}", maturities_[i]));
      }
    }
    /// <summary>
    /// Test case that checks if the piecewise constant guesses are correct for the Log Normal volatilities 
    /// </summary>
    [Test]
    public void TestCapPcGuessesLogNormal()
    {
      Dt asOf = new Dt(16,12,2010);
      Dt settle = new Dt(20,12,2010);
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      double[] guesses;
      TestRateVolatilityCurveBuilderUtil.CalculateCapPcGuesses(asOf, logNormalVols_, maturities_, curve, cap, VolatilityType.LogNormal, out guesses);
      var dates = new List<Dt> {settle};
      for(int i=0;i<maturities_.Length-1;i++)
      {
        Dt rateFixing = maturities_[i];
        Dt expiry = Dt.AddDays(rateFixing, -2, cap.Calendar);
        dates.Add(expiry);
      }
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);

      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, dates.ToArray(), guesses,
        VolatilityType.LogNormal, rateIndex);
      cube.SetIsFlatCube(true);
      var marketPvs = CalculateCapPvs(curve, logNormalVols_, maturities_, cap, asOf, settle, VolatilityType.LogNormal);
      for(int i=0;i<maturities_.Length;i++)
      {
        cap.Maturity = maturities_[i];
        var capFloorPricer = new CapFloorPricer(cap, asOf, settle, curve, curve, cube);
        Assert.AreEqual(marketPvs[i], capFloorPricer.Pv(), 2e-7,
                        String.Format("Pvs dont match for cap maturing on {0}", maturities_[i]));
      }

    }

    /// <summary>
    /// Test case that checks if the piecewise constant guess values are correct for the Normal Volatilities 
    /// </summary>
    [Test]
    public void TestCapPcGuessesNormal()
    {
      Dt asOf = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      double[] guesses;
      TestRateVolatilityCurveBuilderUtil.CalculateCapPcGuesses(asOf, normalVols_, maturities_, curve, cap, VolatilityType.Normal, out guesses);
      var dates = new List<Dt> { settle };
      for (int i = 0; i < maturities_.Length - 1; i++)
      {
        Dt rateFixing = maturities_[i];
        Dt expiry = Dt.AddDays(rateFixing, -2, cap.Calendar);
        dates.Add(expiry);
      }
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);

      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, dates.ToArray(), guesses,
        VolatilityType.Normal, rateIndex);
      cube.SetIsFlatCube(true);
      var marketPvs = CalculateCapPvs(curve, normalVols_, maturities_, cap, asOf, settle, VolatilityType.Normal);
      for (int i = 0; i < maturities_.Length; i++)
      {
        cap.Maturity = maturities_[i];
        var capFloorPricer = new CapFloorPricer(cap, asOf, settle, curve, curve, cube);
        Assert.AreEqual(marketPvs[i], capFloorPricer.Pv(), 1e-7,
                        String.Format("Pvs dont match for cap maturing on {0}", maturities_[i]));
      }
    }


    [Test]
    public void TestCapletCurveBootstrapLogNormal()
    {
      Dt asOf = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      Curve bsCurve;
      double[] fitErrors;
      TestRateVolatilityCurveBuilderUtil.BootstrapCapletCurve(asOf,logNormalVols_,maturities_,curve,cap,VolatilityType.LogNormal, 0.05,out bsCurve,out fitErrors);

      var dates = new Dt[bsCurve.Count];
      var vols = new double[bsCurve.Count];
      for(int i=0;i<dates.Length;i++)
      {
        dates[i] = bsCurve.GetDt(i);
        vols[i] = bsCurve.GetVal(i);
      }
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, dates, vols, VolatilityType.LogNormal,
                                                                      rateIndex);
      for(int i=0;i<maturities_.Length;i++)
      {
        cap.Maturity = maturities_[i];
        Assert.AreEqual(fitErrors[i], 0.0, 5e-3,
                        String.Format("Volatilities dont match for maturity {0}", maturities_[i]));
      }
    }

    [Test]
    public void TestCapletCurveBootstrapNormal()
    {
      Dt asOf = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Dt effective = new Dt(20, 03, 2011);

      Dt maturity = Dt.Add(settle, "1Y");
      Cap cap = new Cap(effective, maturity, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      Curve bsCurve;
      double[] fitErrors;
      TestRateVolatilityCurveBuilderUtil.BootstrapCapletCurve(asOf, normalVols_, maturities_, curve, cap, VolatilityType.Normal, 0.05, out bsCurve, out fitErrors);

      var dates = new Dt[bsCurve.Count];
      var vols = new double[bsCurve.Count];
      for (int i = 0; i < dates.Length; i++)
      {
        dates[i] = bsCurve.GetDt(i);
        vols[i] = bsCurve.GetVal(i);
      }
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"),  Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, dates, vols, VolatilityType.Normal,
                                                                       rateIndex);
      for (int i = 0; i < maturities_.Length; i++)
      {
        Assert.AreEqual(fitErrors[i], 0.0, 5e-3,
                        String.Format("Volatilities dont match for maturity {0}", maturities_[i]));
      }
    }

    /// <summary>
    /// 
    /// </summary>
    [Test]
    public void TestFwdVolCubeCapVolatility1Pc()
    {
      double[,] capVolatilities = new double[maturities_.Length, capStrikes_.Length];
      var capVols = LogNormalCapVols();
      int idx = 0;
      for (int i = 0; i < capStrikes_.Length; i++)
      {
        for (int j = 0; j < maturities_.Length; j++)
          capVolatilities[j, i] = capVols[idx++];
      }

      var capVols1Pc = new double[maturities_.Length];
      for(int i=0;i<capVols1Pc.Length;i++)
        capVols1Pc[i] = capVolatilities[i, 0];

      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");

      Dt asOf = new Dt(16,12,2010);
      Dt settle  = new Dt(20,12,2010);
      double[] lambdaCaps = ArrayUtil.Generate(capStrikes_.Length, (i)=>0.05);

      Cap cap = new Cap(settle, Dt.Empty, Currency.USD, CapFloorType.Cap, 0.01, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var bootstrapCalibrator = new RateVolatilityCapBootstrapCalibrator(asOf, settle, discountCurve, dt=>cap.RateIndex, dt=>discountCurve,
                                                                          VolatilityType.LogNormal,
                                                                          new Dt[] { }, new double[] { }, new double[] { },
                                                                          null, null, maturities_, capStrikes_,
                                                                          capVolatilities, new double[] { },
                                                                          lambdaCaps,
                                                                          VolatilityBootstrapMethod.PiecewiseQuadratic);

      var cube = new RateVolatilityCube(bootstrapCalibrator);
      cube.Fit();

      Curve capletCurve;
      double[] fitErrors;
      Dt effective = Dt.Add(Cap.StandardSettle(asOf, 2, cap.Calendar), "3M");
      cap.Effective = effective;
      //now construct a volatility curve 
      TestRateVolatilityCurveBuilderUtil.BootstrapCapletCurve(asOf, capVols1Pc, maturities_, discountCurve, cap,
                                                              VolatilityType.LogNormal, 0.05, out capletCurve,
                                                              out fitErrors);
      for (int i = 0; i < maturities_.Length;i++ )
      {
        cap.Maturity = maturities_[i];
        Assert.AreEqual(fitErrors[i], 0.0, 1e-2,
                        String.Format("Bootstrapped vols dont match value for maturity {0}", maturities_[i]));

      }

    }

    /// <summary>
    /// 
    /// </summary>
    [Test]
    public void TestFwdVolCubeCapVolatility15Pc()
    {
      var capVolatilities = new double[maturities_.Length, capStrikes_.Length];
      var capVols = LogNormalCapVols();
      int idx = 0;
      for (int i = 0; i < capStrikes_.Length; i++)
      {
        for (int j = 0; j < maturities_.Length; j++)
          capVolatilities[j, i] = capVols[idx++];
      }

      var capVols1Pc = new double[maturities_.Length];
      for (int i = 0; i < capVols1Pc.Length; i++)
        capVols1Pc[i] = capVolatilities[i, 1];

      var discountCurve = LoadDiscountCurve("data/USD.LIBOR_Data101216.xml");

      Dt asOf = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      double[] lambdaCaps = ArrayUtil.Generate(capStrikes_.Length, (i) => 0.05);

      Cap cap = new Cap(settle, Dt.Empty, Currency.USD, CapFloorType.Cap, 0.015, DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = true,
        CycleRule = CycleRule.None
      };
      var bootstrapCalibrator = new RateVolatilityCapBootstrapCalibrator(asOf, settle, discountCurve, dt=> cap.RateIndex, dt=>discountCurve,
                                                                          VolatilityType.LogNormal,
                                                                          new Dt[] { }, new double[] { }, new double[] { },
                                                                          null, null, maturities_, capStrikes_,
                                                                          capVolatilities, new double[] { },
                                                                          lambdaCaps, VolatilityBootstrapMethod.PiecewiseQuadratic);

      var cube = new RateVolatilityCube(bootstrapCalibrator);
      cube.Fit();

      Curve capletCurve;
      double[] fitErrors;
      Dt effective = Dt.Add(Cap.StandardSettle(asOf, 2, cap.Calendar), "3M");
      cap.Effective = effective;
      //now construct a volatility curve 
      TestRateVolatilityCurveBuilderUtil.BootstrapCapletCurve(asOf, capVols1Pc, maturities_, discountCurve, cap,
                                                              VolatilityType.LogNormal, 0.05, out capletCurve,
                                                              out fitErrors);
      for (int i = 0; i < maturities_.Length; i++)
      {
        cap.Maturity = maturities_[i];
        cap.Maturity = maturities_[i];
        Assert.AreEqual(fitErrors[i], 0.0, 1e-2,
                        String.Format("Bootstrapped vols dont match value for maturity {0}", maturities_[i]));

      }

    }

    

    
    private static double[] CalculateCapPvs(DiscountCurve curve,double[]vols,Dt[]maturities,Cap cap,Dt asOf,Dt settle,VolatilityType volType)
    {
      var result = new double[maturities.Length];
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      for(int i=0;i<maturities.Length;i++)
      {
        cap.Maturity = maturities[i];
        var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { maturities[i] },
                                                                         new double[] {vols[i]}, volType,
                                                                        rateIndex);
        var capFloorPricer = new CapFloorPricer(cap, asOf, settle, curve, curve, cube);
        result[i] = capFloorPricer.Pv();
      }
      return result;
    }

    




    public static object RunStaticMethod(System.Type t,string strMethod,object[] aobjParams)
    {
      BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
      return RunMethod(t, strMethod, null, aobjParams, flags);
    }

    private static object RunMethod(System.Type t,string strMethod,object objInstance,object[]aobjParams,BindingFlags flags)
    {
      MethodInfo m;
      try
      {
        m = t.GetMethod(strMethod, flags);
        if(m==null)
        {
          throw new ArgumentException(String.Format("No Method found by the name {0} for type {1}",strMethod,t.ToString()));
        }
        object ret = m.Invoke(objInstance, aobjParams);
        return ret;
      }catch
      {
        throw;
      }
    }

    

    private static double[] LogNormalCapVols()
    {
      var capVols = new double[]{0.926,0.775,0.684,0.6435,0.61,0.5555,0.5075,0.4545,0.4385,0.8985,0.7515,
                                   0.6425,0.58,0.5415,0.486,0.4415,0.394,0.37,0.879,0.7385,0.6135,0.532,0.49,0.4345,0.393,0.35,0.3285,
                                   0.852,0.7245,0.5745,0.465,0.419,0.3625,0.3265,0.2895,0.2735,0.842,0.7205,0.56,0.4425,0.3955,0.338,
                                   0.3035,0.269,0.2535, 0.8335,0.717,0.548,0.426,0.378,0.32,0.2865,0.253,0.2385,0.819,0.712,0.529,
                                   0.405,0.358,0.2975,0.2645,0.2315,0.218,0.8075,0.708,0.5145,0.394,0.35,0.288,0.254,0.2215,0.2075,
                                   0.798,0.705,0.503,0.3885,0.348,0.286,0.251,0.218,0.2035,0.7895,0.7025,0.4935,0.386,0.349,0.2875,
                                   0.252,0.218,0.201};
      return capVols;
    }
    

    private double[] logNormalVols_;
    private double[] normalVols_;
    private Dt[] maturities_;
    private double[] capStrikes_;
    
  }
}
