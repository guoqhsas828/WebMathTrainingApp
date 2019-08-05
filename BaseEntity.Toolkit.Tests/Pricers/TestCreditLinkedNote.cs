//
// Copyright (c)    2018. All rights reserved.
//

using System.Data;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
   [TestFixture]
  public class TestCreditLinkedNote : ToolkitTestBase
  {
    #region Utilities
    private static ReferenceIndex GetReferenceIndex()
    {
      return new InterestRateIndex("USDLIBOR", new Tenor(6, TimeUnit.Months), Currency.USD, DayCount.Actual365Fixed,
                                   Calendar.NYB, BDConvention.Following, Frequency.Daily, CycleRule.None, 2);
    }

    private SurvivalCurve GetCollateralSurvivalCurve()
    {
      return new SurvivalCurve(AsOf, 0.05);
    }

    private double GetCollateralRecovery()
    {
      return 0.4;
    }

    private DiscountCurve GetDiscountCurve()
    {
      return new DiscountCurve(AsOf, 0.045);
    }

    private SurvivalCurve[] GetSurvivalCurves(int n)
    {
      var rc = GetRecoveryCurves(n);
      return ArrayUtil.Generate(n,
                                i =>
                                SurvivalCurve.FromHazardRate(AsOf, GetDiscountCurve(), "5Y", (i % 2) == 0 ? 0.025 : 0.045,
                                                             rc[i].RecoveryRate(AsOf), true));
    }

    private RecoveryCurve[] GetRecoveryCurves(int n)
    {
      return ArrayUtil.Generate(n, i => new RecoveryCurve(AsOf, 0.4));
    }

    private VolatilityCurve GetUnderlierVol()
    {
      return new VolatilityCurve(AsOf, Vol);
    }

    private VolatilityCurve GetCollateralVol()
    {
      return new VolatilityCurve(AsOf, CollVol);
    }

    private Bond GetCollateralBond()
    {
      return new Bond(AsOf, Dt.Add(AsOf, "10Y"), Currency.USD, BondType.None, 0.0, DayCount.Actual360, CycleRule.None,
                      Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB)
               {

                 ReferenceIndex = GetReferenceIndex()
               };


    }

    private double[] GetPrincipals(int n)
    {
      return ArrayUtil.Generate(n, i => 1.0);
    }

    private Copula GetCopula()
    {
      return new Copula();
    }

    private SyntheticCDO GetCDO()
    {
      return new SyntheticCDO(AsOf, Dt.Add(AsOf, "10Y"), Currency.USD, DayCount.Actual365L, Frequency.Quarterly,
                              BDConvention.Following, Calendar.NYB, 0.02, 0.1, 0.10, 0.15);
    }

    private FTD GetFTD()
    {
      return new FTD(AsOf, Dt.Add(AsOf, "10Y"), Currency.USD, 0.05, DayCount.Actual366, Frequency.Quarterly,
                     BDConvention.Following, Calendar.NYB, 3, 1);
    }

    private CDS GetCDS()
    {
      return new CDS(AsOf, Dt.Add(AsOf, "10Y"), Currency.USD, 0.0250, DayCount.ActualActual, Frequency.SemiAnnual,
                     BDConvention.Following, Calendar.NYB);
    }

    private BasketCDS GetBasketCDS()
    {
      return new BasketCDS(AsOf, Dt.Add(AsOf, "10Y"), Currency.USD, 250, DayCount.ActualActual, Frequency.SemiAnnual,
                     BDConvention.Following, Calendar.NYB);
    }


    private CorrelationObject GetCorrelation(int n)
    {
      return new FactorCorrelation(new string[n], 1, ArrayUtil.Generate(n, i => Correlation));
    }

    #endregion

    #region Properties
    /// <summary>
    /// As of
    /// </summary>
    public Dt AsOf { get; set; } = new Dt(6, 2, 2012);

    /// <summary>
    /// Underlier vol
    /// </summary>
    public double Vol { get; set; } = 0.5;

    /// <summary>
    /// Collateral vol
    /// </summary>
    public double CollVol { get; set; } = 0.5;

    /// <summary>
    /// Collateral correlation
    /// </summary>
    public double CollCorr { get; set; } = 0.5;

    /// <summary>
    /// Underlier correlation
    /// </summary>
    public double Correlation { get; set; } = 0.45;

    #endregion

    #region Data

    private int stepSize_ = 3;
    private TimeUnit stepUnit_ = TimeUnit.Months;

    #endregion

    private void FormatResults(IPricer pricer, CreditLinkedNotePricer clnPricer)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Measure", typeof (string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof (double)));
      var timer = new Timer();
      timer.Start();
      using (new CheckStates(true, new[] {clnPricer}))
      {
        DataRow dataRow0 = dataTable.NewRow();
        dataRow0["Measure"] = "FundedPv";
        dataRow0["Value"] = pricer.Pv();
        dataTable.Rows.Add(dataRow0);

        DataRow dataRow1 = dataTable.NewRow();
        dataRow1["Measure"] = "ClnPv";
        dataRow1["Value"] = clnPricer.Pv();
        dataTable.Rows.Add(dataRow1);

        DataRow dataRow1_1 = dataTable.NewRow();
        dataRow1_1["Measure"] = "ClnFeePv";
        dataRow1_1["Value"] = clnPricer.FeeLegPv();
        dataTable.Rows.Add(dataRow1_1);

        DataRow dataRow1_2 = dataTable.NewRow();
        dataRow1_2["Measure"] = "ClnProtectionPv";
        dataRow1_2["Value"] = clnPricer.ProtectionPv();
        dataTable.Rows.Add(dataRow1_2);

        DataRow dataRow2 = dataTable.NewRow();
        dataRow2["Measure"] = "VolDelta";
        dataRow2["Value"] = clnPricer.VolatilityDelta();
        dataTable.Rows.Add(dataRow2);

        DataRow dataRow3 = dataTable.NewRow();
        dataRow3["Measure"] = "VolGamma";
        dataRow3["Value"] = clnPricer.VolatilityGamma();
        dataTable.Rows.Add(dataRow3);

        DataRow dataRow6 = dataTable.NewRow();
        dataRow6["Measure"] = "CollCorrDelta";
        dataRow6["Value"] = clnPricer.CollateralCorrelationDelta();
        dataTable.Rows.Add(dataRow6);

        DataRow dataRow7 = dataTable.NewRow();
        dataRow7["Measure"] = "CollCorrGamma";
        dataRow7["Value"] = clnPricer.CollateralCorrelationGamma();
        dataTable.Rows.Add(dataRow7);

        DataRow dataRow8 = dataTable.NewRow();
        dataRow8["Measure"] = "CollSpreadDelta";
        dataRow8["Value"] = clnPricer.CollateralSpreadDelta();
        dataTable.Rows.Add(dataRow8);
      }
      timer.Stop();
      MatchExpects(ToResultData(dataTable, timer.Elapsed));
    }


     private ResultData ToResultData(DataTable dataTable, double timeUsed)
    {
      int rows = dataTable.Rows.Count;
      var labels = new string[rows];
      var vals = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        DataRow row = dataTable.Rows[i];
        labels[i] = (string) row["Measure"];
        vals[i] = (double) row["Value"];
      }
      var rd = LoadExpects();
      rd.Accuracy = 1e-1;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "RiskMeasures";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = vals;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }


     [Test]
    public void TestCDO()
    {
      int n = 125;
      var cdo = (SyntheticCDO)GetCDO().Clone();
      cdo.CdoType = CdoType.FundedFixed;
      var pricer = BasketPricerFactory.CDOPricerSemiAnalytic(cdo, AsOf, AsOf, AsOf,
                                                             GetDiscountCurve(), GetDiscountCurve(),
                                                             GetSurvivalCurves(n),
                                                             GetPrincipals(n), GetCopula(), GetCorrelation(n), stepSize_,
                                                             stepUnit_, 25, 0.0, 1e6, false, true, null);
      var ucdo = GetCDO();
      var cln = new CreditLinkedNote(cdo, GetCollateralBond(), AsOf, Dt.Add(AsOf, "10Y"), ucdo.Ccy, ucdo.Premium,
                                     ucdo.DayCount, ucdo.CycleRule, ucdo.Freq, ucdo.BDConvention, ucdo.Calendar,
                                     null);
      var clnPricer = CreditLinkedNotePricerFactory.Create(AsOf, AsOf, 1e6, cln, GetDiscountCurve(),
                                                           GetSurvivalCurves(n), GetPrincipals(n),
                                                           GetCorrelation(n), GetUnderlierVol(),
                                                           GetCollateralSurvivalCurve(),
                                                           GetCollateralRecovery(), CollCorr, 
                                                           stepSize_, stepUnit_, 25);
      FormatResults(pricer, clnPricer);
    }



    [Test]
    public void TestNTD()
    {
      int n = 5;
      var ntd = (FTD)GetFTD().Clone();
      ntd.NtdType = NTDType.FundedFixed;
      var pricer = BasketPricerFactory.NTDPricerSemiAnalytic(new[] { ntd }, AsOf, AsOf, AsOf,
                                                             GetDiscountCurve(), GetDiscountCurve(),
                                                             GetSurvivalCurves(n),
                                                             GetPrincipals(n), GetCopula(), GetCorrelation(n), stepSize_,
                                                             stepUnit_, 25, new[] { 1e6 });
      //pricer.CounterpartySurvivalCurve = GetCollateralSurvivalCurve();
      var uftd = GetFTD();
      var cln = new CreditLinkedNote(ntd, GetCollateralBond(), AsOf, Dt.Add(AsOf, "10Y"), uftd.Ccy, uftd.Premium,
                                     uftd.DayCount, uftd.CycleRule, uftd.Freq, uftd.BDConvention, uftd.Calendar,
                                     null);
      var clnPricer = CreditLinkedNotePricerFactory.Create(AsOf, AsOf, 1e6, cln, GetDiscountCurve(),
                                                           GetSurvivalCurves(n), GetPrincipals(n),
                                                           GetCorrelation(n), GetUnderlierVol(),
                                                           GetCollateralSurvivalCurve(),
                                                           GetCollateralRecovery(), CollCorr, 
                                                           stepSize_, stepUnit_, 25);
      FormatResults(pricer[0], clnPricer);
    }

    [Test]
    public void TestCDS()
    {
      int n = 1;
      var cds = (CDS)GetCDS().Clone();
      cds.CdsType = CdsType.FundedFixed;
      var pricer = new CDSCashflowPricer(cds, AsOf, GetDiscountCurve(), GetSurvivalCurves(n)[0]);
      pricer.Notional = 1e6;
      var ucds = GetCDS();
      var cln = new CreditLinkedNote(ucds, GetCollateralBond(), AsOf, Dt.Add(AsOf, "10Y"), ucds.Ccy, ucds.Premium,
                                     ucds.DayCount, ucds.CycleRule, ucds.Freq, ucds.BDConvention, ucds.Calendar,
                                     ucds.ReferenceIndex);
      var clnPricer = CreditLinkedNotePricerFactory.Create(AsOf, AsOf, 1e6, cln, GetDiscountCurve(),
                                                           GetSurvivalCurves(1), GetPrincipals(1),
                                                           null, GetUnderlierVol(), GetCollateralSurvivalCurve(),
                                                           GetCollateralRecovery(), CollCorr, 
                                                           stepSize_, stepUnit_, 25);
      FormatResults(pricer, clnPricer);
    }
  }
}
