//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /*
   * The arbitrary hedge tenor intends to hedge tenor by taking  
   * several different tenor formats: 
   *  1. Tenor name (must exist in survival curves) 
   *  2. Tenor date 
   *  3. string literal "maturity"
   */

  //[TestFixture]
  [TestFixture("Test Arbitrary Hedge Tenor-10Y")]
  [TestFixture("Test Arbitrary Hedge Tenor-3Y")]
  [TestFixture("Test Arbitrary Hedge Tenor-5Y")]
  [TestFixture("Test Arbitrary Hedge Tenor-7Y")]
  public class TestArbitraryHedgeTenor : ToolkitTestBase
  {

    public TestArbitraryHedgeTenor(string name) : base(name) {}
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(basketDataFile_);

      bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      bd.RescaleStrikes = this.rescaleStrikes_;

      if (bd.CreditData == null)
      {
        if (creditDataFile_ == null)
          throw new System.Exception("No credit data");
        filename = GetTestFilePath(creditDataFile_);
        bd.CreditData = (CreditData)XmlLoadData(filename, typeof(CreditData));
      }

      if (bd.DiscountData == null)
      {
        if (irDataFile_ == null)
          throw new System.Exception("No interest rates data");
        filename = GetTestFilePath(irDataFile_);
        bd.DiscountData = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      }

      if (basketType_ != null && basketType_.Length > 0)
        bd.Type = (BasketData.BasketType)Enum.Parse(typeof(BasketData.BasketType), basketType_);

      if (copulaData_ != null && copulaData_.Length > 0)
      {
        string[] elems = copulaData_.Replace(" ", "").Split(new char[] { ',' });
        if (elems.Length < 3)
          throw new ArgumentException("Invalid copula data");
        bd.CopulaType = (CopulaType)Enum.Parse(typeof(CopulaType), elems[0]);
        bd.DfCommon = Int32.Parse(elems[1]);
        bd.DfIdiosyncratic = Int32.Parse(elems[2]);
      }

      ExtractAllParameters();

      // Create three CDO Pricers for three CDO having different maturity dates
      CreateCDOPricers();

      return;
    }
    #endregion

    #region Test

    [Test, Smoke]
    public void Test_Uniform_Relative()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";      
      BumpType bumpType = BumpType.Uniform;
      bool bumpRelative = true;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();      
      MatchExpects(diff, null, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_Uniform_Absolute()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";
      BumpType bumpType = BumpType.Uniform;
      bool bumpRelative = false;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();
      MatchExpects(diff, null, timer.Elapsed);
    }
    
    [Test, Smoke]
    public void Test_Parallel_Relative()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";
      BumpType bumpType = BumpType.Parallel;
      bool bumpRelative = true;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();
      MatchExpects(diff, null, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_Parallel_Absolute()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";
      BumpType bumpType = BumpType.Parallel;
      bool bumpRelative = false;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();
      MatchExpects(diff, null, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_ByTenor_Relative()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";
      BumpType bumpType = BumpType.ByTenor;
      bool bumpRelative = true;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();
      MatchExpects(diff, null, timer.Elapsed);
    }
    
    [Test, Smoke]
    public void Test_ByTenor_Absolute()
    {
      // Test equality of hedge between sensitivities using 3Y and date corresponding to 3Y
      Timer timer = new Timer();
      timer.Start();

      //string hedgeTenor = "3Y";
      BumpType bumpType = BumpType.ByTenor;
      bool bumpRelative = false;

      double[] diff = CalcSensitivity(HedgeTenor, bumpType, bumpRelative);

      timer.Stop();
      MatchExpects(diff, null, timer.Elapsed);
    }

    /// <summary>
    ///  Test whether qSpread01() and qSpreadSensitivity hedge delta give same results
    ///  Suppose we have a CDO matures at T, the hedge delta computes the change of 
    ///  Pv for a CDS product implied at T from credit curve, using qSpreadSensitivity,
    ///  and qSpread01 directly computes the change of Pv of the CDS product. This test
    ///  suppose the maturity T happens to be one of the curve tenor.
    /// </summary>
    /// <returns></returns>
    [Test, Smoke]
    public void TieOut_qSpreadSensitivity_qSpread01()
    {
      Timer timer = new Timer();
      ResultData results = new ResultData();
      results.TestName = "TieOutHedgeDelta";
      results.Accuracy = 0.01;
      results.Results = new ResultData.ResultSet[cdoPricers_.Length];

      for (int j = 0; j < cdoPricers_.Length; j++)
      {
        results.Results[j] = new ResultData.ResultSet();
        results.Results[j].Name = cdoPricers_[j].CDO.Maturity.ToString();

        DataTable dt = Sensitivities.Spread(cdoPricers_[j], "Pv", 0, 5, 5, 
          false, true, BumpType.Parallel, null, false, true, "Maturity", null);

        double[] res1 = new double[dt.Rows.Count];
        double[] res2 = new double[dt.Rows.Count];

        for (int i = 0; i < dt.Rows.Count; ++i)
          res1[i] = (double)dt.Rows[i]["Hedge Delta"];
        
        results.Results[j].Labels = Array.ConvertAll<SurvivalCurve, string>(survCurves, delegate(SurvivalCurve c) { return c.Name; });
        results.Results[j].Actuals = res1;

        // Find the matching tenor on the curve for cdo pricer maturity
        int pos = FindClosestTenorForCDOPricer(cdoPricers_[j], survCurves);

        // Create hedging cds pricer at maturity of j'th CDO 
        // and calculate the spread01 for the cds pricer
        for(int i = 0; i < dt.Rows.Count; ++i)
        {
          Dt asOf = survCurves[0].AsOf;
          CDS cds = new CDS(asOf, cdoPricers_[j].Maturity, cdoPricers_[j].CDO.Ccy,
            ((CDS)survCurves[i].Tenors[pos].Product).Premium, ((CDS)survCurves[i].Tenors[pos].Product).DayCount,
            ((CDS)survCurves[i].Tenors[pos].Product).Freq, ((CDS)survCurves[i].Tenors[pos].Product).BDConvention,
            ((CDS)survCurves[i].Tenors[pos].Product).Calendar);
          
          Dt settle = ConfigUseNaturalSettlement ?Dt.Add(asOf, 1, TimeUnit.Days):asOf;
          CDSCashflowPricer cdsPricer = new CDSCashflowPricer(cds, asOf, settle, discountCurve, survCurves[i], 3, TimeUnit.Months);
          cdsPricer.Notional = 1000000;
          res2[i] = Sensitivities.Spread01(cdsPricer, "Pv", 5, 5);
        }
        results.Results[j].Expects = res2;
      }

      results.TimeUsed = timer.Elapsed;
      MatchExpects(results);
    }

    #endregion Test

    #region helper

    private int FindClosestTenorForCDOPricer(SyntheticCDOPricer syntheticCDOPricer, SurvivalCurve[] curves)
    {
      // Assume all survival curves have identical tenors list
      
      Dt date = syntheticCDOPricer.CDO.Maturity;

      if (Dt.Cmp(date, curves[0].Tenors[0].Maturity) <= 0)
        return 0;
      if (Dt.Cmp(date, curves[0].Tenors[curves[0].Tenors.Count - 1].Maturity) >= 0)
        return curves[0].Tenors.Count - 1;

      int pos = 0;
      for (int i = 0; i < curves[0].Tenors.Count - 1; ++i, ++pos)
      {
        if (Dt.Cmp(date, curves[0].Tenors[i].Maturity) == 0)
          return pos;
        if (Dt.Cmp(date, curves[0].Tenors[i + 1].Maturity) == 0)
          return pos + 1;
        if (Dt.Cmp(date, curves[0].Tenors[i].Maturity) > 0 && Dt.Cmp(date, curves[0].Tenors[i + 1].Maturity) < 0)
        {
          break;
        }
      }
      // Check which one (pos, pos-1) is closer to date
      //if (pos == 0 && Dt.Cmp(date, curves[0].Tenors[1].Maturity) == 0)
      //  return 1;
      if (Dt.Diff(date, curves[0].Tenors[pos].Maturity) > Dt.Diff(date, curves[0].Tenors[pos - 1].Maturity))
        return pos - 1;
      else
        return pos;      
    }


    private void ResetForceFit(IList<SurvivalCurve> survivalCurves, bool[] original)
    {
      for (int i = 0; i < survivalCurves.Count; ++i)
      {
        if ((survivalCurves[i]).Calibrator is SurvivalFitCalibrator)
          ((SurvivalFitCalibrator)(survivalCurves[i]).Calibrator).ForceFit = original[i];
      }
      return;
    }    
    private bool[] SetForceFit(IList<SurvivalCurve> survivalCurves)
    {
      bool[] original = new bool[survivalCurves.Count];

      for (int i = 0; i < survivalCurves.Count; ++i)
      {
        if ((survivalCurves[i]).Calibrator is SurvivalFitCalibrator)
        {
          original[i] = ((SurvivalFitCalibrator)(survivalCurves[i]).Calibrator).ForceFit;
          ((SurvivalFitCalibrator)(survivalCurves[i]).Calibrator).ForceFit = true;
        }
        else
          original[i] = false;
      }

      return original;
    }
    private double[] CalcSensitivity(string hedgeTenor, BumpType bumpType, bool bumpRelative)
    {
      bool forceFit = false;
      string[] bumpTenors = null;
      bool[] originalForceFits = null;
      List<SurvivalCurve> sc = new List<SurvivalCurve>();

      string measures = "Pv";
      double initialBump = 0;
      double upBump = 0.5;
      double downBump = 0;
      bool scaleDelta = true;

      bool calcGamma = false;
      bool calcHedge = true;

      DataTable dt1 = new DataTable("UsingTenorName");
      DataTable dt2 = new DataTable("UsingTenorDate");
      List<double> hedgeDelta1 = new List<double>();
      List<double> hedgeDelta2 = new List<double>();
      List<double> hedgeNotional1 = new List<double>();
      List<double> hedgeNotional2 = new List<double>();

      try
      {
        dt1 = Sensitivities.Spread(cdoPricers_, measures, initialBump, upBump, downBump,
          bumpRelative, scaleDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, null, null);
        foreach (DataRow row in dt1.Rows)
        {
          hedgeDelta1.Add((double)row["Hedge Delta"]);
          hedgeNotional1.Add((double)row["Hedge Notional"]);
        }
      }
      finally
      {
        if (forceFit)
          ResetForceFit(sc, originalForceFits);
      }

      Dt dt = Dt.CDSMaturity(asOf_, Toolkit.Base.Tenor.Parse(hedgeTenor));
      hedgeTenor = Dt.ToExcelDate(dt).ToString();
      try
      {
        dt2 = Sensitivities.Spread(cdoPricers_, measures, initialBump, upBump, downBump,
          bumpRelative, scaleDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, null, null);
        foreach (DataRow row in dt2.Rows)
        {
          hedgeDelta2.Add((double)row["Hedge Delta"]);
          hedgeNotional2.Add((double)row["Hedge Notional"]);
        }
      }
      finally
      {
        if (forceFit)
          ResetForceFit(sc, originalForceFits);
      }

      double[] diff = new double[hedgeNotional1.Count];
      for (int i = 0; i < hedgeNotional1.Count; ++i)
      {
        diff[i] = Math.Abs(hedgeNotional1[i] - hedgeNotional2[i]);
      }

      return diff;
    }
    public string HedgeTenor { get; set; } = "3Y";

    private void CreateCDOPricers()
    {
      cdoPricers_ = bd.GetSyntheticCDOPricers(corrObj_, discountCurve, survCurves);

      if (cdoPricers_ == null)
        throw new System.NullReferenceException("CDO Pricers not available");
      cdoNames_ = new string[cdoPricers_.Length];

      for (int i = 0; i < cdoNames_.Length; ++i)
      {
        cdoNames_[i] = cdoPricers_[i].CDO.Description;
        //cdoPricers_[i].Notional = 1000000;

        double breakEvenFeeOrPremium =
          ((SyntheticCDO)cdoPricers_[i].Product).Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
      }
    }
    private void ExtractAllParameters()
    {
      asOf_ = Dt.FromStr(bd.DiscountData.AsOf, "%D");
      settle_ = Dt.FromStr(bd.Settle, "%D");
      effective_ = Dt.FromStr(bd.IndexData.Effective, "%D");
      ccy_ = bd.IndexData.Currency;
      dayCount_ = bd.IndexData.DayCount;
      freq_ = bd.IndexData.Frequency;
      calendar_ = bd.IndexData.Calendar;
      roll_ = bd.IndexData.Roll;

      if (bd.IndexData.TenorNames == null || bd.IndexData.TenorNames.Length == 0)
      {
        if (bd.IndexData.Maturities == null || bd.IndexData.TenorNames.Length == 0)
          throw new Exception("Index tenors and maturities can not both be null ");
        else
        {
          cdxMaturities_ = new Dt[bd.IndexData.Maturities.Length];
          for (int i = 0; i < bd.IndexData.Maturities.Length; ++i)
            cdxMaturities_[i] = Dt.FromStr(bd.IndexData.Maturities[i], "%D");
        }
      }
      else
      {
        if (bd.IndexData.Maturities != null &&
          (bd.IndexData.Maturities.Length != bd.IndexData.TenorNames.Length))
        {
          throw new Exception("Index tenors dimension must match index maturities dimension.");
        }
        cdxMaturities_ = new Dt[bd.IndexData.TenorNames.Length];
        for (int i = 0; i < bd.IndexData.TenorNames.Length; ++i)
        {
          Dt maturity = (bd.IndexData.Maturities == null || bd.IndexData.Maturities.Length == 0) ?
                        Dt.CDSMaturity(effective_, bd.IndexData.TenorNames[i]) : Dt.FromStr(bd.IndexData.Maturities[i], "%D");
          cdxMaturities_[i] = maturity;
        }
      }

      principals = bd.Principals;

      // Get the discount curve and survival curves
      discountCurve = bd.DiscountData.GetDiscountCurve();

      survCurves = bd.CreditData.GetSurvivalCurves(discountCurve);

      if (bd.CreditData.ScalingFactors != null)
      {
        scaledSurvCurves_ = new SurvivalCurve[survCurves.Length];
        for (int i = 0; i < survCurves.Length; ++i)
        {
          scaledSurvCurves_[i] = survCurves[i];
          scaledSurvCurves_[i].Name = scaledSurvCurves_[i] + "_Scaled";
        }
      }
      else
      {
        if (bd.CreditData.ScalingWeights == null || bd.CreditData.ScalingWeights.Length == 0)
        {
          scalingWeights_ = new double[survCurves.Length];
          for (int i = 0; i < survCurves.Length; ++i)
            scalingWeights_[i] = 1.0;
        }
        else
          scalingWeights_ = Array.ConvertAll<double, double>(bd.CreditData.ScalingWeights,
            delegate(double p) { return p != 1.0 ? 1.0 : p; });

        scalingMethods_ = bd.IndexData.ScalingMethods;
      }

      dealPremia_ = bd.IndexData.DealPremia;
      marketQuotes_ = bd.IndexData.Quotes;

      if (bd.GetCorrelationObject() == null)
      {
        corrObj_ = (CorrelationObject)(BasketData.GetBaseCorrelation(bd.Correlation));
      }
      else
        corrObj_ = (CorrelationObject)(bd.GetCorrelationObject());
      bd.QuadraturePoints = 128;
    }
    
    #endregion helper

    #region Data
    const double epsilon = 1.0E-9;

    //- Data files
    private string irDataFile_ = null;
    private string creditDataFile_ = null;
    private string basketDataFile_ = "TestArbitraryHedgeTenors_BasketData.xml";

    //- other params
    private string basketType_ = null;
    private string copulaData_ = null;
    private bool rescaleStrikes_ = true;

    Dt asOf_, settle_, effective_;
    Dt[] cdxMaturities_ = null;
    Currency ccy_;
    DayCount dayCount_;
    Frequency freq_;
    Calendar calendar_;
    BDConvention roll_;

    double[] dealPremia_;
    double[] marketQuotes_;
    double[] scalingWeights_;
    CDXScalingMethod[] scalingMethods_;

    double[] testBumps_ = new double[] { 0.1, 1.0, 10.0, 100.0 };
    double[] principals;
    CorrelationObject corrObj_ = null;

    protected SyntheticCDO[] cdos_ = null;
    protected SyntheticCDOPricer[] cdoPricers_ = null;
    protected CDX cdx_ = null;
    protected CDX[] cdx = null;
    protected CDXPricer[] cdxPricers_ = null;
    protected string[] cdoNames_ = null;
    protected DiscountCurve discountCurve = null;
    protected SurvivalCurve[] survCurves = null;
    protected SurvivalCurve[] scaledSurvCurves_ = null;

    private BasketData bd = null;

    #endregion // Data
  }
}
