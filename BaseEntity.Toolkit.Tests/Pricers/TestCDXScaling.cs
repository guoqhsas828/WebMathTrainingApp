//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("TestCDXScaling", Category = "Smoke")]
  [TestFixture("TestCDXScaling002")]
  [TestFixture("TestCDXScaling003")]
  [TestFixture("TestCDXScaling004")]
  [TestFixture("TestCDXScaling005")]
  [TestFixture("TestCDXScaling006")]
  [TestFixture("TestCDXScaling007")]
  [TestFixture("TestCDXScaling008")]
  [TestFixture("TestCDXScaling009")]
  [TestFixture("TestCDXScaling010")]
  public class TestCDXScaling : ToolkitTestBase
  {
    public TestCDXScaling(string name) : base(name)
    {}

    #region Data

    private Dt asOf_;
    private Dt settle_;
    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve[] survivalCurves_ = null;

    private string[] tenors_ = null;
    private CDX[] cdx_ = null;
    private double[] quotes_ = null;
    private bool quotesArePrices_ = false;
    private double[] scalingWeights_ = null;
    private bool relativeScaling_ = false;

    #endregion // Data

    #region SetUp

    /// <summary>
    ///    Initializer
    /// </summary>
    /// 
    /// <remarks>
    /// This function is called once after a class object is constructed
    /// and before all the tests in this fixture.
    /// </remarks>
    /// 
    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(BasketFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      asOf_ = Dt.FromStr(bd.AsOf, "%D");
      settle_ = Dt.FromStr(bd.Settle, "%D");
      relativeScaling_ = !(bd.IndexData.AbsoluteScaling);
      discountCurve_ = bd.GetDiscountCurve();
      if (bd.CreditData != null)
        survivalCurves_ = bd.CreditData.GetSurvivalCurves(discountCurve_);

      Initialize(bd.IndexData);
    }

    /// <summary>
    ///   Initialize index data
    /// </summary>
    /// <param name="id">index data</param>
    private void Initialize(BasketData.Index id)
    {
      // Check survivalcurves
      if (id.CreditNames != null && id.CreditNames.Length != survivalCurves_.Length)
      {
        SurvivalCurve[] sc = survivalCurves_;
        survivalCurves_ = new SurvivalCurve[id.CreditNames.Length];
        int idx = 0;
        foreach (string name in id.CreditNames)
          survivalCurves_[idx++] = (SurvivalCurve)FindCurve(name, sc);
      }

      // Set up the index data for scaling
      tenors_ = id.TenorNames;
      int nTenors = tenors_.Length;

      // Create indices for scaling
      Dt effective = Dt.FromStr(id.Effective, "%D");
      Dt firstPremiumDate = id.FirstPremium == null ? new Dt() : Dt.FromStr(id.FirstPremium, "%D");
      string[] maturities = id.Maturities;
      double[] dealPremiums = id.DealPremia;
      double[] indexWeights = id.CreditWeights;

      cdx_ = new CDX[nTenors];
      for (int i = 0; i < nTenors; i++)
      {
        Dt maturity = (maturities == null || maturities.Length == 0) ?
          Dt.CDSMaturity(effective, tenors_[i]) : Dt.FromStr(maturities[i], "%D");
        cdx_[i] = new CDX(effective, maturity, id.Currency,
                          dealPremiums[i] / 10000.0, id.DayCount,
                          id.Frequency, id.Roll, id.Calendar, indexWeights);
        if (!firstPremiumDate.IsEmpty())
          cdx_[i].FirstPrem = firstPremiumDate;
        cdx_[i].Funded = false;
      }

      // Setup quotes and methods
      quotesArePrices_ = id.QuotesArePrices;
      quotes_ = new double[nTenors];
      for (int i = 0; i < nTenors; ++i)
        quotes_[i] = id.Quotes[i] / 10000.0;
      double[] scalingWeights = id.ScalingWeights;

      return;
    }

    // Helper
    private Curve FindCurve(string name, Curve[] curves)
    {
      foreach (Curve c in curves)
        if (String.Compare(name, c.Name) == 0)
          return c;
      throw new System.Exception(String.Format("Curve name '{0}' not found", name));
    }

    #endregion // SetUp

    #region Tests

    [Test, Smoke]
    public void TestScalingDuration()
    {
      ResultData rd = LoadExpects();
      Timer timer = new Timer();
      CalcScalingFactors(CDXScalingMethod.Duration, relativeScaling_, rd.Results[0], timer);
      rd.TimeUsed = timer.Elapsed;
      MatchExpects(rd);
    }

    [Test, Smoke]
    public void TestScalingSpread()
    {
      ResultData rd = LoadExpects();
      Timer timer = new Timer();
      CalcScalingFactors(CDXScalingMethod.Spread, relativeScaling_, rd.Results[0], timer);
      rd.TimeUsed = timer.Elapsed;
      MatchExpects(rd);
    }

    [Test, Smoke]
    public void TestScalingModel()
    {
      ResultData rd = LoadExpects();
      Timer timer = new Timer();
      CalcScalingFactors(CDXScalingMethod.Model, relativeScaling_, rd.Results[0], timer);
      rd.TimeUsed = timer.Elapsed;
      MatchExpects(rd);
    }

    /// <summary>
    ///   Calculate scaling factors
    /// </summary>
    /// <param name="method">Scaling method</param>
    /// <param name="rs">Result dataset</param>
    /// <param name="timer">Timer</param>
    /// <returns>Array of scaling factors</returns>
    private double[] CalcScalingFactors(
      CDXScalingMethod method,
      bool relativeScaling,
      ResultData.ResultSet rs,
      Timer timer)
    {
      // Setup scaling methods
      int nTenors = tenors_.Length;
      CDXScalingMethod[] scalingMethods = new CDXScalingMethod[nTenors];
      for (int i = 0; i < nTenors; ++i)
      {
        scalingMethods[i] = quotes_[i] <= 0.0 ? CDXScalingMethod.Next : method;
      }
      for (int i = nTenors - 1; i >= 0; --i)
      {
        if (scalingMethods[i] != CDXScalingMethod.Next)
          break;
        scalingMethods[i] = CDXScalingMethod.Previous;
      }
      // scalingMethod_ is the user inputs
      if (ScalingMethods != null)
      {
        // overriden by the use inputs
        string[] sms = ScalingMethods.Split(',');
        for (int i = 0; i < sms.Length && i < scalingMethods.Length; ++i)
          if(sms[i] != null)
          {
            string m = sms[i].Trim();
            if (m.Length <= 0)
              continue;
            scalingMethods[i] = (CDXScalingMethod)Enum.Parse(typeof(CDXScalingMethod), m);
          }
      }

      double[] overrideFactors = null;
      if (OverrideFactors != null)
      {
        overrideFactors = new double[nTenors];
        // overriden by the use inputs
        string[] sos = OverrideFactors.Split(',');
        for (int i = 0; i < sos.Length && i < nTenors; ++i)
          if (sos[i] != null)
          {
            string m = sos[i].Trim();
            if (m.Length <= 0)
              continue;
            overrideFactors[i] = Double.Parse(m);
          }
      }

      // Call scaling routine
      if (timer != null)
        timer.Resume();
      double[] factors = CDXPricer.Scaling(asOf_, settle_, cdx_, tenors_, quotes_, quotesArePrices_,
        scalingMethods, relativeScaling,overrideFactors, discountCurve_, survivalCurves_, scalingWeights_);
      if (timer != null)
        timer.Stop();

      if (rs != null)
      {
        rs.Name = method.ToString();
        rs.Labels = tenors_;
        rs.Actuals = factors;
      }

      return factors;
    }

    #endregion // Tests

    #region Properties
    /// <summary>
    ///   File containing input data
    /// </summary>
    public string BasketFile { get; set; } = "Index Tranche Pricing using Base Correlations Basket.xml";

    /// <summary>
    ///  Array of scaling methods
    /// </summary>
    public string ScalingMethods { get; set; } = null;

    /// <summary>
    ///  Array of scaling methods
    /// </summary>
    public string OverrideFactors { get; set; } = null;

    #endregion // Properties
  }
}
