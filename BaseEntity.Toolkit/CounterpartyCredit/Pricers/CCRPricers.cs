using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;
using log4net;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  #region Utils

  /// <exclude></exclude>
  public static class CcrPricerUtils
  {
    /// <exclude></exclude>
    public static void FastFillCashflow(DiscountCurve referenceCurve, Frequency freq, Cashflow cf,
                                          double[] accrualFactors, double[] spreads, Dt from)
    {
      for (int i = 0; i < cf.Count; ++i)
      {
        if (cf.GetDt(i) <= from || !cf.GetProjectedAt(i))
          continue;
        Dt start = cf.GetStartDt(i);
        Dt end = cf.GetEndDt(i);
        //match FillFloat
        double price = referenceCurve.DiscountFactor(start, end);
        double f = RateCalc.RateFromPrice(price, accrualFactors[i], freq);
        if (spreads != null)
          f += spreads[i];
        cf.Set(i, cf.GetAmount(i), f*cf.GetPeriodFraction(i)*cf.GetPrincipalAt(i), cf.GetDefaultAmount(i));
      }
    }

    /// <exclude></exclude>
    public static void FastFillCashflow(Payment[][] paymentSchedule, Cashflow cf, Dt from)
    {
      int idx = 0;
      while (cf.GetDt(idx) <= from)
        ++idx;
      if (idx > 0)
        idx--;
      for (int i = idx; i < cf.Count; i++)
      {
        if (!cf.GetProjectedAt(i))
          continue;
        double amt = 0.0;
        Payment[] pmts = paymentSchedule[i];
        InterestPayment ip = null;
        for (int j = 0; j < pmts.Length; ++j)
        {
          Payment p = pmts[j];
          var otp = p as OneTimePayment;
          if (otp != null)
            amt += p.DomesticAmount;
          else
            ip = p as InterestPayment;
        }
        if (ip != null)
          cf.Set(i, amt/cf.OriginalPrincipal, ip.DomesticAmount/cf.OriginalPrincipal, cf.GetDefaultAmount(i));
        else
          cf.Set(i, amt/cf.OriginalPrincipal, 0.0, cf.GetDefaultAmount(i));
      }
    }

    /// <exclude></exclude>
    public static double Pv(Dt settle, Payment[][] paymentSchedule, DiscountCurve discountCurve,
                              bool discountingAccrued, bool includeSettlePayments, double productNotional,
                              double pricerNotional, bool cleanPv = false)
    {
      int idx = 0;
      for (int i = 0; i < paymentSchedule.Length; ++i, ++idx)
      {
        if (paymentSchedule[i][0].PayDt < settle)
          continue;
        if (paymentSchedule[i][0].PayDt == settle)
        {
          if (includeSettlePayments)
            break;
          continue;
        }
        break;
      }
      if (idx >= paymentSchedule.Length)
        return 0.0;
      double settleDf = discountCurve.Interpolate(settle);
      double pv = 0.0;
      //acccrued
      Payment[] payments = paymentSchedule[idx];
      double df = discountCurve.Interpolate(payments[0].PayDt)/settleDf;
      for (int i = 0; i < payments.Length; ++i)
      {
        Payment p = payments[i];
        p.VolatilityStartDt = settle;
        if (discountingAccrued && !cleanPv)
        {
          pv += df * p.DomesticAmount;
          if (SwapLegCcrPricer.BinaryLogger.IsObjectLoggingEnabled)
          {
            CCRPricerDiagnosticsUtil.AddTableEntry(settle, p.PayDt, idx, df, p.DomesticAmount, productNotional, pricerNotional);
          }
          continue;
        }
        var ip = p as InterestPayment;
        if (ip == null || ip.AccrualStart >= settle)
        {
          pv += df * p.DomesticAmount;
          continue;
        }
        var accrual = ip.DomesticAmount;
        var accrued = ((double)Dt.Diff(ip.AccrualStart, settle, ip.DayCount)) /
          Dt.Diff(ip.AccrualStart, ip.AccrualEnd, ip.DayCount) * accrual;
        if (discountingAccrued)
        {
          pv += df * accrual;
        }
        else
        {
          accrual -= accrued;
          pv += accrued + df * accrual;
        }
        if (cleanPv) pv -= accrued;
      }
      ++idx;
      for (int i = idx; i < paymentSchedule.Length; ++i)
      {
        payments = paymentSchedule[i];
        df = discountCurve.Interpolate(payments[0].PayDt)/settleDf;
        for (int j = 0; j < payments.Length; ++j)
        {
          Payment p = payments[j];
          p.VolatilityStartDt = settle;
          pv += df*p.DomesticAmount;
          if (SwapLegCcrPricer.BinaryLogger.IsObjectLoggingEnabled)
          {
            CCRPricerDiagnosticsUtil.AddTableEntry(settle, p.PayDt, i, df, p.DomesticAmount, productNotional, pricerNotional);
          }
        }
      }
      if (productNotional != 0.0 && pricerNotional != 1.0)
        pv /= productNotional; //normalize
      return pv*pricerNotional;
    }

    public static double CfForwardValue(this Cashflow cf,
      BondPricer pricer, Dt settle, double fullPrice, Dt expiry)
    {
      var dc = pricer.DiscountCurve;
      var sc = pricer.SurvivalCurve;

      var effective = cf.Effective;
      if (effective > expiry)
      {
        // Bond will be issued after the expiry
        var pv = CashflowModel.Price(cf, effective, effective,
          dc, sc, null, 0, (int) pricer.CashflowModelFlags,
          pricer.StepSize, pricer.StepUnit, cf.Count);
        return pv*dc.DiscountFactor(expiry, effective)*(
          sc == null ? 1.0 : sc.SurvivalProb(expiry, effective));
      }
      if (effective > settle)
      {
        // Bond will be issued after the settle but before the expiry
        return CashflowModel.Price(cf, expiry, expiry,
          dc, sc, null, 0, (int) pricer.CashflowModelFlags,
          pricer.StepSize, pricer.StepUnit, cf.Count);
      }

      // Bond issued on or before the settle
      var df = dc.DiscountFactor(settle, expiry);
      var sp = sc == null ? 1.0 : sc.SurvivalProb(settle, expiry);

      int idx = 0;
      for (int n = cf.Count; idx < n; ++idx)
      {
        if (cf.GetEndDt(idx) > expiry) break;
      }
      var cfpv = CashflowModel.Price(cf, settle, settle,
        dc, sc, null, 0, (int) pricer.CashflowModelFlags,
        pricer.StepSize, pricer.StepUnit, idx);
      Dt begin;
      if (sc != null && (begin = cf.GetStartDt(idx)) < expiry)
      {
        Dt end = cf.GetEndDt(idx);
        double df0 = 1.0, sp0 = 1.0;
        if (begin > settle)
        {
          df0 = dc.DiscountFactor(settle, begin);
          sp0 = sc.SurvivalProb(settle, begin);
        }
        double df1 = dc.DiscountFactor(settle, end),
          sp1 = sc.SurvivalProb(settle, end);
        // we should add protection till expiry
        cfpv += 0.5*((df0 + df1)*(sp0 - sp1) - (df + df1)*(sp - sp1))
          *cf.GetDefaultAmount(idx);
      }
      return (fullPrice - cfpv)/df/sp;
    }

    /// <summary>
    ///  Sets the ApproximateForFastCalculation flag
    ///  based on the global configurations.
    /// </summary>
    /// <param name="pricer">The pricer</param>
    /// <returns>The original flag value</returns>
    public static bool SetApproximateForFastCalculation(
      PricerBase pricer)
    {
      bool approx = pricer.ApproximateForFastCalculation;
      pricer.ApproximateForFastCalculation = ToolkitConfigurator.Settings
        .Simulations.AlwaysUseApproximateForFastCalculation || approx;
      return approx;
    }
  }

  #endregion

  #region CCRPricerDiagnosticsUtil

  /// <summary>
  /// Util for Diagnostic Logging of the CCR Pricers
  /// </summary>
  public class CCRPricerDiagnosticsUtil
  {
    public static void AddTableEntry(Dt settle, Dt payDt, int index, double df, double domesticAmount, double productNotional, double pricerNotional)
    {
      var datatable = CCRPricerDiagnosticsUtil.GetDiagnosticDataTable("SwapLegCcrPricer");
      var row = datatable.NewRow();
      row["PathId"] = ObjectLoggerUtil.GetPath("CCRPricerPath");
      row["PricerSettleDt"] = settle;
      row["PaymentDt"] = payDt;
      row["Idx"] = index;
      row["DiscountFactor"] = df;
      row["DomesticAmount"] = domesticAmount;
      row["ProductNotional"] = productNotional;
      row["PricerNotational"] = pricerNotional;
      row["SwapLegId"] = CCRPricerDiagnosticsUtil.GetSwapLegId();
      datatable.Rows.Add(row);
      CCRPricerDiagnosticsUtil.SetDiagnosticDataTable(datatable, "SwapLegCcrPricer");
    }

    public static DataTable GetDiagnosticDataTable(string key)
    {
      return (DataTable)Thread.GetData(Thread.GetNamedDataSlot(key));
    }

    public static void SetDiagnosticDataTable(DataTable dt, string key)
    {
      Thread.SetData(Thread.GetNamedDataSlot(key), dt);
    }

    public static string GetSwapLegId()
    {
      return (string)Thread.GetData(Thread.GetNamedDataSlot("SwapLegId"));
    }

    public static void SetSwapLegId(string swapLegId)
    {
      Thread.SetData(Thread.GetNamedDataSlot("SwapLegId"), swapLegId);
    } 
  }

  #endregion

  #region Configuration

  /// <exclude/>
  [Serializable]
  public class CcrPricerConfig
  {
    /// <exclude/>
    [ToolkitConfig("If false, CCR pricer uses fixed flat volatility to evaluate forward option prices; otherwise, allow forward volatility to vary with time")]
    public readonly bool EnableForwardVolatilityTermStructure = false;

    /// <exclude/>
    [ToolkitConfig("If true, enable the new and heavily optimized routine for calibration volatilities/factors from swaption volatility surface")]
    public readonly bool EnableFastCalibrationFromSwaptionVolatility = false;

    /// <exclude/>
    [ToolkitConfig("If true, enable the optimized exposure calculator")]
    public readonly bool EnableOptimizedExposureCalculator = true;

    /// <exclude/>
    [ToolkitConfig("The assembly qualified name of the type to create optimized exposure evaluator")]
    public readonly string OptimizerFactory = null;

    /// <exclude/>
    [ToolkitConfig("If true, payments starts with pricer settle date; otherwise, from the as-of date")]
    public readonly bool PaymentScheduleFromSettle = false;

    /// <exclude/>
    [ToolkitConfig("If true, the volatility for convexity adjustment will fixed at initial ATM levels, not change as the simulated rate changes")]
    public readonly bool FixVolatilityForConvexityAdjustment = true;

    /// <exclude/>
    [ToolkitConfig("If true (recommended), the volatility for convexity adjustment starts with the exposure dates; otherwise, starts with the pricing date")]
    public readonly bool CmsCapletConvexityFromExposureDate = false;

    [ToolkitConfig("The AMC realization generator to use if PEO is enabled.  Empty means no optimizer in AMC.")]
    public readonly string AmcRealizationGeneratorBuilder = "BaseEntity.Toolkit.Peo.Simulation.PeoRealizationBuilder, BaseEntity.Toolkit.Peo";
  }

  #endregion

  #region CcrPricer

  /// <summary>
  /// fast pricer interface
  /// </summary>
  public interface ISimulationPricer
  {
    /// <summary>
    /// Denomination currency of the Pv
    /// </summary>
    Currency Ccy { get; }

    /// <summary>
    /// Net present value of a stream of future cash flows 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv discounted to settle</returns>
    /// <remarks>The public state of the pricers might be modified</remarks>
    double FastPv(Dt settle);

    /// <summary>
    /// Pv depends on given term structure 
    /// </summary>
    /// <param name="marketObject"></param>
    /// <returns></returns>
    /// <remarks>Implement to optimize recalculation in sensitivities</remarks>
    bool DependsOn(object marketObject);

    /// <summary>
    /// Significant dates for exposure profile of this pricer
    /// </summary>
    Dt[] ExposureDates { get; set; }
  }

  /// <summary>
  /// Fast pricers for CCR simulations
  /// </summary>
  [Serializable]
  public class CcrPricer : BaseEntityObject, ISimulationPricer
  {
    /// <exclude></exclude>
    protected static readonly ILog logger = LogManager.GetLogger(typeof(CcrPricer));

    #region Properties

    /// <summary>
    /// Denomination currency of the Pv
    /// </summary>
    public Currency Ccy { get; protected set; }

    /// <summary>
    ///Underlying pricer 
    /// </summary>
    public IPricer Pricer { get; protected set; }


    /// <summary>
    /// ISimulationPricer constructed for IPricer.PaymentPricer
    /// </summary>
    public ISimulationPricer PaymentPricer { get; protected set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    public CcrPricer()
    {}

    /// <summary>
    /// Constructor of a default CcrPricer. 
    /// Default CcrPricers are simply IPricer
    /// </summary>
    /// <param name="pricer">IPricer object</param>
    public CcrPricer(IPricer pricer)
    {
      Pricer = pricer;
      Ccy = pricer.ValuationCurrency;
      if (pricer.PaymentPricer != null)
        PaymentPricer = Get(pricer.PaymentPricer);
    }

    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="pricer">IPricer object</param>
    /// <returns>CcrPricer wrapper</returns>
    public static CcrPricer Get(IPricer pricer)
    {
      if (pricer is SyntheticCDOPricer)
        return new CdoCcrPricer((SyntheticCDOPricer)pricer);
      if (pricer is SwapPricer)
        return new SwapCcrPricer((SwapPricer)pricer);
      if (pricer is MultiLeggedSwapPricer)
        return new SwapCcrPricer((MultiLeggedSwapPricer)pricer);
      if (pricer is SwapLegPricer)
        return new SwapLegCcrPricer((SwapLegPricer)pricer);
      if (pricer is InflationBondPricer)
        return new InflationBondCcrPricer((InflationBondPricer)pricer);
      if (pricer is CDSCashflowPricer && ((CDSCashflowPricer)pricer).FxCurve == null)
        return new CdsCcrPricer((CDSCashflowPricer)pricer);
      if (pricer is CDXPricer)
        return new CdxCcrPricer((CDXPricer)pricer);
      if (pricer is BondPricer)
        return new BondCcrPricer((BondPricer)pricer);
      if (pricer is CDSOptionPricer || pricer is ICreditIndexOptionPricer || pricer is SwaptionBlackPricer ||
          pricer is FxOptionPricerBase || pricer is StockOptionPricer || pricer is CommodityOptionPricer ||
          pricer is CommodityForwardOptionBlackPricer || pricer is CommodityFutureOptionBlackPricer || pricer is BondOptionBlackPricer)
      {
        var option = pricer.Product as IBasicExoticOption;
        if (option != null)
        {
          if (option.IsSingleBarrier())
            return new SingleBarrierOptionCcrPricer(pricer);
          if (option.IsDoubleBarrier())
            return new DoubleBarrierOptionCcrPricer(pricer);
        }
        // make sure option style is european. Should always be true for CCR as Bermudan/Americans go through AMC
        // but for HVaR we want to fall back to underlying tree pricers
        var singleAssetOption = pricer.Product as SingleAssetOptionBase;
        if (singleAssetOption != null)
        {
          if (singleAssetOption.Style == OptionStyle.European)
            return new OptionCcrPricer(pricer);
          else
            return new CcrPricer(pricer);
        }
        return new OptionCcrPricer(pricer);
      }
      if (pricer is CapFloorPricerBase)
        return new CapFloorCcrPricer((CapFloorPricerBase)pricer);
      if (pricer is StockPricer)
      {
        // although StockPricer implements GetPaymentSchedule, 
        // the schedule generated shows just the dividends and will not price the stock correctly
        return new CcrPricer(pricer);
      }
      if (pricer is CommoditySwapPricer)
        return new CommoditySwapCcrPricer((CommoditySwapPricer)pricer);
      if (pricer is CommoditySwapLegPricer)
        return new CommoditySwapLegCcrPricer((CommoditySwapLegPricer)pricer);
      if (pricer is PricerBase)
      {
        var p = pricer as PricerBase;
        try
        {
          var ps = p.GetPaymentSchedule(null, GetPsFromDate(p));
        }
        catch
        {
          return new CcrPricer(pricer);
        }
        return new CcrPricerWithPaymentSchedule(pricer);
      }

      return new CcrPricer(pricer);
    }

    public static Dt GetPsFromDate(PricerBase pricer)
    {
      if (pricer is CapFloorPricerBase)
      {
        //TODO: revisit this.
        //  Cap/Floor CCR pricer hard-coded cutoff = pricer.Settle
        return pricer.Settle;
      }
      return ToolkitConfigurator.Settings.CcrPricer.PaymentScheduleFromSettle
        ? pricer.Settle : pricer.AsOf;
    }
    #endregion

    #region Methods

    /// <summary>
    /// Net present value of a stream of future cash flows 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv discounted to settle</returns>
    /// <remarks>The public state of the pricers might be modified</remarks>
    public virtual double FastPv(Dt settle)
    {
      if (Pricer == null) return 0.0;
      Pricer.Settle = settle;
      Pricer.AsOf = settle;
      Pricer.Reset();
      var pv = Pricer.Pv();
      if (PaymentPricer != null)
        pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="marketObject"></param>
    /// <returns></returns>
    public virtual bool DependsOn(object marketObject)
    {
      var evaluator = new PricerEvaluator(Pricer);
      var curve = marketObject as Curve;
      if (curve == null)
        return false; 
      return evaluator.DependsOn(curve);
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public virtual Dt[] ExposureDates
    {
      get
      {
				if(_exposureDts == null)
					_exposureDts = new Dt[] { Pricer.Settle, Dt.Add(Pricer.Product.Maturity, -1) , Pricer.Product.Maturity };
	      return _exposureDts; 
      }
      set
      {
        _exposureDts = value;
        Init();
      }
    }

    private void Init()
    {
      var exposureDts = new UniqueSequence<Dt>();
      var maxDt = Dt.Roll(Pricer.Product.Maturity, BDConvention.Following, Calendar.None);
      if (_exposureDts != null && _exposureDts.Any(dt => dt <= maxDt))
      {
        exposureDts.Add(_exposureDts.Where(dt => dt <= maxDt).ToArray());
        var lastDt = exposureDts.Max();
        if (lastDt < maxDt && _exposureDts.Any(dt => dt > lastDt))
          exposureDts.Add(_exposureDts.First(dt => dt > lastDt));
      }
      exposureDts.Add(Pricer.AsOf);
      _exposureDts = exposureDts.ToArray();
    }

    /// <summary>
    /// Gets the payment schedule and attaches the convexity adjustment
    ///  volatility fixers based on the user configurations.
    /// </summary>
    /// <param name="pricer">The pricer</param>
    /// <param name="settle">The settle date</param>
    /// <returns>PaymentSchedule.</returns>
    public static PaymentSchedule GetPaymentSchedule(
      PricerBase pricer, Dt settle)
    {
      return AttachVolatilityFixer(pricer.GetPaymentSchedule(null, settle));
    }

    /// <summary>
    /// Attaches the volatility fixer.
    /// </summary>
    /// <param name="ps">The payments.</param>
    /// <returns>PaymentSchedule.</returns>
    public static PaymentSchedule AttachVolatilityFixer(PaymentSchedule ps)
    {
      if (ToolkitConfigurator.Settings.CcrPricer.FixVolatilityForConvexityAdjustment)
      {
        ConvexityAdjustmentUtility.AttachVolatilityFixers(ps);
      }
      return ps;
    }

    #endregion

    [NonSerialized][NoClone] private Dt[] _exposureDts; 
  }

  #endregion

  #region BondCcrPricer

  /// <summary>
  /// CCR Bond pricer
  /// </summary>
  [Serializable]
  public class BondCcrPricer : CcrPricer
  {
    [NonSerialized][NoClone] private double[] AccrualFactors;
    [NonSerialized][NoClone] private Cashflow LazyCashflow;
    [NonSerialized][NoClone] private double[] Spreads;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="bondPricer">Bond pricer object</param>
    public BondCcrPricer(BondPricer bondPricer)
      : base(bondPricer)
    {
    }

    private void Init()
    {
      var pricer = (BondPricer) Pricer;
      Dt next = pricer.NextCouponDate();
      Dt exDivDate = BondModelUtil.ExDivDate(pricer.Bond, next);
      if (pricer.TradeSettle < exDivDate)
        LazyCashflow = pricer.TradeCashflow;
      else
        LazyCashflow = pricer.Cashflow;
      if (pricer.Bond.Floating)
      {
        AccrualFactors = new double[LazyCashflow.Count];
        Spreads = new double[LazyCashflow.Count];
        for (int i = 0; i < LazyCashflow.Count; ++i)
        {
          Dt start = LazyCashflow.GetStartDt(i), end = LazyCashflow.GetEndDt(i);
          AccrualFactors[i] = pricer.Bond.Schedule.Fraction(start, end, pricer.Bond.DayCount);
          Spreads[i] = CouponPeriodUtil.CouponAt(pricer.Bond.CouponSchedule, pricer.Bond.Coupon, start);
        }
      }
    }

    /// <summary>
    /// Pv of a forward settling at settle CDS discounted to settle
    /// </summary>
    /// <param name="settle">Future settlement date</param>
    /// <returns>Present value of the cds contract discounted to settle </returns>
    public override double FastPv(Dt settle)
    {
      var pricer = (BondPricer) Pricer;
      if (settle > pricer.Bond.Maturity)
        return 0.0;
      if (LazyCashflow == null)
        Init();
      else if (pricer.Bond.Floating)
        CcrPricerUtils.FastFillCashflow(pricer.ReferenceCurve ?? pricer.DiscountCurve, pricer.Bond.Freq, LazyCashflow,
                                        AccrualFactors, Spreads, settle);
      double pv = CashflowModel.Pv(LazyCashflow, settle, settle, pricer.DiscountCurve, pricer.SurvivalCurve, null, 0.0,
                                   false, false, pricer.DiscountingAccrued, pricer.StepSize, pricer.StepUnit)*
                  pricer.Notional;
      if (LazyCashflow.DefaultPayment != null)
        pv += LazyCashflow.DefaultPayment.DefaultPv(settle, settle, pricer.DiscountCurve, false)*pricer.Notional;
      return pv;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="fwdSettleDate"></param>
    /// <param name="fullPrice"></param>
    /// <returns></returns>
    public double ForwardPrice(Dt settle, Dt fwdSettleDate, double fullPrice)
    {
      var pricer = (BondPricer) Pricer;
      if (settle > pricer.Bond.Maturity || IsOnLastDate(settle))
        return 0.0;

      if (LazyCashflow == null)
        Init();
      else if (pricer.Bond.Floating)
        CcrPricerUtils.FastFillCashflow(pricer.ReferenceCurve ?? pricer.DiscountCurve, pricer.Bond.Freq, LazyCashflow,
                                        AccrualFactors, Spreads, settle);

      if (pricer.HasDefaulted)
        return LazyCashflow.DefaultPayment != null
                 ? BondModelDefaulted.FwdPrice(fullPrice, pricer.DefaultPaymentDate, LazyCashflow.DefaultPayment.Amount,
                                               LazyCashflow.DefaultPayment.Accrual, settle, fwdSettleDate, pricer.DiscountCurve)
                 : 0.0;

      return LazyCashflow.CfForwardValue(pricer, settle, fullPrice, fwdSettleDate);
    }

    private bool IsOnLastDate(Dt settle)
    {
      var pricer = (BondPricer)Pricer;
      if (pricer.IsDefaulted(settle))
      {
        if (pricer.DefaultPaymentDate.IsValid() && settle == pricer.DefaultPaymentDate)
          return true;
        return false;
      }
      return (settle == pricer.Bond.Maturity);
    }
  }

  #endregion

  #region CdsCcrPricer

  /// <summary>
  /// CCR CDS pricer. Caches and compresses cashflows 
  /// </summary>
  [Serializable]
  public class CdsCcrPricer : CcrPricer
  {
    [NonSerialized][NoClone] private double[] AccrualFactors;
    [NonSerialized][NoClone] private Cashflow LazyCashflow;
    [NonSerialized][NoClone] private double[] Spreads;
		private Dt[] _exposureDates; 

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="cdsPricer">CDS pricer object</param>
    public CdsCcrPricer(CDSCashflowPricer cdsPricer)
      : base(cdsPricer)
    {
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
	  public override Dt[] ExposureDates
		{
		  get
		  {
				if(_exposureDates == null)
					InitExposureDts();
			  return _exposureDates;
		  }
		  set
		  {
			  _exposureDates = value;
		    if (PaymentPricer != null)
		      PaymentPricer.ExposureDates = value;
				InitExposureDts();
		  }
	  }

	  private void Init(CDSCashflowPricer pricer)
    {
      InitCashflows(pricer, out LazyCashflow, out AccrualFactors, out Spreads);
    }

	  private void InitExposureDts()
	  {
			if(LazyCashflow == null)
				InitCashflows((CDSCashflowPricer)Pricer, out LazyCashflow, out AccrualFactors, out Spreads);
			var dts = new UniqueSequence<Dt>();
			dts.Add(Pricer.AsOf);
		  var cds = ((CDSCashflowPricer)Pricer).CDS; 
		  var dtMax = Dt.Roll(cds.Maturity, BDConvention.Following, cds.Calendar);
      if (_exposureDates != null && _exposureDates.Any(dt => dt <= dtMax))
		  {
        dts.Add(_exposureDates.Where(dt => dt <= dtMax).ToArray());
        var lastDt = dts.Max();
        if (lastDt < dtMax && _exposureDates.Any(dt => dt > lastDt))
          dts.Add(_exposureDates.First(dt => dt > lastDt));
		   
			  dtMax = _exposureDates.First();
		  }
		  for (int i = 0; i < LazyCashflow.Count; i++)
		  {
			  var dt = LazyCashflow.GetDt(i);
			  if (dt <= dtMax)
			  {
					var beforePaymentDt = Dt.Add(dt, -1);
					if (beforePaymentDt > Pricer.AsOf)
						dts.Add(beforePaymentDt);
					dts.Add(dt);  
			  }
		  }
	    if (PaymentPricer != null)
	      dts.Add(PaymentPricer.ExposureDates);
		  _exposureDates = dts.ToArray();
	  }

    private static void InitCashflows(CDSCashflowPricer pricer, out Cashflow cf, out double[] accrualFactors,
                                      out double[] spreads)
    {
      cf = pricer.GenerateCashflow(null, pricer.AsOf, pricer.CDS, 0.0);
      accrualFactors = null;
      spreads = null;
      if (pricer.CDS.CdsType == CdsType.FundedFloating)
      {
        accrualFactors = new double[cf.Count];
        spreads = new double[cf.Count];
        for (int i = 0; i < cf.Count; ++i)
        {
          Dt start = cf.GetStartDt(i), end = cf.GetEndDt(i);
          accrualFactors[i] = pricer.CDS.Schedule.Fraction(start, end, pricer.CDS.DayCount);
          spreads[i] = CouponPeriodUtil.CouponAt(pricer.CDS.PremiumSchedule, pricer.CDS.Premium, start);
        }
      }
    }

    /// <summary>
    ///   Calculate the present value to asOf of any upfront fee settling on or after
    ///   settle date.
    /// </summary>
    private static double UpfrontFeePv(CDSCashflowPricer pricer, Dt asOf, Dt settle)
    {
      Dt feeSettle = pricer.FeeSettle;
      Dt defaultDt = pricer.SurvivalCurve.DefaultDate;
      if ((feeSettle > settle) && (!(defaultDt.IsValid()) || (defaultDt.IsValid() && defaultDt >= feeSettle)))
        return pricer.Fee()*pricer.Notional*pricer.DiscountCurve.DiscountFactor(asOf, feeSettle);
      return 0.0;
    }

    public static double Pv(CDSCashflowPricer pricer, Cashflow cf, Dt asOf, Dt settle)
    {
      double pv = 0.0;
      if (pricer.CDS.PayRecoveryAtMaturity)
      {
        var discountCurve = new DiscountCurve(asOf, 0.0);
        pv += CashflowModel.ProtectionPv(cf, asOf, settle, discountCurve, pricer.SurvivalCurve, pricer.CounterpartyCurve,
                                         pricer.Correlation, pricer.IncludeSettlePayments,
                                         pricer.IncludeMaturityProtection,
                                         pricer.DiscountingAccrued, pricer.StepSize, pricer.StepUnit)*
              pricer.CurrentNotional*pricer.DiscountCurve.DiscountFactor(asOf, pricer.CDS.Maturity);
        pv += CashflowModel.FeePv(cf, asOf, settle, pricer.DiscountCurve, pricer.SurvivalCurve, pricer.CounterpartyCurve,
                                  pricer.Correlation, pricer.IncludeSettlePayments, pricer.IncludeMaturityProtection,
                                  pricer.DiscountingAccrued, pricer.StepSize, pricer.StepUnit)*pricer.CurrentNotional;
      }
      else
        pv += CashflowModel.Pv(cf, asOf, settle, pricer.DiscountCurve,
                               pricer.SurvivalCurve, pricer.CounterpartyCurve, pricer.Correlation,
                               pricer.IncludeSettlePayments, pricer.IncludeMaturityProtection,
                               pricer.DiscountingAccrued, pricer.StepSize, pricer.StepUnit)*pricer.CurrentNotional;
      pv += UpfrontFeePv(pricer, asOf, settle);
      return pv;
    }

    /// <summary>
    /// Pv of a forward settling at settle CDS discounted to settle
    /// </summary>
    /// <param name="settle">Future settlement date</param>
    /// <returns>Present value of the cds contract discounted to settle </returns>
    public override double FastPv(Dt settle)
    {
      if (Pricer == null || settle > Pricer.Product.Maturity) return 0.0;
      var pricer = (CDSCashflowPricer) Pricer;
      if (LazyCashflow == null)
        Init(pricer);
      else if (pricer.CDS.CdsType == CdsType.FundedFloating)
        CcrPricerUtils.FastFillCashflow(pricer.ReferenceCurve ?? pricer.DiscountCurve, pricer.CDS.Freq,
                                        LazyCashflow, AccrualFactors, Spreads, settle);
      Dt protectionStart = (settle <= pricer.Product.Effective) ? pricer.Product.Effective : settle;
      double pv = Pv(pricer, LazyCashflow, settle, protectionStart);
      PricerPv(Pricer, settle);
      pv*=pricer.SurvivalCurve.Interpolate(settle);
      if (PaymentPricer != null)
        pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void PricerPv(IPricer iPricer, Dt settle)
    {
      var pricer = (CDSCashflowPricer)iPricer;
      pricer.ProductPv();
      pricer = (CDSCashflowPricer)iPricer.CloneObjectGraph();
      pricer.AsOf = pricer.Settle = settle;
      pricer.ProductPv();
      return;
    }
  }

  #endregion

  #region CdxCcrPricer

  /// <summary>
  /// Fast CDX pricer for CCR. Caches cashflows 
  /// </summary>
  [Serializable]
  public class CdxCcrPricer : CcrPricer
  {
    private readonly ToolkitConfigSettings Settings = ToolkitConfigurator.Settings;
    [NonSerialized][NoClone] private double[] AccrualFactors;
    [NonSerialized][NoClone] private CDSCashflowPricer CDSPricer;
    [NonSerialized][NoClone] private Cashflow LazyCashflow;
    [NonSerialized][NoClone] private double[] Spreads;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="cdxPricer">CDX pricer object</param>
    public CdxCcrPricer(CDXPricer cdxPricer) : base(cdxPricer)
    {
    }

    private void Init()
    {
      var cdxPricer = (CDXPricer) Pricer;
      InitializeCashflows(cdxPricer, out CDSPricer, out LazyCashflow, out AccrualFactors, out Spreads);
    }


    private static CDSCashflowPricer GetCDSPricer(CDXPricer cdxPricer)
    {
      CDX note = cdxPricer.CDX;
      var cds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem, note.Premium, note.DayCount, note.Freq,
                        note.BDConvention, note.Calendar);
      if (note.CdxType == CdxType.FundedFloating)
      {
        cds.CdsType = CdsType.FundedFloating;
        DiscountCurve referenceCurve = cdxPricer.ReferenceCurve ?? cdxPricer.DiscountCurve;
        if (referenceCurve == null || note.Premium <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate cds");
      }
      else if (note.CdxType == CdxType.FundedFixed)
        cds.CdsType = CdsType.FundedFixed;
      else
        cds.CdsType = CdsType.Unfunded;
      var pricer = new CDSCashflowPricer(cds, cdxPricer.AsOf, cdxPricer.Settle, cdxPricer.DiscountCurve, null, 0,
                                         TimeUnit.None);
      foreach (RateReset r in cdxPricer.RateResets) pricer.RateResets.Add((RateReset) r.Clone());
      return pricer;
    }


    private static void InitializeCashflows(CDXPricer cdxPricer, out CDSCashflowPricer pricer, out Cashflow cf,
                                            out double[] accrualFactors, out double[] spreads)
    {
      pricer = GetCDSPricer(cdxPricer);
      cf = pricer.GenerateCashflow(null, pricer.AsOf, pricer.CDS, 0.0);
      accrualFactors = null;
      spreads = null;
      if (pricer.CDS.CdsType == CdsType.FundedFloating)
      {
        accrualFactors = new double[cf.Count];
        spreads = new double[cf.Count];
        for (int i = 0; i < cf.Count; ++i)
        {
          Dt start = cf.GetStartDt(i), end = cf.GetEndDt(i);
          accrualFactors[i] = pricer.CDS.Schedule.Fraction(start, end, pricer.CDS.DayCount);
          spreads[i] = pricer.CDS.Schedule.Fraction(start, end, pricer.CDS.DayCount);
        }
      }
    }

    /// <summary>
    /// Pv of a forward settling at settle CDS discounted to settle
    /// </summary>
    /// <param name="settle">Future settlement date</param>
    /// <returns>Present value of the cds contract discounted to settle </returns>
    public override double FastPv(Dt settle)
    {
      if (settle > Pricer.Product.Maturity)
        return 0.0;
      var cdxPricer = Pricer as CDXPricer;
      SurvivalCurve[] survivalCurves = cdxPricer.SurvivalCurves;
      if (survivalCurves == null || survivalCurves.Length == 0)
        return 0.0;
      if (LazyCashflow == null)
        Init();
      else if (cdxPricer.CDX.CdxType == CdxType.FundedFloating)
        CcrPricerUtils.FastFillCashflow(CDSPricer.ReferenceCurve ?? CDSPricer.DiscountCurve, CDSPricer.CDS.Freq,
                                        LazyCashflow, AccrualFactors, Spreads, settle);
      //redo this 

      double[] weights = cdxPricer.CDX.Weights;
      if (survivalCurves.Length == 1)
        weights = null;
      double totPv = 0.0;
      for (int i = 0; i < survivalCurves.Length; i++)
      {
        if (!survivalCurves[i].DefaultDate.IsEmpty() && cdxPricer.CDX.AnnexDate >= survivalCurves[i].DefaultDate)
          continue;
        double r = 0.4;
        if (survivalCurves[i].SurvivalCalibrator != null)
        {
          r = survivalCurves[i].SurvivalCalibrator.RecoveryCurve.RecoveryRate(cdxPricer.CDX.Maturity);
          if (survivalCurves[i].Defaulted == Defaulted.HasDefaulted &&
              survivalCurves[i].SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate.IsValid() &&
              survivalCurves[i].SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate > settle)
          {
            //maybe can do this more efficient 
            CDSCashflowPricer cdsCashflowPricer = GetCDSPricer(cdxPricer);
            cdsCashflowPricer.AsOf = settle;
            cdsCashflowPricer.Settle = settle;
            cdsCashflowPricer.SurvivalCurve = survivalCurves[i];
            double dfltedPv = cdsCashflowPricer.Pv();
            totPv += (weights != null) ? dfltedPv*weights[i] : dfltedPv/survivalCurves.Length;
            continue;
          }
        }
        LazyCashflow.SetDefaultAmount(cdxPricer.CDX.CdxType == CdxType.Unfunded ? r - 1 : r);
        Dt protectionStart = (settle <= cdxPricer.Product.Effective) ? cdxPricer.Product.Effective : settle;
        double pv = CashflowModel.Pv(LazyCashflow, protectionStart, protectionStart, cdxPricer.DiscountCurve,
                                     survivalCurves[i], null, 0.0,
                                     Settings.CDSCashflowPricer.IncludeSettlePaymentDefault,
                                     Settings.CDSCashflowPricer.IncludeMaturityProtection,
                                     Settings.CashflowPricer.DiscountingAccrued, 0, TimeUnit.None);
        double sp = survivalCurves[i].Interpolate(protectionStart);
        pv *= sp;
        totPv += (weights != null) ? pv*weights[i] : pv/survivalCurves.Length;
      }
      return totPv*cdxPricer.Notional;
    }
  }

  #endregion

  #region SwapLegCcrPricer

  /// <summary>
  /// Non credit contingent pricer with payment schedule
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class SwapLegCcrPricer : CcrPricer
  {
    [ObjectLogger(
      Name = "SwapLegPricer",
      Category = "Exposures",
      Description = "Swap Leg Pricing within Monte Carlo Simulation",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics" })]
    public static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(SwapLegCcrPricer));

    [NonSerialized][NoClone] private Payment[][] PaymentSchedule;
    [NonSerialized][NoClone] private Dt[] _exposureDts;
    [NonSerialized][NoClone] private Dt _rolledMaturity; 

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    public SwapLegCcrPricer(SwapLegPricer pricer) : base(pricer)
    {
    }

    /// <summary>
    /// Fast Pv 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv</returns>
    public override double FastPv(Dt settle)
    {
      return FastPv(settle, false);
    }

    /// <summary>
    ///   Fast Pv
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <param name="clean">If true, calculate the clean pv; otherwise, the full pv.</param>
    /// <returns>Pv</returns>
    public double FastPv(Dt settle, bool clean)
    {
      if (PaymentSchedule == null)
        Init();
      if (settle > _rolledMaturity)
        return 0.0;
      var pricer = (SwapLegPricer)Pricer;
      Dt mutualPutDt = pricer.SwapLeg.NextBreakDate;

      if (mutualPutDt.IsValid() && settle >= mutualPutDt)
        return 0.0; //for cpty risk purposes, we can safely assume that mutual put is exercised 
      var pv = CcrPricerUtils.Pv(settle, PaymentSchedule, pricer.DiscountCurve, pricer.DiscountingAccrued, false,
                               pricer.SwapLeg.Notional, pricer.Notional, clean);
      if (PaymentPricer != null)
        pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    private void Init()
    {
      var p = (SwapLegPricer)Pricer;
      bool approx = CcrPricerUtils.SetApproximateForFastCalculation(p);
      var ps = GetPaymentSchedule(p, p.Settle);
      p.ApproximateForFastCalculation = approx;
      var pmts = new List<Payment[]>();
      var exposureDts = new UniqueSequence<Dt>();
      var maxDt = _rolledMaturity = Dt.Roll(p.SwapLeg.Maturity, p.SwapLeg.BDConvention, p.SwapLeg.Calendar);
      if (_exposureDts != null && _exposureDts.Any(dt => dt <= maxDt))
	    {
        exposureDts.Add(_exposureDts.Where(dt => dt <= maxDt).ToArray());
        var lastDt = exposureDts.Max();
        if (lastDt < maxDt && _exposureDts.Any(dt => dt > lastDt))
          exposureDts.Add(_exposureDts.First(dt => dt > lastDt));
		    maxDt = _exposureDts.First();
	    }
			exposureDts.Add(p.AsOf);
      if (ps != null && ps.Count > 0)
      {
        foreach (Dt dt in ps.GetPaymentDates())
        {
          Payment[] pmt = ps.GetPaymentsOnDate(dt).ToArray();
          for (int i = 0; i < pmt.Length; ++i)
          {
            if (!pmt[i].IsProjected)
              pmt[i].Amount = pmt[i].Amount;
            //avoid multiplication by accrued during simulation if payment is not projected
          }
          pmts.Add(pmt);
          if (dt > p.AsOf && dt <= maxDt)
          {
            var beforePaymentDt = Dt.Add(dt, -1);
            if (beforePaymentDt > p.AsOf)
              exposureDts.Add(beforePaymentDt);
            exposureDts.Add(dt);
          }
        }
      }
      PaymentSchedule = pmts.ToArray();
      if (PaymentPricer != null)
        exposureDts.Add(PaymentPricer.ExposureDates);
      _exposureDts = exposureDts.ToArray();

      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        BuildSwapPricerDiagnosticTable();
      }
    }

    public static void BuildSwapPricerDiagnosticTable()
    {
      var diagnosticsDataTable = new DataTable("SwapLegCcrPricer");
      diagnosticsDataTable.Columns.Add("PathId", typeof(int));
      diagnosticsDataTable.Columns.Add("PricerSettleDt", typeof(string));
      diagnosticsDataTable.Columns.Add("PaymentDt", typeof(string));
      diagnosticsDataTable.Columns.Add("Idx", typeof(int));
      diagnosticsDataTable.Columns.Add("DiscountFactor", typeof(double));
      diagnosticsDataTable.Columns.Add("DomesticAmount", typeof(double));
      diagnosticsDataTable.Columns.Add("ProductNotional", typeof(double));
      diagnosticsDataTable.Columns.Add("PricerNotational", typeof(double));
      diagnosticsDataTable.Columns.Add("SwapLegId", typeof(string));
      CCRPricerDiagnosticsUtil.SetDiagnosticDataTable(diagnosticsDataTable, "SwapLegCcrPricer");
    }

    public override Dt[] ExposureDates
    {
      get
      {
        if(_exposureDts == null)
          Init();
        return _exposureDts;
      }
	    set
	    {
		    _exposureDts = value;
	      if (PaymentPricer != null)
	        PaymentPricer.ExposureDates = value; 
				Init();
	    }
    }
  }

  #endregion

  #region CommoditySwapLegCcrPricer

  /// <summary>
  /// Non credit contingent pricer with payment schedule
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class CommoditySwapLegCcrPricer : CcrPricer
  {
    [ObjectLogger(
      Name = "CommoditySwapLegPricer",
      Category = "Exposures",
      Description = "Swap Leg Pricing within Monte Carlo Simulation",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics" })]
    public static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(CommoditySwapLegCcrPricer));

    [NonSerialized]
    [NoClone]
    private Payment[][] PaymentSchedule;
    [NonSerialized]
    [NoClone]
    private Dt[] _exposureDts;
    [NonSerialized]
    [NoClone]
    private Dt _rolledMaturity;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    public CommoditySwapLegCcrPricer(CommoditySwapLegPricer pricer) : base(pricer)
    {
    }

    /// <summary>
    /// Fast Pv 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv</returns>
    public override double FastPv(Dt settle)
    {
      return FastPv(settle, false);
    }

    /// <summary>
    ///   Fast Pv
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <param name="clean">If true, calculate the clean pv; otherwise, the full pv.</param>
    /// <returns>Pv</returns>
    public double FastPv(Dt settle, bool clean)
    {
      if (PaymentSchedule == null)
        Init();
      if (settle > _rolledMaturity)
        return 0.0;
      var pricer = (CommoditySwapLegPricer)Pricer;
      var pv = CcrPricerUtils.Pv(settle, PaymentSchedule, pricer.DiscountCurve, true, pricer.IncludePaymentOnSettle,
                               pricer.SwapLeg.Notional, pricer.Notional, true);
      if (PaymentPricer != null)
        pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    private void Init()
    {
      var p = (CommoditySwapLegPricer)Pricer ;
      var approx = CcrPricerUtils.SetApproximateForFastCalculation(p);
      var ps = GetPaymentSchedule(p, p.Settle);
      p.ApproximateForFastCalculation = approx;
      var pmts = new List<Payment[]>();
      var exposureDts = new UniqueSequence<Dt>();
      var maxDt = _rolledMaturity = Dt.Roll(p.SwapLeg.Maturity, p.SwapLeg.BDConvention, p.SwapLeg.Calendar);
      if (_exposureDts != null && _exposureDts.Any(dt => dt <= maxDt))
      {
        exposureDts.Add(_exposureDts.Where(dt => dt <= maxDt).ToArray());
        var lastDt = exposureDts.Max();
        if (lastDt < maxDt && _exposureDts.Any(dt => dt > lastDt))
          exposureDts.Add(_exposureDts.First(dt => dt > lastDt));
        maxDt = _exposureDts.First();
      }
      exposureDts.Add(p.AsOf);
      if (ps != null && ps.Count > 0)
      {
        foreach (Dt dt in ps.GetPaymentDates())
        {
          Payment[] pmt = ps.GetPaymentsOnDate(dt).ToArray();
          for (int i = 0; i < pmt.Length; ++i)
          {
            if (!pmt[i].IsProjected)
              pmt[i].Amount = pmt[i].Amount;
            //avoid multiplication by accrued during simulation if payment is not projected
          }
          pmts.Add(pmt);
          if (dt <= maxDt)
          {
            var beforePaymentDt = Dt.Add(dt, -1);
            if (beforePaymentDt > p.AsOf)
              exposureDts.Add(beforePaymentDt);
            exposureDts.Add(dt);
          }
        }
      }
      PaymentSchedule = pmts.ToArray();
      if (PaymentPricer != null)
        exposureDts.Add(PaymentPricer.ExposureDates);
      _exposureDts = exposureDts.ToArray();

      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        BuildSwapPricerDiagnosticTable();
      }
    }

    public static void BuildSwapPricerDiagnosticTable()
    {
      var diagnosticsDataTable = new DataTable("CommoditySwapLegCcrPricer");
      diagnosticsDataTable.Columns.Add("PathId", typeof(int));
      diagnosticsDataTable.Columns.Add("PricerSettleDt", typeof(string));
      diagnosticsDataTable.Columns.Add("PaymentDt", typeof(string));
      diagnosticsDataTable.Columns.Add("Idx", typeof(int));
      diagnosticsDataTable.Columns.Add("DiscountFactor", typeof(double));
      diagnosticsDataTable.Columns.Add("DomesticAmount", typeof(double));
      diagnosticsDataTable.Columns.Add("ProductNotional", typeof(double));
      diagnosticsDataTable.Columns.Add("PricerNotional", typeof(double));
      diagnosticsDataTable.Columns.Add("SwapLegId", typeof(string));
      CCRPricerDiagnosticsUtil.SetDiagnosticDataTable(diagnosticsDataTable, "CommoditySwapLegCcrPricer");
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public override Dt[] ExposureDates
    {
      get
      {
        if (_exposureDts == null)
          Init();
        return _exposureDts;
      }
      set
      {
        _exposureDts = value;
        if (PaymentPricer != null)
          PaymentPricer.ExposureDates = value;
        Init();
      }
    }
  }

  #endregion

  #region InflationBondCcrPricer

  /// <summary>
  /// Non credit contingent pricer with payment schedule
  /// </summary>
  [Serializable]
  public class InflationBondCcrPricer : CcrPricer
  {
    [NonSerialized][NoClone] private Cashflow LazyCashflow;
    [NonSerialized][NoClone] private Payment[][] PaymentSchedule;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    public InflationBondCcrPricer(InflationBondPricer pricer)
      : base(pricer)
    {
    }

    /// <summary>
    /// Fast Pv 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv</returns>
    public override double FastPv(Dt settle)
    {
      if (settle > Pricer.Product.Maturity)
        return 0.0;
      var pricer = Pricer as InflationBondPricer;
      if (PaymentSchedule == null)
        Init();
      else if (LazyCashflow != null)
        CcrPricerUtils.FastFillCashflow(PaymentSchedule, LazyCashflow, settle);
      if (LazyCashflow != null)
        return CashflowModel.Pv(LazyCashflow, settle, settle, pricer.DiscountCurve, pricer.SurvivalCurve, null, 0, false,
                                false, pricer.DiscountingAccrued, 0, TimeUnit.None)*pricer.Notional;
      else
        return CcrPricerUtils.Pv(settle, PaymentSchedule, pricer.DiscountCurve, pricer.DiscountingAccrued, false,
                                 pricer.InflationBond.Notional, pricer.Notional);
    }

    private void Init()
    {
      var p = Pricer as InflationBondPricer;
      PaymentSchedule ps = GetPaymentSchedule(p, p.AsOf);
      if (p.SurvivalCurve != null)
        LazyCashflow = PaymentScheduleUtils.FillCashflow(null, ps, p.AsOf, p.InflationBond.Notional, p.RecoveryRate);
      var pmts = new List<Payment[]>();
      foreach (Dt dt in ps.GetPaymentDates())
      {
        Payment[] pmt = ps.GetPaymentsOnDate(dt).ToArray();
        for (int i = 0; i < pmt.Length; ++i)
        {
          if (!pmt[i].IsProjected)
            pmt[i].Amount = pmt[i].Amount;
          //avoid multiplication by accrued during simulation if payment is not projected
        }
        pmts.Add(pmt);
      }
      PaymentSchedule = pmts.ToArray();
    }
  }

  #endregion

  #region SwapCcrPricer

  /// <summary>
  /// CCR Swap pricer
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class SwapCcrPricer : CcrPricer
  {
    [ObjectLogger(
      Name = "SwapPricer",
      Category = "Exposures",
      Description = "Swap Pricing within Monte Carlo Simulation (requires Swap Leg Pricer be enabled)",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics", "BaseEntity.Toolkit.CounterpartyCredit.Pricers.SwapLegCcrPricer" })]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(SwapCcrPricer));

    #region Data

    [NonSerialized][NoClone] private SwapLegCcrPricer[] _swapLegCcrPricers;
    [NonSerialized][NoClone] private Dt[] _exposureDts; 

    private readonly IList<SwapLegPricer> _swapLegPricers;
    private readonly Dt _breakDate;
    
    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    public SwapCcrPricer(SwapPricer swapPricer)
      : base(swapPricer)
    {
      SwapLegPricer r = swapPricer.ReceiverSwapPricer;
      SwapLegPricer p = swapPricer.PayerSwapPricer;
      _swapLegPricers = new List<SwapLegPricer> {r, p};
      _breakDate = PvEvaluator.GetNextBreakDate(swapPricer);
    }


    /// <summary>
    /// Constructor
    /// </summary>
    public SwapCcrPricer(MultiLeggedSwapPricer swapPricer)
      : base(swapPricer)
    {
      _swapLegPricers = swapPricer.SwapLegPricers.ToList();
      _breakDate = PvEvaluator.GetNextBreakDate(swapPricer);
    }

    #endregion

    #region Methods

    private void Init()
    {
      _swapLegCcrPricers = new SwapLegCcrPricer[_swapLegPricers.Count];
      var exposureDts = new UniqueSequence<Dt>();

      for (int i = 0; i < _swapLegPricers.Count; i++)
      {
        var swapLegPricer = _swapLegPricers[i];
        var fastPricer = new SwapLegCcrPricer(swapLegPricer);
        _swapLegCcrPricers[i] = fastPricer; 
        if (_exposureDts != null && _exposureDts.Any())
        {
          fastPricer.ExposureDates = _exposureDts;
        }
        exposureDts.Add(fastPricer.ExposureDates);
      }
      if (PaymentPricer != null)
        exposureDts.Add(PaymentPricer.ExposureDates);
      _exposureDts = exposureDts.ToArray(); 
    }

    /// <summary>
    /// Pv of a forward settling at settle CDS discounted to settle
    /// </summary>
    /// <param name="settle">Future settlement date</param>
    /// <returns>Present value of the cds contract discounted to settle </returns>
    public override double FastPv(Dt settle)
    {
      return FastPv(settle, false);
    }

    public double FastPv(Dt settle, bool clean)
    {
      if (_swapLegCcrPricers == null)
        Init();
      if (!_breakDate.IsEmpty() && settle >= _breakDate)
        return 0;

      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        double pv = 0;
        foreach (var swapLegPricer in _swapLegCcrPricers)
        {
          var swapLegId = swapLegPricer.Pricer.Product.ToString();
          CCRPricerDiagnosticsUtil.SetSwapLegId(swapLegId);
          pv += swapLegPricer.FastPv(settle, clean);
        }
        if (settle >= Pricer.Product.EffectiveMaturity)
        {
          // trade specific dates will log here
          var key = string.Format("SwapLegCcrPricer.Path{0}", ObjectLoggerUtil.GetPath("CCRPricerPath"));
          var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), key);
          binaryLogAggregator.Append(typeof(SwapLegCcrPricer), key, AppenderUtil.DataTableToDataSet(CCRPricerDiagnosticsUtil.GetDiagnosticDataTable("SwapLegCcrPricer"))).Log();
          SwapLegCcrPricer.BuildSwapPricerDiagnosticTable();
        }
        return pv;
      }

      return _swapLegCcrPricers.Sum(p => p.FastPv(settle, clean));
    }

    #endregion

    /// <summary>
    /// Exposure dates
    /// </summary>
    public override Dt[] ExposureDates
    {
      get
      {
        if(_exposureDts == null)
          Init();
        return _exposureDts;
      }
	    set
	    {
		    _exposureDts = value;
	      if (PaymentPricer != null)
	        PaymentPricer.ExposureDates = value;
		    Init();
	    }
    }
  }

  #endregion

  #region CommoditySwapCcrPricer

  /// <summary>
  /// CCR Swap pricer
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class CommoditySwapCcrPricer : CcrPricer
  {
    [ObjectLogger(
      Name = "CommoditySwapPricer",
      Category = "Exposures",
      Description = "Commodity Swap Pricing within Monte Carlo Simulation (requires Swap Leg Pricer be enabled)",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics", "BaseEntity.Toolkit.CounterpartyCredit.Pricers.CommoditySwapLegCcrPricer" })]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(CommoditySwapCcrPricer));

    #region Data

    [NonSerialized]
    [NoClone]
    private CommoditySwapLegCcrPricer[] _swapLegCcrPricers;
    [NonSerialized]
    [NoClone]
    private Dt[] _exposureDts;

    private readonly IList<CommoditySwapLegPricer> _swapLegPricers;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    public CommoditySwapCcrPricer(CommoditySwapPricer swapPricer)
      : base(swapPricer)
    {
      var r = swapPricer.ReceiverSwapPricer;
      var p = swapPricer.PayerSwapPricer;
      _swapLegPricers = new List<CommoditySwapLegPricer> { r, p };
    }

    #endregion

    #region Methods

    private void Init()
    {
      _swapLegCcrPricers = new CommoditySwapLegCcrPricer[_swapLegPricers.Count];
      var exposureDts = new UniqueSequence<Dt>();

      for (int i = 0; i < _swapLegPricers.Count; i++)
      {
        var swapLegPricer = _swapLegPricers[i];
        var fastPricer = new CommoditySwapLegCcrPricer(swapLegPricer);
        _swapLegCcrPricers[i] = fastPricer;
        if (_exposureDts != null && _exposureDts.Any())
        {
          fastPricer.ExposureDates = _exposureDts;
        }
        exposureDts.Add(fastPricer.ExposureDates);
      }
      if (PaymentPricer != null)
        exposureDts.Add(PaymentPricer.ExposureDates);
      _exposureDts = exposureDts.ToArray();
    }

    /// <summary>
    /// Pv of a forward settling at settle CDS discounted to settle
    /// </summary>
    /// <param name="settle">Future settlement date</param>
    /// <returns>Present value of the cds contract discounted to settle </returns>
    public override double FastPv(Dt settle)
    {
      return FastPv(settle, false);
    }

    public double FastPv(Dt settle, bool clean)
    {
      if (_swapLegCcrPricers == null)
        Init();
      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        double pv = 0;
        foreach (var swapLegPricer in _swapLegCcrPricers)
        {
          var swapLegId = swapLegPricer.Pricer.Product.ToString();
          CCRPricerDiagnosticsUtil.SetSwapLegId(swapLegId);
          pv += swapLegPricer.FastPv(settle, clean);
        }
        if (settle >= Pricer.Product.EffectiveMaturity)
        {
          // trade specific dates will log here
          var key = string.Format("CommoditySwapLegCcrPricer.Path{0}", ObjectLoggerUtil.GetPath("CCRPricerPath"));
          var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), key);
          binaryLogAggregator.Append(typeof(SwapLegCcrPricer), key, AppenderUtil.DataTableToDataSet(CCRPricerDiagnosticsUtil.GetDiagnosticDataTable("CommoditySwapLegCcrPricer"))).Log();
          SwapLegCcrPricer.BuildSwapPricerDiagnosticTable();
        }
        return pv;
      }

      return _swapLegCcrPricers.Sum(p => p.FastPv(settle, clean));
    }

    #endregion


    /// <summary>
    /// Exposure dates
    /// </summary>
    public override Dt[] ExposureDates
    {
      get
      {
        if (_exposureDts == null)
          Init();
        return _exposureDts;
      }
      set
      {
        _exposureDts = value;
        if (PaymentPricer != null)
          PaymentPricer.ExposureDates = value;
        Init();
      }
    }
  }

  #endregion

  #region CdoCcrPricer

  [Serializable]
  public class CdoCcrPricer : CcrPricer
  {
    private readonly Calculator _detachCalculator;
    private readonly Calculator _attachCalculator;

    #region Constructors

    public CdoCcrPricer(SyntheticCDOPricer pricer)
      : base(pricer)
    {
      // Try construct a basket with a single tranche correlation.
      _detachCalculator = new Calculator(pricer, null);
      if (_detachCalculator.Approximate == true)
      {
        // Success! Use the single basket approximate.
        _attachCalculator = null;
        return;
      }
      // Failed.  Now try construct two base tranche baskets.
      var lossLevels = BasketPricerFactory.LossLevelsFromTranches(new[] { pricer.CDO });
      var dpCalculator = CreateBaseTrancheCalculator(pricer, lossLevels, true);
      var apCalculator = CreateBaseTrancheCalculator(pricer, lossLevels, false);
      if (apCalculator.Approximate != true || dpCalculator.Approximate != true)
      {
        // If any of the attachment/detachment baskets fails,
        // we switch to the original basket.
        return;
      }
      // Okay, we use two base tranche baskets.
      _detachCalculator = dpCalculator;
      _attachCalculator = apCalculator;
    }

    private static Calculator CreateBaseTrancheCalculator(
      SyntheticCDOPricer pricer, double[,] lossLevels, bool isDetachment)
    {
      var basketNotional = pricer.Notional / pricer.CDO.TrancheWidth;
      var cdo = (SyntheticCDO)pricer.CDO.ShallowCopy();
      if (!isDetachment) cdo.Detachment = pricer.CDO.Attachment;
      cdo.Attachment = 0;
      var notional = basketNotional * cdo.TrancheWidth;
      var basket = (BasketPricer)pricer.Basket.ShallowCopy();
      return new Calculator(pricer.Substitute(cdo, basket, notional, true), lossLevels);
    }

    #endregion

    #region Method

    public override double FastPv(Dt settle)
    {
      var dpv = _detachCalculator.FastPv(settle);
      return _attachCalculator == null
        ? dpv
        : (dpv - _attachCalculator.FastPv(settle));
    }

    #endregion

    #region Static methods
    private static BasketPricer BuildApproximateBasketPricer(
      SyntheticCDOPricer pricer, double[,] lossLevels, double factorLoading)
    {
      BasketPricer basketPricer = pricer.Basket;
      CreditPool originalPool = pricer.Basket.OriginalBasket;
      return BasketPricerFactory.LargePoolBasketPricer(basketPricer.PortfolioStart, basketPricer.AsOf,
                                                       basketPricer.Settle,
                                                       basketPricer.Maturity,
                                                       (originalPool != null)
                                                         ? originalPool.SurvivalCurves
                                                         : basketPricer.SurvivalCurves,
                                                       (originalPool != null)
                                                         ? originalPool.Participations
                                                         : basketPricer.Principals,
                                                       new Copula(CopulaType.Gauss),
                                                       new SingleFactorCorrelation(
                                                         new string[(originalPool != null)
                                                                      ? originalPool.PortfolioCount
                                                                      : basketPricer.Count],
                                                         factorLoading),
                                                       basketPricer.StepSize, basketPricer.StepUnit,
                                                       lossLevels, 20);
    }


    private static Cashflow GenerateCashflowForFee(SyntheticCDOPricer pricer, Dt settle)
    {
      SyntheticCDO cdo = pricer.CDO;
      if (cdo.FeeGuaranteed)
      {
        CycleRule cycleRule = cdo.CycleRule;
        CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;
        if (cdo.AccruedOnDefault)
          flags |= CashflowFlag.AccruedPaidOnDefault;
        var schedParams = new ScheduleParams(cdo.Effective, cdo.FirstPrem, cdo.LastPrem, cdo.Maturity, cdo.Freq,
          cdo.BDConvention, cdo.Calendar, cycleRule, flags);
        const double fee = 0.0;
        Dt feeSettle = Dt.Empty;
        const double principal = 0.0;
        Currency defaultCcy = cdo.Ccy;
        const double defaultAmount = 0.0;
        Dt defaultDate = Dt.Empty;
        return CashflowFactory.FillFixed(null, pricer.Basket.AsOf, schedParams, cdo.Ccy, cdo.DayCount,
          cdo.Premium, null, principal, null, defaultAmount, defaultCcy, defaultDate,
          fee, feeSettle);
      }
      DiscountCurve referenceCurve = pricer.ReferenceCurve ?? pricer.DiscountCurve;
      return PriceCalc.GenerateCashflowForFee(settle, cdo.Premium,
        cdo.Effective, cdo.FirstPrem, cdo.Maturity,
        cdo.Ccy, cdo.DayCount, cdo.Freq, cdo.BDConvention, cdo.Calendar,
        pricer.CounterpartySurvivalCurve,
        cdo.CdoType == CdoType.FundedFloating ||
          cdo.CdoType == CdoType.IoFundedFloating,
        referenceCurve, pricer.RateResets);
    }

    // Generate simple cashflow stream
    private static Cashflow GenerateCashflowForProtection(SyntheticCDOPricer pricer, Dt settle)
    {
      SyntheticCDO cdo = pricer.CDO;
      return PriceCalc.GenerateCashflowForProtection(
        settle, cdo.Maturity, cdo.Ccy, pricer.CounterpartySurvivalCurve);
    }

    /// <summary>
    ///   Calculate price based on a cashflow stream
    /// </summary>
    private static double Price(Cashflow cashflow, BasketPricer basket, Dt settle, SyntheticCDOPricer pricer,
      bool includeFees, bool includeProtection, bool includeSettle)
    {
      if (settle > basket.Maturity)
        return 0;
      double trancheWidth = pricer.CDO.Detachment - pricer.CDO.Attachment;
      bool withAmortize = SyntheticCDOPricer.NeedAmortization(pricer.CDO, basket);
      if (trancheWidth < 1E-9) return 0.0;
      return PriceCalc.Price(cashflow, settle, pricer.DiscountCurve, delegate(Dt date)
      {
        double loss =
          basket.AccumulatedLoss(date,
            pricer.CDO.Attachment,
            pricer.CDO.Detachment) /
              trancheWidth;
        return Math.Min(1.0, loss);
      },
        delegate(Dt date)
        {
          double loss = basket.AccumulatedLoss(date,
            pricer.CDO.Attachment,
            pricer.CDO.Detachment);
          if (withAmortize)
            loss += basket.AmortizedAmount(date, pricer.CDO.Attachment,
              pricer.CDO.Detachment);
          return Math.Max(0.0, 1.0 - loss / trancheWidth);
        },
        pricer.CounterpartySurvivalCurve, includeFees, includeProtection, includeSettle,
        pricer.DefaultTiming,
        pricer.AccruedFractionOnDefault, basket.StepSize, basket.StepUnit, cashflow.Count);
    }


    private static double ProtectionPv(Dt settle, Cashflow cf, SyntheticCDOPricer pricer, BasketPricer basketPricer)
    {
      SyntheticCDO cdo = pricer.CDO;
      Dt maturity = cdo.Maturity;
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
        || cdo.CdoType == CdoType.Po || cdo.CdoType == CdoType.IoFundedFloating ||
          cdo.CdoType == CdoType.IoFundedFixed
            || Dt.Cmp(pricer.GetProtectionStart(), maturity) > 0)
        return 0.0;
      double pv = Price(cf, basketPricer, settle, pricer, false, true, false);
      pv += basketPricer.DefaultSettlementPv(settle, cdo.Maturity, pricer.DiscountCurve, cdo.Attachment, cdo.Detachment,
        true, false);
      pv *= pricer.Notional;
      return pv;
    }


    private static double FeePv(Dt settle, Cashflow cf, SyntheticCDOPricer pricer, BasketPricer basketPricer)
    {
      SyntheticCDO cdo = pricer.CDO;
      if (settle > cdo.Maturity)
        return 0;
      double pv = 0.0;
      double bulletFundedPayment = 0.0;
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.Po)
      {
        double survivalPrincipal = cdo.Detachment - cdo.Attachment;
        survivalPrincipal -= basketPricer.AccumulatedLoss(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#if EXCLUDE_AMORTIZE
        if (cdo.AmortizePremium)
          survivalPrincipal -= basket.AmortizedAmount(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#endif
        survivalPrincipal *= pricer.DiscountCurve.DiscountFactor(settle, cdo.Maturity);
        bulletFundedPayment = survivalPrincipal * pricer.TotalPrincipal;
        bulletFundedPayment +=
          basketPricer.DefaultSettlementPv(settle, cdo.Maturity, pricer.DiscountCurve, cdo.Attachment, cdo.Detachment,
            false, true) * pricer.Notional;
      }
      if (cdo.CdoType == CdoType.Po)
        return bulletFundedPayment;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      if (cdo.FeeGuaranteed)
      {
        pv = CashflowModel.FeePv(cf, settle, settle, pricer.DiscountCurve, null, null, 0.0, false, false, false,
          basketPricer.StepSize, basketPricer.StepUnit);
        pv *= trancheWidth;
        return pv * pricer.TotalPrincipal + bulletFundedPayment;
      }
      else
      {
        pv = Price(cf, basketPricer, settle, pricer, true, false, false);
        pv *= pricer.Notional;
        return pv + bulletFundedPayment;
      }
    }

    private static double UpFrontFeePv(Dt settle, SyntheticCDOPricer pricer)
    {
      SyntheticCDO cdo = pricer.CDO;
      if (cdo.Fee != 0.0 && Dt.Cmp(cdo.FeeSettle, settle) > 0)
      {
        if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating ||
          cdo.CdoType == CdoType.IoFundedFloating || cdo.CdoType == CdoType.IoFundedFixed ||
            cdo.CdoType == CdoType.Po)
          return -cdo.Fee * pricer.EffectiveNotional;
        return cdo.Fee * pricer.EffectiveNotional;
      }
      return 0.0;
    }


    private static BasketPricer CalibrateCorrelation(SyntheticCDOPricer pricer,
      double[,] lossLevels, out double factorLoading, out bool? approximate)
    {
      double tgt = pricer.Pv(pricer.Settle);
      var protectionLegCf = GenerateCashflowForProtection(pricer, pricer.Settle);
      var feeLegCf = GenerateCashflowForFee(pricer, pricer.Settle);
      var fastBasketPricer = BuildApproximateBasketPricer(pricer, lossLevels, 0.5);
      var correlation = (SingleFactorCorrelation)fastBasketPricer.Correlation;

      double tol = 1e-8;
      double a = 0.0;
      double d = 1e3;
      double b = (2 * a + d) / 3;
      double c = (a + 2 * d) / 3;
      while (Math.Abs(a - d) > tol)
      {
        correlation.SetFactor(b);
        fastBasketPricer.ResetCorrelation();
        double fb = Math.Abs(FastPv(pricer.Settle, pricer, protectionLegCf, feeLegCf, fastBasketPricer) - tgt);
        correlation.SetFactor(c);
        fastBasketPricer.ResetCorrelation();
        double fc = Math.Abs(FastPv(pricer.Settle, pricer, protectionLegCf, feeLegCf, fastBasketPricer) - tgt);
        if (fb > fc)
          a = b;
        else
          d = c;
        b = (2 * a + d) / 3;
        c = (a + 2 * d) / 3;
      }
      factorLoading = (b + c) / 2;
      correlation.SetFactor(factorLoading);
      fastBasketPricer.ResetCorrelation();
      if (Math.Abs((FastPv(pricer.Settle, pricer, protectionLegCf, feeLegCf, fastBasketPricer) - tgt) / tgt) < 1e-4)
      {
        approximate = true;
        return fastBasketPricer;
      }
      approximate = false;
      return pricer.Basket;
    }

    private static double FastPv(Dt settle, SyntheticCDOPricer pricer, Cashflow protectionLegCf, Cashflow feeLegCf,
      BasketPricer basketPricer)
    {
      double ppv = ProtectionPv(settle, protectionLegCf, pricer, basketPricer);
      double fpv = FeePv(settle, feeLegCf, pricer, basketPricer) + UpFrontFeePv(settle, pricer);
      return ppv + fpv;
    }

    #endregion

    #region Nested type: Calculator

    [Serializable]
    private class Calculator : CcrPricer
    {
      #region Data

      [NonSerialized] [NoClone] private double[] AccrualFactors;
      public bool? Approximate;
      [NonSerialized] [NoClone] private BasketPricer BasketPricer;
      public double FactorLoading;
      [NonSerialized] [NoClone] private Cashflow FeeLegCf;
      [NonSerialized] [NoClone] private Cashflow ProtectionLegCf;
      [NonSerialized] [NoClone] private double[,] _lossLevels;

      #endregion

      #region Constructors

      // Generate simple cashflow stream
      public Calculator(SyntheticCDOPricer pricer, double[,] lossLevels)
        : base(pricer)
      {
        _lossLevels = lossLevels;
        BasketPricer = CalibrateCorrelation(pricer, lossLevels, out FactorLoading, out Approximate);
      }

      #endregion

      #region Methods

      [OnDeserialized, AfterFieldsCloned]
      private void BuildBasketOnDeserialized(StreamingContext context)
      {
        if (Approximate.HasValue)
        {
          if (Approximate.Value)
            BasketPricer = BuildApproximateBasketPricer((SyntheticCDOPricer)Pricer, _lossLevels, FactorLoading);
          else
            BasketPricer = ((SyntheticCDOPricer)Pricer).Basket;
          return;
        }
        CalibrateCorrelation((SyntheticCDOPricer)Pricer, _lossLevels, out FactorLoading, out Approximate);
      }

      #endregion

      private void Init(SyntheticCDOPricer pricer)
      {
        ProtectionLegCf = GenerateCashflowForProtection(pricer, pricer.Settle);
        FeeLegCf = GenerateCashflowForFee(pricer, pricer.Settle);
        AccrualFactors = null;
        SyntheticCDO product = pricer.CDO;
        bool floater = (product.CdoType == CdoType.FundedFloating || product.CdoType == CdoType.IoFundedFloating);
        if (floater)
        {
          AccrualFactors = new double[FeeLegCf.Count];
          for (int i = 0; i < FeeLegCf.Count; ++i)
            AccrualFactors[i] = Dt.Fraction(FeeLegCf.GetStartDt(i), FeeLegCf.GetEndDt(i), product.DayCount);
        }
      }


      public override double FastPv(Dt settle)
      {
        if (Pricer == null || settle > Pricer.Product.Maturity) return 0.0;
        var pricer = Pricer as SyntheticCDOPricer;
        if (ProtectionLegCf == null || FeeLegCf == null)
          Init(pricer);
        else if (AccrualFactors != null)
          CcrPricerUtils.FastFillCashflow(pricer.ReferenceCurve ?? pricer.DiscountCurve,
            pricer.CDO.Freq, FeeLegCf, AccrualFactors, null, settle);
        double ppv = ProtectionPv(settle, ProtectionLegCf, pricer, BasketPricer);
        double fpv = FeePv(settle, FeeLegCf, pricer, BasketPricer) + UpFrontFeePv(settle, pricer);
        return ppv + fpv;
      }

    }

    #endregion
  }

  #endregion

  #region NtdCcrPricer

  #endregion

  #region IUnderlier

  /// <summary>
  /// Underlying is a log-normal martingale under the appropriate measure
  /// </summary>
  public interface IUnderlier
  {
    /// <summary>
    /// Underlier level
    /// </summary>
    /// <param name="dt">Date</param>
    /// <param name="numeraire">Overwritten by numeraire</param>
    /// <returns>Underlier</returns>
    double Value(Dt dt, out double numeraire);

    /// <summary>
    /// Black lognormal vol
    /// </summary>
    /// <param name="dt">Date</param>
    /// <returns>Vol</returns>
    double Vol(Dt dt);

  }

  #region Forward volatility

  /// <summary>
  ///  Holder of forward volatility
  /// </summary>
  /// <remarks>
  ///  <para>If forward volatility term structure is enabled, it constructs
  ///  a curve such that <c>curve.Interpolate(date)</c> gives the forward
  ///  average (Black) volatility for the period from <c>date</c> to option
  ///  expiration.</para>
  /// 
  /// <para>Otherwise, it holds a single flat volatility number.</para>
  /// </remarks>
  public struct ForwardVolatility
  {
    public double At(Dt forwardDate)
    {
      if (_curve == null || forwardDate <= _asOf || _flat < Tiny)
        return _flat;
      return _curve.Interpolate(forwardDate);
    }

    #region Private constructors

    private ForwardVolatility(double flatVolatility)
    {
      _curve = null;
      _asOf = Dt.Empty;
      _flat = flatVolatility;
    }

    public ForwardVolatility(Curve curve, double expiryVolatility)
    {
      _curve = curve;
      _asOf = curve.AsOf;
      _flat = expiryVolatility;
    }

    #endregion

    #region Build Forward Volatility Curve

    /// <summary>
    ///  Build the forward curve from the Black volatilities
    ///  by shifting the expiration dates.
    /// 
    ///  This applies to most vanilla options.
    /// </summary>
    public static ForwardVolatility From<T>(T pricer,
      Func<T, double> getVolatility) where T : class, IPricer
    {
      var vm = getVolatility(pricer);
      if (vm < Tiny || !ToolkitConfigurator.Settings.
        CcrPricer.EnableForwardVolatilityTermStructure)
      {
        return new ForwardVolatility(vm);
      }

      var option = (IOptionProduct)pricer.Product;
      var savedMaturity = option.Maturity;
      Dt asOf = pricer.AsOf, expiry = option.Expiration;
      double tm = Dt.RelativeTime(asOf, expiry);
      try
      {
        var curve = new Curve(asOf)
        {
          DayCount = DayCount.None,
          Interp = DefaultInterp
        };
        curve.Add(asOf, vm);
        foreach (var dt in GetStandardTenorDates(asOf, expiry))
        {
          var t = Dt.RelativeTime(asOf, dt);
          option.Maturity = dt;
          pricer.Reset();
          var v = getVolatility(pricer) / vm;
          var vt = v * v * t;
          curve.Add(option.Expiration, Math.Sqrt((tm - vt) / (tm - t)) * vm);
        }
        return new ForwardVolatility(curve, vm);
      }
      finally
      {
        option.Maturity = savedMaturity;
        pricer.Reset();
      }
    }

    /// <summary>
    ///  Build forward volatility curve by shifting the pricing (as-of) date
    ///  while keeping the expiration unchanged.
    /// </summary>
    public static ForwardVolatility From(SwaptionBlackPricer pricer)
    {
      var vm = pricer.Volatility;
      if (vm < Tiny || !ToolkitConfigurator.Settings.
        CcrPricer.EnableForwardVolatilityTermStructure)
      {
        return new ForwardVolatility(vm);
      }

      Dt asOf = pricer.AsOf, expiry = pricer.Swaption.Expiration;
      try
      {
        var curve = new Curve(asOf)
        {
          DayCount = DayCount.None,
          Interp = DefaultInterp
        };
        curve.Add(asOf, vm);
        foreach (var dt in GetStandardTenorDates(asOf, expiry))
        {
          ResetVolatilityCalculator(pricer, dt);
          curve.Add(dt, pricer.Volatility);
        }
        return new ForwardVolatility(curve, vm);
      }
      finally
      {
        ResetVolatilityCalculator(pricer, asOf);
      }
    }

    /// <summary>
    ///   Change the pricing (as-of) date and adjust the volatility
    ///   calculator accordingly.
    /// </summary>
    private static void ResetVolatilityCalculator(
      SwaptionBlackPricer pricer, Dt asOf)
    {
      pricer.AsOf = asOf;
      pricer.Reset();
      pricer.SetUpVolatilityCalculator();
    }

    #endregion

    public static IEnumerable<Dt> GetStandardTenorDates(Dt asOf, Dt expiry)
    {
      for (int i = 0; i < StandardTenors.Length; ++i)
      {
        Dt dt = Dt.Add(asOf, StandardTenors[i]);
        if (expiry <= dt) { yield break; }
        yield return dt;
      }
    }

    public static readonly string[] StandardTenors =
    {
      "1W", "2W", "3W", 
      "1M", "2M", "3M", "4M", "5M", "6M", "9M", 
      "1Y", "2Y", "5Y", "10Y", "15Y", "20Y", "30Y", "50Y"
    };
    private const double Tiny = 1E-12;
    private readonly Curve _curve;
    private readonly Dt _asOf;
    private readonly double _flat;
    private static readonly Interp DefaultInterp
      = new Linear(new Const(), new Const());
  }

  #endregion

  #region SwapRate

  /// <summary>
  /// Swap rate
  /// </summary>
  public class SwapRate : IUnderlier
  {
    public readonly SwapLegCcrPricer FixedLegPricer;
    public readonly SwapLegCcrPricer FloatingLegPricer;
    private readonly ForwardVolatility _volatility;
    public readonly double UnitCoupon = RateVolatilityUtil.UnitCouponForSwaptionAnnuity;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Swaption pricer</param>
    public SwapRate(SwaptionBlackPricer pricer)
    {
      var unitLeg = (SwapLeg) pricer.Swaption.UnderlyingFixedLeg.Clone();
      unitLeg.CouponSchedule.Clear();
      unitLeg.Index = "";
      unitLeg.Coupon = UnitCoupon;
      if (unitLeg.CustomPaymentSchedule != null && unitLeg.CustomPaymentSchedule.Count > 0)
      {
        foreach (var payment in unitLeg.CustomPaymentSchedule.OfType<FixedInterestPayment>())
        {
          payment.FixedCoupon = UnitCoupon;
        }
      }

      var unitFloatLeg = (SwapLeg) pricer.Swaption.UnderlyingFloatLeg.Clone();
      unitFloatLeg.CouponSchedule.Clear();
      unitFloatLeg.Coupon = 0.0;
      if (unitFloatLeg.CustomPaymentSchedule != null && unitFloatLeg.CustomPaymentSchedule.Count > 0)
      {
        foreach (var payment in unitFloatLeg.CustomPaymentSchedule.OfType<FloatingInterestPayment>())
        {
          payment.FixedCoupon = 0.0; // spread => 0.0
        }
      }
      
      var fix = new SwapLegPricer(unitLeg, pricer.AsOf, pricer.Settle, 1.0,
                                  pricer.DiscountCurve, null, null, null, null, null);
      var flt = new SwapLegPricer(unitFloatLeg, pricer.AsOf, pricer.Settle, 1.0, pricer.DiscountCurve,
                                  pricer.ReferenceIndex, pricer.ReferenceCurve, null,
                                  null, null);
      FixedLegPricer = new SwapLegCcrPricer(fix);
      FloatingLegPricer = new SwapLegCcrPricer(flt);
      _volatility = ForwardVolatility.From(pricer);
    }

    #region IUnderlier Members

    /// <summary>
    /// Underlier level
    /// </summary>
    /// <param name="dt">Date</param>
    /// <param name="numeraire">Numeraire</param>
    /// <returns>Underlier</returns>
    public double Value(Dt dt, out double numeraire)
    {
      numeraire = FixedLegPricer.FastPv(dt)/UnitCoupon;
      return FloatingLegPricer.FastPv(dt)/numeraire;
    }

    /// <summary>
    /// Black vol
    /// </summary>
    /// <param name="dt">Date</param>
    /// <returns>Vol</returns>
    public double Vol(Dt dt)
    {
      return _volatility.At(dt);
    }

    #endregion
  }

  #endregion

  #region ForwardFxRate

  public class ForwardFxRate : IUnderlier
  {
    public readonly FxCurve ForwardFxCurve;
    public readonly Dt Maturity;
    private readonly ForwardVolatility Volatility;
    public readonly DiscountCurve DiscountCurve;
    private readonly double[] _diagnosticValues;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer"></param>
    public ForwardFxRate(FxOptionPricerBase pricer)
    {
      ForwardFxCurve = pricer.FxCurve;
      DiscountCurve = ForwardFxCurve.Ccy2DiscountCurve ?? pricer.DiscountCurve;
      Maturity = pricer.FxOption.Maturity;
      Volatility = pricer.SmileAdjustment != SmileAdjustmentMethod.NoAdjusment ||
        pricer.SmileAdjustment == SmileAdjustmentMethod.VolatilityInterpolation
        ? ForwardVolatility.From(pricer, p => p.VolatilityCurve.Interpolate(
          p.FxOption.Maturity))
        : ForwardVolatility.From(pricer, p => p.VolatilitySurface.Interpolate(
          p.FxOption.Maturity, p.FxOption.Strike));
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer"></param>
    /// <param name="diagnosticLoggingEnabled"></param>
    public ForwardFxRate(FxOptionPricerBase pricer, bool diagnosticLoggingEnabled)
      : this(pricer)
    {
      if (diagnosticLoggingEnabled)
      {
        _diagnosticValues = new double[2];
      }
    }

    #region IUnderlier Members

    /// <summary>
    /// Underlier level
    /// </summary>
    /// <param name="dt">Date</param>
    /// <param name="numeraire">Numeraire</param>
    /// <returns>Underlier</returns>
    public double Value(Dt dt, out double numeraire)
    {
      if (dt >= Maturity)
      {
        numeraire = 1.0;
        return ForwardFxCurve.SpotRate;
      }
      numeraire = DiscountCurve.Interpolate(dt, Maturity);
      if (_diagnosticValues != null)
      {
        _diagnosticValues[0] = numeraire;
        _diagnosticValues[1] = ForwardFxCurve.SpotRate;
      }
      return ForwardFxCurve.FxRate(Maturity);
    }

    /// <summary>
    /// Black vol
    /// </summary>
    /// <param name="dt">Date</param>
    /// <returns>Vol</returns>
    public double Vol(Dt dt)
    {
      return Volatility.At(dt);
    }

    public double[] DiagnosticsValues()
    {
      return _diagnosticValues;
    }

    #endregion
  }

  #endregion

  #region CDXSpread

  /// <summary>
  /// Underlier for CDS/CDX options
  /// </summary>
  public class CDXSpread : IUnderlier
  {
    #region Data

    private readonly double[] AccrualFactors;
    private readonly bool AdjustSpread;
    private readonly DiscountCurve Discount;
    private readonly Dt Effective;
    private readonly double[] Lgd;
    public readonly Dt[] ScheduleDates;
		
    private readonly SurvivalCurve[] Survivals;
    private readonly ForwardVolatility Volatility;
    private readonly double[] Weights;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer"></param>
    public CDXSpread(CDSOptionPricer pricer)
    {
      Discount = pricer.DiscountCurve;
      Survivals = new[] {pricer.SurvivalCurve};
      AdjustSpread = !pricer.Knockout;
      CDS cds = pricer.CDSOption.CDS;
      Schedule sched = cds.Schedule;
      Effective = sched.GetPeriodStart(0);
      ScheduleDates = ArrayUtil.Generate(sched.Count, i => sched.GetPaymentDate(i));
      AccrualFactors = ArrayUtil.Generate(sched.Count, i => sched.Fraction(i, cds.DayCount));
      Weights = new[] {1.0};
      if (pricer.RecoveryCurve != null)
        Lgd = new[] {1.0 - pricer.RecoveryCurve.Interpolate(cds.Maturity)};
      else
        Lgd = new[] {1.0 - pricer.SurvivalCurve.SurvivalCalibrator.RecoveryCurve.Interpolate(cds.Maturity)};
      Volatility = ForwardVolatility.From(pricer, IVol);
    }


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer"></param>
    public CDXSpread(CDXOptionPricer pricer)
    {
      Discount = pricer.DiscountCurve;
      Survivals = pricer.SurvivalCurves;
      CDX cdx = pricer.CDX;
      Schedule sched = cdx.Schedule;
      Effective = sched.GetPeriodStart(0);
      ScheduleDates = ArrayUtil.Generate(sched.Count, i => { return sched.GetPaymentDate(i); });
      AccrualFactors = ArrayUtil.Generate(sched.Count, i => { return sched.Fraction(i, cdx.DayCount); });
      AdjustSpread = pricer.AdjustSpread;
      double w = 1.0/Survivals.Length;
      Weights = pricer.CDX.Weights ?? ArrayUtil.Generate(pricer.SurvivalCurves.Length, i => { return w; });
      Lgd = new double[Survivals.Length];
      if (pricer.RecoveryCurves != null && (pricer.RecoveryCurves.Length == Survivals.Length))
        for (int i = 0; i < Survivals.Length; ++i)
          Lgd[i] = Weights[i]*(1.0 - pricer.RecoveryCurves[i].Interpolate(cdx.Maturity));
      else
        for (int i = 0; i < Survivals.Length; ++i)
          Lgd[i] = Weights[i]*(1.0 - Survivals[i].SurvivalCalibrator.RecoveryCurve.Interpolate(cdx.Maturity));
      Volatility = ForwardVolatility.From(pricer, IVol);
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="numeraire"></param>
    /// <returns></returns>
    public double Value(Dt dt, out double numeraire)
    {
      if (dt >= ScheduleDates[ScheduleDates.Length - 1])
      {
        numeraire = 0.0;
        return 0.0;
      }
      double l, d, p;
      Calc(dt, out l, out p, out d);
      numeraire = d;
      if (numeraire == 0.0)
        return 0.0;
      return AdjustSpread ? (l + p)/d : p/d;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public double Vol(Dt dt)
    {
      return Volatility.At(dt);
    }

	  private double IVol(CDSOptionPricer pricer)
    {
      double pv = pricer.Pv()/pricer.Notional;
      double l, d, p;
      Calc(pricer.Settle, out l, out p, out d);
      pv /= d;
      double T = Dt.Diff(pricer.Settle, pricer.CDSOption.Maturity)/365.0;
      double K = pricer.CDSOption.Strike;
      return Black.ImpliedVolatility(pricer.CDSOption.Type, T, p/d, K, pv);
    }


    private double IVol(CDXOptionPricer pricer)
    {
      double pv = pricer.Pv()/pricer.Notional;
      double l, d, p;
      Calc(pricer.Settle, out l, out p, out d);
      double spread = pricer.AdjustSpread ? (l + p)/d : p/d;
      pv /= d;
      double T = pricer.Time;
      double K = pricer.EffectiveStrike;
      return Black.ImpliedVolatility(pricer.CDXOption.Type, T, spread, K, pv);
    }

    private void Calc(Dt settle, out double l, out double p, out double d)
    {
      Dt effective = Effective;
      int idx = 0;
      if (settle > ScheduleDates[0])
      {
        idx = Array.BinarySearch(ScheduleDates, settle);
        if (idx < 0)
          idx = ~idx;
        effective = ScheduleDates[idx - 1];
      }
      l = p = d = 0.0;
      double settleDf = Discount.Interpolate(settle);
      var df = new double[ScheduleDates.Length];
      for (int j = idx; j < df.Length; ++j)
        df[j] = Discount.Interpolate(ScheduleDates[j]);
      for (int i = 0; i < Survivals.Length; ++i)
      {
        SurvivalCurve sc = Survivals[i];
        if (sc.Defaulted == Defaulted.HasDefaulted)
          continue;
        double delta, df0, s0, df1 = Discount.Interpolate(effective), s1 = sc.Interpolate(effective);
        if (effective > Effective)
          l += Lgd[i]*(sc.Interpolate(Effective) - s1);
        double pi = 0.0, di = 0.0;
        for (int j = idx; j < ScheduleDates.Length; ++j)
        {
          df0 = df1;
          df1 = df[j];
          s0 = s1;
          s1 = sc.Interpolate(ScheduleDates[j]);
          delta = AccrualFactors[j];
          di += 0.5*df1*delta*(s0 + s1);
          pi += 0.5*(df0 + df1)*(s0 - s1);
        }
        pi *= Lgd[i];
        di *= Weights[i];
        p += pi;
        d += di;
      }
      p /= settleDf;
      d /= settleDf;
    }

    #endregion
  }

  #endregion

  #region CreditIndexForward
  public class CreditIndexForward : IUnderlier
  {
    #region Data
    private readonly ICreditIndexOptionPricer _pricer;
    private readonly ForwardVolatility _volatility;
    public readonly bool IsPriceVolatility;
    #endregion

    #region Constructor
    public CreditIndexForward(ICreditIndexOptionPricer pricer)
    {
      _pricer = (ICreditIndexOptionPricer)
        ((BaseEntityObject)pricer).ShallowCopy();
      IsPriceVolatility = _pricer.IsPriceVolatility();
      _volatility = ForwardVolatility.From(_pricer,
        p => p.ImplyVolatility(p.FairPrice()));
    }
    #endregion

    #region IUnderlier Members

    public double Value(Dt dt, out double numeraire)
    {
      var pricer = _pricer;
      pricer.AsOf = dt;
      if (dt > _pricer.Settle) _pricer.Settle = dt;
      pricer.Reset();
      numeraire = pricer.GetNumerairLevel(IsPriceVolatility);
      double fwd = pricer.EffectiveForwardSpread();
      if (IsPriceVolatility)
      {
        var pv01 = pricer.ForwardPv01;
        return 1 + pv01 * (pricer.CDXOption.CDX.Premium - fwd);
      }
      return fwd;
    }

    public double Vol(Dt dt)
    {
      return _volatility.At(dt);
    }

    public OptionType OptionType
    {
      get
      {
        var type = _pricer.CDXOption.Type;
        if (_pricer.IsPriceVolatility()) return type;
        return type == OptionType.Put
          ? OptionType.Call
          : (type == OptionType.Call ? OptionType.Put : OptionType.None);
      }
    }
    #endregion
  }
  #endregion

  #region ForwardAssetPrice
  public class ForwardStockPrice : IUnderlier
  {
    private readonly DiscountCurve DiscountCurve;
    private readonly ForwardPriceCurve ForwardCurve;
    private readonly Dt Maturity;
    private readonly ForwardVolatility Volatility;

    public ForwardStockPrice(StockOptionPricer pricer)
    {
      ForwardCurve = pricer.StockCurve;
      DiscountCurve = pricer.DiscountCurve;
      Maturity = pricer.StockOption.Expiration;
      Volatility = ForwardVolatility.From(pricer, p=>p.Volatility);
    }

    #region IUnderlier Members

    public double Value(Dt dt, out double numeraire)
    {
      if (dt > Maturity)
      {
        numeraire = 1.0;
        int idx = ForwardCurve.After(dt);
        return ForwardCurve.Interpolate(Maturity);
      }
      numeraire = DiscountCurve.Interpolate(dt, Maturity);
      return ForwardCurve.Interpolate(Maturity);
    }

    public double Vol(Dt dt)
    {
      return Volatility.At(dt);
    }

    #endregion
  }

  public class ForwardCommodityPrice : IUnderlier
  {
    private readonly DiscountCurve DiscountCurve;
    private readonly ForwardPriceCurve ForwardCurve;
    private readonly Dt Maturity;
    private readonly ForwardVolatility Volatility;

    public ForwardCommodityPrice(CommodityOptionPricer pricer)
    {
      ForwardCurve = pricer.CommodityCurve;
      DiscountCurve = pricer.DiscountCurve;
      Maturity = pricer.CommodityOption.Expiration;
      Volatility = ForwardVolatility.From(pricer, p=>p.Volatility);
    }

    public ForwardCommodityPrice(CommodityFutureOptionBlackPricer pricer)
    {
      ForwardCurve = pricer.CommodityCurve;
      DiscountCurve = pricer.DiscountCurve;
      Maturity = pricer.CommodityFutureOption.Underlying.Maturity;
      Volatility = ForwardVolatility.From(pricer, p=>p.Volatility);
    }

    public ForwardCommodityPrice(CommodityForwardOptionBlackPricer pricer)
    {
      ForwardCurve = pricer.CommodityCurve;
      DiscountCurve = pricer.DiscountCurve;
      Maturity = pricer.CommodityForwardOption.Underlying.Maturity;
      Volatility = ForwardVolatility.From(pricer, p=>p.Volatility);
    }

    #region IUnderlier Members

    public double Value(Dt dt, out double numeraire)
    {
      if (dt > Maturity)
      {
        numeraire = 1.0;
        return ForwardCurve.Interpolate(Maturity);
      }
      numeraire = DiscountCurve.Interpolate(dt, Maturity);
      return ForwardCurve.Interpolate(Maturity);
    }

    public double Vol(Dt dt)
    {
      return Volatility.At(dt);
    }

    #endregion
  }

  public class ForwardBondPrice : IUnderlier
  {
    private readonly DiscountCurve DiscountCurve;
    private readonly BondCcrPricer UnderlyingBondCcrPricer;
    private readonly Dt Maturity;
    private readonly ForwardVolatility Volatility;

    public ForwardBondPrice(BondOptionBlackPricer pricer)
    {
      UnderlyingBondCcrPricer = new BondCcrPricer(pricer.BondPricer);
      DiscountCurve = pricer.DiscountCurve;
      Maturity = pricer.BondOption.Maturity;
      Volatility = ForwardVolatility.From(pricer, p => p.Volatility);
    }

    #region IUnderlier Members

    public double Value(Dt dt, out double numeraire)
    {
      var underly = UnderlyingBondCcrPricer;
      var bondPricer = (BondPricer) underly.Pricer;
      if (dt > Maturity)
      {
        numeraire = 1.0;
        return underly.ForwardPrice(dt, Maturity, underly.FastPv(dt))
          - bondPricer.AccruedInterest(Maturity, Maturity);
      }

      double survivalAtExpiry = (bondPricer.SurvivalCurve != null)
        ? bondPricer.SurvivalCurve.Interpolate(dt, Maturity)
        : 1.0;
      numeraire = DiscountCurve.Interpolate(dt, Maturity)*survivalAtExpiry;
      return underly.ForwardPrice(dt, Maturity, underly.FastPv(dt))
        - bondPricer.AccruedInterest(Maturity, Maturity);
    }

    public double Vol(Dt dt)
    {
      return Volatility.At(dt);
    }

    #endregion
  }

  #endregion

  #endregion

  #region VanillaOptionCcrPricer

  /// <summary>
  /// Vanilla black pricer 
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class OptionCcrPricer : CcrPricer
  {
    #region Data
    /// <summary>
    /// Cash settle date
    /// </summary>
    [NonSerialized][NoClone] protected Dt CashSettleDate;

    /// <summary>
    /// Effective strike
    /// </summary>
    [NonSerialized][NoClone] protected double EffectiveStrike;

    /// <summary>
    /// Expiry date
    /// </summary>
    [NonSerialized][NoClone] protected Dt Expiry;
    [NonSerialized][NoClone] private double _notional;

    /// <summary>
    /// Option type
    /// </summary>
    [NonSerialized][NoClone] protected OptionType OptionType;

    /// <summary>
    /// Boolean type. PhysicalSettled
    /// </summary>
    [NonSerialized][NoClone] protected bool PhysicallySettled;
    [NonSerialized][NoClone] public IUnderlier Underlier;

    /// <summary>
    /// 
    /// </summary>
    [NonSerialized][NoClone] protected Dt UnderlierMaturity;
    [NonSerialized][NoClone] private ISpot _spotFactorAdjustment;

    public bool _useRelativeTime = true;
    public readonly DistributionType _distribution;
    [NonSerialized][NoClone] private Dt[] _exposureDates;

    /// <summary>
    /// Exposure dates
    /// </summary>
	  public override Dt[] ExposureDates
	  {
		  get
		  {
				if(_exposureDates == null)
					Init();
			  return _exposureDates;
		  }
		  set
		  {
			  _exposureDates = value;
		    if (PaymentPricer != null)
		      PaymentPricer.ExposureDates = value;
				Init();
		  }
	  }

    /// <summary>
    /// Notional
    /// </summary>
    protected double Notional
    {
      get
      {
        return _spotFactorAdjustment == null ? _notional
          : (_notional/_spotFactorAdjustment.Value);
      }
    }
	  #endregion

    #region ExerciseState

    /// <summary>
    /// Exercise state
    /// </summary>
    public enum ExerciseState
    {
      /// <summary>
      /// No exercise decision yet been made
      /// </summary>
      None,

      /// <summary>
      /// Option was exercised
      /// </summary>
      Exercised,

      /// <summary>
      /// Option expired un-exercised
      /// </summary>
      NotExercised,
    }

    #endregion

    #region Constructor

    [ObjectLogger(
      Name = "OptionPricer",
      Category = "Exposures",
      Description = "Option Pricing within Monte Carlo Simulation",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics" })]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(OptionCcrPricer));

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="blackPricer">option pricer</param>
    public OptionCcrPricer(IPricer blackPricer) : base(blackPricer)
    {      
      var swpnPricer = blackPricer as SwaptionBlackPricer;
      _distribution = swpnPricer == null || swpnPricer.VolatilityObject == null
        ? DistributionType.LogNormal
        : swpnPricer.VolatilityObject.DistributionType;
    }

    /// <summary>
    /// Initialize
    /// </summary>
    protected virtual void Init()
    {
			IList<Dt> underlierDts = null;

	    if (Pricer is SwaptionBlackPricer)
      {
        var p = Pricer as SwaptionBlackPricer;
				var underlier = new SwapRate(p);
	      Underlier = underlier;
        _notional = p.Notional;
        OptionType = p.Swaption.OptionType;
        Expiry = p.Swaption.Expiration;
        PhysicallySettled = p.Swaption.SettlementType == SettlementType.Physical;
        EffectiveStrike = p.EffectiveStrike;
				UnderlierMaturity = Dt.Roll(p.Swaption.UnderlyingFixedLeg.Maturity, BDConvention.Following, p.Swaption.UnderlyingFixedLeg.Calendar);
        CashSettleDate = Dt.Roll(p.Swaption.Maturity, p.Swaption.UnderlyingFixedLeg.BDConvention, p.Swaption.UnderlyingFixedLeg.Calendar);
        _useRelativeTime = false; //TODO: revisit this
	      if (PhysicallySettled)
	      {
					if (_exposureDates != null && _exposureDates.Any())
						underlier.FloatingLegPricer.ExposureDates = _exposureDates;
		      underlierDts = underlier.FloatingLegPricer.ExposureDates; 
	      }
      }
      if (Pricer is FxOptionPricerBase)
      {
        var p = Pricer as FxOptionPricerBase;
        Underlier = new ForwardFxRate(p, BinaryLogger.IsObjectLoggingEnabled);
        _notional = p.Notional;
        OptionType = p.FxOption.Type;
        Expiry = p.FxOption.Maturity;
        PhysicallySettled = false;
        EffectiveStrike = p.FxOption.Strike;
        UnderlierMaturity = p.FxCurve.SpotFxRate.FxSpotDate(p.FxOption.Maturity);
        CashSettleDate = UnderlierMaturity;
        if (p.PremiumInBaseCcy) _spotFactorAdjustment = p.FxCurve.SpotFxRate;
        _useRelativeTime = false; //TODO:revisit this
      }
      if (Pricer is CDSOptionPricer)
      {
        var p = Pricer as CDSOptionPricer;
				var underlier = new CDXSpread(p);
	      Underlier = underlier;
        _notional = p.Notional;
        OptionType = p.CDSOption.Type;
        Expiry = p.CDSOption.Maturity;
        PhysicallySettled = p.CDSOption.SettlementType == SettlementType.Physical;
        EffectiveStrike = p.CDSOption.Strike;
        UnderlierMaturity = p.CDSOption.CDS.Maturity;
        CashSettleDate = Dt.Roll(Expiry, p.CDSOption.CDS.BDConvention,p.CDSOption.CDS.Calendar);
				if (PhysicallySettled)
				{
					underlierDts = new List<Dt>();
					foreach (var paymentDt in underlier.ScheduleDates)
					{
						var beforePaymentDt = Dt.Add(paymentDt, -1);
						if (beforePaymentDt > p.AsOf)
							underlierDts.Add(beforePaymentDt);
						underlierDts.Add(paymentDt);
					}
				}
      }
      if (Pricer is ICreditIndexOptionPricer)
      {
        var p = Pricer as ICreditIndexOptionPricer;
        var u = new CreditIndexForward(p);
        Underlier = u;
        OptionType = u.OptionType;
        _notional = p.Notional;
        Expiry = p.CDXOption.Maturity;
        PhysicallySettled = p.CDXOption.SettlementType == SettlementType.Physical;
        EffectiveStrike = p.GetForwardStrike(u.IsPriceVolatility);
        UnderlierMaturity = p.CDXOption.CDX.Maturity;
        CashSettleDate = Expiry;
				if (PhysicallySettled)
				{
					underlierDts = new List<Dt>();
					var sched = ArrayUtil.Generate(p.CDXOption.CDX.Schedule.Count, i => p.CDXOption.CDX.Schedule.GetPaymentDate(i));
      		foreach (var paymentDt in sched)
					{
            var beforePaymentDt = Dt.Add(paymentDt, -1);
						if (beforePaymentDt > p.AsOf)
							underlierDts.Add(beforePaymentDt);
						underlierDts.Add(paymentDt);
					}
				}
      }
      if (Pricer is StockOptionPricer) //black pricer for assets
      {
        var p = Pricer as StockOptionPricer;
        Underlier = new ForwardStockPrice(p);
        _notional = p.Notional;
        OptionType = p.StockOption.Type;
        Expiry = p.StockOption.Expiration;
        PhysicallySettled = false;
        EffectiveStrike = p.StockOption.Strike;
        UnderlierMaturity = Dt.MaxValue;
        CashSettleDate = Dt.Roll(Expiry, BDConvention.Following, Calendar.None);
      }
      if(Pricer is CommodityOptionPricer)
      {
        var p = Pricer as CommodityOptionPricer;
        Underlier = new ForwardCommodityPrice(p);
        _notional = p.Notional;
        OptionType = p.CommodityOption.Type;
        Expiry = p.CommodityOption.Expiration;
        PhysicallySettled = false;
        EffectiveStrike = p.CommodityOption.Strike;
        UnderlierMaturity = p.CommodityOption.Expiration;
        CashSettleDate = Dt.Roll(Expiry, BDConvention.Following, Calendar.None);
      }
      if (Pricer is CommodityForwardOptionBlackPricer)
      {
        var p = Pricer as CommodityForwardOptionBlackPricer;
        Underlier = new ForwardCommodityPrice(p);
        _notional = p.Notional;
        OptionType = p.CommodityForwardOption.Type;
        Expiry = p.CommodityForwardOption.Expiration;
        PhysicallySettled = true;
        EffectiveStrike = p.CommodityForwardOption.Strike;
        UnderlierMaturity = p.CommodityForwardOption.Underlying.Maturity;
				CashSettleDate = Dt.Roll(Expiry, BDConvention.Following, Calendar.None); 
      }
      if (Pricer is CommodityFutureOptionBlackPricer)
      {
        var p = Pricer as CommodityFutureOptionBlackPricer;
        Underlier = new ForwardCommodityPrice(p);
        _notional = p.Notional;
        OptionType = p.CommodityFutureOption.Type;
        Expiry = p.CommodityFutureOption.Expiration;
        PhysicallySettled = false;
        EffectiveStrike = p.CommodityFutureOption.Strike;
        UnderlierMaturity = p.CommodityFutureOption.Underlying.Maturity;
				CashSettleDate = Dt.Roll(Expiry, BDConvention.Following, Calendar.None);
		  }
      if (Pricer is BondOptionBlackPricer)
      {
        var p = Pricer as BondOptionBlackPricer;
        Underlier = new ForwardBondPrice(p);
        _notional = p.Notional;
        OptionType = p.BondOption.Type;
        Expiry = p.BondOption.Expiration;
        PhysicallySettled = false;
        EffectiveStrike = p.BondOption.Strike;
        UnderlierMaturity = p.BondOption.Underlying.Maturity;
				CashSettleDate = Dt.Roll(Expiry, p.BondOption.Bond.BDConvention, p.BondOption.Bond.Calendar);
		  }

			if(Expiry.IsEmpty())
				throw new ArgumentException(String.Format("Option Pricer [{0}] not supported", Pricer.GetType().Name));

      var maxDt = PhysicallySettled ? UnderlierMaturity : CashSettleDate;
      var exposureDates = new UniqueSequence<Dt>();
      if (_exposureDates != null && _exposureDates.Any(dt => dt <= maxDt))
      {
        exposureDates.Add(_exposureDates.Where(dt => dt <= maxDt).ToArray());
        var lastDt = exposureDates.Max();
        if (lastDt < maxDt && _exposureDates.Any(dt => dt > maxDt))
        {
          exposureDates.Add(_exposureDates.First(dt => dt > maxDt));
        }
        maxDt = _exposureDates.First();
      }

			if (Pricer.AsOf <= maxDt)
				exposureDates.Add(Pricer.AsOf);
			if (CashSettleDate <= maxDt)
			{
        // fill in monthly dates up to CashSettleDate
        // a crude way to capture non linear exposure profile of options
			  var nextDt = Dt.AddMonth(Pricer.AsOf, 1, true);
			  while (nextDt < CashSettleDate)
			  {
			    exposureDates.Add(nextDt);
          nextDt = Dt.AddMonth(nextDt, 1, true);
			  }
				exposureDates.Add(CashSettleDate);
				exposureDates.Add(Dt.Add(Expiry, -1));
				var p = Pricer as SwaptionBlackPricer;
				if (p != null && p.Swaption.NotificationDays != 0)
				  exposureDates.Add(p.Swaption.Maturity);
			}

			if (PhysicallySettled)
			{
				if (underlierDts != null && underlierDts.Any())
				{
					foreach (var underlierDt in underlierDts)
					{
						if (underlierDt <= maxDt)
							exposureDates.Add(underlierDt);
					}
				}
			  if (UnderlierMaturity <= maxDt)
			  {
			    exposureDates.Add(UnderlierMaturity);
			  }
			}

      if (PaymentPricer != null)
        exposureDates.Add(PaymentPricer.ExposureDates);
			_exposureDates = exposureDates.ToArray();
       
      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        BuildDiagnosticsTable();
      }
    }

    #endregion

    // used for binary logging
    public DataTable _diagnosticsDataTable;

    #region Methods

    public Dt SavedDt { get; set; }

    public double SavedLevel { get; set; }

    public ExerciseState CurrentExerciseState { get; set; }

    public void Restart(Dt settle)
    {
      SavedDt = settle;
      double numeraire;
      SavedLevel = Underlier.Value(settle, out numeraire);
      CurrentExerciseState = ExerciseState.None;
    }

    public ExerciseState ExerciseDecision(double f)
    {
      if (OptionType == OptionType.Call)
        return (f > EffectiveStrike) ? ExerciseState.Exercised : ExerciseState.NotExercised;
      return (f < EffectiveStrike) ? ExerciseState.Exercised : ExerciseState.NotExercised;
    }

    public static double Intrinsic(OptionType optionType, double f, double k, double numeraire)
    {
      if (optionType == OptionType.Call)
        return (f - k)*numeraire;
      return (k - f)*numeraire;
    }

    /// <summary>
    /// Option pv
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv</returns>
    public override double FastPv(Dt settle)
    {
      if (Underlier == null)
        Init();
      if (settle <= SavedDt)
        Restart(settle);
      if (settle <= Expiry)
      {
        double T = _useRelativeTime
          ? Dt.RelativeTime(settle, Expiry)
          : Dt.Fraction(settle, Expiry, DayCount.Actual365Fixed);
        double vol = Underlier.Vol(settle);
        SavedDt = settle;
        double numeraire;
        SavedLevel = Underlier.Value(settle, out numeraire);
        if (settle == Expiry)
        {
          if (BinaryLogger.IsObjectLoggingEnabled)
          {
            AddTableEntry(settle, T, numeraire, vol);
          }
          CurrentExerciseState = ExerciseDecision(SavedLevel);
        }

        var option = _distribution == DistributionType.Normal
          ? BlackNormal.P(OptionType, T, 0.0, SavedLevel, EffectiveStrike, vol)
          : Black.P(OptionType, T, SavedLevel, EffectiveStrike, vol);
        if (settle < Expiry)
        {
          if (BinaryLogger.IsObjectLoggingEnabled)
          {
            AddTableEntry(settle, T, numeraire, vol);
          }
          var pv = Notional * numeraire * option;
          if (PaymentPricer != null)
            pv += PaymentPricer.FastPv(settle);
          return pv;
        }
      }

      // flush logs and reset diagnosticsDataTable
      if (BinaryLogger.IsObjectLoggingEnabled && _diagnosticsDataTable.Rows.Count > 0)
      {
        var key = string.Format("{0}.Path{1}", string.IsNullOrEmpty(Pricer.Product.Description) ? Pricer.Product.ToString() : Pricer.Product.Description, ObjectLoggerUtil.GetPath("CCRPricerPath"));
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), key);
        binaryLogAggregator.Append(typeof(OptionCcrPricer), key, AppenderUtil.DataTableToDataSet(_diagnosticsDataTable)).Log();

        BuildDiagnosticsTable();
      }

      if (settle > UnderlierMaturity)
        return 0.0;

      if (CurrentExerciseState == ExerciseState.None && !SavedDt.IsEmpty())
      {
        double numeraire;
        double level = Underlier.Value(Expiry, out numeraire);
        //brownian bridge interpolation to infer Exercise decision
        double vol = Underlier.Vol(settle);
        double dt = Dt.FractDiff(SavedDt, Expiry)/365.0;
        double dT = Dt.FractDiff(SavedDt, settle)/365.0;
        double h = dt/dT;
        double v = 0.5*vol*vol*dT;
        double dw = Math.Log(level/SavedLevel);
        double spreadAtExpiry = SavedLevel*Math.Exp(dw*h + h*(1.0 - h)*v);
        CurrentExerciseState = ExerciseDecision(spreadAtExpiry);
        SavedLevel = spreadAtExpiry;
      }

      if (CurrentExerciseState == ExerciseState.Exercised)
      {
        double numeraire;
        double level;
        if (PhysicallySettled)
        {
          level = Underlier.Value(settle, out numeraire);
        }
        else
        {
          if (settle >= CashSettleDate)
          {
            if (PaymentPricer != null)
              return PaymentPricer.FastPv(settle);
            return 0.0;
          }
          level = SavedLevel;
          Underlier.Value(settle, out numeraire);
        }
        var pv = Notional*Intrinsic(OptionType, level, EffectiveStrike, numeraire);
        if (PaymentPricer != null)
          pv += PaymentPricer.FastPv(settle);
        return pv; 
      }
      if (PaymentPricer != null)
        return PaymentPricer.FastPv(settle);
      return 0.0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="T"></param>
    /// <param name="numeraire"></param>
    /// <param name="vol"></param>
    protected void AddTableEntry(Dt settle, double T, double numeraire, double vol)
    {
      var row = _diagnosticsDataTable.NewRow();
      var forwardFxRate = Underlier as ForwardFxRate;
      var additionalDiagnosticValues = (forwardFxRate != null) ? forwardFxRate.DiagnosticsValues() : new double[2];
      row["PathId"] = ObjectLoggerUtil.GetPath("CCRPricerPath");
      row["PricerSettleDt"] = settle;
      row["Expiry"] = Expiry;
      row["T"] = T;
      row["UnderlyerLevel"] = SavedLevel;
      row["Numeraire"] = numeraire;
      row["Vol"] = vol;
      row["Strike"] = EffectiveStrike;
      row["ForeignDF"] = additionalDiagnosticValues[0];
      row["CurrentSpotRate"] = additionalDiagnosticValues[1];
      _diagnosticsDataTable.Rows.Add(row);
    }

    /// <summary>
    /// 
    /// </summary>
    protected void BuildDiagnosticsTable()
    {
      _diagnosticsDataTable = new DataTable("OptionCcrPricer");
      _diagnosticsDataTable.Columns.Add("PathId", typeof(int));
      _diagnosticsDataTable.Columns.Add("PricerSettleDt", typeof(string));
      _diagnosticsDataTable.Columns.Add("Expiry", typeof(string));
      _diagnosticsDataTable.Columns.Add("T", typeof(double));
      _diagnosticsDataTable.Columns.Add("UnderlyerLevel", typeof(double));
      _diagnosticsDataTable.Columns.Add("Numeraire", typeof(double));
      _diagnosticsDataTable.Columns.Add("Vol", typeof(double));
      _diagnosticsDataTable.Columns.Add("Strike", typeof(double));
      _diagnosticsDataTable.Columns.Add("ForeignDF", typeof(double));
      _diagnosticsDataTable.Columns.Add("CurrentSpotRate", typeof(double));
    }

    #endregion
  }

  #endregion

  #region PricerWithPaymentSchedule

  /// <summary>
  /// Pricer with payment schedule
  /// </summary>
  [Serializable]
  public class CcrPricerWithPaymentSchedule : CcrPricer
  {
    #region Data

    [NonSerialized][NoClone] private DiscountCurve DiscountCurve;
    [NonSerialized][NoClone] private Payment[][] PaymentSchedule;
    [NonSerialized][NoClone] private Dt[] _exposureDates;
    [NonSerialized][NoClone] private Dt _rolledMaturity;

	  #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    public CcrPricerWithPaymentSchedule(IPricer pricer)
      : base(pricer)
    {
    }

    private void Init()
    {
      var p = Pricer as PricerBase;
      PropertyInfo propertyInfo = p.GetType().GetProperty("DiscountCurve");
      DiscountCurve = (DiscountCurve) propertyInfo.GetValue(p, null);
      var ps = GetPaymentSchedule(p, GetPsFromDate(p));
      var pmts = new List<Payment[]>();
	    var maxDt = _rolledMaturity = Dt.Roll(p.Product.Maturity, BDConvention.Following, Calendar.None);
			var exposureDts = new UniqueSequence<Dt>();
	    if (_exposureDates != null && _exposureDates.Any(dt => dt <= maxDt))
	    {
        exposureDts.Add(_exposureDates.Where(dt => dt <= maxDt).ToArray());
	      var lastDt = exposureDts.Max();
        if (lastDt < maxDt && _exposureDates.Any(dt => dt > maxDt))
	        exposureDts.Add(_exposureDates.First(dt => dt > maxDt));
		    maxDt = _exposureDates.First();
	    }
			exposureDts.Add(p.AsOf);
      if (ps != null && ps.Count > 0)
      {
        foreach (Dt dt in ps.GetPaymentDates())
        {
          Payment[] pmt = ps.GetPaymentsOnDate(dt).ToArray();
          for (int i = 0; i < pmt.Length; ++i)
          {
            if (!pmt[i].IsProjected)
              pmt[i].Amount = pmt[i].Amount;
            //avoid multiplication by accrued during simulation if payment is not projected
          }
          pmts.Add(pmt);
          if (dt <= maxDt)
          {
            var beforePaymentDt = Dt.Add(dt, -1);
            if (beforePaymentDt > p.AsOf)
              exposureDts.Add(beforePaymentDt);
            exposureDts.Add(dt);
          }
        }
      }
      PaymentSchedule = pmts.ToArray();
      if (PaymentPricer != null)
        exposureDts.Add(PaymentPricer.ExposureDates);
	    _exposureDates = exposureDts.ToArray();
    }

    #endregion

    #region Methods

    public override double FastPv(Dt settle)
    {
      if (PaymentSchedule == null)
        Init();
      if (Pricer == null || settle > _rolledMaturity) return 0.0;
      var p = Pricer as PricerBase;
      var pv = CcrPricerUtils.Pv(settle, PaymentSchedule, DiscountCurve, false, false, p.Product.Notional, p.Notional);
      if (PaymentPricer != null)
        pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    #endregion

	  public override Dt[] ExposureDates
	  {
		  get
		  {
				if(_exposureDates == null)
					Init();
			  return _exposureDates;
		  }
		  set
		  {
			  _exposureDates = value;
		    if (PaymentPricer != null)
		      PaymentPricer.ExposureDates = value;
				Init();
		  }
	  }
  }

  #endregion

  #region CapFloorPricer

  [Serializable]
  [ObjectLoggerEnabled]
  public class CapFloorCcrPricer : CcrPricer
  {
    [ObjectLogger(
      Name = "CapFloorPricer",
      Category = "Exposures",
      Description = "Cap Floor Pricing within Monte Carlo Simulation",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics" })]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(CapFloorCcrPricer));

    private static bool CmsCapletConvexityFromExposureDate =>
      ToolkitConfigurator.Settings.CcrPricer.CmsCapletConvexityFromExposureDate;

    [NonSerialized][NoClone] private PaymentSchedule _caplets;
    [NonSerialized][NoClone] private Dt[] _exposureDts;
    [NonSerialized][NoClone] private List<double> _vols;
    
    // used for binary logging
    private DataTable _diagnosticsDataTable;

    public CapFloorCcrPricer(CapFloorPricerBase iPricer)
      : base(iPricer)
    {
    }

    public void Init()
    {
      var capPricer = Pricer as CapFloorPricerBase;
      if (capPricer == null)
        return;
      if (_caplets == null)
        _caplets = AttachVolatilityFixer(capPricer.Caplets);
	    var dts = new UniqueSequence<Dt>();
	    var maxDt = Dt.Roll(capPricer.Cap.Maturity, capPricer.Cap.BDConvention, capPricer.Cap.Calendar);
      if (_exposureDts != null && _exposureDts.Any(dt => dt <= maxDt))
	    {
		    dts.Add(_exposureDts.Where(dt => dt <= maxDt).ToArray());
	      var lastDt = dts.Max();
        if (lastDt < maxDt && _exposureDts.Any(dt => dt > maxDt))
	        dts.Add(_exposureDts.First(dt => dt > maxDt));
		    maxDt = _exposureDts.First();
	    }
	    dts.Add(capPricer.AsOf);
      foreach (CapletPayment caplet in _caplets)
      {
				if (caplet.PayDt <= maxDt && caplet.PayDt >= capPricer.AsOf)
	      {
		      var beforeDt = Dt.Add(caplet.PayDt, -1);
					if (beforeDt > capPricer.AsOf)
						dts.Add(beforeDt);
					dts.Add(caplet.PayDt);
	      }
      }

      if (PaymentPricer != null)
        dts.Add(PaymentPricer.ExposureDates);
	    _exposureDts = dts.ToArray();

      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        BuildDiagnosticsTable();
      }
    }

    private void SetUpVolatilities()
    {
      var vols = _vols = new List<double>();
      var capPricer = Pricer as CapFloorPricerBase;
      if (capPricer == null) return;

      var cap = capPricer.Cap;
      var settle = capPricer.Settle;
      foreach (CapletPayment caplet in _caplets)
      {
        var vol = (caplet.Expiry > settle)
          ? capPricer.VolatilityCube.CapletVolatility(caplet.Expiry,
            capPricer.ForwardRate(caplet), caplet.Strike, cap.ReferenceRateIndex)
          : 0.0;
        vols.Add(vol);
      }
    }

    public override Dt[] ExposureDates
	  {
		  get
		  {
				if(_exposureDts == null)
					Init();
			  return _exposureDts;
		  }
		  set
		  {
			  _exposureDts = value;
		    if (PaymentPricer != null)
		      PaymentPricer.ExposureDates = value;
			  Init();
		  }
	  }

	  public override double FastPv(Dt settle)
    {
      var capPricer = Pricer as CapFloorPricerBase;
      if (capPricer == null)
        return 0;

      if (_caplets == null)
        Init();
      if (_vols == null)
        SetUpVolatilities();

      double pv = 0;
      int index = 0;

      Dt cutoff = settle > Pricer.Settle ? settle : Pricer.Settle;

      // Go through schedule and price caplets
      foreach (CapletPayment caplet in _caplets)
      {
        
        if (caplet.PayDt > cutoff)
        {
          // Value caplet         
          pv += CapletPv(settle, capPricer.Cap, caplet, capPricer.DiscountCurve, capPricer.ForwardRate,
                         capPricer.VolatilityType, index);
        }
        index++;
      }
      pv /= capPricer.DiscountCurve.DiscountFactor(settle);

	    if (BinaryLogger.IsObjectLoggingEnabled)
	    {
	      if (_caplets.Count > 0)
	      {
	        Dt lastDate = _caplets.First().PayDt;
	        foreach (CapletPayment caplet in _caplets)
	        {
	          if (lastDate < caplet.PayDt)
	          {
	            lastDate = caplet.PayDt;
	          }
	        }
	        if (settle >= lastDate)
	        {
            var key = string.Format("{0}.Path{1}", Pricer.Product.ToString(), ObjectLoggerUtil.GetPath("CCRPricerPath"));
            var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), key);
            binaryLogAggregator.Append(typeof(CapFloorCcrPricer), key, AppenderUtil.DataTableToDataSet(_diagnosticsDataTable)).Log();

	          BuildDiagnosticsTable();
	        }
	      }
	    }

	    pv *= capPricer.EffectiveNotional;
	    if (PaymentPricer != null)
	      pv += PaymentPricer.FastPv(settle);
      return pv;
    }

    private void BuildDiagnosticsTable()
    {
      _diagnosticsDataTable = new DataTable("CapFloorCcrPricer");
      _diagnosticsDataTable.Columns.Add("PathId", typeof(int));
      _diagnosticsDataTable.Columns.Add("PricerSettleDt", typeof(string));
      _diagnosticsDataTable.Columns.Add("CapletPayDt", typeof(string));
      _diagnosticsDataTable.Columns.Add("PeriodFraction", typeof(double));
      _diagnosticsDataTable.Columns.Add("Rate", typeof(double));
      _diagnosticsDataTable.Columns.Add("Vol", typeof(double));
      _diagnosticsDataTable.Columns.Add("T", typeof(double));
      _diagnosticsDataTable.Columns.Add("Strike", typeof(double));
      _diagnosticsDataTable.Columns.Add("DiscountFactor", typeof(double));
      _diagnosticsDataTable.Columns.Add("OptionDigitalType", typeof(bool));
      _diagnosticsDataTable.Columns.Add("DigitalFixedPayout", typeof(double));
      _diagnosticsDataTable.Columns.Add("VolatilityTypeNormal", typeof(bool));
      _diagnosticsDataTable.Columns.Add("Notional", typeof(double));
      _diagnosticsDataTable.Columns.Add("Pv", typeof(double));
      _diagnosticsDataTable.Columns.Add("EffectiveNotional", typeof(double));
    }

    private double CapletPv(Dt settle, CapBase cap, CapletPayment caplet,
                            DiscountCurve discountCurve, Func<CapletPayment, Dt, Dt, double> projectRate,
                            VolatilityType volatilityType, int index)
    {
      double pv = 0, vol, rate, T;

      caplet.VolatilityStartDt = CmsCapletConvexityFromExposureDate
        ? settle : Pricer.AsOf;
      rate = projectRate(caplet, settle, discountCurve.AsOf);
      if (caplet.Expiry <= settle)
      {
        vol = 0;
        T = 0;
      }
      else
      {
        // Always take the precalculated volatility.
        vol = _vols[index];
        T = CapFloorPricerBase.CalculateTime(settle, caplet.Expiry, cap.DayCount);
      }
      rate *= caplet.IndexMultiplier;
      if (volatilityType == VolatilityType.Normal)
        vol *= Math.Abs(caplet.IndexMultiplier);

      // Time
      var dt = caplet.PeriodFraction;
      var discountFactor = discountCurve.DiscountFactor(caplet.PayDt);
      // Add
      if (caplet.OptionDigitalType != OptionDigitalType.None)
      {
        if (volatilityType == VolatilityType.Normal)
          pv += dt * discountFactor* DigitalOption.NormalBlackP(OptionStyle.European, cap.OptionType, cap.OptionDigitalType, T,
                                              rate, caplet.Strike, vol, caplet.DigitalFixedPayout);
        else
          pv += dt * discountFactor*DigitalOption.BlackP(OptionStyle.European, cap.OptionType, cap.OptionDigitalType, T,
                                        rate, caplet.Strike, vol, caplet.DigitalFixedPayout);
      }
      else
      {
        if (volatilityType == VolatilityType.Normal)
        {
          pv += discountFactor * dt * BlackNormal.P(cap.OptionType, T, 0, rate, caplet.Strike, vol);
        }
        else if (rate <= 0.0 || caplet.Strike <= 0)
        {
          pv += discountFactor * dt * Math.Max((cap.OptionType == OptionType.Call ? 1.0 : -1.0) * (rate - caplet.Strike), 0.0);
        }
        else
        {
          pv += discountFactor * dt * Black.P(cap.OptionType, T, rate, caplet.Strike, vol);
        }
      }

      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        var row = _diagnosticsDataTable.NewRow();
        row["PathId"] = ObjectLoggerUtil.GetPath("CCRPricerPath");
        row["PricerSettleDt"] = settle;
        row["CapletPayDt"] = caplet.PayDt;
        row["PeriodFraction"] = dt;
        row["Rate"] = rate;
        row["Vol"] = vol;
        row["T"] = T;
        row["Strike"] = caplet.Strike;
        row["DiscountFactor"] = discountFactor;
        row["OptionDigitalType"] = caplet.OptionDigitalType != OptionDigitalType.None;
        row["DigitalFixedPayout"] = caplet.OptionDigitalType != OptionDigitalType.None ? caplet.DigitalFixedPayout : 0;
        row["VolatilityTypeNormal"] = volatilityType == VolatilityType.Normal;
        row["Notional"] = caplet.Notional;
        row["Pv"] = pv;
        var capPricer = Pricer as CapFloorPricerBase;
        if (capPricer != null)
        {
          row["EffectiveNotional"] = capPricer.EffectiveNotional;
        }
        _diagnosticsDataTable.Rows.Add(row);
      }

      return caplet.Notional*pv;
    }
  }

  #endregion
}