//
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// <para>Price a <see cref="T:BaseEntity.Toolkit.Products.StockCliquetOption">Stock Cliquet Option</see>
  /// using the <see cref="T:BaseEntity.Toolkit.Models.CliquetOption" /> model.</para>
  /// </summary>
  /// 
  /// <remarks>
  /// <para><h2>Product</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StockCliquetOption" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// 
  /// <para>Let <m>X_i \equiv S_{t_i}/S_{t_{i-1}}</m> and notice that<math>\begin{align}
  ///   \max\{L_i, \min(X_i - 1, U_i)\}
  ///    &amp;= (X_i - 1 - L_i)^+ - (X_i - 1-U_i)^+ + L_i
  ///  \end{align}</math>
  /// </para>
  /// <para>Then the value of the cliquet option WITHOUT the global floor is
  /// <math>\begin{align}
  ///   E&amp;\left[\sum_{i=1}^n \max\{L_i, \min(X_i - 1, U_i)\} \right]
  ///   \\ &amp;= \sum_{i=1}^n \left[ E(X_i - 1 - L_i)^+ - E(X_i - 1-U_i)^+ + L_i \right]
  ///  \end{align}</math>
  /// </para>
  ///
  /// <para>The option values inside the bracket can be calculated analytically by
  ///  various models.</para>
  /// 
  /// <para><h3>The Black-Scholes model</h3></para>
  /// 
  ///  <para>In the Black-Scholes model, the stock price follows the SDE given by<math>
  ///   \frac{d S_t}{S_t} = \mu_t d{t} + \sigma_t\,d{W_t}
  /// </math>where <m>\mu_t = r_t - d_t</m>, <m>r_t</m> is the short rate and <m>d_t</m> the dividend yield.
  /// </para>
  /// <para>Hence the stock prices are given by<math>
  ///   S_t = \exp\left(
  ///     \int_0^t{\mu_s\,d{s}}
  ///     - \frac{1}{2}\int_0^t\sigma_s^2\,d{s}
  ///     + \int_0^t \sigma_s\,d{W_s}\right)
  /// </math>
  /// </para>
  /// <para><math>
  ///   X_i \equiv \frac{S_{t_i}}{S_{t_{i-1}}} = \exp\left(
  ///     \int_{t_{i-1}}^{t_i} {\mu_s\,d{s}}
  ///     - \frac{1}{2}\int_{t_{i-1}}^{t_i} \sigma_s^2\,d{s}
  ///     + \int_{t_{i-1}}^{t_i} \sigma_s\,d{W_s}\right)
  /// </math> 
  /// </para>
  /// <para>Easy to see <m>X_i</m> is log-normally distributed<math>
  ///   X_i \sim LN\left(
  ///      \frac{s_i}{s_{i-1}},
  ///      \sqrt{\int_{t_{i-1}}^{t_i} \sigma^2_s d{s}}
  ///   \right)
  /// </math>where <m>s_i \equiv E[S_i]</m>.
  ///  </para>
  /// 
  /// <inheritdoc cref="BlackScholesPricerBase" />
  /// </remarks>
  /// 
  /// <seealso cref="T:BaseEntity.Toolkit.Products.StockCliquetOption" />
  /// <seealso cref="T:BaseEntity.Toolkit.Pricers.BlackScholesPricerBase" />
  [Serializable]
  public sealed partial class StockCliquetOptionPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <inheritdoc />
    /// <summary>
    /// Construct pricer using a single vol
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="pricingDate">Date to price the cliquet option</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockCurve">stockCurve containing stock and dividend information</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volatilitySurface">Volatility surface information</param>
    /// <param name="historicalPrices">History of Stock Movement: This will be required if pricing date is after effective date</param>
    public StockCliquetOptionPricer(
      StockCliquetOption option, Dt pricingDate, Dt settle, StockCurve stockCurve,
      DiscountCurve discountCurve, CalibratedVolatilitySurface volatilitySurface, RateResets historicalPrices
    )
      : base(option, pricingDate, settle)
    {
      // Set data, using properties to include validation
      AsOf = pricingDate;
      StockCurve = stockCurve;
      DiscountCurve = discountCurve;
      VolatilitySurface = volatilitySurface;
      HistoricalPrices = historicalPrices;
    }

    #endregion Constructors

    #region Utility Methods

    /// <inheritdoc />
    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (StockCurve == null)
        InvalidValue.AddError(errors, this, "StockCurve", "Missing StockCurve - must be specified");
      else if (StockCurve.StockPrice <= 0.0)
        InvalidValue.AddError(errors, this, "StockPrice",
          $"Invalid price. Must be non negative, Not {StockCurve.StockPrice}");
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing discount curve");
      if (VolatilitySurface == null)
        InvalidValue.AddError(errors, this, "VolatilitySurface", "Invalid volatility surface. Cannot be null");
      else
        VolatilitySurface.Validate(errors);
    }

    /// <inheritdoc />
    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _pv = _delta = _gamma = _vega = _rho = null;
    }

    #endregion

    #region Properties



    /// <summary>
    /// Find instaneous risk free rate at the expiration from discount curve
    /// </summary>
    public double Rfr => RateCalc.Rate(DiscountCurve, new Dt(StockCliquetOption.Expiration, -0.000000001), StockCliquetOption.Expiration);

    /// <summary>
    /// Volatility at first segment 
    /// </summary>
    public double Volatility => volatility(StockCliquetOption.ResetDates[0], StockCliquetOption.ResetDates[1],
        StockCliquetOption.NotionalPrice);

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public CalibratedVolatilitySurface VolatilitySurface { get; private set; }


    /// <summary>
    ///  StockCurve for cliquet option
    /// It contains information of Underlying stock, Dividend schedule, Yield, Discount Curve
    /// </summary>
    public StockCurve StockCurve { get; set; }


    /// <summary>
    ///   Cliquet Stock Option product
    /// </summary>
    public StockCliquetOption StockCliquetOption => (StockCliquetOption)Product;


    /// <summary>
    ///   Discount Curve used for Payment
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Historical Strike Resets
    /// </summary>
    public RateResets HistoricalPrices { get; set; }

    /// <summary>
    /// Realized Rate of return
    /// </summary>
    /// <returns></returns>
    public double RealizedRate
    {
      get { return _realizedRate.Value; }
    }

    /// <summary>
    /// Estimated return of current segment
    /// </summary>
    public double FractionRate
    {
      get { return _fractionalRate.Value; }
    }

    /// <summary>
    /// Estimated Return of segments after current segment 
    /// </summary>
    public double UnrealizedRate
    {
      get { return _unrealizedRate.Value; }
    }

    /// <summary>
    /// Total return after applying for global return
    /// </summary>
    public double TotalRate
    {
      get { return _totalRate.Value; }
    }



    #region IPricer

    /// <inheritdoc />
    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment == null) return paymentPricer_;
        return paymentPricer_ ?? (paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve));
      }
    }

    #endregion IPricer

    #endregion Sensitivities

    #region Greeks
    /// <summary>
    /// Delta of Cliquet Option: Sensitivity to 1 unit of stock value
    /// </summary>
    public double Delta()
    {
      if (_delta != null) return _delta.Value;
      throw new System.Exception("Delta is null!");
    }

    /// <summary>
    /// Gamma of Cliquet Option: Sensitity to the change of delta
    /// </summary>
    /// <returns></returns>
    public double Gamma()
    {
      if (_gamma != null) return _gamma.Value;
      throw new System.Exception("Gamma is null!");
    }

    /// <summary>
    /// Vega of Cliquet Option: Sensitivity to 1% volatility increase
    /// </summary>
    public double Vega()
    {
      if (_vega != null) return _vega.Value;
      throw new System.Exception("Vega is null!");
    }

    /// <summary>
    /// Rho of Cliquet Option: Sensitivity to 1% interest rate increase
    /// </summary>
    public double Rho()
    {
      if (_rho != null) return _rho.Value;
      throw new SystemException("Rho is null");
    }


 

    #endregion Sensitivities



    #region optionRetuns

    /// <summary>
    /// Obtain volatility values from volatility surface 
    /// </summary>
    /// <param name="beginDt">Current Date measuring volatility</param>
    /// <param name="endDt">Expiration of current option segment</param>
    /// <param name="strikePrice">Strike Price of Current Segment</param>
    /// <returns></returns>
    public double volatility(Dt beginDt, Dt endDt, double strikePrice)
    {
      return VolatilitySurface.Interpolate(Dt.Add(VolatilitySurface.AsOf, (int)Dt.RelativeTime(beginDt, endDt).Days), strikePrice);
    }


    /// <inheritdoc />
    /// <summary>
    /// Fair Value of Cliquet Option
    /// </summary>
    /// <returns></returns>
    public override double ProductPv()
    {
      if (_pv != null) return _pv.Value;
      throw new SystemException("PV Value is null");
    }


    /// <summary>
    /// Fair Value of Cliquet Option
    /// </summary>
    /// <returns></returns>
    public double FairValue()
    {
      return ProductPv();
    }

    /// <summary>
    /// Return already realized by Underlying stock movement
    /// </summary>
    /// <returns></returns>
    public double RealizedReturn()
    {
      CliquetOptionValue();
      if (_realizedRate != null) return _realizedRate.Value;
      throw new SystemException("Realized Value is null");
    }

    /// <summary>
    /// Estimated return of current segment 
    /// </summary>
    /// <returns></returns>
    public double FractionalReturn()
    {
      if (_fractionalRate != null) return _fractionalRate.Value;
      throw new SystemException("Fractional Value is null");
    }

    /// <summary>
    /// Estimated return of future segment to expiration
    /// </summary>
    /// <returns></returns>
    public double UnrealizedReturn()
    {
      if (_unrealizedRate != null) return _unrealizedRate.Value;
      throw new SystemException("Unrealized Value is null");
    }

    /// <summary>
    /// Return of realized + fractional + unrealized , then applying global floor
    /// </summary>
    /// <returns></returns>
    public double TotalReturn()
    {
      if (_totalRate != null) return _totalRate.Value;
      throw new SystemException("Total Value is null");
    }


    /// <summary>
    /// Decomposed returns of cliquet option 
    /// Return Tuple of 5 members 
    /// Item1: Fair value of the cliquet option, Discounting X Size x Notional Stock Price x Total Return 
    /// Item2: Realized return from Stock Movement 
    /// Item3: Estimated return from current segment where the pricing date belongs 
    /// Item4: Estimated return from after current segment to the expiration 
    /// Item5: Apply global floor on Item2+Item3+Item4 
    /// Item1 = Discounting x Size x Notional StockPrice x Item 5 
    /// If pricing date is before effective date, The option is estimated before effective, hence Item2 = Item3 =0 
    /// </summary>
    /// <returns></returns>
    public void CliquetOptionValue()
    {
      // realized return by past stock movement
      _realizedRate = 0.0;
      // unrealized return estimated on the current fractional segment
      _fractionalRate = 0.0;
      //unrealized return estimated after the current segment to expiration
      _unrealizedRate = 0.0;

      //divide pricing cases (1) pricing date is on or before the effective date (2) pricing date is after effective date( and before expiration)
      //(1)Pricing date is on or before the effective date
      if (AsOf <= StockCliquetOption.Effective)
      {
        for (var idx = 0; idx < StockCliquetOption.ResetDates.Length - 1; idx++)
        {
          var x = StockCurve.Interpolate(StockCliquetOption.ResetDates[idx + 1]) /
                  StockCurve.Interpolate(StockCliquetOption.ResetDates[idx]);

          var segDt = Dt.RelativeTime(StockCliquetOption.ResetDates[idx], StockCliquetOption.ResetDates[idx + 1]).Value;
          var vol = this.volatility(StockCliquetOption.ResetDates[idx], StockCliquetOption.ResetDates[idx + 1],
            StockCurve.Interpolate(StockCliquetOption.ResetDates[idx]));
          _unrealizedRate += StockCliquetOption.FloorRate
                              + BlackScholes.P(OptionStyle.European, OptionType.Call, segDt, x, 1 + StockCliquetOption.FloorRate, 0, 0, vol)
                              - BlackScholes.P(OptionStyle.European, OptionType.Call, segDt, x, 1 + StockCliquetOption.CapRate, 0, 0, vol);
        }
      }

      //(2) pricing date is after effective date:Divides the returns into three categories  
      //1.Before the current pricing date where option return has been realized (Stock Price History needs to be furnished by User)
      //2.Current segment contribution where the pricing date is included (Current Stock Price is to be provided)
      //3.After the current pricing date (same evaluation as in case (1))
      else
      {
        //1.Past Realized Return from Stock Price
        var pastStockPrice = StockCliquetOption.NotionalPrice;
        foreach (var reset in HistoricalPrices.AllResets)
        {
          if (reset.Key >= AsOf || reset.Key == StockCliquetOption.ResetDates[0]) continue;

          var segmentReturn = reset.Value / pastStockPrice - 1; pastStockPrice = reset.Value;
          //return must be within cap and floor rate 
          _realizedRate += Math.Max(StockCliquetOption.FloorRate, Math.Min(StockCliquetOption.CapRate, segmentReturn));
        }
        //2.Current segment contribution where the pricing date is included
        //Current Stock Price input & the latest stock price at reset date are required
        //The model only needs to evaluate from current to the next reset (only fractional portion of a segment)

        Dt fractionalDate;
        //finding next reset date
        try
        {
          fractionalDate = StockCliquetOption.ResetDates.Where(a => a > AsOf).OrderBy(a => a).First();
        }
        catch
        {
          //throws an error when the date falls between the last reset date and expiration date
          fractionalDate = StockCliquetOption.ResetDates.Last();
        }
        var segFDt = Dt.RelativeTime(AsOf, fractionalDate).Value;
        var volF = this.volatility(AsOf, fractionalDate, pastStockPrice);

        var xf = StockCurve.Interpolate(fractionalDate) / pastStockPrice;
        _fractionalRate = BlackScholes.P(OptionStyle.European, OptionType.Call, segFDt, xf, 1 + StockCliquetOption.FloorRate, 0, 0, volF)
                                  - BlackScholes.P(OptionStyle.European, OptionType.Call, segFDt, xf, 1 + StockCliquetOption.CapRate, 0, 0, volF) + StockCliquetOption.FloorRate;

        //3.Estimated return after the coming reset to the expiration
        //Same evaluation method as in (1),but with only reset dates after the current stock
        for (var idx = 0; idx < StockCliquetOption.ResetDates.Length - 1; idx++)
        {
          if (StockCliquetOption.ResetDates[idx] <= fractionalDate &&
              StockCliquetOption.ResetDates[idx] != fractionalDate) continue;
          var xu = StockCurve.Interpolate(StockCliquetOption.ResetDates[idx + 1]) / StockCurve.Interpolate(StockCliquetOption.ResetDates[idx]);
          var segUDt = Dt.RelativeTime(StockCliquetOption.ResetDates[idx], StockCliquetOption.ResetDates[idx + 1]).Value;

          var vol = this.volatility(StockCliquetOption.ResetDates[idx], StockCliquetOption.ResetDates[idx + 1],
            StockCurve.Interpolate(StockCliquetOption.ResetDates[idx]));
          _unrealizedRate += StockCliquetOption.FloorRate
                             + BlackScholes.P(OptionStyle.European, OptionType.Call, segUDt, xu,
                               1 + StockCliquetOption.FloorRate, 0, 0, vol)
                             - BlackScholes.P(OptionStyle.European, OptionType.Call, segUDt, xu,
                               1 + StockCliquetOption.CapRate, 0, 0, vol);
        }
      }
      //Calculate the total return rate & compare with global rate 
      _totalRate = Math.Max(_realizedRate.Value + _fractionalRate.Value + _unrealizedRate.Value, StockCliquetOption.GlobalFloor);
      //Calcuate the present value of cliquet option 
      _pv = (Notional * StockCliquetOption.NotionalPrice) * Math.Exp(-Rfr * Dt.RelativeTime(AsOf, Settle).Value) * _totalRate;

    }


    #endregion optionReturns


    #region Data

    private double? _pv;
    private double? _realizedRate;
    private double? _unrealizedRate;
    private double? _fractionalRate;
    private double? _totalRate;
    private double? _delta;
    private double? _gamma;
    private double? _vega;
    private double? _rho;



    #endregion Data
  }
}