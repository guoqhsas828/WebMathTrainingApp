//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.IO;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// This test will test the Pv of an equity CDO2 pricer is not 0 when there're some defalted curves
  /// which do not wipe out inner CDOs.
  /// </summary>
  [TestFixture]
  public class CDO2WithDefaultsTest : ToolkitTestBase
  {
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      // Get usd discount cuve and eur discount curve
      string irStringName = GetTestFilePath(usdISDADataFile_);
      DiscountData dd = (DiscountData)XmlLoadData(irStringName, typeof(DiscountData));
      usdDiscountCurve_ = dd.GetDiscountCurve();

      // Get pricing and settle dates
      asOf_ = usdDiscountCurve_.AsOf;
      settle_ = Dt.Add(asOf_, 1);

      string creditsFileName = GetTestFilePath(creditsFile_);
      CreditData cd = (CreditData)XmlLoadData(creditsFileName, typeof(CreditData));
      survCurves = cd.GetSurvivalCurves(usdDiscountCurve_);
      // Default some curves
      BuildDefaultCurves();  
      // Remove null curve
      List<SurvivalCurve> curves = new List<SurvivalCurve>();
      for (int i = 0; i < survCurves.Length-2; i++)
        if(survCurves[i] != null)
          curves.Add(survCurves[i]);
      survCurves = curves.ToArray();

      correlation = new SingleFactorCorrelation(
        Array.ConvertAll<SurvivalCurve, string>(survCurves, s=>s.Name), 
        Math.Sqrt(correlationVal));

      BuildCDO2();

      // Get principals
      string principalFile = "data/Principals_TestCDO2WithDefaults.txt";
      principalFile = GetTestFilePath(principalFile);
      StreamReader reader = File.OpenText(principalFile);
      string line = null;
      int cdos = underlyingCDOPrincipals_.Length;      
      List<double[]> cdoPrincipals = new List<double[]>();
      int count = 0;
      while( (line = reader.ReadLine()) != null)
      {
        string[] currentLine = line.Split('\t');
        double[] prins = Array.ConvertAll<string, double>(currentLine, s => (s == "1" ? underlyingCDOPrincipals_[count] : 0.0));
        cdoPrincipals.Add(prins);
        count++;
      }
      reader.Close();

      cdo2Principals_ = new double[cdoPrincipals[0].Length, underlyingCDOPrincipals_.Length];
      for (int i = 0; i < underlyingCDOPrincipals_.Length; i++)
        for (int j = 0; j < cdoPrincipals[0].Length; j++)
          cdo2Principals_[j, i] = cdoPrincipals[i][j];
      
      return;
    }

    #endregion SetUP

    #region Test

    [Test, Smoke]
    public void TestNonZeroPv()
    {
      BuildCDO2Pricer();
      double pv = cdo2Pricer_.Pv();
      Assert.IsTrue(Math.Abs(pv) > 0, "Pv of CDO2 with several defaults should not be 0");
    }

    #endregion Test

    #region Helpers

    /// <summary>
    ///  Build all credit curves
    /// </summary>
    private void BuildDefaultCurves()
    {
      int[] index = new int[]{13, 14, 45, 221, 222, 226, 230, 231, 400, 404, 405, 406, 413, 414, 415, 447, 450,
      516, 517, 540, 568, 569, 593, 596, 597, 638, 731, 732, 782, 835, 927, 933, 955, 991, 992, 993, 1004, 1005,
      1061, 1108, 1109, 1110};
      double[] recov = new double[]{0.0325, 0.0325, 0.01, 0.15, 0.3, 0.0238, 0.6813, 0.6813, 0.26, 0.94,0.94,0.98,
      0.9151, 0.9151, 0.999, 0.03, 0.125, 0.0175, 0.0175, 0.4125, 0.0663, 0.0238, 0.0125, 0.385, 0.0863, 0.155,
      0.12, 0.12, 0.14, 0.05, 0.0888, 0.32, 0.15, 0.7775, 0.6513, 0.6325, 0.015, 0.015, 0.05, 0.57, 0.57, 0.2105};
      Dt[] dfltDate = new Dt[]
        {
          new Dt(27, 3, 2009), new Dt(24, 4, 2009), new Dt(15, 4, 2009), new Dt(19, 3, 2009), new Dt(9, 10, 2009),
          new Dt(15, 4, 2009), new Dt(2,11,2009), new Dt(2,11,2009), new Dt(3,12,2009), new Dt(7,9,2008),  
          new Dt(7,9,2008),new Dt(7,9,2008), new Dt(7, 9, 2008), new Dt(7, 9, 2008),new Dt(7, 9, 2008),
          new Dt(8,10,2008), new Dt(1, 6, 2009), new Dt(31, 3, 2009), new Dt(31, 3, 2009), new Dt(21,1,2008), 
          new Dt(9, 10, 2008),  new Dt(9, 10, 2008), new Dt(7, 10, 2008), new Dt(2,7,2009), new Dt(15, 9, 2008),
          new Dt(7,1,2009), new Dt(14, 1, 2009), new Dt(14, 1, 2009), new Dt(15, 6, 2009), new Dt(15, 5, 2009), 
          new Dt(26, 1, 2009), new Dt(4,3,2009), new Dt(1, 5, 2009), new Dt(1,12,2009), new Dt(10,8,2009), 
          new Dt(10,8,2009), new Dt(8,12,2008), new Dt(8,12,2008), new Dt(28, 5, 2009), new Dt(27, 9, 2008),
          new Dt(27, 9, 2008),new Dt(27, 9, 2008),
        };
      Dt[] settleDate = new Dt[]
        {
          new Dt(27, 3, 2009), new Dt(24, 4, 2009), new Dt(4, 6, 2009), new Dt(21, 4, 2009), new Dt(24, 1, 2010),
          new Dt(4, 6, 2009), new Dt(1, 12, 2009), new Dt(1, 12, 2009), new Dt(7,1,2010), new Dt(15,10,2008),
          new Dt(15,10,2008),new Dt(15,10,2008), new Dt(15, 10, 2008), new Dt(15, 10, 2008),new Dt(15, 10, 2008),
          new Dt(20, 11, 2008), new Dt(18, 6, 2009), new Dt(1, 5, 2009), new Dt(1, 5, 2009), new Dt(4,3,2008), 
          new Dt(20, 11, 2008), new Dt(20, 11, 2008), new Dt(20, 11, 2008), new Dt(28, 7, 2009), new Dt(21,10,2008),  
          new Dt(10, 2, 2009), new Dt(18, 2, 2009), new Dt(18, 2, 2009), new Dt(16, 7, 2009), new Dt(15, 6, 2009),
          new Dt(26,2,2009), new Dt(7, 4, 2009), new Dt(3, 6, 2009), new Dt(17,12,2009), new Dt(29,10,2009),
          new Dt(29,10,2009), new Dt(16,1,2009), new Dt(16,1,2009), new Dt(28, 6, 2009), new Dt(7, 11, 2008), 
          new Dt(7, 11, 2008),new Dt(12, 11, 2008),
        };

      for (int i = 0; i < index.Length; i++)
        survCurves[index[i]] = SurvivalCurve.FitCDSQuotes(
          survCurves[index[i]].Name, asOf_, Dt.Empty, Currency.USD, "", CDSQuoteType.ParSpread,
          100, SurvivalCurveParameters.GetDefaultParameters(), usdDiscountCurve_, null, null,
          null, new double[] {recov[i]}, 0, new Dt[] {dfltDate[i], settleDate[i]}, null, 0, true);

      return;
    }

    /// <summary>
    ///  Build a CDO2 product
    /// </summary>
    private void BuildCDO2()
    {
      CdoType useType = CdoType.Unfunded;
      cdo2_ = new SyntheticCDO(cdo2Effective_, cdo2Maturity_,
        cdo2Currency_, cdo2Premium_ / 10000.0, cdo2DayCount_, cdo2Freq_, cdo2Roll_, cdo2Cal_);

      cdo2_.FirstPrem = Dt.Empty;//Schedule.DefaultFirstCouponDate(cdo2Effective_, cdo2Freq_, cdo2Maturity_, false);
      cdo2_.LastPrem = Dt.Empty;//Schedule.DefaultLastCouponDate(cdo2_.FirstPrem, cdo2Freq_, cdo2Maturity_, false);
      cdo2_.Attachment = cdo2Attach_;
      cdo2_.Detachment = cdo2Detach_;
      cdo2_.CdoType = useType;

      if (cdo2Fee_ != 0.0)
      {
        cdo2_.Fee = cdo2Fee_;
        cdo2_.FeeSettle = cdo2Effective_;
      }

      cdo2_.Bullet = null;
      cdo2_.FeeGuaranteed = false;
      cdo2_.Description = "cdo_squared";
      cdo2_.Validate();
      return;
    }

    /// <summary>
    ///  Build CDO2 Monte Carlo pricer given underlying CDO maturities
    /// </summary>
    /// <param name="cdoMaturities"></param>
    /// <param name="sampleSize">MC sample size</param>    
    private void BuildCDO2Pricer()
    {
      Dt maturity = cdo2_.Maturity;
      int sampleSize = 10;
      int nChild = cdo2Principals_.GetLength(1);
      double[] prins = new double[survCurves.Length * nChild];
      for (int i = 0, idx = 0; i < nChild; ++i)
        for (int j = 0; j < survCurves.Length; ++j)
          prins[idx++] = cdo2Principals_[j, i];

      // Get correlation
      var corrIn = (Correlation)correlation;
      GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation(corrIn);
      copula_ = new Copula(CopulaType.Gauss, 2, 2);

      cdo2Pricer_ = BasketPricerFactory.CDO2PricerSemiAnalytic(
        new SyntheticCDO[] { cdo2_ }, Dt.Empty, asOf_, settle_, usdDiscountCurve_, survCurves, cdo2Principals_, attachments_,
        detachments_, null, false, copula_, corr, 3, TimeUnit.Months, sampleSize, 3, new double[] { cdo2Notional_ })[0];

      return;
    }
    #endregion Helpers

    #region Data

    Dt asOf_, settle_;

    #region IR data
    private string usdISDADataFile_ = "data/USD_ISDA_TestCDO2WithDefaults.xml";
    private string creditsFile_ = "data/CREDITS_TestCDO2WithDefaults.xml";
    protected DiscountCurve discountCurve = null;
    protected DiscountCurve discountCurveISDA = null;
    protected DiscountCurve usdDiscountCurve_ = null;
    #endregion IR data

    #region credit data
    protected SurvivalCurve[] survCurves = null;
    private double[] runningPrems = new double[303];
    #endregion credit data

    #region CDO2 data
    private double correlationVal = 0.2;
    private SingleFactorCorrelation correlation = null;
    private Dt cdo2Effective_ = new Dt(1, 1, 2010);
    private Dt cdo2Maturity_ = new Dt(20, 6, 2017);
    private Currency cdo2Currency_ = Currency.USD;
    private DayCount cdo2DayCount_ = DayCount.Actual360;
    private Frequency cdo2Freq_ = Frequency.Quarterly;
    private BDConvention cdo2Roll_ = BDConvention.Following;
    private Calendar cdo2Cal_ = Calendar.NYB;
    private double cdo2Premium_ = 0.0;
    private double cdo2Fee_ = 0.0;
    private double cdo2Attach_ = 0.0;
    private double cdo2Detach_ = 0.0171;
    private double cdo2Notional_ = 10000000.0;
    protected SyntheticCDO cdo2_ = null;
    protected SyntheticCDOPricer cdo2Pricer_ = null;
    private Copula copula_ = null;
    private double[,] cdo2Principals_;

    private double[] underlyingCDOPrincipals_ = new double[]
    {
       173913043,  23809524,  25287356, 32000000,  23809524,  26666667,  33333333,  42857143, 33333333,  23809524,  
       23809524, 43333333,  17777778,  35000000,  26666667,  11111111, 11111111,  25555556,  26666667,  27777778, 
       10666667,  16000000, 26666667,  20000000,  18518519, 33333333.33, 27777777.77, 20000000, 22222222.23, 22222222.23, 
       40000000, 40000000, 23809523.81, 22222222.22, 28000000, 28000000,13333333.33, 24000000,26666666.67,32000000,48000000, 
       8333333.04, 17142857.14, 17142857.14, 20000000, 20000000,50000000,50000000,26666666.67, 30000000,33333333.33};

    #endregion CDO2 data

    #region underlying CDO data
    private double[] attachments_ = new double[]
    {
    0.15, 0.16, 0.17, 0.093, 0.16,	0.115,	0.18,	0.14, 0.22, 0.16, 0.16, 0.14, 0.30, 0.20, 0.15, 0.15, 0.30, 0.23, 0.15, 0.18, 
    0.15, 0.15, 0.22, 0.15, 0.30, 0.15, 0.15, 0.30, 0.15, 0.15, 0.20, 0.20, 0.16, 0.175, 0.17, 0.17, 0.195, 0.25, 0.185, 0.93, 
    0.15, 0.30, 0.25, 0.30, 0.30, 0.15, 0.40, 0.40, 0.20, 0.15, 0.20
    };

    private double[] detachments_ = new double[]
    {
    0.20, 0.30, 0.32, 0.14, 0.30, 0.17, 0.30, 0.24, 0.37, 0.30, 0.30, 0.24, 0.60, 0.30, 0.30, 0.30, 0.60, 0.38, 0.30, 0.30, 0.30,
    0.30, 0.32, 0.30, 0.60, 0.25, 0.30, 0.60, 0.30, 0.30, 0.30, 0.30, 0.30, 0.33, 0.27, 0.27, 0.35, 0.50, 0.34, 0.14, 0.25, 0.60,
    0.60, 0.60, 0.60, 0.30, 0.60, 0.60, 0.35, 0.20, 0.30
    };
    private Dt[] maturities_ = new Dt[]
    {
      new Dt(20, 6, 2017)
    };
    #endregion underlying CDO data

    #endregion // Data
  }
}