// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  /// TestPointCalibration class.
  /// </summary>
  [TestFixture]
  public class TestSimpleCalibration : ToolkitTestBase
  {
    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestSimpleCalibration));
    #endregion

    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    public TestSimpleCalibration()
    {
    }
    #endregion

    #region Tests
    /// <summary>
    /// Tests a Curve instance against an equivalent CalibratedCurve instance using a SimpleCalibrator.
    /// </summary>
    [Test, Smoke]
    public void TestSimpleCurve()
    {
      Dt asOf = new Dt(1, 1, 2009);
      Curve baseCurve = new Curve(asOf);
      RateCurve calibratedCurve = new RateCurve(asOf);

      // setup curves
      baseCurve.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      baseCurve.Frequency = Frequency.Annual;
      baseCurve.DayCount = DayCount.Actual365Fixed;
      calibratedCurve.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      calibratedCurve.Frequency = Frequency.Annual;
      calibratedCurve.DayCount = DayCount.Actual365Fixed;
      for (int i = 0; i < 5; i++)
      {
        Dt maturity = Dt.Add(asOf, i+1, TimeUnit.Years);
        baseCurve.Add(maturity, i+1);
        calibratedCurve.AddRate(maturity, i+1);
      }

      // Fit calibrated curve
      calibratedCurve.Fit();

      // Test interpolation after calibration
      for (int i = 0; i < 72; i++)
      {
        Dt date = Dt.AddMonth(asOf, i, true);
        Assert.AreEqual(
          baseCurve.Interpolate(date), 
          calibratedCurve.Interpolate(date),
          String.Format("Month {0}", i));
      }
    }

    #endregion
  }
}
