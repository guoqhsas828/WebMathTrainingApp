//
// CDXOptionPricerMoriniBrigo.cs
//  -2008. All rights reserved.
//
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Arbitrage free pricing of Credit Index options.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDXOption" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <para>This pricer is based on arbitrage free model for CDS pricing.</para>
  /// <para>A factory class <see cref="CreditIndexOptionPricerFactory"/> provides a simple interface
  /// to the alternate credit index option models.</para>
  /// <para>By definition, the value at expiry of a <em>payer credit index option</em> is
  /// <math>V_T = \left[A_T\,(S_T-K) + F_T\right]^+</math>
  /// where <m>T</m> is option expiry, <m>A_T</m> is the forward PV01 and <m>S_T</m> the forward
  /// spread at <m>T</m>, <m>F_T</m> the front end protection up to <m>T</m>, and K is the strike on
  /// spread.</para>
  /// <para>The current market standard model defines an adjusted spread
  /// <math>\tilde{S}_T = S_T + F_T/A_T</math>
  /// and applies Black formula to
  /// <math>V_T = \left[A_T\,(\tilde{S}_T-K)\right]^+</math>
  /// using <m>A_T</m> as numeraire and assuming <m>\tilde{S}_T</m> follows log-normal distribution
  /// after the change of measure.</para>
  /// <para>This approach is not valid globally, since in the event that all names default before
  /// <m>T</m>, <m>A_T</m> is zero, but the value of <m>V_T</m> is not always zero. Hence
  /// <m>\tilde{S}_T</m> is not well defined for some non-trivial states in which <m>V_T</m> is not
  /// zero.</para>
  /// <para>Morini and Brigo propose to rewrite the value of the payer option as
  /// <math>V_T = \left[A_T\,(S_T-K) - E_T\, F_T\right]^+ + (1-E_T)\,F_T</math>
  /// where <m>E_T</m> is so-called <em>no-Armageddon event</em> that at least one credit survives
  /// at time <m>T</m>. They notice that the term in the bracket becomes zero when all names become
  /// defaulted at <m>T</m>, in which case both <m>A_T</m> and <m>E_T</m> are zero. If we define a
  /// no-Armageddon adjusted spread as
  /// <math>\hat{S}_T = S_T + E_T\,F_T/A_T</math>
  /// Then <m>\hat{S}_T</m> is well defined in all the states where the bracket
  /// term is nonzero.  We have
  /// <math>V_T = \left[A_T\,(\hat{S}_T-K)\right]^+ + (1-E_T)\,F_T</math>
  /// It is valid to apply Black formula to the first term, while the expectation of the second term
  /// can be calculated directly. This provides a method of arbitrage-free pricing of the credit
  /// index option.</para>
  /// <para>For a <em>receiver credit index option</em>, the value is given by</para>
  /// <math>
  ///    V_T = \left[A_T\,(K-S_T) - F_T\right]^+ 
  ///        = \left[A_T\,(K-S_T) - E_T \, F_T\right]^+
  /// </math>
  /// <para>There is no second term because the option will not exercise when
  /// the entire portfolio are wiped out at time <m>T</m>.  Hence the value of the
  /// receiver option is
  /// <math>V_T = A_T\,(K-\hat{S}_T)</math>
  /// to which we can apply the Black formula directly.</para>
  /// <para>This approach is based on Massimo Morini and Damiano Brigo,
  /// "Arbitrage-free pricing of Credit Index Options.  The no-Armageddon pricing
  /// measure and the role of correlation after subprime crisis," 2007.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDXOption">Credit Index Option</seealso>
  /// <seealso cref="CreditIndexOptionPricerFactory">CDS Index Option Pricer factory</seealso>
  [Serializable]
  public class CDXOptionPricerMoriniBrigo : CDXOptionPricer
  {
    // Logger
    private static readonly log4net.ILog logger = 
      log4net.LogManager.GetLogger(typeof(CDXOptionPricerMoriniBrigo));

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CDXOptionPricerMoriniBrigo"/> class.
    /// </summary>
    /// <param name="option">Credit index option.</param>
    /// <param name="asOf">Pricing date.</param>
    /// <param name="settle">Settle date.</param>
    /// <param name="discountCurve">Discount curve.</param>
    /// <param name="survivalCurves">Portfolio survival curves (can be null for market only approach).</param>
    /// <param name="basketSize">Size of the basket.</param>
    /// <param name="marketQuote">Market quote.</param>
    /// <param name="volatility">Spread volatility.</param>
    /// <param name="correlation">Default correlation among credits.</param>
    /// <param name="copula">Copula model.</param>
    /// <param name="accuracy">Accuracy level for probability calculation.</param>
    public CDXOptionPricerMoriniBrigo(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      int basketSize,
      double marketQuote,
      VolatilitySurface volatility,
      Correlation correlation,
      Copula copula,
      double accuracy)
      : base(option, asOf, settle, discountCurve,
        survivalCurves, basketSize, marketQuote, volatility)
    {
      if (survivalCurves != null && basketSize != survivalCurves.Length)
        throw new ArgumentException(String.Format(
          "basket size {0} not consistent with credit curves (len={1})",
          basketSize, survivalCurves.Length));
      BasketSize = basketSize;
      if (correlation is CorrelationTermStruct)
      {
        correlation_ = (CorrelationTermStruct)correlation;
      }
      else
      {
        correlation_ = CorrelationTermStruct.FromCorrelations(
          correlation.Names, new Dt[1] { option.Expiration },
          new Correlation[1] { correlation });
      }
      copula_ = copula;
      accuracy_ = accuracy;
    }

    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerMoriniBrigo(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      int basketSize,
      double marketQuote,
      double volatility,
      Correlation correlation,
      Copula copula,
      double accuracy)
      : this(option, asOf, settle, discountCurve, survivalCurves, basketSize,
        marketQuote, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility),
      correlation, copula, accuracy)
    {
    }
    #endregion Constructor

    #region Methods
    /// <summary>
    /// Calculate the market value of the Option at the specified volatility.
    /// </summary>
    /// <param name="vol">Volatility.</param>
    /// <returns>
    /// the market value of the option in dollars
    /// </returns>
    /// <example>
    /// 	<code language="C#">
    /// // ...
    /// double vol    = 1.2;                           // volatility 120%
    /// // Create a pricing for the CDX Option.
    /// CDXOptionPricer pricer =
    /// new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    /// pricer.ModelType = model;
    /// pricer.AdjustSpread = adjustSpread;
    /// // Calculate market value using volatility 60%
    /// double marketValue = pricer.MarketValue( 0.6 );
    /// </code>
    /// </example>
    public override double MarketValue(double vol)
    {
      // On expiry date value is just the cash settled if exercised
      if (Dt.Cmp(CDXOption.Expiration, this.Settle) == 0)
        return Intrinsic();
      // after that option is done
      if (Dt.Cmp(CDXOption.Expiration, this.Settle) < 0)
        return 0;

      // The normal case
      double savedVol = this.Volatility;
      double value = 0;
      try
      {
        this.Volatility = vol;
        double spread = this.Spread;

        value = (this.Type == OptionType.Put ?
          PayerValue(spread) : ReceiverValue(spread));
        value *= this.Notional;
      }
      finally
      {
        this.Volatility = savedVol;
      }

      return value;
    }

    /// <summary>
    /// Calculates implied volatility for index option
    /// </summary>
    /// <param name="fv">Fair value of CDX Option in dollars</param>
    /// <returns>Implied volatility of CDX Option</returns>
    /// <example>
    /// 	<code language="C#">
    /// // ...
    /// // Create a pricing for the CDX Option.
    /// CDXOptionPricer pricer =
    /// new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    /// survivalCurves,  spread, vol );
    /// pricer.ModelType = model;
    /// pricer.AdjustSpread = adjustSpread;
    /// // Calculate implied volatility
    /// double vol = pricer.IVol(fv);
    /// </code>
    /// </example>
    public override double IVol(double fv)
    {
      logger.Debug("Calculating implied volatility for CDS Index Option...");

      double spread = this.Spread;
      return (this.Type == OptionType.Put ?
        PayerIVol(spread, fv) : ReceiverIVol(spread, fv));
    }

    /// <summary>
    /// Index upfront value at a given market spread
    /// </summary>
    /// <param name="spread">Market spread in raw number (1bp = 0.0001)</param>
    /// <param name="premium">The premium in raw numbers.</param>
    /// <returns>Index upfront value</returns>
    /// <remarks>
    /// This is the present value of the expected forward value
    /// plus the front end protection.  The expectation is taken
    /// based on the particular distribution of spread or price
    /// with the given spread as the current market spread quote.
    /// The value is per unit notional and
    /// is discounted by both the survival probability from settle to expiry
    /// and the discount factor from as-of to expiry.
    /// </remarks>
    protected override double IndexUpfrontValue(double spread, double premium)
    {
      double pv = ForwardPV01(spread) * (spread - premium);
      if (this.AdjustSpread)
        pv += FrontEndProtection(spread);
      return pv;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    public override void Reset()
    {
      armageddonEvent_ = null;
      base.Reset();
    }

    /// <summary>
    /// Calculate the probability that the option is in the money
    /// on the expiry.
    /// </summary>
    /// <param name="spread">The forward spread (unadjusted).</param>
    /// <returns>Probability.</returns>
    protected override double CalculateProbabilityInTheMoney(double spread)
    {
      double pv01 = ForwardPV01(spread);
      double amloss;
      spread += SpreadAdjustment(spread, pv01, out amloss);

      double sigma = this.Volatility * Math.Sqrt(this.Time);
      double k = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
      k = (this.Type == OptionType.Put
             ? (Math.Log(spread/k) - sigma*sigma/2)
             : (Math.Log(k/spread) + sigma*sigma/2));
      return Toolkit.Numerics.Normal.cumulative(k, 0.0, sigma)
             *(1 - Armageddon.Probability);
    }
    #endregion Methods

    #region Black Formula
    /// <summary>
    ///   Calculate put option value based on Black formula
    /// </summary>
    /// <exclude />
    private double PayerValue(double spread)
    {
      double pv01 = ForwardPV01(spread);
      double amloss;
      spread += SpreadAdjustment(spread, pv01, out amloss);

      double expect = spread;
      double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
      if (K > 1e-10)
      {
        double T = this.Time;
        expect = Toolkit.Models.Black.B(spread / K,
          this.Volatility * Math.Sqrt(T));
        expect *= K;
      }
      expect *= pv01;
      return expect + amloss;
    }

    /// <summary>
    ///   Calculate call option value based on Black formula
    /// </summary>
    /// <exclude />
    private double ReceiverValue(double spread)
    {
      double pv01 = ForwardPV01(spread);
      double amloss;
      spread += SpreadAdjustment(spread, pv01, out amloss);

      double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
      double expect = K;
      if (spread > 1e-10)
      {
        double T = this.Time;
        expect = Toolkit.Models.Black.B(K / spread,
          this.Volatility * Math.Sqrt(T));
        expect *= spread;
      }
      expect *= pv01;
      return expect;
    }

    /// <summary>
    ///   Calculate put option implied volatility
    /// </summary>
    /// <exclude />
    private double PayerIVol(double spread, double fv)
    {
      double pv01 = ForwardPV01(spread);
      double amloss;
      spread += SpreadAdjustment(spread, pv01, out amloss);
      fv -= amloss;

      double vol = 0;
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        if (K > 1e-10)
        {
          fv /= pv01;
          vol = Toolkit.Models.Black.BI(spread / K, fv / K, 1e-8);
          vol /= Math.Sqrt(this.Time);
        }
      return vol;
    }

    /// <summary>
    ///   Calculate call option implied volatility
    /// </summary>
    /// <exclude />
    private double ReceiverIVol(double spread, double fv)
    {
      double pv01 = ForwardPV01(spread);
      double amloss;
      spread += SpreadAdjustment(spread, pv01, out amloss);

      double vol = 0;
      if (spread > 1e-10)
      {
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        fv /= pv01;
        vol = Toolkit.Models.Black.BI(
          K / spread, fv / spread, 1e-8);
        vol /= Math.Sqrt(this.Time);
      }
      return vol;
    }

    /// <summary>
    ///  Calculate the spreads adjustment and expected Armageddon loss.
    /// </summary>
    /// <param name="spread">Spread.</param>
    /// <param name="pv01">PV01.</param>
    /// <param name="amloss">Expected Armageddon loss.</param>
    /// <returns>Spread adjustment</returns>
    private double SpreadAdjustment(double spread, 
      double pv01, out double amloss)
    {
      if (this.AdjustSpread)
      {
        ArmageddonEvent ae = Armageddon;
        amloss = ae.LossRate * ae.Probability
          * DiscountCurve.DiscountFactor(AsOf, CDXOption.Expiration);
        double fp = FrontEndProtection(spread);
        return (fp - amloss) / pv01;
      }

      // no adjustment
      amloss = 0;
      return 0;
    }
    #endregion Black Formula

    #region Probability of Wiped-Out Event

    #region Properties
    /// <summary>
    ///   Gets the probability that all the credits default before option expiry.
    /// </summary>
    /// <value>The wiped out probability.</value>
    public double WipedOutProbability
    {
      get { return this.Armageddon.Probability; }
    }
    /// <summary>
    ///  Get the ArmageddonEvent
    /// </summary>
    internal protected ArmageddonEvent Armageddon
    {
      get
      {
        if (armageddonEvent_ == null)
          armageddonEvent_ = new ArmageddonEvent(this);
        return armageddonEvent_;
      }
    }
    #endregion Properties

    #region Data

    private CorrelationTermStruct correlation_;
    private Copula copula_;
    private double accuracy_;

    // intermediate result
    private ArmageddonEvent armageddonEvent_;

    #endregion Data

    #region Calculations
    /// <summary>
    ///  ArmageddonEvent class
    /// </summary>
    [Serializable]
    internal protected class ArmageddonEvent
    {
      /// <summary>
      ///  Constructor
      /// </summary>
      /// <param name="pricer">CDXOptionPricerMoriniBrigo</param>
      public ArmageddonEvent(CDXOptionPricerMoriniBrigo pricer)
      {
        // sates
        Dt start = pricer.Settle;
        Dt maturity = pricer.CDXOption.Expiration;

        // set up basket inputs and calculate the loss rate
        SurvivalCurve[] sc = pricer.PortfolioSurvivalCurves;
        if (sc == null || pricer.MarketAdjustOnly && !pricer.FullReplicatingMethod)
        {
          sc = ArrayUtil.NewArray((int)pricer.BasketSize,
            pricer.MarketSurvivalCurve);
          LossRate = 1 - pricer.MarketRecoveryRate;
        }
        else if (sc.Length != 0)
        {
          double[] weights = pricer.CDX.Weights;
          double uniweight = 1.0 / sc.Length;
          LossRate = ArrayUtil.Sum(0, sc.Length, (int i) =>
          {
            SurvivalCalibrator cal = sc[i].SurvivalCalibrator;
            if (cal == null || cal.RecoveryCurve == null)
              throw new InvalidOperationException(String.Format(
                "No recovery curve inside credit curve {0}", sc[i].Name));
            return (1 - cal.RecoveryCurve.RecoveryRate(maturity))
              * (weights == null ? uniweight : weights[i]);
          });
        }
        else
        {
          throw new ArgumentException("Portfolio credit curves cannot be empty.");
        }
        double[] ones = ArrayUtil.NewArray(sc.Length, 1.0);
        double[] zeros = new double[sc.Length];
        
        // initialize distribution object
        double accuracy = pricer.accuracy_;
        Curve2D lossDistribution = new Curve2D(start);
        lossDistribution.Initialize(2, 2);
        lossDistribution.SetDate(0, start);
        lossDistribution.SetDate(1, maturity);
        lossDistribution.SetLevel(0, 0.0);
        lossDistribution.SetLevel(1, 1 - 0.1 / sc.Length);
        if (accuracy < 1)
          lossDistribution.SetAccuracy(accuracy);

        CorrelationTermStruct corr = pricer.correlation_;
        Copula copula = pricer.copula_;
        SemiAnalyticBasketModel2.ComputeDistributions(
          true, 0, lossDistribution.NumDates(),
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic,
          copula.Data, corr.Correlations, corr.GetDatesAsInt(start),
          (int)accuracy, emptyDoubleArray,
          sc, ones, zeros, zeros, emptySCArray,
          (int)Baskets.RecoveryCorrelationType.None, 0.0,
          lossDistribution, emptyDistribution);
        double noArmageddonProbability = lossDistribution.GetValue(1, 1);
        Probability = 1 - noArmageddonProbability;
      }

      // computation results
      /// <summary>
      /// Probability
      /// </summary>
      public readonly double Probability;
      /// <summary>
      /// LossRate
      /// </summary>
      public readonly double LossRate;

      // Static fields
      private static Curve2D emptyDistribution = new Curve2D();
      private static double[] emptyDoubleArray = new double[0];
      private static SurvivalCurve[] emptySCArray = new SurvivalCurve[0];
    }

    #endregion Calculations

    #endregion Probability of Wiped-Out Event
  }
}
