// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  /// Test class that tests that the rate option param collection is generated correctly
  /// </summary>
  [TestFixture]
  public class TestRateOptionParamCollection : ToolkitTestBase
  {

    [Test, Smoke]
    public void TestCapSchedule1Y()
    {
      Dt asof = new Dt(16,12,2010);
      Dt settle = new Dt(20,12,2010);
      Dt effective = new Dt(20,03,2011);
      
      Dt maturity = Dt.Add(settle,"1Y");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
                  {
                    AccrueOnCycle = false,
                    CycleRule = CycleRule.None
                  };
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      PaymentSchedule sched = cap.GetPaymentSchedule(asof, new RateResets(new List<RateReset>()));
      DataTable dt = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      Assert.AreEqual(sched.Count, dt.Rows.Count,
                      "Number of rows on the schedule does not match the rate option param collection");
      int idx = 0;
     foreach(CapletPayment caplet in sched)
     {
       DataRow row = dt.Rows[idx];
       Assert.AreEqual(row["Date"], caplet.RateFixing, String.Format("Rate Fixing does not match start date for caplet"));
       var T = Dt.Fraction(asof, caplet.Expiry, cap.DayCount);
       Assert.AreEqual(row["Fraction"], T, String.Format("Fraction does nto match for caplet expiring on {0} ",caplet.Expiry));
       idx++;
     }
    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 1Y Cap in a 2Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule2Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "1Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 2Y maturity cap 
      maturity = Dt.Add(settle, "2Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach(DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 2Y Cap in a 3Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule3Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "2Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 3Y maturity cap 
      maturity = Dt.Add(settle, "3Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 3Y Cap in a 4Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule4Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "3Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 4Y maturity cap 
      maturity = Dt.Add(settle, "4Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 4Y Cap in a 5Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule5Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "4Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 4Y maturity cap 
      maturity = Dt.Add(settle, "5Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 5Y Cap in a 7Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule7Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "5Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 4Y maturity cap 
      maturity = Dt.Add(settle, "7Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 7Y Cap in a 10Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule10Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "7Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 4Y maturity cap 
      maturity = Dt.Add(settle, "10Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 10Y Cap in a 15Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule15Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "10Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 4Y maturity cap 
      maturity = Dt.Add(settle, "15Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

    /// <summary>
    /// The purpose of this test is to ensure that the embedded 15Y Cap in a 20Y cap has the same rate option parameters 
    /// </summary>
    [Test]
    public void TestFwdCapSchedule20Y()
    {
      Dt asof = new Dt(16, 12, 2010);
      Dt settle = new Dt(20, 12, 2010);
      Dt effective = new Dt(20, 03, 2011);
      Dt maturity = Dt.Add(settle, "15Y");
      DiscountCurve curve = LoadDiscountCurve("data/USD.LIBOR_Data101025.xml");
      Cap cap = new Cap(effective, maturity, Toolkit.Base.Currency.USD, CapFloorType.Cap, 0.0, Toolkit.Base.DayCount.Actual360,
                        Toolkit.Base.Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        AccrueOnCycle = false,
        CycleRule = CycleRule.None
      };
      DataTable dt1y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      //now construct a 20Y maturity cap 
      maturity = Dt.Add(settle, "20Y");
      cap.Maturity = maturity;

      DataTable dt2Y = TestRateVolatilityCurveBuilderUtil.RateOptionParamTable(asof, maturity, curve, cap);

      int idx = 0;
      foreach (DataRow row in dt1y.Rows)
      {
        DataRow row2 = dt2Y.Rows[idx];
        Assert.AreEqual(row["Date"], row2["Date"],
                        String.Format("Dates dont match {0} != {1}", row["Date"], row2["Date"]));
        Assert.AreEqual(row["Fraction"], row2["Fraction"],
                        String.Format("Fractions dont match {0} != {1}", row["Fraction"], row2["Fraction"]));
        Assert.AreEqual(row["Rate"], row2["Rate"],
                        String.Format("Rate does not match {0} != {1}", row["Rate"], row2["Rate"]));
        Assert.AreEqual(row["Level"], row2["Level"],
                        String.Format("Levels dont match {0} != {1}", row["Level"], row2["Level"]));
        idx++;
      }

    }

  }


}
