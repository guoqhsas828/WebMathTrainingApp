//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture("TestCurveConsistency001")]
  [TestFixture("TestCurveConsistency002")]
  [TestFixture("TestCurves")]
  [Smoke]
  public class TestCurves : ToolkitTestBase
  {
     public TestCurves(string name) : base(name) {}

    private const double epsilon = 1e-6;

    #region Constructors

    [OneTimeSetUp]
    public void
    Init()
    {
      // Initialisation for tests.
      // Turn off stdout for exceptions
      // TBD. RTD. Nov04
    }

    #endregion Constructors

    #region Helpers
    /// <summary>
    ///   Add prices calculated at a fixed rate, based on the curve day count and frequency,
    ///   to a curve and return the prices for the check of consistency
    /// </summary>
    /// <param name="c">The curve</param>
    /// <param name="dates">An array of dates</param>
    /// <param name="rate">The rate</param>
    /// <returns>The prices added to the curve</returns>
    private static double[] AddRate(Curve c, Dt[] dates, double rate)
    {
      double[] prices = new double[dates.Length];
      for (int i = 0; i < dates.Length; ++i)
      {
        double price = RateCalc.PriceFromRate(rate, c.AsOf, dates[i], c.DayCount, c.Frequency);
        c.Add(dates[i], price);
        prices[i] = price;
      }
      return prices;
    }

    /// <summary>
    ///   Calculate the new price with a spread added to the original rate
    /// </summary>
    /// <param name="price">Original price</param>
    /// <param name="spread">Spread to add</param>
    /// <param name="start">Start date to calc the rate</param>
    /// <param name="end">End date to calculate the rate</param>
    /// <param name="dc">Day count to calculate the rate</param>
    /// <param name="freq">frequency to calculate the rate</param>
    /// <returns>The new price</returns>
    private double AddSpread(double price, double spread,
      Dt start, Dt end, DayCount dc, Frequency freq)
    {
      double rate = RateCalc.RateFromPrice(price, start, end, dc, freq) + spread;
      return RateCalc.PriceFromRate(rate, start, end, dc, freq);
    }

    #endregion Helpers

    #region Tests

    [Test, Smoke]
    public void TestInterpolationConsistency()
    {
      Dt asOf = new Dt(1, 1, 2000);
      Curve c = new Curve(asOf);
      c.DayCount = CurveDayCount;
      c.Frequency = CurveFrequency;
      c.Spread = CurveSpread;

      AssertEqual("AsOf", c.AsOf, asOf);
      AssertEqual("DayCount", c.DayCount, CurveDayCount);
      AssertEqual("Frequency", c.Frequency, CurveFrequency);
      AssertEqual("Spread", c.Spread, CurveSpread);

      // Add points to curve
      const double rate = 0.05;
      Dt[] dates = new Dt[]{
        new Dt(1, 4, 2000),
        new Dt(1, 1, 2001),
        new Dt(1, 1, 2002),
        new Dt(1, 1, 2003)
      };
      double[] values = AddRate(c, dates, rate);

      // Check contents of Curve: GetVal() and Interpolate() should return the original prices
      Assert.AreEqual(c.Count, dates.Length);
      for (int i = 0; i < dates.Length; ++i)
      {
        AssertEqual("Date " + i, dates[i], c.GetDt(i));
        AssertEqual("Point " + i, values[i], c.GetVal(i));
        AssertEqual("Interpolate " + i, values[i], c.Interpolate(c.GetDt(i)));
      }

      c.Clear();
      AssertEqual("Count after clear", c.Count, 0);

      return;
    }

    [Test, Smoke]
    public void TestDefaultInterpolation()
    {
      Dt asOf = new Dt(1,1,2000);
      Curve c = new Curve(asOf);

      Assert.IsTrue( Dt.Cmp(c.AsOf, asOf) == 0 );

      // Add points to curve
      c.Add(new Dt(1,4,2000), 100.0);
      c.Add(new Dt(1,1,2001), 2.0);
      c.Add(new Dt(1,1,2002), 4.0);
      c.Add(new Dt(1,1,2003), 6.0);

      // Update values
      c.SetDt(0, asOf);
      c.SetVal(0, 0.0);

      // Check contents of Curve
      Assert.AreEqual( c.Count, 4 );
      Assert.IsTrue( Dt.Cmp(c.GetDt(0), asOf) == 0 );
      Assert.AreEqual( 0.0, c.GetVal(0) );
      Assert.IsTrue( Dt.Cmp(c.GetDt(3), new Dt(1,1,2003)) == 0 );
      Assert.AreEqual( 6.0, c.GetVal(3) );

      // Test linear interpolation
      // See the associated test spreadsheet for calculations.
      Assert.AreEqual(2.0, c.Interpolate(new Dt(1, 1, 2001)), epsilon);
      Assert.AreEqual(2.99178082, c.Interpolate(new Dt(1, 7, 2001)), epsilon);
      Assert.AreEqual(0.99453552, c.Interpolate(new Dt(1, 7, 2000)), epsilon);
      Assert.AreEqual(6.0, c.Interpolate(new Dt(1, 1, 2010)), epsilon);
      Assert.IsTrue( Dt.Cmp(c.Solve(2.0), new Dt(1,1,2001)) == 0);

      c.Clear();
      Assert.AreEqual(c.Count, 0);

      return;
    }

    [Test, Smoke]
    public void TestExplicitInterpolation()
    {
      Dt asOf = new Dt(1,1,2000);
      Curve c = new Curve(asOf, InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                          DayCount.Actual365Fixed, Frequency.Continuous);

      Assert.IsTrue( Dt.Cmp(c.AsOf, asOf) == 0 );

      // Add points to curve
      c.Add(new Dt(1,4,2000), 100.0);
      c.Add(new Dt(1,1,2001), 0.6);
      c.Add(new Dt(1,1,2002), 0.4);
      c.Add(new Dt(1,1,2003), 0.2);

      // Update values
      c.SetDt(0, asOf);
      c.SetVal(0, 1.0);

      // Check contents of Curve
      Assert.AreEqual( 4, c.Count );
      Assert.IsTrue( Dt.Cmp(c.GetDt(0), asOf) == 0 );
      Assert.AreEqual( 1.0, c.GetVal(0) );
      Assert.IsTrue( Dt.Cmp(c.GetDt(3), new Dt(1,1,2003)) == 0 );
      Assert.AreEqual( 0.2, c.GetVal(3) );

      // Test linear interpolation of spot rate.
      // See the associated test spreadsheet for calculations.
      c.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      Assert.AreEqual(0.88133760, c.Interpolate(new Dt(1, 7, 2000)), epsilon);
      Assert.AreEqual(0.6, c.Interpolate(new Dt(1, 1, 2001)), epsilon);
      Assert.AreEqual(0.57735564, c.Interpolate(new Dt(1, 2, 2001)), epsilon);
      Assert.AreEqual(0.55799721, c.Interpolate(new Dt(1, 3, 2001)), epsilon);
      Assert.AreEqual(0.53770393, c.Interpolate(new Dt(1, 4, 2001)), epsilon);
      Assert.AreEqual(0.51913829, c.Interpolate(new Dt(1, 5, 2001)), epsilon);
      Assert.AreEqual(0.50099599, c.Interpolate(new Dt(1, 6, 2001)), epsilon);
      Assert.AreEqual(0.48438810, c.Interpolate(new Dt(1, 7, 2001)), epsilon);
      Assert.AreEqual(0.00468072, c.Interpolate(new Dt(1, 1, 2010)), epsilon);
      Assert.IsTrue( Dt.Cmp(c.Solve(0.6), new Dt(1,1,2001)) == 0 );

      // Test weighted interpolation of spot rate.
      // See the associated test spreadsheet for calculations.
      c.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      Assert.AreEqual(0.77567853, c.Interpolate(new Dt(1, 7, 2000)), epsilon);
      Assert.AreEqual(0.6, c.Interpolate(new Dt(1, 1, 2001)), epsilon);
      Assert.AreEqual(0.57968966, c.Interpolate(new Dt(1, 2, 2001)), epsilon);
      Assert.AreEqual(0.56193642, c.Interpolate(new Dt(1, 3, 2001)), epsilon);
      Assert.AreEqual(0.54291456, c.Interpolate(new Dt(1, 4, 2001)), epsilon);
      Assert.AreEqual(0.52511961, c.Interpolate(new Dt(1, 5, 2001)), epsilon);
      Assert.AreEqual(0.50734401, c.Interpolate(new Dt(1, 6, 2001)), epsilon);
      Assert.AreEqual(0.49071494, c.Interpolate(new Dt(1, 7, 2001)), epsilon);
      Assert.AreEqual(0.00468072, c.Interpolate(new Dt(1, 1, 2010)), epsilon);

      c.Clear();
      Assert.AreEqual( 0, c.Count );

      return;
    }

    /// <summary>
    ///   Test setting of spread for curves
    /// </summary>
    [Test, Smoke]
    public void TestSpread()
    {
      Dt asOf = new Dt(1, 1, 2000);

      // Build curve with the configurable inputs
      Curve c = new Curve(asOf, InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                          CurveDayCount, CurveFrequency);
      double spread = (CurveSpread == 0 ? 0.01 : CurveSpread);

      // Test the default case
      c.Add(new Dt(1, 1, 2001), 0.6);
      AssertEqual("Count", c.Count, 1);
      AssertEqual("Price", c.GetVal(0), 0.6);

      double expect = AddSpread(c.GetVal(0), spread, asOf, c.GetDt(0), CurveDayCount, CurveFrequency);
      c.Spread = spread;
      string label = CurveFrequency.ToString() + " " + CurveDayCount.ToString();
      AssertEqual(label + " get", expect, c.GetVal(0), epsilon);
      AssertEqual(label + " interp", expect, c.Interpolate(c.GetDt(0)), epsilon);
      c.Spread = 0.0; // restore the original spread

      // Test interpolation using Simple Act/365 spread
      Curve c1 = new Curve(c.AsOf, c.Interp, DayCount.Actual365Fixed, Frequency.None);
      c1.Add(c.GetDt(0), c.GetVal(0));

      expect = AddSpread(c.GetVal(0), spread, asOf, c.GetDt(0), DayCount.Actual365Fixed, Frequency.None);
      c1.Spread = spread;
      AssertEqual("Simple Act/365 get", expect, c1.GetVal(0), epsilon);
      AssertEqual("Simple Act/365 interp", expect, c1.Interpolate(c.GetDt(0)), epsilon);

      // Test interpolation using SA Act/360 spread
      c1 = new Curve(asOf, InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
              DayCount.Actual360, Frequency.SemiAnnual);
      c1.Add(c.GetDt(0), c.GetVal(0));

      expect = AddSpread(c.GetVal(0), spread, asOf, c.GetDt(0), DayCount.Actual360, Frequency.SemiAnnual);
      c1.Spread = spread;
      AssertEqual("SA Act/360 get", expect, c1.GetVal(0), epsilon);
      AssertEqual("SA Act/360 interp", expect, c1.Interpolate(c.GetDt(0)), epsilon);

      return;
    }

    /// <summary>
    ///   Test curve set method (copy and decouple curve)
    /// </summary>
    [Test, Smoke]
    public void TestOfSetMethod()
    {
      Dt asOf = new Dt(1,1,2000);
      Curve c = new Curve(asOf, InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                          DayCount.Actual365Fixed, Frequency.Continuous);

      Assert.IsTrue( Dt.Cmp(c.AsOf, asOf) == 0 );

      // Add points to curve
      c.Add(new Dt(1,4,2000), 100.0);
      c.Add(new Dt(1,1,2001), 0.6);
      c.Add(new Dt(1,1,2002), 0.4);
      c.Add(new Dt(1,1,2003), 0.2);

      // Create curve to set
      Curve c2 = new Curve(new Dt(1, 2, 2000));
      c2.Add(new Dt(1,4,2000), 5.0);

      // Set curve from original
      c2.Set(c);

      // Verify curve is copied.
      Assert.AreEqual( c.AsOf, c2.AsOf );
      Assert.AreEqual( c.DayCount, c2.DayCount );
      Assert.AreEqual( c.Frequency, c2.Frequency );
      Assert.AreEqual( c.Count, c2.Count );
      for( int i = 0; i < c.Count; i++ )
      {
        Assert.AreEqual( c.GetDt(i), c2.GetDt(i) );
        Assert.AreEqual( c.GetVal(i), c2.GetVal(i) );
      }

      // Verify data is decoupled
      c.Clear();
      Assert.AreEqual( 0, c.Count );
      Assert.AreEqual( 4, c2.Count );

      return;
    }

    /// <summary>
    ///  Test curve solver
    /// </summary>
    [Test, Smoke]
    public void TestCurveSolve()
    {
      Dt asOf = new Dt(1,1,2000);
      Curve c = new Curve(asOf, InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                          DayCount.Actual365Fixed, Frequency.Continuous);

      Assert.IsTrue( Dt.Cmp(c.AsOf, asOf) == 0 );

      // Add points to curve
      c.Add(asOf, 1.0);
      c.Add(new Dt(1,1,2001), Math.Exp(-0.02));
      c.Add(new Dt(1,1,2002), Math.Exp(-0.04));
      c.Add(new Dt(1,1,2003), Math.Exp(-0.06));

      // Solve for a date with value very close to 1
      Dt defaultDt = c.Solve(1.0 - 1.0e-6);

      // the date should be no later than as-of date
      Assert.IsTrue( Dt.Cmp(defaultDt, asOf) >= 0 );

      return;
    }

    /// <summary>
    ///   Test we trap contruction using invalid as-of date
    /// </summary>
    [Test, Smoke]
    public void InvalidAsOf()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt();
        Curve c = new Curve(asOf);
      });
    }
    
    /// <summary>
    ///   Test we trap setting invalid as-of dates
    /// </summary>
    [Test, Smoke]
    public void InvalidSetAsOf()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt();
        Curve c = new Curve();
        c.AsOf = asOf;
      });
    }

    /// <summary>
    ///   Test we don't allow invalid tenor dates
    /// </summary>
    [Test, Smoke]
    public void InvalidTenorDate()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);

        c.Add(new Dt(), 1.0);
      });
    }

    /// <summary>
    ///   Test we don't allow tenors to be added before the curve as-of date
    /// </summary>
    [Test, Smoke]
    public void TenorDateBeforeAsOf()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);

        c.Add(new Dt(1, 1, 2000), 1.0);
      });
    }

    /// <summary>
    ///   Test we trap adding curve tenors out of order
    /// </summary>
    [Test, Smoke]
    public void TenorOutOfOrder()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);

        c.Add(new Dt(1, 1, 2004), 1.0);
        c.Add(new Dt(1, 1, 2003), 1.0);
      });
    }

    /// <summary>
    ///   Test we don't allow setting of the daycount after points have been added to the curve
    /// </summary>
    [Test, Smoke]
    public void ChangingDaycount()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);

        c.Add(new Dt(1, 1, 2004), 1.0);
        c.DayCount = DayCount.ActualActualEuro;
      });
    }

    /// <summary>
    ///   Test we don't allow setting of the frequency after points have been added to the curve
    /// </summary>
    [Test, Smoke]
    public void ChangingFrequency()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);

        c.Add(new Dt(1, 1, 2004), 1.0);
        c.Frequency = Frequency.Monthly;
      });
    }

    /// <summary>
    ///   Test that we don't allow setting the same interp object as in the curve
    /// </summary>
    [Test, Smoke]
    public void ChangeInterp()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Curve c = new Curve(asOf);
        c.Interp = c.Interp;
      });
    }

    /// <summary>
    ///   Test the protected curve copy function
    /// </summary>
    /// <remarks>
    ///   To gain access to the protected member functions,
    ///   we define a derived class, MySurvivalCurve, and work with it.
    /// </remarks>
    [Test, Smoke]
    public void TestCurveCopy()
    {
      Dt asOf = new Dt(1, 1, 2002);
      MySurvivalCurve c = new MySurvivalCurve(asOf, 0.01);
      Interp interp = c.Interp;

      // copy should not change the interp object ptr
      c.DoCopy(c);
      Assert.AreEqual(true, Interp.getCPtr(c.Interp).Handle == Interp.getCPtr(interp).Handle);

      // clone should change the interp object ptr (cloned)
      MySurvivalCurve c2 = (MySurvivalCurve)c.Clone();
      Assert.AreEqual(true, Interp.getCPtr(c2.Interp).Handle != Interp.getCPtr(interp).Handle);

      // set interp should make a clone of the object
      c2.Interp = interp;
      Assert.AreEqual(true, Interp.getCPtr(c2.Interp).Handle != Interp.getCPtr(interp).Handle);
    }
    internal class MySurvivalCurve : SurvivalCurve
    {
      internal MySurvivalCurve(Dt asOf, double rate) : base(asOf, rate) { }
      internal void DoCopy(MySurvivalCurve c) { Copy(c); }
    }

    [Test, Smoke]
    public void TestImplicitCast()
    {
      Dt asOf=Dt.Today();
      var curve = new SurvivalCurve(asOf, 0.05);
      var func = CastToFunc(curve);
      foreach(var tenor in new[]{"3M", "6M", "1Y", "10Y"})
      {
        var dt = Dt.Add(asOf, tenor);
        Assert.AreEqual(curve.Interpolate(dt), func(dt), 2E-16, tenor);
      }

      // make sure null curve works
      curve = null;
      Assert.IsNull(CastToFunc(curve));
    }

    private static Func<Dt,double> CastToFunc(Func<Dt,double> f)
    {
      return f;
    }
    #endregion Tests

    #region Properties
    /// <summary>
    ///   Curve Day count
    /// </summary>
    public DayCount CurveDayCount { get; set; } = DayCount.Actual365Fixed;

    /// <summary>
    ///  Curve Frequency
    /// </summary>
    public Frequency CurveFrequency { get; set; } = Frequency.Continuous;

    /// <summary>
    ///   Curve spread
    /// </summary>
    public double CurveSpread { get; set; } = 0;

    #endregion Properties

    #region Data

    #endregion Data

  }
}
