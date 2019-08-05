//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// This qunit test will show for a protection seller default name will lead to 
  /// a loss when using scenario utility. The more names default, the more loss. 
  /// And scenario for widest name should equal to jump-to-default(qVOD) calculation  
  /// </summary>
  [TestFixture]
  public class TestCDODefaultScenario : ToolkitTestBase
  {
    #region SetUP

    [OneTimeSetUp]
    public void Initialize()
    {
      // Get usd discount cuve 
      string irStringName = GetTestFilePath(usdDataFile_);
      DiscountData dd = (DiscountData) XmlLoadData(irStringName, typeof (DiscountData));
      usdDiscountCurve_ = dd.GetDiscountCurve();

      // Get pricing and settle dates
      asOf_ = usdDiscountCurve_.AsOf;
      settle_ = Dt.Add(asOf_, 1);

      // Get the surviavl curves
      BuildCreditCurves();

      // Get the CDO and CDO pricer information from basket data
      string basketStringName_ = GetTestFilePath(basketDataFile_);
      BasketData bd = (BasketData) XmlLoadData(basketStringName_, typeof (BasketData));

      if (bd.GetCorrelationObject() == null)
      {
        correlation_ = (CorrelationObject) (BasketData.GetBaseCorrelation(bd.Correlation));
      }
      else
        correlation_ = (CorrelationObject) (bd.GetCorrelationObject());
      copula_ = new Copula(bd.CopulaType, bd.DfCommon, bd.DfIdiosyncratic);

      // Get CDO
      cdo_.AddRange(bd.GetSyntheticCDOs());

      // Build the cdo pricer
      BuildCDOPricer(false);

      // Get 5 widest curves using SortedList
      GetWidestCurves();
    }

    #endregion setup

    #region Tests

    [Test, Smoke]
    public void TestLossUponWidestName()
    {
      bool[] rescaleStrikes = Array.ConvertAll(cdoPricers_, p => false);
      TestCompareWithVOD(rescaleStrikes);
    }

    [Test, Smoke]
    public void TestLossUponWidestNameRescaleStrike()
    {
      bool[] rescaleStrikes = Array.ConvertAll(cdoPricers_, p => true);
      TestCompareWithVOD(rescaleStrikes);
    }

    [Test, Smoke]
    public void TestDecreasingLoss()
    {
      bool[] rescaleStrikes = Array.ConvertAll(cdoPricers_, p => false);
      TestDecreasingLossTrend(rescaleStrikes);
    }

    [Test, Smoke]
    public void TestDecreasingLossRescaleStrikes()
    {
      bool[] rescaleStrikes = Array.ConvertAll(cdoPricers_, p => true);
      TestDecreasingLossTrend(rescaleStrikes);
    }

    #endregion Tests

    #region helpers

    private void BuildCreditCurves()
    {
      Dictionary<string, Dt[]> eventDates_ = new Dictionary<string, Dt[]>();
      eventDates_.Add("GCI", new Dt[] {new Dt(8, 9, 2008), new Dt(6, 10, 2008)});
      eventDates_.Add("GE-CapCorp", new Dt[] {new Dt(8, 9, 2008), new Dt(6, 10, 2008)});
      eventDates_.Add("WFC", new Dt[] {new Dt(29, 9, 2008), new Dt(23, 10, 2008)});

      survivalCurves_ = new SurvivalCurve[creditNames_.Length];
      for (int i = 0; i < creditNames_.Length; i++)
      {
        int numTenors = tenors_.Length;
        double[] quoteD = new double[numTenors];
        for (int j = 0; j < numTenors; j++)
        {
          quoteD[j] = quotes_[j, i];
        }
        survivalCurves_[i] = SurvivalCurve.FitCDSQuotes(
          creditNames_[i], asOf_, settle_, Currency.USD, "", true,
          quoteTypes_[i] == 1 ? CDSQuoteType.Upfront : CDSQuoteType.ParSpread, runningPrem_,
          SurvivalCurveParameters.GetDefaultParameters(), usdDiscountCurve_, tenors_, tenorDates_, quoteD,
          new[] {recoveries_[i]}, 0, eventDates_.ContainsKey(creditNames_[i]) ? eventDates_[creditNames_[i]] : null,
          null, 0, 0.4, null, true);
      }
      return;
    }

    private void BuildCDOPricer(bool rescaleStrikes)
    {
      Dt[] maturities = Array.ConvertAll(cdo_.ToArray(), cdo=>cdo.Maturity);      
      Dt portfolioStart = new Dt();
      cdoPricers_ = new SyntheticCDOPricer[cdo_.Count];
      cdoPricers_ = BasketPricerFactory.CDOPricerSemiAnalytic(
        cdo_.ToArray(), portfolioStart, asOf_, settle_, usdDiscountCurve_, null, maturities, survivalCurves_, 
        Array.ConvertAll(creditNames_, c=>(1.0/creditNames_.Length)), copula_, correlation_, 3, TimeUnit.Months, 
        40, 0, Array.ConvertAll(cdo_.ToArray(), cdo=>1e7), rescaleStrikes, false, null);
      return;
    }

    private void GetWidestCurves()
    {
      // Get the widest 5 curves by using SortedList data structure
      // First calculate the default probabilities for eah curve by amturity
      // defaulted curve don't count so make them different negative numbers
      double[] defaultProbabilitiesByMaturity = new double[survivalCurves_.Length];
      Dt maturity = cdo_[0].Maturity;
      int x = -1;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        defaultProbabilitiesByMaturity[i] =
          (survivalCurves_[i].Defaulted == Defaulted.HasDefaulted)
            ? x--
            : (survivalCurves_[i].DefaultProb(asOf_, maturity));
      }

      Random rand = new Random(); //add a small rand to ensure keys are different
      SortedList<double, SurvivalCurve> sl = new SortedList<double, SurvivalCurve>();
      int n=survivalCurves_.Length;
      for (int i = 0; i < n; i++)
        sl.Add(defaultProbabilitiesByMaturity[i] + rand.NextDouble() * 1e-9, survivalCurves_[i]);

      wideCurves_.Add(sl.Values[n-1]);
      wideCurves_.Add(sl.Values[n-2]);
      wideCurves_.Add(sl.Values[n-3]);
      wideCurves_.Add(sl.Values[n-4]);
      wideCurves_.Add(sl.Values[n-5]);
      return;
    }

    private void TestCompareWithVOD(bool[] rescaleStrikes)
    {
      double[] corrBumps = Array.ConvertAll(cdoPricers_, p => 0.0);

      // First test when the widest name default, there will be a loss
      var shift = new ScenarioShiftDefaults(new[] { wideCurves_[0] });
      var results = Scenarios.CalcScenario(cdoPricers_, "Pv", new IScenarioShift[] { shift }, rescaleStrikes[0], true);

      Assert.IsTrue(results[0] < 0, "Default widest name fail to get a loss");

      // Second test this value is equal to VOD
      double[] vod = Sensitivities.VOD(cdoPricers_, "Pv", rescaleStrikes);
      Assert.AreEqual(vod[0], results[0], 1e-2, "Failed to get VOD = ScenarioUponWidestName");

    }

    private void TestDecreasingLossTrend(bool[] rescaleStrikes)
    {
      double[] corrBumps = Array.ConvertAll(cdoPricers_, p => 0.0);
      var defaults = new List<SurvivalCurve>();
      double[] results = new double[5];
      for (int j = 0; j < results.Length; j++)
      {
        // For each j, add "Defaulted" to survBumps
        defaults.Add(wideCurves_[j]);
        var shift = new ScenarioShiftDefaults(defaults.ToArray());
        var shift2 = new ScenarioShiftCorrelation(new[] { correlation_ }, corrBumps, ScenarioShiftType.Absolute);
        results[j] = Scenarios.CalcScenario(cdoPricers_, "Pv", new IScenarioShift[] { shift, shift2 }, rescaleStrikes[0], true)[0];
      }
      // Test decreasing with number of defaults
      bool decreasing = true;
      for (int i = 0; i < results.Length - 1; i++)
        decreasing &= (results[i + 1] < results[i]);
      Assert.IsTrue(decreasing, "Loss decreasing with defaults failed");
    }
    
    #endregion helpers

    #region data

    #region curve data
    private Dt asOf_;
    private Dt settle_;
    private DiscountCurve usdDiscountCurve_;
    private string usdDataFile_ = "data/IrCurveTestCDODefaultsScenario.xml";

    private SurvivalCurve[] survivalCurves_;

    private string[] creditNames_ = new string[]
                                      {
                                        "ACE", "AET", "AL", "AA", "ALL", "MO", "AEP", "AXP", "AIG", "AMGN", "APC", "ARW"
                                        , "ATTINC", "ATTINC-ML", "AZO",
                                        "BAX", "BLC", "BA-CapCorp", "BMY", "BNI", "CPB", "COF-Bank", "CAH", "CCL", "CAT"
                                        , "CBSCOR", "CTX", "CTL", "CB",
                                        "CI", "CIT", "CMCSA-CableLLC", "CSC", "CAG", "COP", "CEG", "CCR-HomeLoans",
                                        "COX-CommInc", "CSX", "CVSCRM",
                                        "DRI", "DE", "DVN", "D", "DOW", "DUKECO", "DD", "EMN", "EMBRQ", "FHLMC", "FNMA",
                                        "FE", "FO", "GCI", "GE-CapCorp",
                                        "GIS", "GR", "HAL", "HIG", "HPQ", "HD", "HON", "IACI", "IR-NJ", "IBM",
                                        "AIG-IntLeaseFin", "IP", "SFI", "JCP",
                                        "JNY", "KFT", "KR", "LEN", "LTD", "LIZ", "LMT", "LTR", "M", "MAR", "MMC",
                                        "MBI-InsCorp", "MCD", "MCK", "MWV",
                                        "MET", "MOT", "NRUC", "NWL", "NWS-AmInc", "JWN", "NSC", "NOC", "OMC", "PGN",
                                        "PHM", "DGX", "DNY", "RDN", "RTN",
                                        "ROH", "SWY", "SLE", "SRE", "SHW", "SPG-LP", "LUV", "S", "HOT", "TGT",
                                        "TXT-FinCorp", "TW", "TOL", "RIG", "UNP",
                                        "UHS", "VLOC", "VRZN", "WMT", "DIS", "WM", "WFC", "WY", "WHR", "WYE", "XL"
                                      };

    private int[] quoteTypes_ = new int[]
                                  {
                                    1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1,
                                    1, 1, 1, 1, 1,
                                    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1,
                                    1, 1, 1, 1, 1,
                                    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                                    1, 1, 1, 1, 1,
                                    1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0
                                  };

    private double runningPrem_ = 100.0;

    private double[] recoveries_ = new double[]
                                     {
                                       0.4, 0.4, 0.4, 0.4, 0.3, 0.43, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.38, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.4, 0.52, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.43,
                                       0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0, 0, 0.4, 0.36, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.38, 0.39, 0.38, 0.4, 0.4, 0.42, 0.39, 0.4, 0.38, 0.4, 0.38, 0.4, 0.4, 0.17, 0.4
                                       , 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.4, 0.4, 0.4, 0.42, 0.33, 0.39, 0.4, 0.4, 0.38, 0.39, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                                       0.4, 0.4, 0.45, 0.4, 0, 0.4, 0.4, 0.4, 0.4
                                     };
    private Dt[] tenorDates_ = new Dt[] {new Dt(20,12,2012), new Dt(20,12,2014), new Dt(20, 12, 2016), new Dt(20,12,2019)};
    private string[] tenors_ = new string[] {"3Y", "5Y", "7Y", "10Y"};

    private double[,] quotes_ = new double[,]
                                  {
                                    {
                                      0.021444, 64.28, -0.016504, 0.000198, 0.164139, 0.228134, -0.024066, -0.015404,
                                      -0.015958, -0.013861,-0.014232, -0.007496, -0.024271, 0.00257, -0.013981, -0.003659, -0.023381,
                                      0.061869, -0.019229, -0.015712,-0.020445, -0.015763, -0.00803, -0.01972, 0.003028, 78.94, 0.000572, 
                                      0.018011, 0.020251, 0.030251, -0.018153, -0.012841, -0.020221, -0.011733, -0.024639, -0.020515, 
                                      -0.015422, -0.010578, -0.010912, -0.015493, -0.019071, -0.017591, -0.017111, -0.014992, -0.020304, 
                                      0.009424, -0.001006, 0.005635, -0.018844, -0.015354, -0.015446, -0.021152, 0.00493, 0, 0, 0.007837, 
                                      0.077726, 0.027518, -0.021217, -0.018998, -0.019405, -0.015159, 0.031321, -0.020079, 0.026891, -0.025135, 
                                      -0.025135, -0.023471, 0.003069, -0.017053, 0.009881, 0.010064, -0.003559, -0.012472, -0.010579, 0.044801, 
                                      0.152538, -0.021902, 0.036665, -0.016762, 0.012744, 0.039788, -0.00426, 0.685185, -0.023921, -0.022619, 
                                      0.033504, -0.019762, -0.002639, -0.000273, -0.008168, -0.0186, 0.010476, -0.015662, 0.00718, -0.013747,
                                      -0.014008, -0.015437, 0.014464, 0.3907, 56.47, -0.007368, -0.019051, 0.092994, 0.394677, -0.022818, 
                                      -0.018223, 0.009627, -0.006047, -0.014478, -0.014977, 0.012133, -0.020462, 0.033451, -0.011527, -0.015715, 
                                      0.027383, -0.014174, -0.004176, 0.007388, 0, -0.015371, 0.018185, -0.026415, 125.27
                                    },
                                    {
                                      0.049231, 73.82, -0.019063, 0.008013, 0.238752, 0.298516, -0.034473, -0.014014,
                                      -0.019217, -0.012405, -0.012579, -0.004571, -0.030273, 0.010888, -0.014115,
                                      0.001858, -0.032491, 0.112711, -0.023812, -0.020623, -0.027211, -0.016374,
                                      -0.004359, -0.027028, 0.022152, 99.37, 0.008317, 0.043053, 0.040919, 0.050919,
                                      -0.024041, -0.015634, -0.026868, -0.010126, -0.03479, -0.026932, -0.018662,
                                      -0.005191, -0.006533, -0.019531, -0.02432, -0.019372, -0.020116, -0.018269,
                                      -0.025853, 0.052458, 0.014003, 0.022855, -0.02448, -0.019004, -0.016846, -0.025575
                                      , 0.018793, 0, 0, 0.025659, 0.141291, 0.04623, -0.028844, -0.025013, -0.025984,
                                      -0.018536, 0.061317, -0.026471, 0.057158, -0.035424, -0.035424, -0.032334, 0.01726
                                      , -0.021321, 0.030775, 0.031463, 0.00801, -0.010368, -0.009096, 0.082192, 0.230346
                                      , -0.030216, 0.076121, -0.021807, 0.032655, 0.075868, 0.007133, 0.740981,
                                      -0.034388, -0.032092, 0.065382, -0.021753, 0.004149, 0.014824, -0.001067,
                                      -0.025159, 0.027114, -0.021301, 0.026443, -0.013624, -0.012546, -0.018175,
                                      0.035172, 0.45982, 65.08, -0.000943, -0.024871, 0.153963, 0.442611, -0.030814,
                                      -0.024746, 0.025337, -0.00384, -0.015852, -0.013674, 0.029757, -0.024386, 0.067106
                                      , -0.011322, -0.018459, 0.05749, -0.0132, -0.000633, 0.027283, 0, -0.021388,
                                      0.041874, -0.038982, 140.59
                                    },
                                    {
                                      0.07425, 77.95, -0.02281, 0.013267, 0.2772, 0.325964, -0.042087, -0.015935,
                                      -0.022932, -0.01359, -0.007603, 0.002041, -0.034407, 0.015652, -0.015706, 0.004748
                                      , -0.040166, 0.147202, -0.029611, -0.024664, -0.033546, -0.019979, -0.002957,
                                      -0.033272, 0.037521, 108.85, 0.011541, 0.059028, 0.057318, 0.067318, -0.026992,
                                      -0.018966, -0.034155, -0.006538, -0.044888, -0.028702, -0.02251, 0.00307, -0.00365
                                      , -0.022494, -0.029916, -0.02203, -0.023927, -0.021432, -0.030478, 0.074158,
                                      0.026425, 0.033565, -0.03022, -0.022863, -0.015225, -0.028157, 0.028416, 0, 0,
                                      0.036881, 0.179734, 0.059211, -0.03541, -0.031247, -0.03264, -0.022115, 0.083337,
                                      -0.033211, 0.077878, -0.043712, -0.043712, -0.039751, 0.031387, -0.026238,
                                      0.046171, 0.045195, 0.016158, -0.009435, -0.010005, 0.113288, 0.263245, -0.037875,
                                      0.102632, -0.026751, 0.047771, 0.100721, 0.014352, 0.772995, -0.043814, -0.041716,
                                      0.088069, -0.022645, 0.01491, 0.027428, 0.008474, -0.031391, 0.037244, -0.026013,
                                      0.040701, -0.012059, -0.009277, -0.021307, 0.050495, 0.479026, 67.74, 0.00816,
                                      -0.030806, 0.19713, 0.466494, -0.037171, -0.030531, 0.034951, -0.00239, -0.017769,
                                      -0.014532, 0.049071, -0.026331, 0.090212, -0.011299, -0.021509, 0.077505,
                                      -0.010399, 0.000804, 0.039979, 0, -0.025676, 0.064304, -0.050903, 147.08
                                    },
                                    {
                                      0.109191, 79.96, -0.026793, 0.021053, 0.31831, 0.348468, -0.050977, -0.018333,
                                      -0.026676, -0.013309, 0.004931, 0.010664, -0.042477, 0.020681, -0.013973, 0.009539
                                      , -0.049702, 0.185782, -0.035242, -0.030071, -0.040094, -0.021869, 0.000307,
                                      -0.041399, 0.060667, 117.9, 0.016328, 0.080175, 0.08028, 0.09028, -0.030348,
                                      -0.022504, -0.044187, 0.000328, -0.050285, -0.028185, -0.024664, 0.017095,
                                      0.001309, -0.021876, -0.035616, -0.024334, -0.027613, -0.024761, -0.031669,
                                      0.103192, 0.055594, 0.051977, -0.035772, -0.027866, -0.015247, -0.021036, 0.041895
                                      , 0, 0, 0.053798, 0.223138, 0.075393, -0.042994, -0.037484, -0.041458, -0.021385,
                                      0.112016, -0.043075, 0.110905, -0.051406, -0.051406, -0.047093, 0.053922,
                                      -0.030859, 0.071162, 0.077297, 0.034876, -0.002233, -0.005718, 0.150841, 0.300596,
                                      -0.046723, 0.141353, -0.031561, 0.071235, 0.140684, 0.025729, 0, -0.052849,
                                      -0.049946, 0.116513, -0.017473, 0.039982, 0.046579, 0.029038, -0.037805, 0.052628,
                                      -0.03094, 0.060749, -0.008631, -0.004449, -0.024286, 0.076398, 0.491274, 78,
                                      0.023574, -0.037377, 0.246939, 0.48333, -0.037755, -0.033948, 0.051028, -0.000028,
                                      -0.017921, -0.010777, 0.073642, -0.026541, 0.118578, -0.01164, -0.024207, 0.103416
                                      , -0.005287, 0.002522, 0.059869, 0, -0.028182, 0.09942, -0.065737, 155.03
                                    }
                                  };

    #endregion curve data

    private string basketDataFile_ = "data/BasketDataTestCDODefaultsScenario.xml";
    private SyntheticCDOPricer[] cdoPricers_;
    private List<SyntheticCDO> cdo_ = new List<SyntheticCDO>();
    private Copula copula_;
    private CorrelationObject correlation_; 
    private List<SurvivalCurve> wideCurves_ = new List<SurvivalCurve>();
    #endregion data
  }
}
