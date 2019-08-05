//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base.ReferenceIndices;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Curves
{
  /// <summary>
  /// Test class for testing the rate volatility cube 
  /// </summary>
  [TestFixture]
  public class TestRateVolatilityCube : ToolkitTestBase
  {
    [OneTimeSetUp]
    public void Initialize()
    {
      logNormalVols_ = new double[] {0.8707, 0.6432, 0.5877, 0.5214, 0.4785, 0.4361, 0.4015, 0.3780, 0.3851};
      dates_ = new Dt[]{new Dt(20,12,2011),new Dt(20,12,2012),new Dt(20,12,2013),new Dt(20,12,2014),new Dt(20,12,2015),new Dt(20,12,2017),
      new Dt(20,12,2020),new Dt(20,12,2025),new Dt(20,12,2030)};
      curve_ = new Curve(new Dt(16,12,2010))
                 {
                   Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const)
                 };

      for(int i=0;i<dates_.Length;i++)
        curve_.Add(dates_[i],logNormalVols_[i]);
      asOf_ = new Dt(16,12,2010);
    }

    /// <summary>
    /// This test case checks if the 
    /// </summary>
    [Test]
    public void TestFlatCapletVolatility()
    {
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates_, logNormalVols_,
                                                                       VolatilityType.LogNormal, rateIndex);
      for (int i = 0; i < dates_.Length; i++)
        Assert.AreEqual(curve_.Interpolate(dates_[i]), cube.CapletVolatility(dates_[i], 0.01), 1e-8,
                        String.Format("caplet volatilities does not match for date {0}", dates_[i]));


    }

    /// <summary>
    /// This test case checks if the caplet volatility when the interpolation scheme is flat between the expiries 
    /// This is useful when one constructs a volatility cube using piecewise constant method 
    /// </summary>
    [Test]
    public void TestFlatCapletVolatility2()
    {
      var dates = new List<Dt>();
      dates.Add(asOf_);
      for(int i=0;i<dates_.Length-1;i++)
        dates.Add(dates_[i]);
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);

      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates.ToArray(), logNormalVols_,
                                                                       VolatilityType.LogNormal, rateIndex);
      cube.SetIsFlatCube(true);
      
      
      var curve = new Curve(asOf_)
                    {
                      Interp = new Flat(1.0)
                    };
      curve.Add(dates.ToArray(),logNormalVols_);

      var dtArray = new Dt[] {new Dt(01, 01, 2012), new Dt(01, 01, 2013), new Dt(01, 01, 2014)};
      for (int i = 0; i <dtArray.Length; i++)
        Assert.AreEqual(curve.Interpolate(dtArray[i]), cube.CapletVolatility(dtArray[i], 0.01), 1e-8,
                        String.Format("Caplet Volatilities do not match for date {0}", dtArray[i]));

      
    }

    /// <summary>
    /// Test case to ensure that caplet vols are 0 on the as of date . 
    /// </summary>
    [Test]
    public void TestCapletVolatilityLeftExtrap1()
    {
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates_, logNormalVols_,
                                                                       VolatilityType.LogNormal, rateIndex);
      Assert.AreEqual(0.0, cube.CapletVolatility(new Dt(16, 12, 2010), 0.01), 1e-8,
                      String.Format("caplet vols should be 0 on as of date "));

      
    }

    /// <summary>
    /// Test case that ensures that the caplet vols are 0 before the as of date ( caplets that have already expired should have no volatility)
    /// </summary>
    [Test]
    public void TestCapletVolatilityLeftExtrap2()
    {
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates_, logNormalVols_,
                                                                       VolatilityType.LogNormal, rateIndex);
      Assert.AreEqual(0.0, cube.CapletVolatility(new Dt(16, 12, 2009), 0.01), 1e-8,
                      String.Format("caplet vols should be 0 before as of date "));


    }

    /// <summary>
    /// Test case that ensures that we are not extrapolating on regions we 
    /// </summary>
    [Test]
    public void TestFirstCapIdxCase1()
    {
      var dates = new Dt[]
                    {
                      new Dt(20, 3, 2011), new Dt(20, 6, 2011), new Dt(20, 12, 2011), new Dt(20, 12, 2012),
                      new Dt(20, 12, 2013)
                    };
      var vols = new double[] { 0.9, 0.8, 0.7, 0.6, 0.5 };
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates, vols, VolatilityType.LogNormal,
                                                                       rateIndex);
      cube.SetFirstCapIdx(new int[]{2});
      var curve = new Curve(asOf_)
                    {
                      Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const)
                    };
      curve.Add(new Dt(20,12,2011),0.7 );
      curve.Add(new Dt(20,12,2012),0.6 );
      curve.Add(new Dt(20, 12, 2013), 0.5);

      Assert.AreEqual(curve.Interpolate(new Dt(20, 3, 2011)), cube.CapletVolatility(new Dt(20, 3, 2011), 0.01), 1.0e-8,
                      String.Format("Caplet vols do not match"));
      Assert.AreEqual(curve.Interpolate(new Dt(20, 6, 2012)), cube.CapletVolatility(new Dt(20, 6, 2012), 0.01), 1.0e-8, String.Format("Caplet Vols do not match"));
      Assert.AreEqual(curve.Interpolate(new Dt(20, 6, 2013)), cube.CapletVolatility(new Dt(20, 6, 2013), 0.01), 1.0e-8, String.Format("Caplet Vols do not match"));
      Assert.AreEqual(curve.Interpolate(new Dt(20, 6, 2014)), cube.CapletVolatility(new Dt(20, 6, 2014), 0.01), 1.0e-8, String.Format("Caplet Vols do not match"));


    }

    /// <summary>
    /// Test case that ensures that we are not extrapolating on regions we 
    /// </summary>
    [Test]
    public void TestFirstCapIdxCase2()
    {
      var dates = new Dt[]
                    {
                      new Dt(20, 3, 2011), new Dt(20, 6, 2011), new Dt(20, 12, 2011), new Dt(20, 12, 2012),
                      new Dt(20, 12, 2013)
                    };
      var vols = new double[] { 0.9, 0.8, 0.7, 0.6, 0.5 };
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.None,
                                            DayCount.Actual360, Calendar.None, BDConvention.Following, 2);
      var cube = RateVolatilityCube.CreateFlatVolatilityCube(asOf_, dates, vols, VolatilityType.LogNormal,
                                                                       rateIndex);
      
      var curve = new Curve(asOf_)
      {
        Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const)
      };
      curve.Add(new Dt(20, 12, 2011), 0.7);
      curve.Add(new Dt(20, 12, 2012), 0.6);
      curve.Add(new Dt(20, 12, 2013), 0.5);

      Assert.AreNotEqual(curve.Interpolate(new Dt(20, 3, 2011)), cube.CapletVolatility(new Dt(20, 3, 2011), 0.01),
                      String.Format("Caplet vols do not match"));
      
    }







    private double[] logNormalVols_;
    private Dt[] dates_;
    private Curve curve_;
    private Dt asOf_;


  }
}
