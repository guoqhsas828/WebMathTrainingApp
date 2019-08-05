//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture("Bootstrap_CAD_RateBumpsConsistency")]
  [TestFixture("Bootstrap_EUR_RateBumpsConsistency")]
  [TestFixture("Bootstrap_GBP_RateBumpsConsistency")]
  [TestFixture("Bootstrap_JPY_RateBumpsConsistency")]
  [TestFixture("Bootstrap_USD_RateBumpsConsistency")]
  [TestFixture("BootstrapSmooth_RateBumpsConsistency")]

  public class TestRateCurveConsistency : SensitivityTest
  {
    public TestRateCurveConsistency(string name) : base(name)
    {}

    #region Data
    // default data
    private const int DefaultAsOf = 20110609;

    private string rateDataFile_ = "data/comac1_ir_data.csv";
    private string tenor_ = null;
    private string index_ = null;
    private string extrap_ = null;
    private CurveFitMethod fitMethod_ = CurveFitMethod.Bootstrap;
    //private InterpScheme interpScheme_ = InterpScheme.FromString("Weighted",
    //  ExtrapMethod.Const, ExtrapMethod.Const);

    // loaded data
    private RateCurveData rateData_;
    #endregion Data

    #region Properties
    /// <summary>
    ///   Sets the file containing the data for building interest rate curves.
    /// </summary>
    /// <value>The rate data file.</value>
    public string RateDataFile { set { rateDataFile_ = value; } }
    /// <summary>
    /// Sets the index tenor.
    /// </summary>
    /// <value>The index tenor.</value>
    public string IndexTenor { set { tenor_ = value; } }
    /// <summary>
    /// Sets the index of the rate.
    /// </summary>
    /// <value>The index of the rate.</value>
    public string RateIndex { set { index_ = value; } }
    /// <summary>
    /// Sets the curve fit method.
    /// </summary>
    /// <value>The curve fit method.</value>
    public CurveFitMethod CurveFitMethod { set { fitMethod_ = value; } }
    public string ExtrapScheme {set{ extrap_ = value;}}
    #endregion Properties

    private DiscountCurve BuildDiscountCurve(
      CurveFitMethod fitMethod, InterpScheme interpScheme)
    {
      if (rateData_ == null)
        rateData_ = RateCurveData.LoadFromCsvFile(rateDataFile_);
      Dt asOf = new Dt(PricingDate != 0 ? PricingDate : DefaultAsOf);
      Currency ccy = this.Currency;
      if (ccy == Currency.None) ccy = Currency.USD;
      var tenor = tenor_;
      if (String.IsNullOrEmpty(tenor)) tenor = "3M";
      var index = index_;
      if (String.IsNullOrEmpty(index)) index = "LIBOR";
      var curveName = ccy + index;
      index = curveName + '_' + tenor;
      return rateData_.CalibrateDiscountCurve(curveName, asOf,
        index, index, fitMethod, interpScheme);
    }

    private static IPricer[] GetPricers(CalibratedCurve curve)
    {
      var libor = curve.Ccy + "LIBOR";
      var count = curve.Tenors.Count;
      var pricers = new IPricer[count];
      for (int i = 0; i < count; ++i)
      {
        var product = (IProduct)curve.Tenors[i].Product.Clone();
        var name = product.Description.Replace(libor, "Pricer");
        var pricer = pricers[i] = curve.Calibrator.GetPricer(curve, product);
        pricer.Product.Description = name;
        var pv = pricer.Pv();
        // For MM tenor we check both the discount factor and pv values.
        {
          var p = pricer as NotePricer;
          if (p != null)
          {
            Note note = (Note)p.Product;
            Dt maturity = Dt.Roll(note.Maturity, note.BDConvention, note.Calendar);
            double rate = note.Coupon;
            double delta = Dt.Fraction(p.Settle, maturity, p.Settle, maturity, note.DayCount, note.Freq);
            double df = 1/(1 + delta*rate);
            AssertEqual("Df." + name, df,
              curve.Interpolate(p.Settle, maturity), 1E-10);
            AssertEqual("Pv." + name, 1.0, pv, 1E-10);
            continue;
          }
        }
        // check for EDFuture tenor
        {
          var p = pricer as StirFuturePricer;
          if (p != null)
          {
            var f = (StirFuture)p.Product;
            if (f.LastTradingDate != Dt.Empty && p.Settle >= f.LastTradingDate)
              pricers[i] = null;
            else
            {
              var quote = curve.Tenors[i].CurrentQuote;
              var rate = 1 - quote.Value;
              AssertEqual("Rate." + name, rate, p.ModelRate(), 1E-10); 
              //AssertEqual("Pv." + name, 1 - quote.Value, pv, 1E-10);
            }
            // Ignores delta for the time being.
            pricers[i] = null;
            continue;
          }
        }
        // check for Swap tenor
        {
          var p = pricer as SwapPricer;
          if(p!=null)
          {
            AssertEqual("Pv." + name, 0.0, pv, 1E-8);
            continue;
          }
        }
      }
      return pricers.Where((p) => p != null).ToArray();
    }

    private IPricer[] BuildPricers(InterpScheme interpScheme)
    {
      var curve = BuildDiscountCurve(fitMethod_, interpScheme);
      return GetPricers(curve);
    }

    private static void CheckHedgeNotionals(DataTable table, double tolerance)
    {
      // Total
      int rows = table.Rows.Count;

      for (int i = 0; i < rows; i++)
      {
        var row = table.Rows[i];
        var pricer = (string) row["Pricer"];
        var tenor = (string) row["Curve Tenor"];
        var hedge = (double) row["Hedge Notional"];
        if (tenor.Contains(pricer.Replace("Pricer", "")))
        {
          if (hedge != 1)
            AssertEqual(pricer + '/' + tenor, 1.0, hedge, 1E-12);
        }
        else
        {
          if (Math.Abs(hedge) > tolerance)
            AssertEqual(pricer + '/' + tenor, 0.0, hedge, tolerance);
        }
      }
    }

    private void DoTest(string interp)
    {
      if (extrap_ == null) extrap_ = "Const/Smooth";
      var scheme = (interp + "; " + extrap_).ParseInterpScheme();
      var pricers = BuildPricers(scheme);
      var table = RateTable(pricers, null, false, "Keep");
      CheckHedgeNotionals(table, 1E-6);
    }

    [Test]
    public void Linear()
    {
      DoTest("Linear");
    }

    [Test]
    public void Quadratic()
    {
      DoTest("Quadratic");
    }

    [Test]
    public void Cubic()
    {
      DoTest("Cubic");
    }

    [Test]
    public void Pchip()
    {
      DoTest("PCHIP");
    }

    [Test]
    public void TensionC1()
    {
      DoTest("TensionC1");
    }

    [Test]
    public void TensionC2()
    {
      DoTest("TensionC2");
    }

    [Test]
    public void Weighted()
    {
      DoTest("Weighted");
    }

    [Test]
    public void WeightedPchip()
    {
      DoTest("WeightedPCHIP");
    }

    [Test]
    public void WeightedQuadratic()
    {
      DoTest("WeightedQuadratic");
    }

    [Test]
    public void WeightedCubic()
    {
      DoTest("WeightedCubic");
    }

    [Test]
    public void WeightedTensionC1()
    {
      DoTest("WeightedTensionC1");
    }

    [Test]
    public void WeightedTensionC2()
    {
      DoTest("WeightedTensionC2");
    }
  }
}
