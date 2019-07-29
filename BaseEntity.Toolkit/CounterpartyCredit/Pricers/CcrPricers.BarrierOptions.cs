using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{

  #region ISpotValue implementations

  internal interface ISpotValue
  {
    double GetValue(Dt spotDate);
  }

  [Serializable]
  internal class FxSpotRate : ISpotValue
  {
    private readonly FxRate _spotFx;

    public FxSpotRate(FxCurve fxCurve)
    {
      _spotFx = fxCurve.SpotFxRate;
    }

    public double GetValue(Dt spotDate)
    {
      return _spotFx.Rate;
    }
  }

  [Serializable]
  internal class AssetSpotPrice : ISpotValue
  {
    private readonly ForwardPriceCurve _priceCurve;

    public AssetSpotPrice(ForwardPriceCurve priceCurve)
    {
      _priceCurve = priceCurve;
    }

    public double GetValue(Dt spotDate)
    {
      return _priceCurve.Interpolate(spotDate);
    }
  }

  [Serializable]
  internal class SpotFromUnderlyer : ISpotValue
  {
    private readonly IUnderlier _underlier;

    public SpotFromUnderlyer(IUnderlier underlier)
    {
      _underlier = underlier;
    }

    public double GetValue(Dt spotDate)
    {
      double numeraire;
      return _underlier.Value(spotDate, out numeraire);
    }
  }

  #endregion

  #region BarrierOptionPricerBase

  /// <summary>
  /// Single barrier option pricer 
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  internal abstract class BarrierOptionCcrPricer : OptionCcrPricer
  {
    #region Data

    [NonSerialized][NoClone] protected IBasicExoticOption _option;
    [NonSerialized][NoClone] private ISpotValue _spot;
    private bool _isKnocked;

    [ObjectLogger(
      Name = "BarrierOptionPricer", 
      Description = "Barrier Option Pricing within Monte Carlo Simulation",
      Category = "Exposures",
      Dependencies = new string[] { "BaseEntity.Toolkit.Ccr.Simulations.PricerDiagnostics", "BaseEntity.Toolkit.CounterpartyCredit.Pricers.OptionCcrPricer" })]
    private static readonly IObjectLogger BinaryLogger =
      ObjectLoggerUtil.CreateObjectLogger(typeof(BarrierOptionCcrPricer));

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="optionPricer">option pricer</param>
    internal BarrierOptionCcrPricer(IPricer optionPricer)
      : base(optionPricer)
    {
    }

    protected override void Init()
    {
      base.Init();

      _option = (IBasicExoticOption) Pricer.Product;

      var fx = Pricer as FxOptionSingleBarrierPricer;
      if (fx != null)
      {
        _spot = new FxSpotRate(fx.FxCurve);
        return;
      }

      var stk = Pricer as StockOptionPricer;
      if (stk != null)
      {
        _spot = new AssetSpotPrice(stk.StockCurve);
        return;
      }

      var cmm = Pricer as CommodityOptionPricer;
      if (cmm != null)
      {
        _spot = new AssetSpotPrice(cmm.CommodityCurve);
        return;
      }

      _spot = new SpotFromUnderlyer(Underlier);
    }

    #endregion

    #region Methods

    protected abstract double BarrierOptionPrice(double time,
      double spot, double forward, double volatility,
      double numeraire);

    protected abstract bool CheckKnocked(double spotLevel);

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
      {
        Restart(settle);

        // Reset knocked status
        _isKnocked = false;
      }

      var dateComparison = Dt.Cmp(settle, Expiry);
      if (dateComparison <= 0)
      {
        // Record the current date first
        SavedDt = settle;

        if (IsKnocked && !IsExercisable)
        {
          // (Knocked, not exercisable) => already knocked out
          CurrentExerciseState = ExerciseState.NotExercised;
          return 0.0;
        }

        double T = _useRelativeTime
          ? Dt.RelativeTime(settle, Expiry)
          : Dt.Fraction(settle, Expiry, DayCount.Actual365Fixed);
        double vol = Underlier.Vol(settle);
        double numeraire;
        SavedLevel = Underlier.Value(settle, out numeraire);

        double optionPv;
        if (dateComparison < 0 && !IsKnocked)
        {
          var spot = _spot.GetValue(settle);
          _isKnocked = CheckKnocked(spot);
          optionPv = BarrierOptionPrice(T, spot, SavedLevel, vol,
            numeraire)*Notional;
        }
        else if (dateComparison == 0 && !IsExercisable)
        {
          // (Expired, not exercisable) => never knocked in
          CurrentExerciseState = ExerciseState.NotExercised;
          return 0.0;
        }
        else
        {
          // (Knocked or expired, and exercisable) => become a regular option
          optionPv = Black.P(OptionType, T, SavedLevel, EffectiveStrike, vol)
            *numeraire*Notional;
        }
        if (CurrentExerciseState == ExerciseState.None)
        {
          // The exercise state depends on whether the option is knocked-in or knocked-out.
          CurrentExerciseState = IsExercisable
            ? ExerciseDecision(SavedLevel) : ExerciseState.NotExercised;
        }
        if (BinaryLogger.IsObjectLoggingEnabled)
        {
          AddTableEntry(settle, T, numeraire, vol);
        }
        return optionPv;
      }

      // flush logs and reset diagnosticsDataTable
      if (BinaryLogger.IsObjectLoggingEnabled)
      {
        var key = string.Format("{0}.Path{1}", Pricer.Product.ToString(), ObjectLoggerUtil.GetPath("CCRPricerPath"));
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), key);
        binaryLogAggregator.Append(typeof(OptionCcrPricer), key, AppenderUtil.DataTableToDataSet(_diagnosticsDataTable)).Log();

        BuildDiagnosticsTable();
      }

      if (settle > UnderlierMaturity)
        return 0.0;

      if (CurrentExerciseState == ExerciseState.None)
      {
        double numeraire;
        double level = Underlier.Value(Expiry, out numeraire);
        //Brownian bridge interpolation to infer Exercise decision
        double vol = Underlier.Vol(settle);
        double dt = Dt.FractDiff(SavedDt, Expiry) / 365.0;
        double dT = Dt.FractDiff(SavedDt, settle) / 365.0;
        double h = dt / dT;
        double v = 0.5 * vol * vol * dT;
        double dw = Math.Log(level / SavedLevel);
        double spreadAtExpiry = SavedLevel =
          SavedLevel * Math.Exp(dw * h + h * (1.0 - h) * v);

        // Check knock status
        if (!IsKnocked) _isKnocked = CheckKnocked(spreadAtExpiry);

        // The exercise state depends on whether the option is knocked-in or knocked-out.
        CurrentExerciseState = IsExercisable
          ? ExerciseDecision(spreadAtExpiry) : ExerciseState.NotExercised;
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
            return 0.0;
          level = SavedLevel;
          numeraire = 1.0;
        }
        return Notional*Intrinsic(OptionType, level, EffectiveStrike, numeraire);
      }
      return 0.0;
    }

    #endregion

    #region Properties

    protected bool IsKnocked
    {
      get { return _isKnocked; }
    }

    protected abstract bool IsExercisable { get; }

    #endregion
  }

  #endregion

  #region SingleBarrierOption

  /// <summary>
  /// Single barrier option pricer 
  /// </summary>
  [Serializable]
  internal sealed class SingleBarrierOptionCcrPricer : BarrierOptionCcrPricer
  {
    #region Data

    [NonSerialized][NoClone] private Barrier _barrier;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="barrierPricer">option pricer</param>
    internal SingleBarrierOptionCcrPricer(IPricer barrierPricer)
      : base(barrierPricer)
    {
    }

    protected override void Init()
    {
      base.Init();
      _barrier = _option.Barriers[0];
    }

    #endregion

    #region Properties

    protected override bool IsExercisable
    {
      get
      {
        switch (_barrier.BarrierType)
        {
        case OptionBarrierType.DownOut:
        case OptionBarrierType.UpOut:
          return !IsKnocked;
        case OptionBarrierType.DownIn:
        case OptionBarrierType.UpIn:
          return IsKnocked;
        }
        return false;
      }
    }

    #endregion

    #region Methods

    protected override bool CheckKnocked(double spot)
    {
      switch (_barrier.BarrierType)
      {
      case OptionBarrierType.DownOut:
      case OptionBarrierType.DownIn:
        return spot <= _barrier.Value;
      case OptionBarrierType.UpIn:
      case OptionBarrierType.UpOut:
        return spot >= _barrier.Value;
      }
      return false;
    }

    protected override double BarrierOptionPrice(double time,
      double spot, double forward, double vol, double numeraire)
    {
      double r = -Math.Log(numeraire)/time;
      double d = r - Math.Log(forward/spot)/time;
      return Price(time, spot, EffectiveStrike, r, d, vol);
    }

    private double Price(
      double time, double spotPrice, double strike,
      double rfr, double div, double volatility)
    {
      var option = _option;
      var barrier = _barrier;
      var btype = barrier.BarrierType;
      var barrierValue = barrier.Value;

      // Barrier option
      double fv;
      if (option.IsTouchOption())
      {
        fv = TouchOptionPrice(option, OptionType, btype, option.SettlementType,
          time, spotPrice, barrierValue, rfr, div, volatility);
        var rebate = option.Rebate;
        if (rebate > 0 || rebate < 0)
        {
          var noLuckType = (btype == OptionBarrierType.OneTouch
            ? OptionBarrierType.NoTouch
            : OptionBarrierType.OneTouch);
          var p = TouchOptionPrice(option, OptionType, noLuckType,
            SettlementType.Cash, time, spotPrice, strike, rfr, div, volatility);
          fv += p*rebate;
        }
      }
      else if (option.IsDigital())
      {
        const double cashAmt = 1.0;
        var flags = OptionBarrierFlag.Regular;
        if (option.SettlementType == SettlementType.Physical)
          flags |= OptionBarrierFlag.PayAsset;
        fv = DigitalBarrierOption.Price(OptionType,
          btype, time, spotPrice, strike, barrierValue, cashAmt, rfr, div,
          volatility, flags);
        var rebate = option.Rebate;
        if (IsKnocked)
        {
          if (barrier.IsOut && option
            .BarrierPayoffTime == BarrierOptionPayoffTime.AtExpiry &&
            (rebate > 0 || rebate < 0))
          {
            fv += rebate*Math.Exp(-rfr*time);
          }
        }
        else if (rebate > 0 || rebate < 0)
        {
          var noLuckType = (barrier.IsIn
            ? OptionBarrierType.NoTouch : OptionBarrierType.OneTouch);
          var p = TouchOptionPrice(option, OptionType, noLuckType,
            SettlementType.Cash, time, spotPrice, strike, rfr, div, volatility);
          fv += p*rebate;
        }
      }
      else
      {
        int flags = 0;
        var rebate = option.Rebate;
        if (IsKnocked && (barrier.IsIn ||
          option.BarrierPayoffTime != BarrierOptionPayoffTime.AtExpiry))
        {
          // Either (1) no need to pay rebate when the option is knocked in;
          // Or (2) rebate is paid in the past if knocked out and pay-at-barrier-hit.
          rebate = 0;
        }
        else if ((rebate > 0 || rebate < 0) && option
          .BarrierPayoffTime == BarrierOptionPayoffTime.Default)
        {
          // Rebate is not zero nor NaN, and no payoff time is specified,
          // We set payoff time to be at barrier hit for out barrier, and
          // at expiry for in barrier.
          if (barrier.IsOut) flags |= (int) OptionBarrierFlag.PayAtBarrierHit;
        }
        else if (option.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
        {
          if (barrier.IsOut) flags |= (int) OptionBarrierFlag.PayAtBarrierHit;
        }
        fv = TimeDependentBarrierOption.Price(OptionType, btype, time, spotPrice,
          strike, barrierValue, rebate, rfr, div, volatility, flags);
      }
      return fv;
    }

    private static double TouchOptionPrice(
      IBasicExoticOption option, OptionType optionType,
      OptionBarrierType btype, SettlementType stype,
      double time, double spot, double strike, double r, double d,
      double volatility)
    {
      var flags = btype == OptionBarrierType.OneTouch
        ? OptionBarrierFlag.OneTouch
        : OptionBarrierFlag.NoTouch;
      if (option.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
      {
        flags |= OptionBarrierFlag.PayAtBarrierHit;
      }
      if (stype == SettlementType.Physical)
        flags |= OptionBarrierFlag.PayAsset;
      var p = TimeDependentBarrierOption.Price(optionType, btype, time, spot, strike,
        option.Barriers[0].Value, 0.0, r, d, volatility, (int) flags);
      // TimeDependentBarrierOption is the model for FX options.  To use it for equity options
      // and commodity options, it is necessary to make adjustments.
      return (flags & OptionBarrierFlag.PayAsset) == 0 ? p
        : p*((flags & OptionBarrierFlag.PayAtBarrierHit) != 0
          ? spot : (spot*Math.Exp((r - d)*time)));
    }

    #endregion
  }

  #endregion

  #region DoubleBarrierOptions

  /// <summary>
  /// Single barrier option pricer 
  /// </summary>
  [Serializable]
  internal class DoubleBarrierOptionCcrPricer : BarrierOptionCcrPricer
  {
    #region Data

    [NonSerialized][NoClone] private Barrier _lowerBarrier, _upperBarrier;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="barrierPricer">option pricer</param>
    internal DoubleBarrierOptionCcrPricer(IPricer barrierPricer)
      : base(barrierPricer)
    {
    }

    protected override void Init()
    {
      base.Init();

      _lowerBarrier = _option.Barriers[0];
      _upperBarrier = _option.Barriers[1];
    }

    #endregion

    #region Properties

    private bool IsKnockInOption
    {
      get
      {
        return (_lowerBarrier.BarrierType == OptionBarrierType.DownIn
          && _upperBarrier.BarrierType == OptionBarrierType.UpIn);
      }
    }

    private bool IsKnockOutOption
    {
      get
      {
        return (_lowerBarrier.BarrierType == OptionBarrierType.DownOut
          && _upperBarrier.BarrierType == OptionBarrierType.UpOut);
      }
    }

    protected override bool IsExercisable
    {
      get { return IsKnocked ? IsKnockInOption : IsKnockOutOption; }
    }

    #endregion

    #region Methods

    protected override bool CheckKnocked(double spot)
    {
      if (IsKnockInOption)
      {
        return (spot >= _lowerBarrier.Value || spot <= _upperBarrier.Value);
      }
      if (IsKnockOutOption)
      {
        return (spot < _lowerBarrier.Value || spot > _upperBarrier.Value);
      }
      return false;
    }

    protected override double BarrierOptionPrice(double time,
      double spot, double forward, double vol, double numeraire)
    {
      double r = -Math.Log(numeraire) / time;
      double d = r - Math.Log(forward / spot) / time;
      return Price(time, spot, EffectiveStrike, r, d, vol);
    }

    private double Price(
      double time, double spotPrice, double strike,
      double rfr, double div, double volatility)
    {
      var option = _option;

      // Barrier option
      double price;
      if (IsKnocked)
      {
        if (!IsExercisable)
        {
          // knocked out!
          return 0.0;
        }

        if (option.IsDigital())
        {
          price = DigitalOption.P(OptionStyle.European, OptionType,
            OptionDigitalType.Cash, time, spotPrice, strike,
            rfr, div, volatility, 1.0);
        }
        else
        {
          price = BlackScholes.P(OptionStyle.European, OptionType,
            time, spotPrice, strike, rfr, div, volatility);
        }
      }
      else
      {
        var lowerBarrier = _lowerBarrier;
        var upperBarrier = _upperBarrier;
        price = BaseEntity.Toolkit.Models.BGM.DoubleBarrierOptionPricer.Price(
          OptionType, spotPrice, strike,
          lowerBarrier.BarrierType, lowerBarrier.Value,
          upperBarrier.BarrierType, upperBarrier.Value,
          time, rfr, div, volatility, 0);
      }
      return price;
    }

    #endregion
  }

  #endregion
}
