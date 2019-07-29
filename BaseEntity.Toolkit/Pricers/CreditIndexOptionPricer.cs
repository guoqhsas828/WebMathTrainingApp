//
// CreditIndexOptionPricer.cs
//  -2015. All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  Implementation of the pricer for the options on credit default index.
  /// </summary>
  [Serializable]
  public class CreditIndexOptionPricer : PricerBase, ICreditIndexOptionPricer
    , IDefaultSensitivityCurvesGetter, IRecoverySensitivityCurvesGetter
    , ISpreadSensitivityCurvesGetter, IEvaluatorProvider
  {
    private static log4net.ILog logger = log4net.LogManager.GetLogger(typeof (CreditIndexOptionPricer));

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditIndexOptionPricer"/> class.
    /// </summary>
    /// <param name="cdxo">The option.</param>
    /// <param name="pricingDate">The pricing date.</param>
    /// <param name="settleDate">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurves">The survival curves.</param>
    /// <param name="currentFactor">The current factor.</param>
    /// <param name="exisingLossToInclude">The exising loss to include.</param>
    /// <param name="basketSize">Size of the basket (optional, zero or negative value means to be determine by credit curves or index weights).</param>
    /// <param name="marketQuote">The market quote.</param>
    /// <param name="cdxSettleDate">The index settle date.</param>
    /// <param name="recoveryRate">The market recovery rate.</param>
    /// <param name="modelType">The model to use.</param>
    /// <param name="data">Additional model data and flags, or null for none.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="modelBasis">The model basis</param>
    public CreditIndexOptionPricer(
      CDXOption cdxo,
      Dt pricingDate,
      Dt settleDate,
      DiscountCurve discountCurve,

      MarketQuote marketQuote,
      Dt cdxSettleDate,
      double recoveryRate,
      int basketSize,
      SurvivalCurve[] survivalCurves,
      double currentFactor,
      double exisingLossToInclude,

      CDXOptionModelType modelType,
      CDXOptionModelData data,
      VolatilitySurface volatility, 
      double modelBasis)
      : base(cdxo, pricingDate, settleDate > cdxo.Maturity ? cdxo.Maturity : settleDate)
    {
      _requestedSettleDate = settleDate;

      // Set up data required by the underlying index pricer
      _discountCurve = discountCurve;
      _survivalCurves = (survivalCurves == null || survivalCurves.Length == 0
        ? null
        : survivalCurves);
      _basketSize = basketSize > 0
        ? basketSize
        : (_survivalCurves != null
          ? _survivalCurves.Length
          : GetBasketSize(cdxo));
      if (!(recoveryRate >= 0 || recoveryRate <= 1)) recoveryRate = 0.4;
      _recoveryCurve = new RecoveryCurve(pricingDate, recoveryRate);
      _marketQuote = marketQuote;
      _cdxSettleDate = cdxSettleDate.IsEmpty()
        ? Dt.Add(pricingDate, 1)
        : cdxSettleDate;
      if (_cdxSettleDate < pricingDate)
      {
        throw new ArgumentException(String.Format(
          "Index settle date {0} cannot be before pricing date {1}",
          _cdxSettleDate, pricingDate));
      }

      // Set up model basis so that curve sensitivities may work.
      // Calculate both model basis and market pv only when the option is active.
      _modelBasis = Double.IsNaN(modelBasis)
        ? cdxo.CDX.CalculateIndexModelBasis(
          pricingDate, _cdxSettleDate, discountCurve, recoveryRate,
          // For inactive option, only the market pv is calculated.
          pricingDate > cdxo.Expiration ? null : survivalCurves,
          marketQuote, out _marketPv)
        : modelBasis;
      _data = data;
      _volatilitySurface = CdxVolatilityUnderlying.GetCdxModelSurface(
        volatility, cdxo.StrikeIsPrice, modelType);

      _modelType = modelType;
      if (modelType == CDXOptionModelType.FullSpread
        || cdxo.IsDigital || cdxo.IsBarrier)
      {
        _data.Choice |= CDXOptionModelParam.HandleIndexFactors;
      }

      _initialFactor = HandleIndexFactors ?
        GetEffectiveIndexFactor(cdxo, survivalCurves) : 1;
      _currentFactor = currentFactor;
      _includedPastLosses = exisingLossToInclude;
      UpdateCurrentFactorAndLosses();
    }

    /// <summary>
    /// Create a new copy of this pricer with the specified quote
    /// while everything else are copied memberwise to the new pricer.
    /// </summary>
    /// <param name="quote">The market quote.</param>
    /// <returns>ICreditIndexOptionPricer.</returns>
    public ICreditIndexOptionPricer Update(MarketQuote quote)
    {
      return new CreditIndexOptionPricer(CDXOption, AsOf, Settle,
        DiscountCurve, quote, IndexSettleDate, MarketRecoveryRate,
        _basketSize, _survivalCurves, _currentFactor, _includedPastLosses,
        _modelType, _data, _volatilitySurface, _modelBasis);
    }

    private static int GetBasketSize(CDXOption option)
    {
      var weights = option.CDX.Weights;
      return weights == null ? 0 : weights.Length;
    }

    private static double GetEffectiveIndexFactor(
      CDXOption cdxo, SurvivalCurve[] curves)
    {
      if (curves == null)
      {
        return Double.IsNaN(cdxo.IndexFactor) ? 1.0 : cdxo.IndexFactor;
      }
      var date = cdxo.Effective;
      double factor, tmp;
      curves.CheckDefaultedNames(null, cdxo.CDX.Weights, date, date, date,
        null, -1, true, out tmp, out factor);
      return factor;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns>System.Double.</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public override double ProductPv()
    {
      var time = Time;
      if (time < 0.0) return 0.0;
      return CDXOption.CalculateFairValue(AsOf, DiscountCurve,
        MarketRecoveryRate, CalculateForwards(GetQuote(true)),
        Volatility*Math.Sqrt(time), _modelType)*Notional;
    }

    private MarketQuote GetQuote(bool useIndexModelPv)
    {
      if (useIndexModelPv)
      {
        // When model basis is not NaN, we have survival curves
        // to calculate index model pv.
        // Otherwise, we check the public sensitivity survival
        // curves to incorporate any sensitivity calculation.
        double pv = Double.IsNaN(_modelBasis)
          ? this.AdjustSpreadOrDefault(_marketPv, _sensitivitySurvivalCurves)
          : (this.CalculateIndexModelPv() + _modelBasis);
        return new MarketQuote(1 + pv, QuotingConvention.FlatPrice);
      }
      if (_marketQuote.Type == QuotingConvention.None)
      {
        // In this case, we should have calculated the market pv
        // based on the supplied survival curves.
        Debug.Assert(!Double.IsNaN(_marketPv));
        return new MarketQuote(1 + _marketPv, QuotingConvention.FlatPrice);
      }
      return _marketQuote;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks><para>There are some pricers which need to remember some public state
    /// in order to skip redundant calculation steps. This method is provided
    /// to indicate that all public states should be cleared or updated.</para>
    ///   <para>Derived Pricers may implement this and should call base.Reset()</para></remarks>
    public override void Reset()
    {
      _volatility = Double.NaN;
      _forwards = null;
      UpdateCurrentFactorAndLosses();
      base.Reset();
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <remarks><para>Some pricers need to remember certain public states in order
    /// to skip redundant calculation steps.
    /// This function tells the pricer that what attributes of the products
    /// and other data have changed and therefore give the pricer an opportunity
    /// to selectively clear/update its public states.  When used with caution,
    /// this method can be much more efficient than the method Reset() without argument,
    /// since the later resets everything.</para>
    ///   <para>The default behavior of this method is to ignore the parameter
    ///   <paramref name="what" /> and simply call Reset().  The derived pricers
    /// may implement a more efficient version.</para></remarks>
    public override void Reset(ResetAction what)
    {
      if(what == ResetAsOf || what == ResetSettle || what == ResetQuote)
        UpdateModelBasis();
      Reset();
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (CDXOption.IsBarrier && _modelType != CDXOptionModelType.FullSpread)
      {
        logger.DebugFormat(
          "Barrier options always use the Full Spread model.  Model type {0} ignored",
          _modelType);
      }
    }

    /// <summary>
    /// Gets the pricer for the underlying credit index.
    /// </summary>
    /// <returns>ICDXPricer.</returns>
    public CDXPricer GetPricerForUnderlying()
    {
      var quote = GetQuote(false);
      var settle = IndexSettleDate;
      if (settle < AsOf) settle = AsOf;
      return new CDXPricer(CDXOption.CDX, AsOf,
        settle, DiscountCurve, SurvivalCurves)
      {
        MarketQuote = quote.Value,
        QuotingConvention = quote.Type,
        MarketRecoveryRate = MarketRecoveryRate,
        BasketSize = _basketSize
      };
    }

    /// <summary>
    ///  Calculate the fair price.
    /// </summary>
    /// <returns>System.Double.</returns>
    public double CalculateFairPrice(double volatility)
    {
      return CalculateFairPrice(AsOf, volatility);
    }

    /// <summary>
    ///  Calculate the fair price with the volatility starting on the specified date.
    /// </summary>
    /// <param name="volatilityBegin">Volatility start date,
    ///   before which the volatility is assumed 0.</param>
    /// <param name="volatilityValue">The instantaneous volatility value
    ///   for the period starting with the volatility start date.</param>
    /// <returns>The fair value per unit notional, discounted to the pricer as-of date</returns>
    /// <remarks>
    ///   In this function, all the forward protections and survival probability
    ///   are calculated starting from the pricer as-of date.
    /// </remarks>
    public double CalculateFairPrice(Dt volatilityBegin, double volatilityValue)
    {
      var time = Dt.RelativeTime(volatilityBegin, CDXOption.Expiration);
      if (time < 0.0) return 0.0;
      var v = Math.Sqrt(time) *
        (Double.IsNaN(volatilityValue) ? Volatility : volatilityValue);
      return CDXOption.CalculateFairValue(AsOf, DiscountCurve,
        MarketRecoveryRate, CalculateForwards(GetQuote(false)),
        v, _modelType);
    }

    /// <summary>
    /// Calculates the probability that the option ends in the meny on the expiration.
    /// </summary>
    /// <returns>System.Double.</returns>
    public double CalculateExerciseProbability(double volatility)
    {
      var time = Time;
      if (time < 0.0) return 0.0;
      var v = Math.Sqrt(time)*
        (Double.IsNaN(volatility) ? Volatility : volatility);
      return CDXOption.CalculateExerciseProbability(AsOf, DiscountCurve,
        MarketRecoveryRate, CalculateForwards(GetQuote(false)),
        v, _modelType);
    }

    /// <summary>
    /// Implies the volatility.
    /// </summary>
    /// <param name="fairValue">The fair value.</param>
    /// <returns>System.Double.</returns>
    public double ImplyVolatility(double fairValue)
    {
      var time = Time;
      if (time <= 1E-9) return 0.0;
      return CDXOption.ImplyVolatility(fairValue, AsOf, DiscountCurve,
        MarketRecoveryRate, CalculateForwards(GetQuote(false)),
        _modelType)/Math.Sqrt(time);
    }

    private void UpdateCurrentFactorAndLosses()
    {
      // Don't update if we don't have survival curves
      if (_survivalCurves == null || !HandleIndexFactors) return;

      // Calculate historical losses from option effective to settle, no discounting.
      _currentFactor = 1;
      _includedPastLosses = 0;
      Dt optionStart = CDXOption.Effective, settle = IndexSettleDate;
      if (settle < AsOf) settle = AsOf;
      if (optionStart > settle) return;
      _survivalCurves.CheckDefaultedNames(
        null, CDX.Weights, AsOf, optionStart, settle, null,
        _basketSize, true, out _includedPastLosses, out _currentFactor);
    }

    #endregion

    #region Forward values calculation

    private Forwards GetForwards()
    {
      return _forwards == null
        ? (Forwards)(_forwards = CalculateForwards(_marketQuote))
        : (Forwards)_forwards;
    }

    private Forwards CalculateForwards(MarketQuote quote)
    {
      if (AsOf > CDXOption.Expiration) return new Forwards();
      var df = DiscountCurve.DiscountFactor(AsOf, CDXOption.Expiration);
      var cdx = CDXOption.CDX;
      Dt expiry = CDXOption.Expiration, settle = IndexSettleDate;
      if (settle < AsOf) settle = AsOf;

      var survivalCurves = _survivalCurves;
      if (survivalCurves != null && FullReplicatingMethod)
      {
        // The following calculation ensures that the
        // forward pv01 is conditional on survival up to expiry,
        // event when the portfolio contains defaulted names.
        double pv01 = 0, sp = 0, fep = 0, upfront = 0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          var fwd = CalculateForwards(CreateCdsPricer(survivalCurves[i]),
            cdx, ForwardProtectionStartDate, expiry, df);
          var weight = cdx.Weights == null
            ? (1.0 / survivalCurves.Length)
            : cdx.Weights[i];
          upfront += fwd.Upfront * weight;
          sp += fwd.SurvivalProbability * weight;
          pv01 += fwd.SurvivalProbability * fwd.Pv01 * weight;
          fep += fwd.FrontEndProtection * weight;
        }
        if (sp > 0) pv01 /= sp; // this is intentional.
        return new Forwards(pv01, upfront, sp, df, fep,
          CalculateForwardStrikeValue());
      }

      // Below we calculate forwards based on market evaluation only.
      // Losses from the names defaulted between the pricer settle and the expiry
      double loss, fwdFactor;
      (_survivalCurves ?? _sensitivitySurvivalCurves).CheckDefaultedNames(
        null, CDX.Weights, AsOf, settle, expiry, DiscountCurve,
        _basketSize, HandleIndexFactors, out loss, out fwdFactor);
      if (survivalCurves == null) fwdFactor *= CurrentFactor;

      double quoteValue = quote.Value;
      if (loss > 0 && quote.Type == QuotingConvention.FlatPrice
        && !HandleIndexFactors)
      {
        // Exclude the default name from the market curve calibration.
        quoteValue = 1 + (quote.Value - 1 + loss) / fwdFactor;
      }
      var pricer = new CDXPricer(cdx, AsOf, settle, DiscountCurve, quoteValue)
      {
        QuotingConvention = quote.Type,
        MarketRecoveryRate = MarketRecoveryRate
      };
      var forwards = CalculateForwards(pricer.EquivalentCDSPricer,
        cdx, ForwardProtectionStartDate, expiry, df);

      if (!UseProtectionPvForFrontEnd)
      {
        // Recalculate the losses without discounting.
        (_survivalCurves ?? _sensitivitySurvivalCurves).CheckDefaultedNames(
          null, CDX.Weights, AsOf, settle, expiry, null,
          _basketSize, HandleIndexFactors, out loss, out fwdFactor);
        if (survivalCurves == null) fwdFactor *= CurrentFactor;
      }

      loss += ExistingLoss;
      if (loss > 0 || fwdFactor < 1)
      {
        if (UseProtectionPvForFrontEnd) loss /= df;
        double fep = forwards.FrontEndProtection,
          sp = forwards.SurvivalProbability;
        if (HandleIndexFactors)
        {
          forwards = new Forwards(forwards.Pv01, forwards.Upfront,
            sp, df, fep, CalculateForwardStrikeValue(),
            InitialFactor, loss, fwdFactor);
        }
        else
        {
          // Backward compatible mode
          forwards = new Forwards(forwards.Pv01, forwards.Upfront,
            fwdFactor*sp, df, loss + fwdFactor*fep,
            CalculateForwardStrikeValue());
        }
      }
      else
      {
        forwards = new Forwards(forwards.Pv01,
          forwards.Upfront, forwards.SurvivalProbability, df,
          forwards.FrontEndProtection, CalculateForwardStrikeValue());
      }
      return forwards;
    }

    private Dt ForwardProtectionStartDate
    {
      get { return CDXOption.Effective >= AsOf ? CDXOption.Effective : AsOf; }
    }

    private CDSCashflowPricer CreateCdsPricer(SurvivalCurve survivalCurve)
    {
      var p = new CDSCashflowPricer(CDX.CreateCompatibleCds(), AsOf,
        IndexSettleDate, DiscountCurve, survivalCurve, 0, TimeUnit.None);
      if (p.RecoveryCurve == null)
        p.RecoveryCurve = new RecoveryCurve(AsOf, MarketRecoveryRate);
      return p;
    }

    private Forwards CalculateForwards(
      CDSCashflowPricer cdsPricer, CDX cdx,
      Dt fwdProtectionStart, Dt expiry, double df)
    {
      // Calculate the probability of survival up to expiry.
      // First check if this name is defaulted.
      Dt defaultDate = cdsPricer.SurvivalCurve.DefaultDate;
      if (!defaultDate.IsEmpty())
      {
        if (defaultDate <= fwdProtectionStart)
        {
          return new Forwards(0.0, 0.0, 0.0, df, 0, 0);
        }
        if (defaultDate <= expiry)
        {
          // In most cases, this part rarely hit
          var recoveryCurve = cdsPricer.RecoveryCurve;
          var recoveryRate = recoveryCurve == null
            ? cdsPricer.RecoveryRate
            : recoveryCurve.RecoveryRate(defaultDate);
          var loss = UseProtectionPvForFrontEnd
            ? ((1 - recoveryRate) / cdsPricer.DiscountCurve
              .DiscountFactor(defaultDate, expiry))
            : (1 - recoveryRate);
          return new Forwards(0.0, 0.0, 0.0, df, loss, 0);
        }
      }
      var sp = cdsPricer.SurvivalCurve.SurvivalProb(fwdProtectionStart, expiry);

      // For market pricing consistent, we calculate the values for two periods:
      // (struck, maturity] and (expiry, maturity).

      // Calculate the protection from the option struck date to index maturity.
      cdsPricer.CDS.Premium = 1.0;
      cdsPricer.CDS.Fee = 0.0;
      cdsPricer.Settle = fwdProtectionStart;
      cdsPricer.Reset();
      var protect0 = -cdsPricer.ProtectionPv();

      // Calculate the forward PV01 and protection from the option expiry to index maturity,
      // discounted to the option expiry date.
      cdsPricer.Settle = expiry;
      cdsPricer.Reset();
      var protect1 = -cdsPricer.ProtectionPv() / df;
      var pv01 = cdsPricer.FlatFeePv() / df;
      var fwdspread = protect1 / pv01;

      // Calculate the front end protection,
      // discounted to the option expiry date.
      double fep;
      if (UseProtectionPvForFrontEnd)
      {
        protect1 *= sp;
        fep = protect0 / df - protect1;
      }
      else
      {
        fep = (1 - sp) * (1 - cdsPricer.RecoveryRate);
      }

      // Calculate the forward upfront.
      var upfront = pv01 * (fwdspread - cdx.Premium);

      return new Forwards(pv01, upfront, sp, df, fep, 0);
    }

    private double CalculateForwardStrikeValue()
    {
      var cdxo = CDXOption;
      return cdxo.CalculateForwardUpfrontValue(cdxo.Strike,
        cdxo.StrikeIsPrice, DiscountCurve, MarketRecoveryRate);
    }

    /// <summary>
    ///   Nested type represent some forward values at the expiry.
    /// </summary>
    [Serializable]
    public struct Forwards
    {
      //Upfront---market value; StrikeValue---strike value(exercise price)
      //Loss----known loss; Factor---forward factor.
      public readonly double Pv01,
        Upfront,
        SurvivalProbability,
        DiscountFactor,
        FrontEndProtection,
        StrikeValue,
        InitialFactor,
        Loss,
        Factor;

      public Forwards(double pv01, double upfront, double survProb,
        double discount, double fep, double strikeValue,
        double initialFactor = 1, double knownLoss = 0, double fwdFactor = 1)
      {
        Pv01 = pv01;
        Upfront = upfront;
        SurvivalProbability = survProb;
        DiscountFactor = discount;
        FrontEndProtection = fep;
        StrikeValue = strikeValue;
        InitialFactor = initialFactor;
        Loss = knownLoss;
        Factor = fwdFactor;
      }

      public double Value
      {
        get { return FrontEndProtection + SurvivalProbability*Upfront; }
      }

      public double NetValue
      {
        get { return Loss + Factor*Value - InitialFactor*StrikeValue; }
      }

      public double AdJustedStrikeValue
      {
        get { return (InitialFactor*StrikeValue - Loss)/Factor; }
      }

      public double AdjustedForwardPv01
      {
        get { return Factor*Pv01; }
      }
    }

    /// <summary>
    ///   If true, ignore the underly, name by name credit curves
    /// </summary>
    public bool FullReplicatingMethod
    {
      get { return (_data.Choice & CDXOptionModelParam.FullReplicatingMethod) != 0; }
    }

    /// <summary>
    ///   Include maturity date in accrual calculation for CDS/CDO pricers
    /// </summary>
    /// <exclude />
    private bool UseProtectionPvForFrontEnd
    {
      get { return (_data.Choice & CDXOptionModelParam.UseProtectionPvForFrontEnd) != 0; }
    }

    /// <summary>
    ///   Include maturity date in accrual calculation for CDS/CDO pricers
    /// </summary>
    /// <exclude />
    private bool HandleIndexFactors
    {
      get { return (_data.Choice & CDXOptionModelParam.HandleIndexFactors) != 0; }
    }
    #endregion

    #region Data

    private readonly DiscountCurve _discountCurve;

    private MarketQuote _marketQuote;
    private double _modelBasis, _marketPv;
    private SurvivalCurve[] _survivalCurves;
    private readonly RecoveryCurve _recoveryCurve;
    private readonly int _basketSize;
    private readonly Dt _cdxSettleDate;
    private readonly double _initialFactor = 1;

    private readonly VolatilitySurface _volatilitySurface;

    private readonly CDXOptionModelData _data;
    private readonly CDXOptionModelType _modelType;

    // Intermediate or user inputs
    [Mutable] private double _currentFactor = 1, _includedPastLosses = 0;

    // Intermediate results
    [Mutable] private double _volatility = Double.NaN;
    [Mutable] private Forwards? _forwards;
    [Mutable, NonSerialized, NoClone] private SurvivalCurve[] _sensitivitySurvivalCurves;
    [Mutable, NonSerialized, NoClone] private IPricer _paymentPricer;

    #endregion

    #region Properties

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            _paymentPricer = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return _paymentPricer;
      }
    }

    ///<summary>
    /// Convenience (static) method for building a fee pricer for
    /// an additional payment.
    ///</summary>
    ///<param name="payment"></param>
    ///<param name="discountCurve"></param>
    ///<returns></returns>
    public override IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment != null)
      {
        if (payment.PayDt > _requestedSettleDate) // strictly greater than
        {
          var oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
          var pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, _requestedSettleDate, discountCurve, null);
          pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
          return pricer;
        }
      }
      return null;
    }
    /// <summary>
    ///   CDX Option product
    /// </summary>
    public CDXOption CDXOption
    {
      get { return (CDXOption)Product; }
    }

    /// <summary>
    ///   Underlying CDS index
    /// </summary>
    public CDX CDX
    {
      get { return CDXOption.CDX; }
    }

    /// <summary>
    /// Gets the index settle date.
    /// </summary>
    /// <value>The index settle date.</value>
    public Dt IndexSettleDate
    {
      get { return _cdxSettleDate; }
    }

    /// <summary>
    /// Gets the index factor at the option struck date.
    /// </summary>
    /// <value>The index factor.</value>
    public double InitialFactor
    {
      get { return _initialFactor; }
    }

    /// <summary>
    /// Gets/sets the current index factor at the pricing date.
    /// </summary>
    /// <value>The index factor.</value>
    public double CurrentFactor
    {
      get { return _currentFactor; }
      set { _currentFactor = value; }
    }

    /// <summary>
    /// Gets/sets the existing index losses at pricing date which sgould be included
    /// in front end protection.  Normally these are default losses occurred
    /// from the option struck up to the pricing date.
    /// </summary>
    /// <value>The existing index losses to be included in front end protection</value>
    public double ExistingLoss
    {
      get { return _includedPastLosses; }
      set { _includedPastLosses = value; }
    }

    /// <summary>
    /// The effective notional is the original notional adjusted by index factor.
    /// </summary>
    public override double EffectiveNotional
    {
      get { return Notional*InitialFactor; }
    }

    /// <summary>
    /// The effective notional is the original notional adjusted by index factor.
    /// </summary>
    public override double CurrentNotional
    {
      get { return Notional*CurrentFactor; }
    }

    /// <summary>
    ///   Option style
    /// </summary>
    public OptionStyle Style
    {
      get { return CDXOption.Style; }
      // The setter is for reflection only, currently used
      // in batch processing like qPriceCopy.
      private set { CDXOption.Style = value; }
    }

    /// <summary>
    ///   Option type
    /// </summary>
    public OptionType Type
    {
      get { return CDXOption.Type; }
      // The setter is for reflection only, currently used
      // in batch processing like qPriceCopy.
      private set { CDXOption.Type = value; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is payer.
    /// </summary>
    /// <value><c>true</c> if this instance is payer; otherwise, <c>false</c>.</value>
    private bool IsPayer
    {
      get { return Type == OptionType.Put; }
    }

    /// <summary>
    ///   Option strike in either spread (1bp = 0.0001) or price(1 = par)
    /// </summary>
    public double Strike
    {
      get { return CDXOption.Strike; }
      // The setter is for reflection only, currently used
      // in batch processing like qPriceCopy.
      private set { CDXOption.Strike = value; }
    }

    /// <summary>
    ///   Option strike is value rather than spread
    /// </summary>
    public bool StrikeIsPrice
    {
      get { return CDXOption.StrikeIsPrice; }
    }

    /// <summary>
    ///   Annualized Volatility
    /// </summary>
    public double Volatility
    {
      get
      {
        if (Double.IsNaN(_volatility))
        {
          if (_volatilitySurface == null)
          {
            return 0.0;
          }
          var cdxo = CDXOption;
          _volatility = _volatilitySurface.Interpolate(
            cdxo.Expiration, cdxo.Strike);
        }
        return _volatility;
      }
    }

    /// <summary>
    ///   Time to expiration
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, CDXOption.Expiration); }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return _discountCurve; }
    }

    /// <summary>
    ///   Survival curves
    /// </summary>
    public double MarketRecoveryRate
    {
      get { return _recoveryCurve.RecoveryRate(CDX.Maturity); }
    }

    /// <summary>
    ///   Survival curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return _survivalCurves; }
      private set
      {
        _survivalCurves = value;
        Reset();
      }
    }

    /// <summary>
    /// Model basis
    /// </summary>
    public double ModelBasis
    {
      get { return _modelBasis; }
    }

    public bool IsPriceVolatilityModel
    {
      get { return _modelType == CDXOptionModelType.BlackPrice; }
    }

    public int BasketSize
    {
      get { return _basketSize; }
    }

    #endregion

    #region Sensitivity and batch processing properties
    // These are set-only properties, used by the functions
    // like qPricerCopyGroup to set properties for repricing
    // with alternative inputs.  This should be revisited ASAP.
    public double Spread
    {
      get
      {
        if (_marketQuote.Type != QuotingConvention.CreditSpread)
          throw new ArgumentException("Index is not quoted in spread");
        return _marketQuote.Value;
      }
      set
      {
        if (_marketQuote.Type != QuotingConvention.CreditSpread)
          throw new ArgumentException("Index is not quoted in spread");
        _marketQuote = new MarketQuote(value, QuotingConvention.CreditSpread);
        Reset(ResetQuote);
      }
    }

    //Price
    public double Price
    {
      get
      {
        if (_marketQuote.Type != QuotingConvention.FlatPrice)
          throw new ArgumentException("Index is not quoted in price");
        return _marketQuote.Value;
      }
      set
      {
        if (_marketQuote.Type != QuotingConvention.FlatPrice)
          throw new ArgumentException("Index is not quoted in price");
        _marketQuote = new MarketQuote(value, QuotingConvention.FlatPrice);
        Reset(ResetQuote);
      }
    }

    private void UpdateModelBasis()
    {
      if (Double.IsNaN(_modelBasis)) return;
      Dt asOf = AsOf;
      _modelBasis = CDXOption.CDX.CalculateIndexModelBasis(
        asOf, _cdxSettleDate > asOf ? _cdxSettleDate : asOf,
        DiscountCurve, MarketRecoveryRate,
        // For inactive option, only the market pv is calculated.
        asOf > CDXOption.Expiration ? null : SurvivalCurves,
        _marketQuote, out _marketPv);
    }

    private static readonly ResetAction ResetQuote = new ResetAction();
    private Dt _requestedSettleDate;

    #endregion

    #region ICreditIndexOptionPricer Members

    public VolatilitySurface VolatilitySurface
    {
      get { return _volatilitySurface; }
    }

    public double AtTheMoneyForwardValue
    {
      get { return GetForwards().Value; }
    }

    public double ForwardStrikeValue
    {
      get { return GetForwards().AdJustedStrikeValue; }
    }

    public double ForwardUpfrontValue
    {
      get { return GetForwards().Upfront; }
    }

    public double FrontEndProtection
    {
      get { return GetForwards().FrontEndProtection; }
    }

    public double ExpectedSurvival
    {
      get { return GetForwards().SurvivalProbability; }
    }

    public double ForwardPv01
    {
      get { return GetForwards().AdjustedForwardPv01; }
    }

    public double OptionIntrinsicValue
    {
      get
      {
        var fwd = GetForwards();
        return Math.Max(0.0, fwd.DiscountFactor*(IsPayer ? 1 : -1)
          *fwd.NetValue);
      }
    }

    #endregion

    #region IPricer<CDXOption> Members

    CDXOption IPricer<CDXOption>.Product
    {
      get { return (CDXOption)Product; }
    }

    #endregion

    #region ISpreadSensitivityCurvesGetter and IDefaultSensitivityCurvesGetter Members

    IList<SurvivalCurve> IDefaultSensitivityCurvesGetter.GetCurves()
    {
      return GetSurvivalCurves();
    }

    IList<SurvivalCurve> ISpreadSensitivityCurvesGetter.GetCurves()
    {
      return GetSurvivalCurves();
    }

    private IList<SurvivalCurve> GetSurvivalCurves()
    {
      return _survivalCurves ?? _sensitivitySurvivalCurves
        ?? (_sensitivitySurvivalCurves = new[] {CreditMarketSurvivalCurve()});
    }

    private SurvivalCurve CreditMarketSurvivalCurve()
    {
      var curve = GetPricerForUnderlying().MarketSurvivalCurve;
      curve.Name = "MarketCurve";
      curve.Flags |= CurveFlags.Internal;
      return curve;
    }

    #endregion

    #region IRecoverySensitivityCurvesGetter Members

    IList<Curve> IRecoverySensitivityCurvesGetter.GetCurves()
    {
      if (_survivalCurves != null)
      {
        return _survivalCurves;
      }
      return new[] {_recoveryCurve};
    }

    #endregion

    #region IEvaluatorProvider Members

    Func<double> IEvaluatorProvider.GetEvaluator(string measure)
    {
      var fn= CreditIndexOptionPricerFactory.GetEvaluator(measure);
      if (fn == null) return null;
      return () => fn(this);
    }

    #endregion
  }
}
