// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StockOption">Stock Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.Heston">Heston</see> model.
  ///   Supports vanilla, digital, barrier, one-touch and lookback options.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.StockOption" />
  ///   <para><h2>Heston Model</h2></para>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Models.Heston" />
  /// </remarks>
  /// <see cref="BaseEntity.Toolkit.Products.StockOption"/>
  /// <see cref="BaseEntity.Toolkit.Models.Heston"/>
  [Serializable]
  public class StockOptionHestonMCPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Construct instance using a single vol
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividend">%Dividend rate (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends</param>
    /// <param name="simulations">Numer of simulation paths</param>
    /// <param name="steps">Number of time steps</param>
    /// <param name="v0">Volatility at time 0</param>
    /// <param name="kappav">Is the mean reversion rate of volatility</param>
    /// <param name="thetav">Is the long term volatility</param>
    /// <param name="sigmav">Volatility of volatility</param>
    /// <param name="rhov">correlation between underlying price and volatility</param>
    public StockOptionHestonMCPricer(
      StockOption option, Dt asOf, Dt settle, double stockPrice,
      DiscountCurve discountCurve, double dividend, DividendSchedule divs,
      int simulations, int steps,
      double v0, double kappav, double thetav, double sigmav, double rhov
      )
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      StockPrice = stockPrice;
      DiscountCurve = discountCurve;
      Dividend = dividend;
      Divs = divs ?? new DividendSchedule(asOf);
      Simulations = simulations;
      Steps = steps;
      V0 = v0;
      KappaV = kappav;
      ThetaV = thetav;
      SigmaV = sigmav;
      RhoV = rhov;
    }

    #endregion Constructors

    #region Utility Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (StockPrice <= 0.0)
        InvalidValue.AddError(errors, this, "StockPrice", String.Format("Invalid price. Must be non negative, Not {0}", StockPrice));
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing discount curve");
      if (Dividend < 0.0 || Dividend > 2.0)
        InvalidValue.AddError(errors, this, "Dividend", String.Format("Invalid dividend {0}. Must be >= 0 and <= 2", Dividend));
      if (StockOption.StrikeDetermination != OptionStrikeDeterminationMethod.Fixed)
        InvalidValue.AddError(errors, this, "StockOption", "Only fixed strikes are supported by this model");
      if (StockOption.UnderlyingDetermination != OptionUnderlyingDeterminationMethod.Regular)
        InvalidValue.AddError(errors, this, "StockOption", "Only regular price determination options are supported by this model");
      if (StockOption.IsBarrier)
      {
        if (!StockOption.BarrierStart.IsEmpty() && Dt.Cmp(StockOption.BarrierStart, AsOf) > 0)
          InvalidValue.AddError(errors, this, "BarrierStart", "This model does not support non-standard barrier start dates");
        if (!StockOption.BarrierEnd.IsEmpty() && Dt.Cmp(StockOption.BarrierStart, StockOption.Expiration) != 0)
          InvalidValue.AddError(errors, this, "BarrierWindowEnd", "This model does not support non-standard barrier end dates");
      }
      if (StockOption.IsDoubleBarrier)
        InvalidValue.AddError(errors, this, "StockOption", "Double barrier options are not currently supported by this model");
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _pv = null;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Dividend rate (5 percent = 0.05)
    /// </summary>
    public double Dividend { get; set; }

    /// <summary>
    /// Dividend rate (5 percent = 0.05)
    /// </summary>
    public DividendSchedule Divs { get; private set; }

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    public double Time
    {
      get { return (StockOption.Expiration - AsOf) / 365.25; }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    public double Days
    {
      get { return (StockOption.Expiration - AsOf); }
    }

    /// <summary>
    ///   Stock Option product
    /// </summary>
    public StockOption StockOption
    {
      get { return (StockOption)Product; }
    }

    #region Market Data

    /// <summary>
    /// Current Price price of underlying asset
    /// </summary>
    public double StockPrice { get; private set; }

    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    public double Rfr
    {
      get { return DiscountCurve.R(StockOption.Expiration); }
    }

    /// <summary>
    ///   Discount Curve used for Payment
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    #endregion Market Data

    #region Model Parameters

    /// <summary>
    /// Number of simulation paths
    /// </summary>
    public int Simulations { get; set; }

    /// <summary>
    /// Number of time steps
    /// </summary>
    public int Steps { get; set; }

    /// <summary>
    /// Heston Volatility at time 0
    /// </summary>
    public double V0 { get; set; }

    /// <summary>
    /// The mean reversion rate of volatility
    /// </summary>
    public double KappaV { get; set; }

    /// <summary>
    /// The long term volatility
    /// </summary>
    public double ThetaV { get; set; }

    /// <summary>
    /// The volatility of volatility
    /// </summary>
    public double SigmaV { get; set; }

    /// <summary>
    /// Correlation between volatility and stock price
    /// </summary>
    public double RhoV { get; set; }

    #endregion Model Parameters

    #region IPricer

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
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #endregion IPricer

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculates present value of option
    /// </summary>
    /// <returns>Pv of option</returns>
    public override double ProductPv()
    {
      return FairPrice() * Notional;
    }

    /// <summary>
    /// Calculates fair value of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    public double FairValue()
    {
      return FairPrice() * Notional;
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    public double FairPrice()
    {
      if (!_pv.HasValue)
      {
        if (Dt.Cmp(StockOption.Expiration, AsOf) < 0)
        {
          _pv = 0.0;
          return _pv.Value;
        }
        var heston = new Heston(OptionPayoff, Simulations, Steps, Time, StockPrice, Rfr, V0, KappaV, ThetaV, SigmaV, RhoV);
        return heston.Price();
      }
      return _pv.Value;
    }

    /// <summary>
    /// Option payoff function for Heston MC
    /// </summary>
    /// <param name="s">Path of underlying price for simulation</param>
    /// <returns>Option fair value</returns>
    private double OptionPayoff(double[] s)
    {
      StockOption option = StockOption;
      double T = Time;
      if (option.IsBarrier)
      {
        OptionBarrierType barrier1Type = option.Barriers[0].BarrierType;
        double barrier1Level = option.Barriers[0].Value;
        OptionBarrierType barrier2Type = (option.Barriers.Count > 1) ? option.Barriers[0].BarrierType : OptionBarrierType.None;
        double barrier2Level = (option.Barriers.Count > 1) ? option.Barriers[0].Value : 0.0;
        bool hit;
        double pS = StockPrice;
        int i = 0;
        // Must hit any knock-in barriers first
        if (barrier1Type == OptionBarrierType.DownIn || barrier1Type == OptionBarrierType.UpIn ||
            barrier2Type == OptionBarrierType.DownIn || barrier2Type == OptionBarrierType.UpIn)
        {
          for (hit = false; i < s.Length && !hit; i++)
          {
            if (barrier1Type == OptionBarrierType.DownIn || barrier1Type == OptionBarrierType.UpIn)
              hit = (pS < barrier1Level) ? (s[i] >= barrier1Level) : (s[i] <= barrier1Level);
            if (!hit && (barrier2Type == OptionBarrierType.DownIn || barrier2Type == OptionBarrierType.UpIn))
              hit = (pS < barrier2Level) ? (s[i] >= barrier2Level) : (s[i] <= barrier2Level);
            pS = s[i];
          }
          if (!hit)
            return option.Rebate;
        }
        // Now test for any knock-out barriers from where we left off
        if (barrier1Type == OptionBarrierType.DownOut || barrier1Type == OptionBarrierType.UpOut || barrier1Type == OptionBarrierType.OneTouch ||
            barrier1Type == OptionBarrierType.NoTouch |
            barrier2Type == OptionBarrierType.DownOut || barrier2Type == OptionBarrierType.UpOut || barrier2Type == OptionBarrierType.OneTouch ||
            barrier2Type == OptionBarrierType.NoTouch)
        {
          for (hit = false; i < s.Length && !hit; i++)
          {
            if (barrier1Type == OptionBarrierType.DownOut || barrier1Type == OptionBarrierType.UpOut || barrier1Type == OptionBarrierType.OneTouch ||
                barrier1Type == OptionBarrierType.NoTouch)
            {
              hit = (pS < barrier1Level) ? (s[i] >= barrier1Level) : (s[i] <= barrier1Level);
              if (barrier1Type == OptionBarrierType.NoTouch) return 0.0;
            }
            if (!hit &&
                (barrier2Type == OptionBarrierType.DownOut || barrier2Type == OptionBarrierType.UpOut || barrier2Type == OptionBarrierType.OneTouch ||
                 barrier2Type == OptionBarrierType.NoTouch))
            {
              hit = (pS < barrier2Level) ? (s[i] >= barrier2Level) : (s[i] <= barrier2Level);
              if (barrier2Type == OptionBarrierType.NoTouch) return 0.0;
            }
            pS = s[i];
          }
          if (hit)
            return option.Rebate;
        }
      }
      // Option still active
      double sf = s[s.Length - 1];
      if (option.IsDigital)
        return ((option.Type == OptionType.Call && sf > option.Strike) || (option.Type == OptionType.Put && sf < option.Strike)) ? option.Rebate : 0.0;
      else
        return Math.Max(option.Type == OptionType.Call ? sf - option.Strike : option.Strike - sf, 0.0);
    }

    #endregion Methods

    #region Data

    private double? _pv;

    #endregion Data
  }
}
