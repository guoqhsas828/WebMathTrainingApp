/*
 * TestRandomFactorLoadingForCDO2.cs
 * 
 * This test will test Random Factor Loading copula for SemiAnalytic CDO2 pricer
 * 
 * Copyright (c) 2005-2010,   . All rights reserved.
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class CDO2RandomFactorLoadingTest : ToolkitTestBase
  {
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      // Get the discount curves used to build credit curves
      string irStringName = GetTestFilePath(irDataFile_);
      DiscountData dd = (DiscountData)XmlLoadData(irStringName, typeof(DiscountData));
      discountCurve = dd.GetDiscountCurve();
      string irISDAStringName = GetTestFilePath(irISDADataFile_);
      dd = (DiscountData)XmlLoadData(irISDAStringName, typeof(DiscountData));
      discountCurveISDA = dd.GetDiscountCurve();
      asOf_ = discountCurve.AsOf;
      settle_ = Dt.Add(asOf_, 1);

      // Get the credit curves
      for (int i = 0; i < 124; i++)
        runningPrems[i] = 100.0;
      for (int i = 124; i < 224; i++)
        runningPrems[i] = 500.0;
      for (int i = 224; i < 303; i++)
        runningPrems[i] = 100.0;
      BuildCreditCurves();

      correlation = new SingleFactorCorrelation(creditNames, Math.Sqrt(correlationVal));

      BuildCDO2();

      var corrProbs = new double[] { 0.05, 0.15, 0.60, 0.15, 0.05 };
      var corrs = new double[] { 0.10, 0.25, 0.35, 0.50, 0.70 };
      BuildRFLCopula(corrProbs, corrs);

      // Transpose the cdo2Principals_
      for (int i = 0; i < 303; i++)
        for (int j = 0; j < 6; j++)
          cdo2Principals_[i, j] = cdo2Principals[j, i];

      return;
    }

    #endregion SetUP

    #region Test

    /// <summary>
    ///  Test that the CDO2 protection pv and fee pv both reduce magnitude when one 
    ///  or more underlying CDO maturities are shorter than the CDO2 maturity
    /// </summary>
    [Test, Smoke]
    public void TestReducingValues()
    {
      BuildCDO2Pricer(maturities_);
      double protectionPv1 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv1 = Math.Abs(cdo2Pricer_.FeePv());
      double losToDate1 = cdo2Pricer_.LossToDate(cdo2Maturity_);

      // Save CDO maturities
      var savedCDOMaturities = (Dt[])maturities_.Clone();

      // Make one CDO maturity shorter
      maturities_[5] = new Dt(maturities_[5].Day, maturities_[5].Month, maturities_[5].Year - 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv2 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv2 = Math.Abs(cdo2Pricer_.FeePv());
      double losToDate2 = cdo2Pricer_.LossToDate(cdo2Maturity_);

      // Make two CDO maturities
      maturities_[4] = new Dt(maturities_[4].Day, maturities_[4].Month, maturities_[4].Year - 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv3 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv3 = Math.Abs(cdo2Pricer_.FeePv());
      double losToDate3 = cdo2Pricer_.LossToDate(cdo2Maturity_);

      // Make three CDO maturities
      maturities_[3] = new Dt(maturities_[3].Day, maturities_[3].Month, maturities_[3].Year - 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv4 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv4 = Math.Abs(cdo2Pricer_.FeePv());
      double losToDate4 = cdo2Pricer_.LossToDate(cdo2Maturity_);


      // Restore the saved CDO maturities
      maturities_ = savedCDOMaturities;

      // Check
      bool reduce = false;
      reduce = protectionPv4 < protectionPv3;
      reduce &= protectionPv3 < protectionPv2;
      reduce &= protectionPv2 < protectionPv1;
      Assert.IsTrue(reduce, "ProtectionPv failed to reduce the magnitude when underlying CDO maturities are shorter");

      reduce = feePv4 < feePv3;
      reduce &= feePv3 < feePv2;
      reduce &= feePv2 < feePv1;
      Assert.IsTrue(reduce, "FeePv failed to reduce the magnitude when underlying CDO maturities are shorter");
    }

    /// <summary>
    ///  Test the CDO2 protection pv and fee pv remain the same when 
    ///  one or more CDO maturities are later than CDO2 maturity
    /// </summary>
    [Test, Smoke]
    public void TestSameValues()
    {
      BuildCDO2Pricer(maturities_);
      double protectionPv1 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv1 = Math.Abs(cdo2Pricer_.FeePv());

      // Save CDO maturities
      var savedCDOMaturities = (Dt[])maturities_.Clone();

      // Make one CDO maturity shorter
      maturities_[5] = new Dt(maturities_[5].Day, maturities_[5].Month, maturities_[5].Year + 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv2 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv2 = Math.Abs(cdo2Pricer_.FeePv());

      // Make two CDO maturities
      maturities_[4] = new Dt(maturities_[4].Day, maturities_[4].Month, maturities_[4].Year + 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv3 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv3 = Math.Abs(cdo2Pricer_.FeePv());

      // Make three CDO maturities
      maturities_[3] = new Dt(maturities_[3].Day, maturities_[3].Month, maturities_[3].Year + 1);
      BuildCDO2Pricer(maturities_);
      double protectionPv4 = Math.Abs(cdo2Pricer_.ProtectionPv());
      double feePv4 = Math.Abs(cdo2Pricer_.FeePv());

      // Restore the saved CDO maturities
      maturities_ = savedCDOMaturities;

      // Check
      bool reduce = false;
      reduce = protectionPv4 == protectionPv3;
      reduce &= protectionPv3 == protectionPv2;
      reduce &= protectionPv2 == protectionPv1;
      Assert.IsTrue(reduce, "ProtectionPv failed to remain the magnitude when underlying CDO maturities are shorter");

      reduce = feePv4 == feePv3;
      reduce &= feePv3 == feePv2;
      reduce &= feePv2 == feePv1;
      Assert.IsTrue(reduce, "FeePv failed to remain the magnitude when underlying CDO maturities are shorter");
    }

    /// <summary>
    ///  test loss to date. The loss should be increasing function with time, but with shorter CDO 
    ///  maturity he loss after the CDO maturity should be less than those with the same CDO maturity
    /// </summary>
    [Test, Smoke]
    public void TestLossToDate()
    {
      BuildCDO2Pricer(maturities_);

      var scheduleCashflow = cdo2Pricer_.GenerateCashflowForFee(
        cdo2Pricer_.GetProtectionStart(), cdo2Pricer_.CDO.Premium);

      // Get the scheduled dates
      int num = scheduleCashflow.Schedules.Length;
      var scheduleDates = new Dt[num];
      for (int i = 0; i < num; i++)
        scheduleDates[i] = scheduleCashflow.Schedules[i].Date;
      var lossToDates1 = new double[num];
      for (int i = 0; i < num; i++)
        lossToDates1[i] = Math.Abs(cdo2Pricer_.LossToDate(scheduleDates[i]));

      // Save CDO maturities
      var savedCDOMaturities = (Dt[])maturities_.Clone();

      // Make one CDO maturity shorter
      maturities_[5] = new Dt(maturities_[5].Day, maturities_[5].Month, maturities_[5].Year - 1);
      BuildCDO2Pricer(maturities_);
      var lossToDates2 = new double[num];
      for (int i = 0; i < num; i++)
        lossToDates2[i] = Math.Abs(cdo2Pricer_.LossToDate(scheduleDates[i]));

      // Make two CDO maturities shorter
      maturities_[4] = new Dt(maturities_[4].Day, maturities_[4].Month, maturities_[4].Year - 2);
      BuildCDO2Pricer(maturities_);
      var lossToDates3 = new double[num];
      for (int i = 0; i < num; i++)
        lossToDates3[i] = Math.Abs(cdo2Pricer_.LossToDate(scheduleDates[i]));

      bool less = true;
      for (int i = 0; i < num; i++)
      {
        // If current date is earlier than the shorter CDO maturity, check loss equality
        // If current date is later than the shorter CDO maturity, check "less than"
        less &= (scheduleDates[i] < maturities_[5]
                   ? Math.Round(lossToDates2[i], 4) == Math.Round(lossToDates1[i], 4)
                   : Math.Round(lossToDates2[i], 4) < Math.Round(lossToDates1[i], 4));
      }
      Assert.IsTrue(less, "Loss To Date with shorter CDO maturity are not less than those with same maturity");

      less = true;
      for (int i = 0; i < num; i++)
      {
        // If current date is earlier than the shorter CDO maturity, check loss equality
        // If current date is later than the shorter CDO maturity, check "less than"
        less &= (scheduleDates[i] < maturities_[4]
                   ? Math.Round(lossToDates3[i], 4) == Math.Round(lossToDates2[i], 4)
                   : Math.Round(lossToDates3[i], 4) < Math.Round(lossToDates2[i], 4));
      }
      Assert.IsTrue(less, "Loss To Date with shorter CDO maturity are not less than those with same maturity");

      // Restore cdo maturities
      maturities_ = savedCDOMaturities;
      return;
    }
    
    /// <summary>
    /// When all corrs are the same, the CDO2 is equivalent to be using Gaussian and single correlation 
    /// </summary>
    [Test, Smoke]
    public void TestRFLSameCorrs()
    {      
      double corr = correlation.Correlations[0];
      BuildRFLCopula(
        new double[] {0.1, 0.2, 0.3, 0.2, 0.2}, 
        new double[] {corr, corr, corr, corr, corr});
      BuildCDO2Pricer(null, 1000);
      double pv_1 = cdo2Pricer_.Pv();

      copula_ = new Copula(CopulaType.Gauss, 2, 2);
      BuildCDO2Pricer(null, 1000);
      double pv_2 = cdo2Pricer_.Pv();

      BuildRFLCopula(new double[]{0.1, 0.15, 0.2, 0.25, 0.3}, new double[]{corr, 0, 0, 0, 0});
      BuildCDO2Pricer(null, 1000);
      double pv_3 = cdo2Pricer_.Pv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, corr, 0, 0, 0 });
      BuildCDO2Pricer(null, 1000);
      double pv_4 = cdo2Pricer_.Pv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, corr, 0, 0 });
      BuildCDO2Pricer(null, 1000);
      double pv_5 = cdo2Pricer_.Pv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, 0, corr, 0 });
      BuildCDO2Pricer(null, 1000);
      double pv_6 = cdo2Pricer_.Pv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, 0, 0, corr });
      BuildCDO2Pricer(null, 1000);
      double pv_7 = cdo2Pricer_.Pv();

      Assert.AreEqual(pv_2, pv_1, 1e-5, "Failed");
    }
    
    /// <summary>
    /// The corr distribution is set to be increasing : 0.1, 0.15, 0.2, 0.25, 0.3
    /// The correlation array populates only one corr but shift to higher probability
    /// So we test the Pv increase with the following pattern:
    /// [corr, 0, 0, 0, 0], [0, corr, 0, 0, 0, 0], [0 ,0, corr, 0, 0], [0, 0, 0, corr, 0], [0, 0, 0, 0, corr]
    /// </summary>
    [Test, Smoke]
    public void TestRFLIncreaseCorrs()
    {
      double corr = correlation.Correlations[0];
      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { corr, 0, 0, 0, 0 });
      BuildCDO2Pricer(null, 1000);
      double feePv_1 = cdo2Pricer_.FeePv();
      double protectionPv_1 = cdo2Pricer_.ProtectionPv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, corr, 0, 0, 0 });
      BuildCDO2Pricer(null, 1000);
      double feePv_2 = cdo2Pricer_.FeePv();
      double protectionPv_2 = cdo2Pricer_.ProtectionPv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, corr, 0, 0 });
      BuildCDO2Pricer(null, 1000);
      double feePv_3 = cdo2Pricer_.FeePv();
      double protectionPv_3 = cdo2Pricer_.ProtectionPv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, 0, corr, 0 });
      BuildCDO2Pricer(null, 1000);
      double feePv_4 = cdo2Pricer_.FeePv();
      double protectionPv_4 = cdo2Pricer_.ProtectionPv();

      BuildRFLCopula(new double[] { 0.1, 0.15, 0.2, 0.25, 0.3 }, new double[] { 0, 0, 0, 0, corr });
      BuildCDO2Pricer(null, 1000);
      double feePv_5 = cdo2Pricer_.FeePv();
      double protectionPv_5 = cdo2Pricer_.ProtectionPv();

      bool res = feePv_1 < feePv_2 && feePv_2 < feePv_3 && feePv_3 < feePv_4 && feePv_4 < feePv_5;
      res &= (protectionPv_1 < protectionPv_2 && protectionPv_2 < protectionPv_3 && protectionPv_3 < protectionPv_4 &&
              protectionPv_4 < protectionPv_5);
      Assert.IsTrue(res, "FeePv or ProtectionPv of CDO2 does not increase with corr");
    }
    
    #endregion Test

    #region Helpers

    /// <summary>
    ///  Build all credit curves
    /// </summary>
    private void BuildCreditCurves()
    {
      survCurves = new SurvivalCurve[creditNames.Length];
      for (int i = 0; i < survCurves.Length; i++)
      {
        survCurves[i] = SurvivalCurve.FitCDSQuotes(creditNames[i], asOf_, Dt.Empty, Currency.USD, "", false,
                                                   CDSQuoteType.ParSpread, runningPrems[i],
                                                   SurvivalCurveParameters.GetDefaultParameters(), discountCurve,
                                                   new string[] {"5y"}, new Dt[] {new Dt(20, 6, 2014)},
                                                   new[] {quotes[i]}, new[] {recoveryRates[i]}, 0,
                                                   new Dt[] {Dt.Empty, Dt.Empty}, null, 0, convRecoveryRates[i],
                                                   discountCurveISDA, true);
      }
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

      cdo2_.FirstPrem = Dt.Empty;// Schedule.DefaultFirstCouponDate(cdo2Effective_, cdo2Freq_, cdo2Maturity_, false);
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
    ///  Build a random factor loading copula
    /// </summary>
    private void BuildRFLCopula(double[] corrProbs, double[] corrs)
    {
      double[] parameters = new double[2 * corrs.Length];
      for (int i = 0; i < corrs.Length; ++i)
      {
        parameters[2 * i] = corrProbs[i];
        parameters[2 * i + 1] = corrs[i];
      }
      copula_ = new Copula(CopulaType.RandomFactorLoading, parameters);
      return; 
    }

    /// <summary>
    ///  Build CDO2 Monte Carlo pricer given underlying CDO maturities
    /// </summary>
    /// <param name="cdoMaturities"></param>
    private void BuildCDO2Pricer(Dt[] cdoMaturities)
    {
      BuildCDO2Pricer(cdoMaturities, 1000);
    }

    /// <summary>
    ///  Build CDO2 Monte Carlo pricer given underlying CDO maturities
    /// </summary>
    /// <param name="cdoMaturities"></param>
    /// <param name="sampleSize">MC sample size</param>    
    private void BuildCDO2Pricer(Dt[] cdoMaturities, int sampleSize)
    {
      Dt maturity = cdo2_.Maturity;

      int nChild = 6;
      double[] prins = new double[survCurves.Length * nChild];
      for (int i = 0, idx = 0; i < nChild; ++i)
        for (int j = 0; j < survCurves.Length; ++j)
          prins[idx++] = cdo2Principals_[j, i];

      // Get correlation
      var corrIn = (Correlation)correlation;
      GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation(corrIn);

      cdo2Pricer_ = BasketPricerFactory.CDO2PricerSemiAnalytic(
        new SyntheticCDO[] {cdo2_}, Dt.Empty, asOf_, settle_, discountCurve, survCurves, cdo2Principals_, attachments_,
        detachments_, cdoMaturities, false, copula_, corr, 3, TimeUnit.Months, sampleSize, 0, new double[] { cdo2Notional_ })[0];

      return;    
    }
    #endregion Helpers

    #region Data

    Dt asOf_, settle_;

    #region IR data
    private string irDataFile_ = "data/IR_TestDiffMaturitiesCDO2.xml";
    private string irISDADataFile_ = "data/IRISDA_TestDiffMaturitiesCDO2.xml";
    protected DiscountCurve discountCurve = null;
    protected DiscountCurve discountCurveISDA = null;
    #endregion IR data

    #region credit data
    protected SurvivalCurve[] survCurves = null;
    private string[] creditNames = new string[]
      {                                     
        "ABBEY",	"ABBEY",	"AAB-Bank",	"AAB-Bank",	"ACE",	"AEGON",	"ALYON",	"MO",	"AMB-Proper",	"ABK-AssurC",	"ABK",	"AFG",	
        "AIG-AmgenF",	"HNDA-AmHon",	"AIG",	"AMPAU-GpHl",	"AAUK",	"BUD",	"ANTHEM",	"LORFP-Arce",	"ARW",	"ARM",	"C-AssCorpN",	"T",	
        "AWE",	"AVB",	"AVY",	"AVLN",	"AXASA",	"BAPLC",	"BAVB",	"MONTE",	"MILANO",	"BBVSM",	"BCPN",	"SANTAN",	"BACF",	"BMO",	
        "BK",	"ONE-NA",	"ONE-NA",	"BYIF",	"HVB",	"HVB",	"BMW",	"BSC",	"BLS",	"BERTEL",	"HRB-Financ",	"BNP",	"BOMB-CapIn",	"BOMB",	
        "BOOT",	"BPB",	"BMY",	"BATSLN",	"BSY",	"CCO",	"CAPP",	"COF-Bank",	"COF",	"BNROMA",	"CARR",	"CLS",	"CEZ-FinBV",	"CSMSP",	
        "CIT",	"C",	"CZN",	"CNAFNL",	"CNF",	"CL",	"CLP-Realty",	"CMZB",	"STGOBN",	"CSC",	"CCR",	"CCR-HomeLo",	"COX",	"ACAFP",	
        "ACAFP-CRLY",	"CRDSUI",	"CRDSUI",	"CK",	"CSRLTD",	"CSWINV",	"DCX",	"DF",	"DELBB-Amer",	"DPH",	"XRAY",	"DB",	"DB",	"DIX",	
        "DOW",	"DPL",	"DRSDNR",	"DUK",	"DD",	"EON",	"EMN",	"EK",	"EDF",	"EDS",	"LLY",	"EMI",	"ENB",	"EAS",	"ENI",	"ETR",	
        "EOP-EOPOpL",	"EXC-Exelon",	"EIBKOR",	"FFHCN",	"FHLMC",	"FNMA",	"DEXBB-FSAI",	"FE",	"BACF",	"F",	"F-MotorCre",	"FORTIS",	
        "FPL-CapInc",	"BEN",	"FUJITS",	"GMT",	"GMT-Financ",	"GAZDF",	"GE-CapCorp",	"GMAC",	"GM",	"G",	"GS",	"BKIR",	"HCA",	"HR",	
        "OTE",	"F-Hertz",	"HIW-Realty",	"HLT",	"HD",	"HKLAND-Co",	"HSBC-HFC",	"HRP",	"HUWHY",	"HYSAN-MtnL",	"INTNED-Ban",	"INTEL",	
        "AIG-IntLea",	"IPG",	"INVSA",	"ITT",	"SBRY",	"JPM",	"JPM",	"JCI",	"KMG",	"KMG",	"KIRIN",	"KOCH",	"KPN",	"CITNAT",	
        "KDICB",	"KDB",	"KORELE",	"KOREAT",	"LAND-SecPl",	"LBW",	"LEH",	"L",	"LIBMUT",	"LLOYDS-Ban",	"LTR",	"LTR",	"MAYMK",	
        "MAYAU",	"MBI",	"MBI-InsCor",	"KRB-AmerBa",	"KRB",	"MER",	"METFNL",	"MGIC",	"MGG",	"MGG-Mirage",	"TAISHO",	"MIZUHO-Cor",	
        "MNY",	"MESSA",	"MUNRE",	"NBR-Inc",	"CCBP-Natex",	"NCC",	"NOKIA",	"ODP",	"OLN",	"ORIX",	"PCAR-FinCo",	"CE",	"PCCW-HKTTe",	
        "PEP",	"PEUGOT",	"PHHCO",	"PXD",	"PBI-CredCo",	"PBI",	"PMI",	"POHANG",	"POSTAP-Apt",	"PPL-Energy",	"PFGRQ",	"PG",	"PROMIS",	
        "PEG-PSEGEn",	"QANTAS",	"DNY",	"RDN-AssetA",	"RDN",	"RTRGRP",	"ROLLS",	"RSA",	"RCL",	"RWE",	"AYLL",	"SAMSNG-Ele",	"IBSANP",	
        "SBC",	"SCG",	"SCANIA",	"SCHLUM",	"SCOR",	"S",	"SIEM",	"STSP",	"SLMA",	"SOCGEN",	"SO",	"FON",	"STA",	"STAN",	"HOT",	"STM",	
        "LYOE",	"SUMIBK-Ban",	"SMT",	"SUNW",	"SU",	"STI",	"SWEMAT",	"SWIRE",	"SCHREI",	"SCHREI",	"TAKFUJ",	"TLM",	"TE-TampaEl",	
        "TELDAN",	"TE",	"TPSA",	"TELNOR",	"TDS",	"TLIASS",	"TELECO",	"TEMBEC-Ind",	"TENAGA",	"TXT-FinCor",	"TNB",	"TOCCN",	"TKAGR",	
        "TW",	"MPBCO",	"TOKELP",	"TOKGAS",	"TOY",	"TUP-FinCoB",	"TXU",	"TXU-Engy",	"UBS",	"CRDIT",	"UU",	"UVV",	"UNM",	"UPMKYM",	
        "USB",	"USFC",	"VEOLIA",	"VZ-GlobFun",	"VZW-Capita",	"VC",	"VNU",	"VNU",	"VW",	"BKLY",	"WB",	"WB",	"DIS",	"WM",	"WRI",	
        "WFC",	"WOLKLU",	"WOR",	"WYE",	"XL",	"VERSIC-Ins"};

    private double[] runningPrems = new double[303];

    private double[] recoveryRates = new double[]
        {
        0.45, 0.25, 0.4, 0.25, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35, 0.4, 0.35, 0.4,
        0.4, 0.4, 0.4, 0.35, 0.4, 0.4, 0.4, 0.4, 0.35, 0.35, 0.4, 0.45, 0.15, 0.35, 0.4,
        0.2, 0.35, 0.3, 0.25, 0.2, 0.2, 0.2, 0.4, 0.45, 0.35, 0.15, 0.4, 0.4, 0.2,
        0.35, 0.4, 0.4, 0.4, 0.4, 0.25, 0.4, 0.4, 0.35, 0.4, 0.4, 0.4, 0.35, 0.4, 0.35,
        0.4, 0.45, 0.2, 0.4, 0.25, 0.4, 0.3, 0.4, 0.25, 0.4, 0.4, 0.4, 0.4, 0.35, 0.25,
        0.35, 0.4, 0.35, 0.4, 0.4, 0.2, 0.4, 0.45, 0.2, 0.4, 0.35, 0.35, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.45, 0.25, 0.35, 0.4, 0.4, 0.25, 0.4, 0.4, 0.45, 0.45, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.3, 0.4, 0.4, 0.35, 0.3, 0.4, 0.4, 0.35, 0.4, 0.4,
        0.4, 0.4, 0.35, 0.4, 0.4, 0.35, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.45, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35, 0.4, 0.45, 0.35, 0.4, 0.2, 0.4, 0.4,
        0.4, 0.4, 0.35, 0.4, 0.4, 0.2, 0.4, 0.4, 0.2, 0.35, 0.4, 0.35, 0.35, 0.3, 0.35,
        0.35, 0.35, 0.4, 0.45, 0.4, 0.4, 0.4, 0.3, 0.4, 0.25, 0.35, 0.35, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.35, 0.4, 0.4, 0.4, 0.35, 0.35, 0.45, 0.35, 0.4, 0.4, 0.4,
        0.35, 0.4, 0.4, 0.35, 0.4, 0.4, 0.3, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35,
        0.4, 0.35, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35, 0.35, 0.35,
        0.4, 0.35, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35, 0.35, 0.4, 0.45, 0.45, 0.35,
        0.35, 0.2, 0.4, 0.35, 0.45, 0.4, 0.4, 0.4, 0.45, 0.15, 0.4, 0.35, 0.35, 0.3,
        0.35, 0.4, 0.4, 0.35, 0.4, 0.4, 0.45, 0.45, 0.35, 0.45, 0.4, 0.35, 0.4, 0.4,
        0.4, 0.35, 0.4, 0.35, 0.4, 0.4, 0.4, 0.3, 0.4, 0.4, 0.2, 0.3, 0.4, 0.4, 0.4,
        0.4, 0.15, 0.45, 0.4, 0.35, 0.4, 0.4, 0.35, 0.3, 0.4, 0.35, 0.4, 0.3, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.35};

    private double[] convRecoveryRates = new double[]
        {
        0.4, 0.2, 0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.2, 0.4,
        0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.2, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.2, 0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4,
        0.4, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
        0.4, 0.4, 0.4};
    private double[] quotes = new double[]
        {
        10, 19, 9, 16, 54, 31, 32, 200, 38, 25, 27, 65, 26, 20, 17, 24, 33, 18, 32, 41, 104,
        243, 21, 230, 34, 35, 23, 24, 31, 47, 23, 26, 31, 18, 22, 21, 31, 17, 20, 20, 14, 26,
        18, 31, 22, 32, 38, 32, 39, 14, 429, 461, 51, 32, 27, 77, 56, 44, 98, 44, 62, 30, 18,
        317, 35, 166, 41, 28, 297, 86, 58, 11, 58, 27, 27, 34, 41, 41, 137, 15, 8, 14, 21, 444,
        33, 40, 67, 145, 126, 142, 37, 14, 22, 58, 42, 153, 25, 41, 14, 16, 46, 102, 17, 183,
        22, 174, 45, 61, 10, 47, 49, 46, 42, 503, 20, 23, 22, 63, 22, 200, 170, 19, 30, 20,
        44, 118, 118, 14, 25, 175, 205, 11, 32, 10, 124, 64, 52, 146, 154, 74, 12, 36, 23, 66,
        79, 22, 15, 531, 36, 159, 26, 45, 107, 29, 37, 23, 58, 100, 11, 24, 34, 52, 41, 42, 41,
        44, 20, 12, 31, 137, 99, 13, 48, 60, 36, 105, 31, 30, 35, 45, 30, 49, 38, 184, 192,
        7, 25, 27, 192, 20, 25, 11, 30, 22, 58, 56, 32, 22, 88, 70, 16, 27, 53, 60, 30, 16, 38,
        42, 86, 52, 24, 13, 31, 262, 52, 46, 39, 49, 31, 37, 57, 155, 19, 33, 40, 11, 39, 31,
        22, 21, 113, 85, 15, 23, 32, 8, 30, 61, 43, 20, 162, 28, 31, 26, 233, 135, 36, 29,
        24, 32, 16, 26, 62, 42, 59, 35, 178, 49, 20, 46, 26, 22, 356, 50, 33, 88, 32, 72, 67,
        87, 10, 9, 334, 135, 58, 54, 12, 17, 25, 36, 227, 41, 30, 38, 32, 34, 23, 300, 34, 46,
        66, 56, 20, 25, 42, 40, 30, 20, 32, 96, 78, 46, 24};
    #endregion credit data

    #region CDO2 data
    private double correlationVal = 0.3;
    private SingleFactorCorrelation correlation = null;
    private Dt cdo2Effective_ = new Dt(21, 3, 2007);
    private Dt cdo2Maturity_ = new Dt(20, 6, 2012);
    private Currency cdo2Currency_ = Currency.USD;
    private DayCount cdo2DayCount_ = DayCount.Actual360;
    private Frequency cdo2Freq_ = Frequency.Quarterly;
    private BDConvention cdo2Roll_ = BDConvention.Following;
    private Calendar cdo2Cal_ = Calendar.NYB;
    private double cdo2Premium_ = 550.0;
    private double cdo2Fee_ = 0.0;
    private double cdo2Attach_ = 0.07;
    private double cdo2Detach_ = 0.09;
    private double cdo2Notional_ = 10000000.0;
    protected SyntheticCDO cdo2_ = null;
    protected SyntheticCDOPricer cdo2Pricer_ = null;
    private Copula copula_ = null;
    private double[,] cdo2Principals_ = new double[303, 6];
    private double[,] cdo2Principals = new double[6, 303]
                                          {
                                            {
                                              0, 6500000, 0, 6500000, 0, 0, 6500000, 6500000, 6500000, 0, 0, 0, 0, 0, 0,
                                              0, 6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000, 6500000,
                                              6500000, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0,
                                              6500000, 6500000, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0, 6500000, 0,
                                              0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 6500000, 0,
                                              6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 0, 0,
                                              6500000,
                                              6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0, 0, 0,
                                              0, 0, 6500000, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 6500000, 6500000, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 6500000, 6500000, 0, 0, 6500000, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 6500000, 6500000, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 6500000, 6500000, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 6500000,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 6500000, 0,
                                              6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0,
                                              0, 0, 0, 6500000, 6500000, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 0,
                                              0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 6500000, 6500000, 0, 6500000, 0, 0, 6500000, 6500000, 0, 0,
                                              6500000, 0, 0, 6500000, 6500000, 6500000, 6500000, 6500000, 6500000, 0, 0,
                                              6500000, 0, 6500000, 0, 6500000, 0, 0, 0, 0, 6500000, 0, 0
                                            },
                                            {
                                              0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 6500000,
                                              0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0, 6500000, 6500000, 6500000,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              0, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000,
                                              0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 6500000,
                                              6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 6500000, 6500000, 0, 6500000, 0, 0, 0, 6500000, 6500000, 6500000,
                                              0, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 6500000, 6500000,
                                              0, 6500000, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 6500000,
                                              0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 6500000, 0,
                                              0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 6500000, 0, 0, 0, 6500000,
                                              6500000, 0, 6500000, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0,
                                              0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 6500000, 0, 6500000, 0, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 6500000, 6500000, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0,
                                              6500000, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000, 6500000,
                                              0, 6500000, 0, 6500000, 0, 6500000
                                            },
                                            {
                                              0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 6500000,
                                              0, 0, 6500000, 6500000, 6500000, 0, 0, 6500000, 0, 0, 6500000, 6500000, 0,
                                              0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000, 0,
                                              6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000,
                                              6500000, 0, 0, 6500000, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 0,
                                              0, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000, 0, 0, 6500000, 6500000, 0, 0,
                                              0, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 6500000,
                                              6500000, 6500000, 0, 6500000, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 6500000,
                                              0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 0, 6500000, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              0, 6500000, 6500000, 0, 0, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              0, 6500000, 6500000, 6500000, 6500000, 0, 6500000, 6500000, 6500000, 0,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000,
                                              0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000,
                                              0, 0, 6500000, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 6500000, 6500000, 6500000, 0, 6500000, 0, 0, 6500000, 0, 0,
                                              0, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 6500000, 0, 0, 0, 0,
                                              6500000, 0, 6500000, 0, 6500000, 0, 0, 0, 0
                                            },
                                            {
                                              0, 0, 6500000, 0, 0, 6500000, 6500000, 6500000, 6500000, 6500000, 0,
                                              6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0, 6500000, 6500000,
                                              6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 6500000, 6500000, 0,
                                              0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 6500000, 0,
                                              6500000, 0, 6500000, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0,
                                              0, 6500000, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 0, 6500000,
                                              6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0,
                                              6500000, 6500000, 6500000, 0, 0, 0, 0, 6500000, 6500000, 0, 6500000,
                                              6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 0,
                                              0, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000,
                                              6500000, 6500000, 0, 6500000, 0, 0, 0, 6500000, 6500000, 6500000, 0, 0,
                                              6500000, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 6500000, 0, 6500000, 6500000, 0, 6500000, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0, 6500000, 6500000,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000, 6500000, 0,
                                              0, 6500000, 0, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 6500000, 0
                                            },
                                            {
                                              0, 0, 0, 6500000, 0, 6500000, 0, 0, 6500000, 6500000, 6500000, 6500000, 0,
                                              0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 0, 0, 0, 0, 0,
                                              0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000,
                                              0, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0,
                                              6500000, 6500000, 6500000, 6500000, 0, 0, 6500000, 0, 6500000, 6500000,
                                              6500000, 6500000, 6500000, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 6500000, 6500000, 6500000,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 6500000, 0, 0, 0, 6500000, 6500000, 6500000, 6500000, 6500000,
                                              0, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0,
                                              6500000, 6500000, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000,
                                              0, 6500000, 0, 6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 6500000,
                                              0, 0, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0, 6500000, 0, 6500000, 0, 0, 0,
                                              6500000, 0, 0, 6500000, 6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 6500000, 0,
                                              0, 0, 0, 0, 6500000, 0, 6500000, 6500000, 6500000, 0, 6500000, 0, 0,
                                              6500000, 6500000, 6500000, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0,
                                              0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 6500000,
                                              0, 0, 0, 0, 0, 0, 0, 6500000, 6500000, 0, 6500000, 6500000, 6500000,
                                              6500000, 0, 0, 0, 6500000, 0
                                            },
                                            {
                                              0, 6500000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0,
                                              0, 6500000, 0, 6500000, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000,
                                              6500000, 6500000, 0, 0, 6500000, 6500000, 0, 6500000, 0, 6500000, 0,
                                              6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 0,
                                              0, 6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000, 6500000,
                                              0, 6500000, 6500000, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 0, 0,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 6500000, 6500000,
                                              6500000, 0, 0, 6500000, 6500000, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000,
                                              0, 0, 6500000, 0, 6500000, 6500000, 6500000, 0, 6500000, 0, 6500000, 0,
                                              6500000, 0, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 0, 0, 0, 6500000, 6500000, 6500000, 0, 0, 6500000, 0,
                                              6500000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000,
                                              6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0, 0, 0, 6500000, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0,
                                              6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 6500000, 0, 6500000, 0,
                                              0, 0, 0, 0, 6500000, 0, 0, 0, 0, 6500000, 6500000, 0, 0, 6500000, 6500000,
                                              6500000, 6500000, 6500000, 6500000, 0, 0, 0, 6500000, 0, 0, 6500000, 0,
                                              6500000, 6500000, 0, 0, 0, 0, 0, 0, 0, 0, 6500000, 0, 0, 0, 0, 0, 6500000,
                                              0, 0, 0, 6500000, 0, 6500000, 0, 6500000, 6500000, 0, 0, 0
                                            }
                                          };

    #endregion CDO2 data

    #region underlying CDO data
    private double[] attachments_ = new double[] { 0.045, 0.045, 0.045, 0.045, 0.045, 0.045 };
    private double[] detachments_ = new double[] { 0.065, 0.065, 0.065, 0.065, 0.065, 0.065 };
    private Dt[] maturities_ = new Dt[]
    {
      new Dt(20, 6, 2012), new Dt(20, 6, 2012), new Dt(20, 6, 2012), 
      new Dt(20, 6, 2012), new Dt(20, 6, 2012), new Dt(20, 6, 2012)
    };
    #endregion underlying CDO data

    #endregion // Data
  }
}