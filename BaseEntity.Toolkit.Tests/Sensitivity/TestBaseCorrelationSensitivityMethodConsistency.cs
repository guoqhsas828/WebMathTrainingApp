/*
 * TestBaseCorrelationSensitivityMethodConsistency.cs
 * This test will generate base correlation sensitivity table resutls by
 * different methods and compare the results consistencies:
 * Sum of ByPoint should be nearly sum of ByStrike, etc...
 * 
 * Data source: spreadsheet "CDO Pricer Bespokes Delta Jump" Anuj used under case 12994
 */

using System;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using System.Collections.Generic;
using System.Data;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  /// <summary>
  /// TestBaseCorrelationSensitivityMethodConsistency
  /// </summary>
  [TestFixture]
  public class TestBaseCorrelationSensitivityMethodConsistency : SensitivityTest
  {
    #region SetUP and Clean
    [OneTimeSetUp]
    public void Initialize()
    {
      ExtractInfo();
    }
    #endregion SetUp and Clean

    #region Tests
    [Test, Smoke]
    public void TestAbsoluteScale_JoinSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.JoinSurfaces);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 100;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestAbsoluteNoScale_JoinSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.JoinSurfaces);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestRelativeNoScale_JoinSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.JoinSurfaces);
      CreateCDOPricer();
      bumpRelative = true;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestAbsoluteScale_MergeSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.MergeSurfaces);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 100;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestAbsoluteNoScale_MergeSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.MergeSurfaces);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestRelativeNoScale_MergeSurface()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.MergeSurfaces);
      CreateCDOPricer();
      bumpRelative = true;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestAbsoluteScale_PvAveraging()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.PvAveraging);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 100;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestAbsoluteNoScale_PvAveraging()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.PvAveraging);
      CreateCDOPricer();
      bumpRelative = false;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    [Test, Smoke]
    public void TestRelativeNoScale_PvAveraging()
    {
      CreateBasecorrelation(BaseCorrelationCombiningMethod.PvAveraging);
      CreateCDOPricer();
      bumpRelative = true;
      double scale = 0.0;

      DoTest(bumpRelative, scale, BumpTarget.Correlation);
    }
    private void DoTest(bool bumpRelative, double scale, BumpTarget bumpTarget)
    {
      DataTable dt = null;
      bumpType = Toolkit.Sensitivity.BaseCorrelationBumpType.ByComponent;
      dt = Toolkit.Sensitivity.Sensitivities.BaseCorrelation(new SyntheticCDOPricer[] { cdoPricer_ }, new string[] { "Pv" },
               bumpSize_, 0, BumpUnit.None, new string[] { USD_BCS.Name, EUR_BCS.Name }, maturities_.ToArray(), detachmentPoints_.ToArray(),
               bumpTarget, bumpRelative, scale, bumpType, false, calcHedge, dt);
      double res_component = SumIf(dt, "Delta");

      bumpType = Toolkit.Sensitivity.BaseCorrelationBumpType.ByPoint;      
      dt = null;
      dt = Toolkit.Sensitivity.Sensitivities.BaseCorrelation(new SyntheticCDOPricer[] { cdoPricer_ }, new string[] { "Pv" },
         bumpSize_, 0, BumpUnit.None, new string[] { USD_BCS.Name, EUR_BCS.Name }, maturities_.ToArray(), detachmentPoints_.ToArray(),
         bumpTarget, bumpRelative, scale, bumpType, false, calcHedge, dt);
      double res_point = SumIf(dt, "Delta");

      bumpType = Toolkit.Sensitivity.BaseCorrelationBumpType.ByStrike;
      dt = null;
      dt = Toolkit.Sensitivity.Sensitivities.BaseCorrelation(new SyntheticCDOPricer[] { cdoPricer_ }, new string[] { "Pv" },
         bumpSize_, 0, BumpUnit.None, new string[] { USD_BCS.Name, EUR_BCS.Name }, maturities_.ToArray(), detachmentPoints_.ToArray(),
         bumpTarget, bumpRelative, scale, bumpType, false, calcHedge, dt);
      double res_strike = SumIf(dt, "Delta"); ;

      bumpType = Toolkit.Sensitivity.BaseCorrelationBumpType.ByTenor;
      dt = null;
      dt = Toolkit.Sensitivity.Sensitivities.BaseCorrelation(new SyntheticCDOPricer[] { cdoPricer_ }, new string[] { "Pv" },
        bumpSize_, 0, BumpUnit.None, new string[] { USD_BCS.Name, EUR_BCS.Name }, maturities_.ToArray(), detachmentPoints_.ToArray(),
        bumpTarget, bumpRelative, scale, bumpType, false, calcHedge, dt);
      double res_tenor = SumIf(dt, "Delta");

      bumpType = Toolkit.Sensitivity.BaseCorrelationBumpType.Uniform;
      dt = null;
      dt = Toolkit.Sensitivity.Sensitivities.BaseCorrelation(new SyntheticCDOPricer[] { cdoPricer_ }, new string[] { "Pv" },
        bumpSize_, 0, BumpUnit.None, new string[] { USD_BCS.Name, EUR_BCS.Name }, maturities_.ToArray(), detachmentPoints_.ToArray(),
        bumpTarget, bumpRelative, scale, bumpType, false, calcHedge, dt);
      double res_uniform = SumIf(dt, "Delta");

      Assert.AreEqual(0, 2 * Math.Abs((res_point - res_uniform) / (res_point + res_uniform)), 0.02, "Uniform-ByPoint wrong");
      Assert.AreEqual(0, 2 * Math.Abs((res_component - res_uniform) / (res_component + res_uniform)), 0.02, "Uniform-ByComponent wrong");
      Assert.AreEqual(0, 2 * Math.Abs((res_strike - res_uniform) / (res_strike + res_uniform)), 0.02, "Uniform-ByStrike wrong");
      Assert.AreEqual(0, 2 * Math.Abs((res_tenor - res_uniform) / (res_tenor + res_uniform)), 0.02, "Uniform-ByTenor wrong");
    }

    #endregion Tests

    #region helpers

    private void ExtractInfo()
    {
      // Create the discount curves
      discountCurve_USD_LIBOR_ = LoadDiscountCurve(USDDiscountCurveDataFile_);
      discountCurve_EUR_LIBOR_ = LoadDiscountCurve(EURDiscountCurveDataFile_);

      survCurves_USD_ = LoadCreditCurves(USDCreditDataFile_, discountCurve_USD_LIBOR_);
      survCurves_EUR_ = LoadCreditCurves(EURCreditDataFile_, discountCurve_EUR_LIBOR_);

      BasketData USD_bd = new BasketData(), EUR_bd = new BasketData();
      if (USDBasketDataFile_ != null)
      {
        string filename = GetTestFilePath(USDBasketDataFile_);
        USD_bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      }
      USD_bd.RescaleStrikes = false;

      if (EURBasketDataFile_ != null)
      {
        string filename = GetTestFilePath(EURBasketDataFile_);
        EUR_bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      }
      EUR_bd.RescaleStrikes = false;

      BasketData.CorrelationData USD_CorrData = USD_bd.Correlation;

      USD_BCS = LoadCorrelationObject(USDCorrelationDataFile_);
      EUR_BCS = LoadCorrelationObject(EURCorrelationDataFile_);

      // Set up the maximum correlation, otherwise bump will trunk to 1.
      // For example 0.95 + 0.1 = 1.05 ==> 1.0
      ((BaseCorrelationTermStruct)USD_BCS).MaxCorrelation = 2.0;
      ((BaseCorrelationTermStruct)EUR_BCS).MaxCorrelation = 2.0;

      // Get and sort a superset of distinct detachment points
      detachmentPoints_.AddRange(((BaseCorrelationTermStruct)EUR_BCS).BaseCorrelations[0].Detachments);
      for(int i = 0; i < ((BaseCorrelationTermStruct)USD_BCS).BaseCorrelations[0].Detachments.Length; i++)
      {
        if (!(detachmentPoints_.Contains(((BaseCorrelationTermStruct)USD_BCS).BaseCorrelations[0].Detachments[i])))
          detachmentPoints_.Add(((BaseCorrelationTermStruct)USD_BCS).BaseCorrelations[0].Detachments[i]);
      }
      detachmentPoints_.Sort();

      // Get and sort a super set of distinct maturities dates 
      maturities_.AddRange(((BaseCorrelationTermStruct)EUR_BCS).Dates);
      for (int i = 0; i < ((BaseCorrelationTermStruct)USD_BCS).Dates.Length; i++)
      {
        if (!(maturities_.Contains(((BaseCorrelationTermStruct)USD_BCS).Dates[i])))
          maturities_.Add(((BaseCorrelationTermStruct)USD_BCS).Dates[i]);
      }
      maturities_.Sort();
            
      // Create the cdo
      CreateCDO();
      
      // Get the basket survival curves and create the cdo pricer      
      BasketData cdoBsketData_ = new BasketData();
      if (CdoBasketDataFile_ != null)
      {
        string filename = GetTestFilePath(CdoBasketDataFile_);
        cdoBsketData_ = (BasketData)XmlLoadData(filename, typeof(BasketData));
      }
      cdoBsketData_.RescaleStrikes = false;
      string[] creditNames = cdoBsketData_.CreditNames;
      cdoCurves_ = new SurvivalCurve[creditNames.Length];
      SurvivalCurve[] allCurves = new SurvivalCurve[survCurves_USD_.Length + survCurves_EUR_.Length];
      string[] allNames = new string[allCurves.Length];
      for (int i = 0; i < survCurves_USD_.Length; i++)
      {
        allCurves[i] = survCurves_USD_[i];
        allNames[i] = survCurves_USD_[i].Name;
      }
      for (int i = 0; i < survCurves_EUR_.Length; i++)
      {
        allCurves[survCurves_USD_.Length + i] = survCurves_EUR_[i];
        allNames[survCurves_EUR_.Length + i] = survCurves_EUR_[i].Name;
      }

      for (int i = 0; i < cdoCurves_.Length; i++)
      {
        for (int j = 0; j < allNames.Length; j++)
        {
          if (creditNames[i] == allNames[j])
          {
            cdoCurves_[i] = allCurves[j];
            break;
          }
        }
      }
      return;
    }

    private void CreateCDO()
    {
      cdo_ = new SyntheticCDO(cdoEffective_, cdoMaturity_, Currency.USD, premium_ / 10000.0, cdoDayCount_, cdoFreq_, cdoRoll_, cdoCal_);
      cdo_.FirstPrem = Schedule.DefaultFirstCouponDate(cdoEffective_, cdoFreq_, cdoMaturity_, false);
      cdo_.LastPrem = Schedule.DefaultLastCouponDate(cdo_.FirstPrem, cdoFreq_, cdoMaturity_, false);
      cdo_.Attachment = cdoAttachedment_;
      cdo_.Detachment = cdoDetachment_;
      cdo_.CdoType = cdoType_;
      cdo_.Description = "cdo";
      cdo_.Notional = cdoNotional_;
      return;
    }

    private void CreateCDOPricer()
    {
      cdoPricer_ = BasketPricerFactory.CDOPricerSemiAnalytic(
        cdo_, new Dt(), asOf_, settle_, discountCurve_USD_LIBOR_, cdoCurves_, null,
        new Copula(CopulaType.Gauss, 2, 2), bco_, 3, TimeUnit.Months, 48, 0, 15000000, false, false, null);
      return;
    }

    private void CreateBasecorrelation(BaseCorrelationCombiningMethod combineMethod, params CorrelationObject[] corrs)
    {
      // Create the joined base correlation surface.
      if (corrs == null || corrs.Length == 0 || corrs.Length == 2)
        bco_ = BaseCorrelationJoinSurfaceUtil.CombineBaseCorrelations(
          new BaseCorrelationObject[] { (BaseCorrelationObject)USD_BCS, (BaseCorrelationObject)EUR_BCS },
          new double[] { 0.7877, 0.2123 },
          InterpMethod.PCHIP, ExtrapMethod.Smooth, InterpMethod.Linear, ExtrapMethod.Const,
          0, 2, combineMethod, null, null);
      else
        bco_ = (BaseCorrelationObject)corrs[0];    
      bco_.Name = "JoinedBCS";
      return;
    }

    private double SumIf(
      DataTable dt, string columnToSum, params string[] filters) 
    {
      DataColumn sumCol = dt.Columns[columnToSum];
      if(sumCol == null)
        throw new ArgumentOutOfRangeException("columnToSum", String.Format("Invalid column name {0}", columnToSum));
      if (sumCol.DataType != typeof(double))
        throw new ArgumentException(String.Format("Column {0} has the wrong type. Must be double", columnToSum));

      int numFilters = filters.Length / 2;
      if ((filters.Length % 2) != 0)
        throw new ArgumentException("Mismatched number of filter columns and values");

      System.Text.StringBuilder filter = new System.Text.StringBuilder();
      for (int i = 0; i < numFilters; i++)
        filter.AppendFormat("{0}([{1}] = '{2}')", (i > 0) ? " AND " : "", 
          filters[i * 2], BaseEntity.Shared.StringUtil.GetEscapedString(filters[i * 2 + 1]));

      DataView dv = new DataView();
      dv.Table = dt;
      if (filter != null && filter.Length > 0)
        dv.RowFilter = filter.ToString();
      double result = 0.0;
      foreach (DataRowView row in dv)
        result += (double)row[sumCol.Ordinal];
      return result;
    }

    #endregion helpers

    #region Data    
    //Money market rates information
    private Dt asOf_ = new Dt(14, 10, 2008);  //October 14th, 2008  
    private Dt settle_ = new Dt(15, 10, 2008); 
    private DiscountCurve discountCurve_USD_LIBOR_ = null;
    private DiscountCurve discountCurve_EUR_LIBOR_ = null;
    private string USDDiscountCurveDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_USD_Curve.xml";
    private string EURDiscountCurveDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_EUR_Curve.xml";
    private string USDCreditDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_USD_Credits.xml";
    private string EURCreditDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_EUR_Credits.xml";
    private string USDBasketDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_USD_Basket.xml";
    private string EURBasketDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_EUR_Basket.xml";
    private string USDCorrelationDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_USD_Correlation.xml";
    private string EURCorrelationDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_EUR_Correlation.xml";
    private string CdoBasketDataFile_ = @"data\BC_Sens_Consistency_Diff_Methods_Basket.xml";
    private SurvivalCurve[] survCurves_USD_ = null;
    private SurvivalCurve[] survCurves_EUR_ = null;
    private SurvivalCurve[] cdoCurves_ = null;
    private CorrelationObject USD_BCS = null;
    private CorrelationObject EUR_BCS = null;
    private SyntheticCDO cdo_ = null;
    private SyntheticCDOPricer cdoPricer_ = null;
    private Dt cdoEffective_ = new Dt(20, 9, 2008);
    private Dt cdoMaturity_ = new Dt(20, 6, 2017);
    private DayCount cdoDayCount_ = DayCount.Actual360;
    private Frequency cdoFreq_ = Frequency.Quarterly;
    private BDConvention cdoRoll_ = BDConvention.Following;
    private Calendar cdoCal_ = Calendar.NYB;
    private double cdoAttachedment_ = 0;
    private double cdoDetachment_ = 0.15;
    private double cdoNotional_ = 15000000;
    private double premium_ = 100.0;
    private CdoType cdoType_ = CdoType.Unfunded;
    private BaseCorrelationObject bco_ = null;

    private List<double> detachmentPoints_ = new List<double>();
    private List<Dt> maturities_ = new List<Dt>();

    private double bumpSize_ = 0.01;
    private bool bumpRelative; 
    private BaseEntity.Toolkit.Sensitivity.BaseCorrelationBumpType bumpType;
    private bool calcHedge = false;

    #endregion Data
  }
}
