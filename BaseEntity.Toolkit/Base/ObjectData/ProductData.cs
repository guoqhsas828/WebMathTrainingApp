/*
 * CashflowCDOData.cs
 *
 *   2008. All rights reserved.
 *
 * Created by rsmulktis on 8/20/2008 4:04:11 PM
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude />
  public delegate PricerBase PricerFactoryFn(string name, double price, double lastReset);

  /// <summary>
  /// Base Test Case class.
  /// </summary>
  [Serializable]
  public class TestCase
  {
    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    public TestCase()
    {
    }
    #endregion

    #region Methods
    /// <summary>
    /// Load the test case from a file.
    /// </summary>
    /// 
    /// <typeparam name="T">TestCase Type</typeparam>
    /// <param name="filename">The file name to save to</param>
    /// 
    /// <returns>The test case.</returns>
    /// 
    public static T LoadFromFile<T>(string filename) where T : TestCase
    {
      System.Xml.XmlTextReader reader = new System.Xml.XmlTextReader(filename);
      XmlSerializer serializer = new XmlSerializer(typeof(T));

      if (!serializer.CanDeserialize(reader))
        throw new ToolkitException("Cannot deserialize test case!");

      // Load
      T testCase = (T)serializer.Deserialize(reader);

      // Done
      return testCase;
    }


    /// <exclude />
    protected DiscountData NewDiscountDataFromFactors(DiscountCurve curve)
    {
      DiscountData data = new DiscountData();

      data.AsOf = curve.AsOf.ToStr("%D");
      data.Category = curve.Category;
      data.Currency = curve.Ccy;
      data.Extrap = curve.ExtrapMethod;
      data.Interp = curve.InterpMethod;
      data.Name = curve.Name;

      data.Factors = new DiscountData.Factor[curve.Tenors.Count];
      for (int i = 0; i < curve.Tenors.Count; i++)
      {
        data.Factors[i] = new DiscountData.Factor();
        data.Factors[i].Maturity = curve.Tenors[i].Maturity.ToStr("%D");
        data.Factors[i].Discount = curve.DiscountFactor(curve.Tenors[i].Maturity);
      }

      return data;
    }

    /// <exclude />
    protected DiscountData NewDiscountDataFromBootstrap(DiscountCurve curve)
    {
      DiscountData data = new DiscountData();

      data.AsOf = curve.AsOf.ToStr("%D");
      data.Category = curve.Category;
      data.Currency = curve.Ccy;
      data.Extrap = curve.ExtrapMethod;
      data.Interp = curve.InterpMethod;
      data.Name = curve.Name;

      List<string> mmdates = new List<string>();
      List<double> mmrates = new List<double>();
      List<string> mmtenors = new List<string>();
      List<string> swapdates = new List<string>();
      List<double> swaprates = new List<double>();
      List<string> swaptenors = new List<string>();
      bool mminit = false, swapinit = false;
      data.Bootst = new DiscountData.Bootstrap();
      for (int i = 0; i < curve.Tenors.Count; i++)
      {
        if (curve.Tenors[i].Product is Note)
        {
          Note n = (Note)curve.Tenors[i].Product;
          mmdates.Add(curve.Tenors[i].Maturity.ToStr("%D"));
          mmtenors.Add(curve.Tenors[i].Name);
          mmrates.Add(n.Coupon);
          if (!mminit)
          {
            data.Bootst.MmDayCount = n.DayCount;
            mminit = true;
          }
        }
        else if (curve.Tenors[i].Product is SwapLeg)
        {
          SwapLeg s = (SwapLeg)curve.Tenors[i].Product;
          swapdates.Add(curve.Tenors[i].Maturity.ToStr("%D"));
          swaptenors.Add(curve.Tenors[i].Name);
          swaprates.Add(s.Coupon);
          if (!swapinit)
          {
            data.Bootst.SwapDayCount = s.DayCount;
            data.Bootst.SwapExtrap = ExtrapMethod.Const;
            data.Bootst.SwapInterp = InterpMethod.Cubic;
            data.Bootst.SwapFrequency = s.Freq;
            swapinit = true;
          }
        }
      }

      data.Bootst.MmMaturities = mmdates.ToArray();
      data.Bootst.MmRates = mmrates.ToArray();
      data.Bootst.MmTenors = mmtenors.ToArray();
      data.Bootst.SwapMaturities = swapdates.ToArray();
      data.Bootst.SwapRates = swaprates.ToArray();
      data.Bootst.SwapTenors = swaptenors.ToArray();

      return data;
    }

    /// <exclude />
    protected CreditData NewCreditDataFromSurvivalCurve(SurvivalCurve sc)
    {
      CreditData cd = new CreditData();
      cd.BasketSize = 1;
      cd.AsOf = sc.AsOf.ToStr("%D");
      cd.Currency = sc.Ccy;
      cd.DayCount = sc.DayCount;
      cd.Frequency = sc.Frequency;
      cd.Roll = BDConvention.None;
      cd.Interp = sc.InterpMethod;
      cd.Extrap = sc.ExtrapMethod;
      cd.NegSPTreat = sc.SurvivalCalibrator.NegSPTreatment;
      cd.ScalingFactors = null;
      cd.ScalingWeights = null;
      cd.ScalingTenorNames = null;

      cd.TenorNames = new string[sc.Tenors.Count];
      cd.Tenors = new string[sc.Tenors.Count];
      for (int i = 0; i < sc.Tenors.Count; i++)
      {
        cd.TenorNames[i] = sc.Tenors[i].Name;
        cd.Tenors[i] = sc.Tenors[i].Maturity.ToStr("%D");
      }

      cd.Credits = new CreditData.Credit[1];
      CreditData.Credit credit = new CreditData.Credit();
      credit.Name = (sc.Name ?? "[[[NoName]]]");
      credit.RecoveryRate = sc.SurvivalCalibrator.RecoveryCurve.RecoveryRate(sc.AsOf);
      credit.RecoveryDispersion = 0;
      credit.Category = sc.Category;
      CreditData.Credit.Quote[] quotes = new CreditData.Credit.Quote[sc.Tenors.Count];
      for (int j = 0; j < sc.Tenors.Count; ++j)
      {
        CreditData.Credit.Quote q = new CreditData.Credit.Quote();
        q.Maturity = null;
        if (sc.Tenors[j].Product is CDS)
          q.Spread = ((CDS)sc.Tenors[j].Product).Premium;
        quotes[j] = q;
      }
      credit.Quotes = quotes;
      cd.Credits[0] = credit;

      return cd;
    }
    #endregion
  }

  /// <exclude />
  public class SurvivalCurveData
  {
    /// <exclude />
    public DateTime AsOf;
    /// <exclude />
    public DateTime Settle;
    /// <exclude />
    public Currency Currency;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public InterpMethod Interpolation;
    /// <exclude />
    public ExtrapMethod Extrapolation;
    /// <exclude />
    public NegSPTreatment NegSPTreatment;
    /// <exclude />
    public double RecoveryRate;
    /// <exclude />
    public ProductData[] Products;
    /// <exclude />
    public DateTime[] TenorDates;
    /// <exclude />
    public string[] TenorNames;
    /// <exclude />
    public double[] MarketPv;

    /// <exclude />
    public SurvivalCurveData() { }

    /// <exclude />
    public SurvivalCurve ToSurvivalCurve(DiscountCurve dc)
    {
      Dt asOf = new Dt(this.AsOf);
      Dt settle = new Dt(this.Settle);
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle, this.RecoveryRate, dc);
      calibrator.NegSPTreatment = this.NegSPTreatment;
      SurvivalCurve sc = new SurvivalCurve(calibrator);
      sc.Ccy = this.Currency;
      sc.DayCount = this.DayCount;
      sc.Frequency = this.Frequency;
      sc.Interp = InterpFactory.FromMethod(this.Interpolation, this.Extrapolation);
      for (int i = 0; i < this.Products.Length; i++)
        sc.Add(this.Products[i].ToProduct(), this.MarketPv[i]);

      // Fit
      sc.Fit();

      // Done
      return sc;
    }
  }

  /// <exclude />
  public class PrepaymentCurveData
  {
    /// <exclude />
    public DateTime AsOf;
    /// <exclude />
    public string Category;
    /// <exclude />
    public Currency Currency;
    /// <exclude />
    public InterpMethod Interpolation;
    /// <exclude />
    public ExtrapMethod Extrapolation;
    /// <exclude />
    public DateTime[] TenorDates;
    /// <exclude />
    public string[] TenorNames;
    /// <exclude />
    public double[] Probabilities;

    /// <exclude />
    public PrepaymentCurveData() { }

    /// <exclude />
    public PrepaymentCurveData(SurvivalCurve sc)
    {
      this.AsOf = sc.AsOf.ToDateTime();
      this.Category = sc.Category;
      this.Currency = sc.Ccy;
      this.Interpolation = sc.InterpMethod;
      this.Extrapolation = sc.ExtrapMethod;
      this.Probabilities = new double[sc.Tenors.Count];
      this.TenorDates = new DateTime[sc.Tenors.Count];
      this.TenorNames = new string[sc.Tenors.Count];
      for (int i = 0; i < sc.Tenors.Count; i++)
      {
        this.TenorNames[i] = sc.Tenors[i].Name;
        this.TenorDates[i] = sc.Tenors[i].Maturity.ToDateTime();
        this.Probabilities[i] = sc.SurvivalProb(sc.Tenors[i].Maturity);
      }
    }

    /// <exclude />
    public SurvivalCurve ToSurvivalCurve()
    {
      Dt asOf = new Dt(this.AsOf);
      Converter<DateTime, Dt> f = delegate(DateTime date) { return new Dt(date); };
      Dt[] maturities = Array.ConvertAll(this.TenorDates, f);
      SurvivalCurve sc = SurvivalCurve.FromProbabilitiesWithBond(asOf, this.Currency, this.Category, this.Interpolation,
                                                                 this.Extrapolation, maturities, this.Probabilities,
                                                                 this.TenorNames, null, null, null, 0);
      // Done
      return sc;
    }
  }

  /// <summary>
  /// Test case for Loans.
  /// </summary>
  [Serializable]
  public class LoanTestCase : TestCase
  {
    /// <exclude />
    public LoanData Loan;
    /// <exclude />
    public DiscountData DiscountCurve;
    /// <exclude />
    public DiscountData ReferenceCurve;
    /// <exclude />
    public LoanPricerData Pricer;
    /// <exclude />
    public SurvivalCurveData SurvivalCurve;
    /// <exclude />
    public PrepaymentCurveData PrepaymentCurve;

    /// <exclude />
    public LoanTestCase() { }

    /// <exclude />
    public LoanPricer ToPricer()
    {
      Loan loan = (Loan)this.Loan.ToProduct();
      DiscountCurve dc = this.DiscountCurve.GetDiscountCurve();
      DiscountCurve rc = (this.ReferenceCurve == null ? dc : this.ReferenceCurve.GetDiscountCurve());
      SurvivalCurve sc = (this.SurvivalCurve == null ? null : this.SurvivalCurve.ToSurvivalCurve(dc));
      SurvivalCurve pc = (this.PrepaymentCurve == null ? null : this.PrepaymentCurve.ToSurvivalCurve());
      LoanPricer pricer = this.Pricer.ToPricer(loan, dc, rc, sc, pc);

      // Done
      return pricer;
    }
  }

  /// <exclude />
  [Serializable]
  [XmlInclude(typeof(NoteData))]
  [XmlInclude(typeof(BondData))]
  [XmlInclude(typeof(LoanData))]
  [XmlInclude(typeof(SwapLegData))]
  [XmlInclude(typeof(CDSData))]
  [XmlInclude(typeof(SyntheticCDOData))]
  public abstract class ProductData
  {
    /// <exclude />
    public string Name;
    /// <exclude />
    public Currency Currency;
    /// <exclude />
    public DateTime Maturity;
    /// <exclude />
    public DateTime Effective;

    /// <exclude />
    public ProductData() { }

    /// <exclude />
    public abstract Product ToProduct();
  }

  /// <exclude />
  [Serializable]
  public class BondData : ProductData
  {
    /// <exclude />
    public BondType BondType;
    /// <exclude />
    public double Coupon;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public DateTime FirstCoupon;
    /// <exclude />
    public DateTime LastCoupon;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Tenor Tenor;
    /// <exclude />
    public string Index;
    /// <exclude />
    public QuotingConvention QuotingConvention;
    /// <exclude />
    public Amortization[] AmortizationSchedule;
    /// <exclude />
    public CouponPeriod[] CouponSchedule;
    /// <exclude />
    public CallPeriod[] CallSchedule;
    /// <exclude />
    public PutPeriod[] PutSchedule;
    /// <exclude />
    public bool EomRule;
    /// <exclude />
    public bool PeriodAdjustment;

    /// <exclude />
    public BondData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      CycleRule cycleRule = (this.EomRule) ? CycleRule.EOM : CycleRule.None;
      Bond b = new Bond(new Dt(this.Effective), new Dt(this.Maturity), this.Currency, this.BondType, this.Coupon, this.DayCount, cycleRule, this.Frequency, this.BDConvention, this.Calendar);
      b.Description = this.Name;
      b.Index = this.Index;
      b.FirstCoupon = new Dt(this.FirstCoupon);
      b.LastCoupon = new Dt(this.LastCoupon);
      b.Tenor = this.Tenor;
      b.QuotingConvention = this.QuotingConvention;
      b.PeriodAdjustment = this.PeriodAdjustment;
      if (b.Floating)
        b.ReferenceIndex = new ReferenceIndices.InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 0);
      CollectionUtil.Add(b.AmortizationSchedule, this.AmortizationSchedule);
      CollectionUtil.Add(b.CouponSchedule, this.CouponSchedule);
      CollectionUtil.Add(b.CallSchedule, this.CallSchedule);
      CollectionUtil.Add(b.PutSchedule, this.PutSchedule);

      return b;
    }
  }

  /// <exclude />
  [Serializable]
  public class AmortizationData
  {
    /// <exclude />
    public DateTime Date;
    /// <exclude />
    public double Amount;
    /// <exclude />
    public AmortizationType Type;

    /// <exclude />
    public AmortizationData() { }

    /// <exclude />
    public Amortization ToAmortization()
    {
      return new Amortization(new Dt(Date), this.Type, Amount);
    }
  }

  /// <exclude />
  [Serializable]
  public class LoanData : ProductData
  {
    /// <exclude />
    public AmortizationData[] AmortizationSchedule;
    /// <exclude />
    public Pair<string, double>[] PricingGrid;
    /// <exclude />
    public DateTime FirstCoupon;
    /// <exclude />
    public DateTime LastCoupon;
    /// <exclude />
    public DateTime LastDraw;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public int RateResetDays;
    /// <exclude />
    public bool EomRule;
    /// <exclude />
    public bool PeriodAdjustment;
    /// <exclude />
    public string Index;
    /// <exclude />
    public LoanType LoanType;
    /// <exclude />
    public double CommitmentFee;
    /// <exclude />
    public string[] PerformanceLevels;
    /// <exclude />
    public bool UseDrawnNotional;

    /// <exclude />
    public LoanData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      Loan l = new Loan();

      l.BDConvention = this.BDConvention;
      l.Calendar = this.Calendar;
      l.Ccy = this.Currency;
      l.CommitmentFee = this.CommitmentFee;
      l.DayCount = this.DayCount;
      l.Description = this.Name;
      l.Effective = new Dt(this.Effective);
      l.CycleRule = (EomRule ? CycleRule.EOM : CycleRule.None);
      l.FirstCoupon = new Dt(this.FirstCoupon);
      l.Frequency = this.Frequency;
      l.Index = this.Index;
      l.LastCoupon = new Dt(this.LastCoupon);
      l.LastDraw = new Dt(this.LastDraw);
      l.LoanType = this.LoanType;
      l.Maturity = new Dt(this.Maturity);
      l.PeriodAdjustment = this.PeriodAdjustment;
      l.PerformanceLevels = this.PerformanceLevels;
      l.RateResetDays = this.RateResetDays;

      if (this.AmortizationSchedule != null && this.AmortizationSchedule.Length > 0)
      {
        Amortization[] amortizations = new Amortization[this.AmortizationSchedule.Length];
        for (int i = 0; i < this.AmortizationSchedule.Length; i++)
          amortizations[i] = this.AmortizationSchedule[i].ToAmortization();
        CollectionUtil.Add(l.AmortizationSchedule, amortizations);
      }

      for (int i = 0; i < this.PricingGrid.Length; i++)
        l.PricingGrid.Add(this.PricingGrid[i].Key, this.PricingGrid[i].Value);
      return l;
    }
  }

  /// <exclude />
  [Serializable]
  public class SwapLegData : ProductData
  {
    /// <exclude />
    public double Coupon;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public DateTime FirstCoupon;
    /// <exclude />
    public DateTime LastCoupon;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Tenor Tenor;
    /// <exclude />
    public string Index;
    /// <exclude />
    public Amortization[] AmortizationSchedule;
    /// <exclude />
    public CouponPeriod[] CouponSchedule;
    /// <exclude />
    public bool InitialExchange;
    /// <exclude />
    public bool IntermediateExchange;
    /// <exclude />
    public bool FinalExchange;
    /// <exclude />
    public bool IsFloating;

    /// <exclude />
    public SwapLegData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      SwapLeg l;

      if (this.IsFloating)
        l = new SwapLeg(new Dt(this.Effective), new Dt(this.Maturity), this.Currency, this.Coupon, this.DayCount, this.Frequency,
          this.BDConvention, this.Calendar, false, this.Tenor, this.Index);
      else
        l = new SwapLeg(new Dt(this.Effective), new Dt(this.Maturity), this.Currency, this.Coupon, this.DayCount, this.Frequency,
          this.BDConvention, this.Calendar, false);

      l.Description = this.Name;
      l.FirstCoupon = new Dt(this.FirstCoupon);
      l.LastCoupon = new Dt(this.LastCoupon);
      l.InitialExchange = this.InitialExchange;
      l.IntermediateExchange = this.IntermediateExchange;
      l.FinalExchange = this.FinalExchange;

      // Force a valid tenor
      if(l.IndexTenor == Tenor.Empty)
        l.IndexTenor = new Tenor(Frequency);

      CollectionUtil.Add(l.AmortizationSchedule, this.AmortizationSchedule);
      CollectionUtil.Add(l.CouponSchedule, this.CouponSchedule);

      return l;
    }
  }

  /// <exclude />
  [Serializable]
  public class NoteData : ProductData
  {
    /// <exclude />
    public double Coupon;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public DateTime FirstCoupon;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Amortization[] AmortizationSchedule;
    /// <exclude />
    public CouponPeriod[] CouponSchedule;

    /// <exclude />
    public NoteData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      Note n = new Note(new Dt(this.Effective), new Dt(this.Maturity), this.Currency, this.Coupon, this.DayCount,
                        this.Frequency, this.BDConvention, this.Calendar);
      CollectionUtil.Add(n.AmortizationSchedule, this.AmortizationSchedule);
      CollectionUtil.Add(n.CouponSchedule, this.CouponSchedule);

      // Done
      return n;
    }
  }

  /// <exclude />
  [Serializable]
  public class CDSData : ProductData
  {
    /// <exclude />
    public CdsType CdsType;
    /// <exclude />
    public double Premium;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public DateTime FirstPrem;
    /// <exclude />
    public DateTime LastPrem;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Amortization[] AmortizationSchedule;

    /// <exclude />
    public CDSData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      CDS cds = new CDS(new Dt(this.Effective), new Dt(this.Maturity), this.Currency, new Dt(this.FirstPrem), this.Premium, this.DayCount, this.Frequency, this.BDConvention, this.Calendar);
      cds.Description = this.Name;
      cds.CdsType = this.CdsType;
      cds.LastPrem = new Dt(this.LastPrem);

      CollectionUtil.Add(cds.AmortizationSchedule, this.AmortizationSchedule);

      return cds;
    }
  }

  /// <exclude />
  [Serializable]
  public class SyntheticCDOData : ProductData
  {
    /// <exclude />
    public double Premium;
    /// <exclude />
    public Calendar Calendar;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public double AttachmentPoint;
    /// <exclude />
    public double DetachmentPoint;
    /// <exclude />
    public DateTime FirstPremium;
    /// <exclude />
    public bool AccrueOnDefault;
    /// <exclude />
    public CdoType CdoType;
    /// <exclude />
    public double Notional;

    /// <exclude />
    public SyntheticCDOData() { }

    /// <exclude />
    public override Product ToProduct()
    {
      SyntheticCDO cdo = new SyntheticCDO(new Dt(this.Effective), new Dt(this.FirstPremium), new Dt(this.Maturity), this.Currency, this.DayCount,
        this.Frequency, this.BDConvention, this.Calendar, this.Premium, 0, this.AttachmentPoint, this.DetachmentPoint);
      cdo.AccruedOnDefault = AccrueOnDefault;
      cdo.CdoType = this.CdoType;
      cdo.Description = this.Name;
      cdo.Notional = this.Notional;
      return cdo;
    }
  }

  /// <exclude />
  [Serializable]
  [XmlInclude(typeof(BondPricerData))]
  [XmlInclude(typeof(LoanPricerData))]
  [XmlInclude(typeof(SwapLegPricerData))]
  [XmlInclude(typeof(CDSPricerData))]
  [XmlInclude(typeof(SyntheticCDOPricerData))]
  public abstract class PricerBaseData
  {
    /// <exclude />
    public DateTime AsOf;
    /// <exclude />
    public DateTime Settle;
    /// <exclude />
    public double Notional;
    /// <exclude />
    public string Product;
    /// <exclude />
    public double CurrentNotional;

    /// <exclude />
    public PricerBaseData() { }

    /// <exclude />
    public abstract PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves);
  }

  /// <exclude />
  [Serializable]
  public class BondPricerData : PricerBaseData
  {
    /// <exclude />
    public QuotingConvention QuotingConvention;
    /// <exclude />
    public double Quote;
    /// <exclude />
    public double LastReset;
    /// <exclude />
    public double RecoveryRate;

    /// <exclude />
    public BondPricerData() { }

    /// <exclude />
    public override PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves)
    {
      Bond p = (Bond)asset;
      BondPricer pricer = new BondPricer(
        p,
        new Dt(this.AsOf),
        new Dt(this.Settle),
        dc,
        null, 0, TimeUnit.None,
        this.RecoveryRate);
      pricer.MarketQuote = this.Quote;
      pricer.QuotingConvention = this.QuotingConvention;
      pricer.RateResets.Add(new RateReset(p.Effective, this.LastReset));
      pricer.CurrentRate = LastReset;
      pricer.Notional = this.Notional;
      pricer.ReferenceCurve = dc;

      if (this.RecoveryRate >= -1e-8)
      {
        pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve(this.RecoveryRate);
        pricer.SurvivalCurve.Name = pricer.Product.Description + "_SurvivalCurve";
      }

      return pricer;
    }
  }

  /// <exclude />
  [Serializable]
  public class InterestPeriodData
  {
    /// <exclude />
    public DateTime StartDate;
    /// <exclude />
    public DateTime EndDate;
    /// <exclude />
    public double AnnualizedCoupon;
    /// <exclude />
    public double PercentageNotional;
    /// <exclude />
    public Frequency Frequency;
    /// <exclude />
    public DayCount DayCount;
    /// <exclude />
    public BDConvention BDConvention;
    /// <exclude />
    public Calendar Calendar;

    /// <exclude />
    public InterestPeriodData() { }

    /// <exclude />
    public InterestPeriod ToInterestPeriod()
    {
      return new InterestPeriod(new Dt(this.StartDate), new Dt(this.EndDate), this.AnnualizedCoupon,
                                this.PercentageNotional,
                                this.Frequency, this.DayCount, this.BDConvention, this.Calendar);
    }
  }

  /// <exclude />
  [Serializable]
  public class LoanPricerData : PricerBaseData
  {
    /// <exclude />
    public QuotingConvention QuotingConvention;
    /// <exclude />
    public double Quote;
    /// <exclude />
    public double LastReset;
    /// <exclude />
    public double RecoveryRate;
    /// <exclude />
    public double RefinancingCost = 0;
    /// <exclude />
    public double TargetExpectedWAL = double.NaN;
    /// <exclude />
    public double[] Usage;
    /// <exclude />
    public double[] PerformanceDistribution;
    /// <exclude />
    public InterestPeriodData[] InterestPeriods;
    /// <exclude />
    public string CurrentLevel;
    /// <exclude />
    public bool UseDrawnNotional = true;

    /// <exclude />
    public LoanPricerData() { }

    /// <exclude />
    public override PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves)
    {
      Loan p = (Loan)asset;
      LoanPricer pricer;
      Dt asOf = new Dt(this.AsOf);
      Dt settle = new Dt(this.Settle);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf, this.RecoveryRate);
      IList<InterestPeriod> periods = ArrayUtil.Convert<InterestPeriodData, InterestPeriod>(InterestPeriods, Convert);
      
      // Create Pricer
      if (!double.IsNaN(TargetExpectedWAL))
        pricer = LoanPricerFactory.New(p, asOf, settle, Notional, Notional, dc, dc, null, recoveryCurve,
                                       TargetExpectedWAL, RefinancingCost, Quote, CalibrationType.SurvivalCurve,
                                       Loan.DefaultPerformanceLevel, null, null, periods);
      else
        pricer = LoanPricerFactory.New(p, asOf, settle, Notional, Notional, dc, dc, null, recoveryCurve,
                                       null, RefinancingCost, Quote, CalibrationType.SurvivalCurve,
                                       Loan.DefaultPerformanceLevel, null, null, periods);
      // Done
      return pricer;
    }

    /// <exclude />
    public LoanPricer ToPricer(Loan loan, DiscountCurve dc, DiscountCurve rc, SurvivalCurve sc, SurvivalCurve pc)
    {
      Dt asOf = new Dt(AsOf);
      Dt settle = new Dt(Settle);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf, RecoveryRate);
      LoanPricer pricer;
      IList<InterestPeriod> periods = ArrayUtil.Convert<InterestPeriodData, InterestPeriod>(InterestPeriods, Convert);
      CalibrationType calType = (sc == null ? CalibrationType.SurvivalCurve : CalibrationType.None);

      // Create Pricer
      if (pc == null && !double.IsNaN(TargetExpectedWAL))
        pricer = LoanPricerFactory.New(loan, asOf, settle, Notional, CurrentNotional, UseDrawnNotional, dc, rc, sc, recoveryCurve,
                                       TargetExpectedWAL, RefinancingCost, Quote, calType,
                                       CurrentLevel, Usage, PerformanceDistribution, periods);
      else
        pricer = LoanPricerFactory.New(loan, asOf, settle, Notional, CurrentNotional, UseDrawnNotional, dc, rc, sc, recoveryCurve,
                                       pc, RefinancingCost, Quote, calType,
                                       CurrentLevel, Usage, PerformanceDistribution, periods);
      pricer.UseDrawnNotional = UseDrawnNotional;
      // Done
      return pricer;
    }

    private InterestPeriod Convert(InterestPeriodData data)
    {
      return data.ToInterestPeriod();
    }
  }

  /// <exclude />
  [Serializable]
  public class SwapLegPricerData : PricerBaseData
  {
    /// <exclude />
    public double LastReset;

    /// <exclude />
    public SwapLegPricerData() { }

    /// <exclude />
    public override PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves)
    {
      Product p = asset;
      SwapLegPricer pricer = new SwapLegPricer(
        (SwapLeg)p,
        new Dt(this.AsOf),
        new Dt(this.Settle),
        this.Notional,
        dc,
        dc.ReferenceIndex,
        dc, new RateResets(p.Effective, this.LastReset),
        null, null);
      return pricer;
    }
  }

  /// <exclude />
  [Serializable]
  public class CDSPricerData : PricerBaseData
  {
    /// <exclude />
    public double LastReset;
    /// <exclude />
    public string SurvivalCurve;

    /// <exclude />
    public CDSPricerData() { }

    /// <exclude />
    public override PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves)
    {
      Product p = asset;
      SurvivalCurve sc = Array.Find<SurvivalCurve>(curves, new Predicate<SurvivalCurve>(delegate(SurvivalCurve s) { return s.Name == this.SurvivalCurve; }));
      CDSCashflowPricer pricer = new CDSCashflowPricer((CDS)p, new Dt(this.AsOf), new Dt(this.Settle), dc, sc, null, 0, 0, TimeUnit.None);
      pricer.RateResets.Add(new RateReset(p.Effective, this.LastReset));
      pricer.Notional = this.Notional;
      pricer.ReferenceCurve = dc;
      return pricer;
    }
  }

  /// <exclude />
  [Serializable]
  public class SyntheticCDOPricerData : PricerBaseData
  {
    /// <exclude />
    public double LastReset;
    /// <exclude />
    public Pair<string, double>[] Credits;

    /// <exclude />
    public SyntheticCDOPricerData() { }

    /// <exclude />
    public override PricerBase ToPricer(Product asset, DiscountCurve dc, SurvivalCurve[] curves)
    {
      Product p = asset;
      List<RateReset> resets = new List<RateReset>();
      resets.Add(new RateReset(p.Effective, this.LastReset));
      SurvivalCurve[] sc = new SurvivalCurve[this.Credits.Length];
      string[] curveNames = new string[this.Credits.Length];
      double[] principals = new double[this.Credits.Length];

      // Build arrays
      for (int i = 0; i < this.Credits.Length; i++)
      {
        int j = i;
        sc[i] = Array.Find(curves, c => (c != null && c.Name == Credits[j].Key));
        curveNames[i] = this.Credits[i].Key;
        principals[i] = this.Credits[i].Value;
      }

      SyntheticCDOPricer pricer = BasketPricerFactory.CDOPricerSemiAnalytic(
        (SyntheticCDO)p,
        Dt.Empty,
        new Dt(this.AsOf),
        new Dt(this.Settle),
        dc,
        sc, //Survival Curves
        principals, //Weights
        new Copula(CopulaType.Gauss, 0, 0),
        new SingleFactorCorrelation(curveNames, 0.3),
        0, TimeUnit.None, 0, 0,
        this.CurrentNotional,
        false,
        false,
        resets);

      return pricer;
    }
  }
}
