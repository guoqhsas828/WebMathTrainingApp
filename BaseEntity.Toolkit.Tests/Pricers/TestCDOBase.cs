//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  public class TestCdoBase : SensitivityTest
  {
    protected TestCdoBase(string name) : base(name) {}

    #region SetUP
    /// <summary>
    ///   Create an array of CDO Pricers
    /// </summary>
    /// <returns>CDO Pricers</returns>
    protected void CreatePricers()
    {
      // Saved and set configuration
      //SaveStates.AddIgnored(typeof(SyntheticCDOPricer), "Basket.LossDistribution", "Basket.AmorDistribution");

      BasketData bd = new BasketData();
      if (BasketDataFile != null)
      {
        string filename = GetTestFilePath(BasketDataFile);
        bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      }
      bd.RescaleStrikes = this.RescaleStrikes;

      // Load correlation object if specified
      CorrelationObject corrObj = null;
      if (CorrlationDataFile != null)
        corrObj = LoadCorrelationObject(CorrlationDataFile);

      // Load discount data if specified
      DiscountCurve discountCurve = null;
      if (LiborDataFile != null)
        discountCurve = LoadDiscountCurve(LiborDataFile);
      else if (bd.DiscountData == null)
        throw new System.Exception("No interest rates data");

      // Load credit data if specified
      SurvivalCurve[] survivalCurves = null;
      if (CreditDataFile != null)
        survivalCurves = LoadCreditCurves(CreditDataFile, discountCurve);
      else if (bd.CreditData == null)
        throw new System.Exception("No credit data");

      if (BasketType != null && BasketType.Length > 0)
        bd.Type = (BasketData.BasketType)Enum.Parse(typeof(BasketData.BasketType), BasketType);
      // Load copula data if specified
      if (CopulaData != null && CopulaData.Length > 0)
      {
        // remove all spaces
        string[] elems = CopulaData.Replace(" ", "").Split(new char[] { ',' });
        if (elems.Length < 3)
          throw new ArgumentException("Invalid copula data");
        bd.CopulaType = (CopulaType)Enum.Parse(typeof(CopulaType), elems[0]);
        bd.DfCommon = Int32.Parse(elems[1]);
        bd.DfIdiosyncratic = Int32.Parse(elems[2]);
      }
      cdoPricers_ = bd.GetSyntheticCDOPricers(corrObj, discountCurve, survivalCurves);
      if (cdoPricers_ == null)
        throw new System.NullReferenceException("CDO Pricers not available");
      FixCDOPricers(cdoPricers_);

      if (FixedRecoveryRate >= 0)
        cdoPricers_ = Array.ConvertAll<SyntheticCDOPricer, SyntheticCDOPricer>(cdoPricers_,
          delegate(SyntheticCDOPricer p)
          {
            SyntheticCDO cdo = p.CDO;
            cdo.FixedRecoveryRate = FixedRecoveryRate;
            cdo.FixedRecovery = true;
            return new SyntheticCDOPricer(cdo, p.Basket, p.DiscountCurve, p.Notional, p.RateResets);
          });
      cdoNames_ = Array.ConvertAll<SyntheticCDOPricer, string>(cdoPricers_,
        delegate(SyntheticCDOPricer p) { return p.CDO.Description; });

      if (CounterpartyHazardRate >= 0)
      {
        var cpCurve = new SurvivalCurve(
          cdoPricers_[0].AsOf, CounterpartyHazardRate);
        foreach (var p in cdoPricers_)
          p.CounterpartySurvivalCurve = cpCurve;
      }

      if (QcrModel != RecoveryCorrelationType.None)
      {
        foreach (var p in cdoPricers_)
          p.Basket.QCRModel = QcrModel;
      }

      int quadPoints_ = QuadraturePoints;
      if (GridSize >= 0 || quadPoints_ > 0)
        foreach (SyntheticCDOPricer p in cdoPricers_)
        {
          if (GridSize >= 0)
            p.Basket.GridSize = GridSize;
          if (quadPoints_ > 0)
            p.Basket.IntegrationPointsFirst = quadPoints_;
        }

      asOf_ = cdoPricers_[0].AsOf;
      settle_ = cdoPricers_[0].Settle;
      maturity_ = cdoPricers_[0].Maturity;

      // Default tests for sensitivities set rescaleStrikes = false
      // Create the resacleStrikesArray_ to hold true.
      rescaleStrikesArray_ = new bool[cdoPricers_.Length];
      for (int i = 0; i < cdoPricers_.Length; i++)
        rescaleStrikesArray_[i] = true;
      return;
    }

    private static void FixCDOPricers(SyntheticCDOPricer[] pricers)
    {
      foreach (var p in pricers)
      {
        var b = p.Basket;
        FixSurvivalCurves(b.SurvivalCurves);
      }
    }

    private static void FixSurvivalCurves(IEnumerable<SurvivalCurve> curves)
    {
      foreach (SurvivalCurve c in curves)
        FixCDSCurve(c);
    }

    private static void FixCDSCurve(SurvivalCurve curve)
    {
      if (curve == null) return;
      bool needFit = false;
      foreach (CurveTenor tenor in curve.Tenors)
        needFit = needFit || FixCDSLastCouponDate(tenor.Product as CDS);
      if (needFit)
        curve.Fit();
    }

    private static CycleRule ToCycleRule(Dt date, Frequency freq)
    {
      return freq == Frequency.Weekly || freq == Frequency.BiWeekly
        ? (CycleRule.Monday + (int)date.DayOfWeek())
        : (date.Day == 31 ? CycleRule.EOM
          : (CycleRule.First + date.Day - 1));
    }

    private static bool FixCDSLastCouponDate(CDS cds)
    {
      if (cds == null || cds.CycleRule != CycleRule.None || !cds.LastPrem.IsEmpty())
        return false;
      Dt start = cds.FirstCoupon;
      if (start.IsEmpty())
        return false;
      Dt maturity = cds.Maturity;
      if (start >= maturity)
        return false;
      CycleRule rule = ToCycleRule(start, cds.Freq);
      int n = 0;
      Dt next = start;
      Dt prev;
      do
      {
        prev = next;
        next = Dt.Add(start, cds.Freq, ++n, rule);
      } while (next < maturity);
      cds.LastPrem = prev;
      return true;
    }

    #endregion // SetUp

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string BasketDataFile { get; set; } = null;

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = null;

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = null;

    /// <summary>
    ///   Data for index notes
    /// </summary>
    public string IndexDataFile { get; set; } = null;

    /// <summary>
    ///   Data for index notes
    /// </summary>
    public string TrancheDataFile { get; set; } = null;

    /// <summary>
    ///   Copula data
    /// </summary>
    /// <remarks>
    /// Copula data is a comma delimted string in the format
    /// "CopulaType,DfCommon,DfIdiosyncratic". For example,
    /// "StudentT,7,7".
    /// </remarks>
    new public string CopulaData { get; set; } = null;

    /// <summary>
    ///   Correlation data
    /// </summary>
    public string CorrlationDataFile { get; set; } = null;

    /// <summary>
    ///   Basket type
    /// </summary>
    /// <remarks>
    /// One of 'Uniform', 'Homogeneous', 'Heterogeneous', 'MonteCarlo'.
    /// </remarks>
    public string BasketType { get; set; } = null;

    /// <summary>
    ///   Whether to rescale strikes
    /// </summary>
    public bool RescaleStrikes { get; set; } = false;

    /// <summary>
    ///   Grid size
    /// </summary>
    public double GridSize { get; set; } = -1;

    /// <summary>
    ///   Fixed recovery rate
    /// </summary>
    public double FixedRecoveryRate { get; set; } = -1;

    /// <summary>
    ///   Counterparty hazard rate
    /// </summary>
    public double CounterpartyHazardRate { get; set; } = -1;

    /// <summary>
    ///   QCR model.
    /// </summary>
    public RecoveryCorrelationType QcrModel { get; set; } = RecoveryCorrelationType.None;

    #endregion //Properties

    #region Data
    const double epsilon = 1.0E-7;
    
    // Data files

    // other params

    // Data to be initialized by set up routined
    protected Dt asOf_, settle_, maturity_;
    protected SyntheticCDOPricer[] cdoPricers_ = null;
    protected string[] cdoNames_ = null;
    protected bool[] rescaleStrikesArray_ = null;

    #endregion // Data
  }
}
