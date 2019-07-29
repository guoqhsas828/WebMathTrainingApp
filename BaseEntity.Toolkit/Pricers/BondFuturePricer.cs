/*
 *   2011-2014. All rights reserved.
 */
using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Standard pricer for a <see cref="BaseEntity.Toolkit.Products.BondFuture">Bond Future</see>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.BondFuture" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <para>Bond futures come in two flavours - those quoted on a price (like CME TBond Futures, Euro-Bund Futures, etc)
  /// and those quoted on an indexed yield (like ASX TBond Futures).</para>
  /// <para>Bond futures have a market quoted price along with a model price implied by the underlying bond.</para>
  /// <para>For price settled futures, the delivered bond can be selected from a pool of bonds. This delivery option is ignored for this
  /// pricer and analysis is done on a specified cheapest to deliver bond or on a 'nominal' deliverable bond.</para>
  /// <para><b>Quoted Price</b></para>
  /// <para>The <see cref="QuotedPrice">market quoted price</see> of the future must be specified directly.</para>
  /// 
  /// <para><h2>Model Price</h2></para>
  /// <para>For price quoted futures, the model price of the futures contract is the forward price of the CTD bond divided by the
  /// conversion factor.</para>
  /// <para>The forward price of the CTD bond is calculated from the model price of the bond so that sensitivities
  /// to interest rates can be generated. To be clear, this effectively captures all sensitivity to interest
  /// rates to the maturity of the CTD bond.</para>
  /// <para>A <see cref="ModelBasis">basis</see> between the quoted price and the model price can also
  /// be specified. A method is provided to calculate the implied model basis. See <see cref="ImpliedModelBasis"/>
  /// for more details.</para>
  /// <para>For index yield quoted futures suchas ASX TBond futures, the model price of the futures contract is the indexed forward
  /// yield of a 'nominal' deliverable bond.</para>
  /// 
  /// <para><h2>Sensitivities</h2></para>
  /// <para>Sensitivities use the model price to capture sensitivities to the underlying market factors.</para>
  /// <para>See <see cref="BondFuturePricer.ModelPrice"/> for more details.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.BondFuture">Bond Future Product</seealso>
  [Serializable]
  public class BondFuturePricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Futures contract</param>
    /// <param name="asOf">Pricing as of date</param>
    /// <param name="settle">Settlement date (of underlying CTD bond)</param>
    /// <param name="contracts">Number of contracts</param>
    /// <param name="price">Futures price</param>
    public BondFuturePricer(BondFuture future, Dt asOf, Dt settle, double contracts, double price)
      : base(future, asOf, settle)
    {
      QuotedPrice = price;
      Contracts = contracts;
    }

    /// <summary>
    /// Constructor for a bond with a CTD basket
    /// </summary>
    /// <param name="future">Bond future contract</param>
    /// <param name="asOf">Pricing as of date</param>
    /// <param name="settle">Settlement date (of underlying CTD bond)</param>
    /// <param name="contracts">Number of contracts</param>
    /// <param name="price">Futures price</param>
    /// <param name="discountCurve">Discount/term repo curve pricing</param>
    /// <param name="ctdBond">Cheapest to delivery bond</param>
    /// <param name="ctdMarketQuote">Market quote of CTD bond</param>
    /// <param name="ctdQuotingConvention">Market quote convention of CTD bond</param>
    /// <param name="conversionFactor">Conversion factor for cheapest to delivery bond</param>
    public BondFuturePricer(BondFuture future, Dt asOf, Dt settle, double contracts, double price, DiscountCurve discountCurve,
      Bond ctdBond, double ctdMarketQuote, QuotingConvention ctdQuotingConvention, double conversionFactor)
      : base(future, asOf, settle)
    {
      DiscountCurve = discountCurve;
      QuotedPrice = price;
      Contracts = contracts;
      CtdBond = ctdBond;
      CtdMarketQuote = ctdMarketQuote;
      CtdQuotingConvention = ctdQuotingConvention;
      CtdConversionFactor = conversionFactor;
    }

    #endregion Constructors

    #region Utilities

    /// <summary>
    /// Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks> 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (QuotedPrice <= 0.0)
        InvalidValue.AddError(errors, this, "Price", String.Format("Invalid futures price {0}. Must be > 0", QuotedPrice));
      if (BondFuture.QuotedOnIndexedYield && QuotedPrice >= 1.0)
        InvalidValue.AddError(errors, this, "Price", String.Format("Invalid futures price {0}. Must be < 100", QuotedPrice));
      // Note: Even for Futures that have a CTD basket, we allow the CTD bond to be unspecified as some calculations don't require it.
      // An exception will be thrown if a calculation is performed that requires a CTD bond.
      if (CtdBond != null)
      {
        if (CtdMarketQuote <= 0.0)
          InvalidValue.AddError(errors, this, "CtdMarketQuote", String.Format("Invalid CTD market quote {0}, must be >= 0", CtdMarketQuote));
        if (CtdQuotingConvention == QuotingConvention.None)
          InvalidValue.AddError(errors, this, "CtdQuotingConvention", String.Format("Invalid CTD quoting convention {0}", CtdQuotingConvention));
        if (CtdConversionFactor <= 0.0)
          InvalidValue.AddError(errors, this, "CtdConversionFactor", String.Format("Invalid CTD conversion factor {0}, must be >= 0", CtdConversionFactor));
        // Step up bonds and floating rate bonds are not supported.
        if (CtdBond.Floating)
          InvalidValue.AddError(errors, this, "CtdBond", "Float rate CTD bonds are not supported");
        if (CtdBond.Convertible)
          InvalidValue.AddError(errors, this, "CtdBond", "Convertable CTD bonds are not supported");
        if (CtdBond.Amortizes)
          InvalidValue.AddError(errors, this, "CtdBond", "Amortizing CTD bonds are not supported");
        if (CtdBond.StepUp)
          InvalidValue.AddError(errors, this, "CtdBond", "Step up CTD bonds are not supported");
      }
      return;
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _ctdBondPricer = null;
    }

    #endregion Utilities

    #region Properties

    ///<summary>
    /// Quoted futures price
    ///</summary>
    public double QuotedPrice { get; set; }

    /// <summary>
    /// Basis adjustment to use for model pricing. Used to match model price to market price
    /// </summary>
    /// <remarks>
    ///   <para>This is the basis used in the pricing of the futures contract for sensitivity calculations.
    ///   This must be set explicitly. There are methods for calculating this implied basis.</para>
    ///   <seealso cref="ImpliedModelBasis"/>
    /// </remarks>
    public double ModelBasis { get; set; }

    /// <summary>
    /// The repo curve used to calculate forward price and discount forward PnL
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Number of contracts
    /// </summary>
    /// <remarks>
    ///   <para>The <see cref="PricerBase.Notional">Notional</see> is equal to
    ///   the <see cref="FutureBase.ContractSize">Contract size</see> times the
    ///   <see cref="Contracts">Number of Contracts</see>.</para>
    /// </remarks>
    public double Contracts
    {
      get { return Notional / BondFuture.ContractSize; }
      set { Notional = BondFuture.ContractSize * value; }
    }

    /// <summary>
    /// Bond future being priced
    /// </summary>
    public BondFuture BondFuture
    {
      get { return (BondFuture)this.Product; }
    }

    #region Cheapest to Deliver

    /// <summary>
    /// The cheapest to deliver (CTD) bond
    /// </summary>
    public Bond CtdBond { get; set; }

    /// <summary>
    /// Market quote for CTD bond
    /// </summary>
    public double CtdMarketQuote { get; set; }

    /// <summary>
    /// Quoting convention for CTD bond
    /// </summary>
    public QuotingConvention CtdQuotingConvention { get; set; }

    ///<summary>
    /// Conversion factor for CTD bond
    ///</summary>
    public double CtdConversionFactor { get; set; }

    /// <summary>
    /// Bond pricer for CTD bond
    /// </summary>
    private BondPricer CtdBondPricer
    {
      get
      {
        if (_ctdBondPricer == null)
        {
          if (CtdBond == null)
          {
            if (BondFuture.QuotedOnIndexedYield)
            {
              // For ASX Bond Futures, Create a synthetic underlying bond for sensitivity calculations
              var maturity = Dt.Add(Settle, BondFuture.NominalTerm, TimeUnit.Years);
              CtdBond = new Bond(AsOf, maturity, Currency.None, BondType.AUSGovt, BondFuture.NominalCoupon, DayCount.Actual365Fixed,
                                 CycleRule.None, Frequency.SemiAnnual, BDConvention.None, Calendar.AUB);
              if( CtdMarketQuote <= 0.0 )
                CtdMarketQuote = 1.0 - QuotedPrice;
              CtdQuotingConvention = QuotingConvention.Yield;
              CtdConversionFactor = 1.0;
            }
            else
              // Throw exception here if we are trying to calculate something that requires the CTD but we don't have one
              throw new ArgumentException("Cheapest to deliver bond not specified");
          }
          // Note: Create dummy discount curve if we need to. Many futures calculations don't need the discount curve.
          // BondPricer should be able to support construction without a discount curve but can't right now. RTD Oct'11
          _ctdBondPricer = new BondPricer(CtdBond, AsOf, Settle, DiscountCurve ?? new DiscountCurve(AsOf, 0.04),
                                          null, 0, TimeUnit.None, 0.0)
                           {
                             MarketQuote = CtdMarketQuote,
                             QuotingConvention = CtdQuotingConvention
                           };
        }
        return _ctdBondPricer;
      }
    }

    #endregion Cheapest to Deliver

    #endregion Properties

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value of any additional payment.
    /// </summary>
    /// <remarks>
    ///   <para>For bond futures this is the number of contracts times the value of each
    ///   futures contract given the model price minus the model basis.</para>
    ///   <formula>
    ///     V = \text{Margin}( F^m - B_m ) * N
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price from the model price of the underlying bond</description></item>
    ///     <item><description><formula inline="true">B_m</formula> is the basis between the quoted futures price and the model futures price</description></item>
    ///     <item><description><formula inline="true">N</formula> is the number of futures contracts</description></item>
    ///   </list>
    ///   <para>Note that the model basis must be explicitly set. There are methods for calculating the model basis.</para>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>For price settled futures, the model price of the futures contract is implied
    ///   from the forward model price of the CTD bond and the conversion factor.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>For yield indexed futures, the model price of the futures contract is implied
    ///   from the forward yield of the nominal underlying bond.</para>
    ///   <seealso cref="ImpliedModelBasis"/>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <seealso cref="ModelPrice()"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      return ContractMarginValue(ModelPrice() - ModelBasis) * Contracts;
    }

    /// <summary>
    /// Model implied price of future
    /// </summary>
    /// <remarks>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>For price settled futures, the model price of the futures contract is the
    ///   forward price of the CTD bond divided by the conversion factor.</para>
    ///   <para>The model price of the futures contract is calculated from the model price of the
    ///   underlying bond so that sensitivities to interest rates can be generated. To be clear,
    ///   this effectively captures all sensitivity to interest rates to the maturity of the underlying
    ///   bond.</para>
    ///   <para>The model price is:</para>
    ///   <formula>
    ///     F^m = \frac{P_{T}^{m}}{CF}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">P_{T}^{m}</formula> is the CTD forward flat price implied from the model price of the CTD bond</description></item>
    ///     <item><description><formula inline="true">CF</formula> is the CTD conversion factor</description></item>
    ///   </list>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>For yield indexed futures, the model price of the futures contract is 1 minus the
    ///   forward yield of the nominal underlying bond (without convexity adjustment).</para>
    ///   <para>The model price is:</para>
    ///   <formula>
    ///     F^m = 1.0-Y_{T}^{m}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">Y_{T}^{m}</formula> is the forward yield implied from the model price of the nominal underlying bond</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double ModelPrice()
    {
      // Note: In theory bond full price should be pv'd to asOf date and we should ensure that the BondPricer.FwdFullPrice
      // pvs to asOf date also.
      var ctdModelFullPrice = CtdBondPricer.FullModelPrice();
      return ModelPrice(ctdModelFullPrice);
    }

    /// <summary>
    /// Model implied price of future
    /// </summary>
    /// <remarks>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>For price settled futures, the model price of the futures contract is the
    ///   forward price of the CTD bond divided by the conversion factor.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>For yield indexed futures, the model price of the futures contract is 1 minus the
    ///   forward yield of the nominal underlying bond (without convexity adjustment).</para>
    /// </remarks>
    /// <seealso cref="ModelPrice()"/>
    /// <param name="ctdFullPrice">Full price of CTD bond</param>
    /// <returns>Model price of futures</returns>
    public double ModelPrice(double ctdFullPrice)
    {
      if (DiscountCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      var ctdModelFwdFullPrice = CtdBondPricer.FwdFullPrice(BondFuture.LastDeliveryDate, ctdFullPrice);
      if (BondFuture.QuotedOnIndexedYield)
      {
        var cdtModelFwdYield = CtdBondPricer.FwdYield(ctdModelFwdFullPrice, BondFuture.LastDeliveryDate, 0.0, YieldCAMethod.None);
        return (1.0 - cdtModelFwdYield);
      }
      else
        return (ctdModelFwdFullPrice - CtdForwardAccrued()) / CtdConversionFactor;
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    ///   <para>The model basis is the difference between the model futures price implied by the model forward
    ///   price of the underlying bond and the quoted futures price.</para>
    ///   <formula>
    ///     B_m = F^m - Price
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price from the model price of the underlying bond</description></item>
    ///     <item><description><formula inline="true">Price</formula> is the quoted futures price</description></item>
    ///   </list>
    ///   <para>This method calculates the implied basis. The actual basis used during pricing must be explicitly set. The
    ///   default for the basis used in pricing is 0.</para>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double ImpliedModelBasis()
    {
      return ModelPrice() - QuotedPrice;
    }

    /// <summary>
    /// Cash underlying Bond price implied by futures price
    /// </summary>
    /// <remarks>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The cash equivalent price is the CTD bond delivery price as a percent of notional
    ///   implied from the futures price and conversion factor. This is simply the quoted futures
    ///   price times the conversion factor.</para>
    ///   <para>The cash equivalent price is:</para>
    ///   <formula>
    ///     P_t = F * CF
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">P_t</formula> is the cash equivalent (CTD flat) price</description></item>
    ///			<item><description><formula inline="true">F</formula> is the futures price</description></item>
    ///     <item><description><formula inline="true">CF</formula> is the CTD conversion factor</description></item>
    ///   </list>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The cash equivalent price is the nominal underlying bond price as a percent of notional
    ///   given the yield implied by the current futures price.</para>
    ///   <para>The cash equivalent price is:</para>
    ///   <formula>
    ///     P_t = {YieldToPrice}(1-F)
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">P_t</formula> is the cash equivalent (flat) price</description></item>
    ///			<item><description><formula inline="true">F</formula> is the futures price</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Futures price expressed as a cash equivalent price</returns>
    public double CashEquivalentPrice()
    {
      if (BondFuture.QuotedOnIndexedYield)
        return CtdBondPricer.FlatPrice();
      else
        return QuotedPrice * CtdConversionFactor;
    }

    /// <summary>
    /// The underlying bond price expressed in a futures equivalent form
    /// </summary>
    /// <remarks>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The futures equivalent price is simply the flat price of the CTD bond divided by the conversion factor.</para>
    ///   <para>The futures equivalent price is:</para>
    ///   <formula>
    ///     F_p = \frac{P_ctd}{CF}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F_p</formula> is the futures equivalent price</description></item>
    ///     <item><description><formula inline="true">P_ctd</formula> is the CTD spot flat price</description></item>
    ///     <item><description><formula inline="true">CF</formula> is the CTD conversion factor</description></item>
    ///   </list>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The implied futures price is the same as the quoted futures price.</para>
    /// </remarks>
    /// <returns>Underlying bond price expressed as a futures equivalent price</returns>
    public double FuturesEquivalentPrice()
    {
      if (BondFuture.QuotedOnIndexedYield)
        return QuotedPrice;
      else
        return CtdFlatPrice() / CtdConversionFactor;
    }

    /// <summary>
    /// The total nominal value of the future contract based on the current market quote
    /// </summary>
    /// <returns>Total value</returns>
    public double Value()
    {
      return Contracts * ContractMarginValue() + PaymentPv();
    }

    #region Margin Calculations

    /// <summary>
    /// Margin payment
    /// </summary>
    /// <remarks>
    ///   <para>Calculated margin payment for traded contracts from previous futures
    ///   price to current futures price</para>
    ///   <para>The margin is:</para>
    ///   <formula>
    ///     M_t = \left( V(F_t) - V(F_{t-1} \right) * C
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">M_t</formula> is the futures margin</description></item>
    ///     <item><description><formula inline="true">V(F_t)</formula> is the current futures contract value</description></item>
    ///     <item><description><formula inline="true">V(F_{t-1})</formula> is the previous futures contract value</description></item>
    ///     <item><description><formula inline="true">C</formula> is the number of contracts</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="prevPrice">Previous quoted futures price</param>
    public double Margin(double prevPrice)
    {
      return (ContractMarginValue(QuotedPrice) - ContractMarginValue(prevPrice))*Contracts;
    }

    /// <summary>
    /// Value of each futures contract for margin calculation
    /// </summary>
    /// <remarks>
    ///   <para>The futures contract value is the current price value for the quoted futures price.</para>
    ///   <para>For futures based on price this is simply the quoted futures price. For futures based on
    ///   yield (eg ASX 3Yr Bond Future), this is a formula based on the yield (100-quoted price).</para>
    ///   <para>For most futures contracts the value for each contract is rounded to a cent.</para>
    /// </remarks>
    /// <param name="price">Futures price</param>
    /// <returns>Futures contract value</returns>
    public double ContractMarginValue(double price)
    {
      if (BondFuture.QuotedOnIndexedYield)
      {
        int term = (CtdBond != null) ? (CtdBond.Maturity.Year - CtdBond.Effective.Year) : BondFuture.NominalTerm;
        return BondFutureModel.AsxTBondFuturePrice(price, BondFuture.NominalCoupon, term, BondFuture.ContractSize);
      }
      else
        return BondFutureModel.FuturePrice(price, BondFuture.PointValue);
    }

    /// <inheritdoc cref="ContractMarginValue(double)" />
    public double ContractMarginValue()
    {
      return ContractMarginValue(QuotedPrice);
    }

    /// <summary>
    /// Margin value as a percentage of notional
    /// </summary>
    /// <param name="price">Futures price</param>
    /// <returns>Margin value as a percenage of notional</returns>
    public double PercentageMarginValue(double price)
    {
      return ContractMarginValue(price) / BondFuture.ContractSize;
    }

    /// <inheritdoc cref="PercentageMarginValue(double)" />
    public double PercentageMarginValue()
    {
      return PercentageMarginValue(QuotedPrice);
    }

    /// <summary>
    /// Value of a single tick. For most futures this is a fixed amount. For ASX futures this is calculated.
    /// </summary>
    /// <returns>Value of a single tick</returns>
    public double TickValue()
    {
      if (BondFuture.QuotedOnIndexedYield)
      {
        return (ContractMarginValue(QuotedPrice) - ContractMarginValue(QuotedPrice - BondFuture.TickSize));
      }
      else
        return BondFuture.TickValue;
    }

    /// <summary>
    /// Value of a single point. This is the tick value divided by the tick size
    /// </summary>
    /// <returns>Value of a point</returns>
    public double PointValue()
    {
      // Scale by 100 as Toolkit futures point is 0.01 as futures price is scaled to 1.
      return TickValue() / BondFuture.TickSize / 100.0;
    }

    #endregion Margin Calculations

    #region CTD Calculations

    /// <summary>
    /// Full price of CTD bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BondPricer.FullPrice()" />
    /// </remarks>
    /// <returns>Full price of CTD bond</returns>
    public double CtdFullPrice()
    {
      return CtdBondPricer.FullPrice();
    }

    /// <summary>
    /// Clean price of CTD bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BondPricer.FlatPrice()" />
    /// </remarks>
    /// <returns>Clean price of CTD bond</returns>
    public double CtdFlatPrice()
    {
      // Hack as temp workaround to bondpricer bug. RTD Oct'11
      return CtdQuotingConvention == QuotingConvention.FlatPrice ? CtdMarketQuote : CtdBondPricer.FlatPrice();
    }

    /// <summary>
    /// Accrued of CTD bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BondPricer.Accrued()" />
    /// </remarks>
    /// <returns>Accrued of CTD bond on delivery date</returns>
    public double CtdAccrued()
    {
      return CtdBondPricer.Accrued();
    }

    /// <summary>
    /// Yield to maturity of CTD bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BondPricer.YieldToMaturity()" />
    /// </remarks>
    /// <returns>Yield of CTD bond</returns>
    public double CtdYieldToMaturity()
    {
      return CtdBondPricer.YieldToMaturity();
    }

    /// <summary>
    /// Pv01 of CTD bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BondPricer.PV01()" />
    /// </remarks>
    /// <returns>Yield of CTD bond</returns>
    public double CtdPv01()
    {
      return CtdBondPricer.PV01();
    }

    /// <summary>
    /// Forward full price of CTD bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Forward full price as a percentage of notional implied from the current CTD price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <returns>Forward full price of CTD bond</returns>
    public double CtdForwardFullPrice()
    {
      if( DiscountCurve == null )
        throw new ArgumentException("Discount/Funding curve required");
      return CtdBondPricer.FwdFullPrice(BondFuture.LastDeliveryDate, CtdBondPricer.FullPrice());
    }

    /// <summary>
    /// Forward clean price of CTD bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Forward clean price as a percentage of notional implied from the current CTD price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <returns>Forward clean price of CTD bond</returns>
    public double CtdForwardFlatPrice()
    {
      return CtdForwardFullPrice() - CtdForwardAccrued();
    }

    /// <summary>
    /// Forwards accrued of CTD bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Accrued of the CTD on the futures last delivery date as a percentage of notional.</para>
    /// </remarks>
    /// <returns>Forward accrued of CTD bond</returns>
    public double CtdForwardAccrued()
    {
      return CtdBondPricer.AccruedInterest(BondFuture.LastDeliveryDate, BondFuture.LastDeliveryDate);
    }

    /// <summary>
    /// The futures price implied by the underlying bond and it's current market quoted price
    /// </summary>
    /// <remarks>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The implied price of the futures contract is the forward price of the CTD bond divided by the
    ///   conversion factor.</para>
    ///   <formula>
    ///     F = \frac{P_T}{CF}
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">P_T</formula> is the forward flat price of the CTD bond implied from the marekt price of the CTD bond</description></item>
    ///     <item><description><formula inline="true">CF</formula> is the conversion factor of the CTD bond</description></item>
    ///   </list>
    ///   <para>The method <see cref="ModelPrice"/> is similar but calculates the futures price implied by the
    ///   model price of the CTD bond rather than the market price of the CTD bond.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The implied futures price is the same as the quoted futures price.</para>
    /// </remarks>
    /// <returns>Futures price implied by underlying bond quoted market price</returns>
    public double CtdImpliedFuturesPrice()
    {
      if (BondFuture.QuotedOnIndexedYield)
        return QuotedPrice;
      else
        return CtdForwardFlatPrice() / CtdConversionFactor;
    }

    #endregion CTD Calculations

    #region Basis Calculations

    /// <summary>
    /// Repo rate implied from futures price
    /// </summary>
    /// <remarks>
    ///   <para>The implied repo rate is rate of return from the cash and carry trade. This is buying a deliverable bond (funded) and
    ///   simultaneously selling a futures contract.</para>
    ///   <para>Without intermediate coupon cashflows, the implied repo rate is:</para>
    ///   <formula>
    ///     r = \left( \frac{F * CF}{P_t+AI_t} -1 \right) \left( \frac {365}{\text{t}} \right)
    ///   </formula>
    ///   <para>Or more generally:</para>
    ///   <formula>
    ///     r = \text{IRR} \left[ P_t + AI_t + \sum_{i=1}^n cf_i - F * CF - AI_T \right]
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">r</formula> is the implied repo rate</description></item>
    ///			<item><description><formula inline="true">F</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">P_t</formula> is the CTD spot clean price</description></item>
    ///     <item><description><formula inline="true">AI_t</formula> is the CTD accrued interest</description></item>
    ///     <item><description><formula inline="true">AI_T</formula> is the CTD accrued interest at delivery</description></item>
    ///     <item><description><formula inline="true">CF</formula> is the CTD conversion factor</description></item>
    ///     <item><description><formula inline="true">t</formula> is the number of days from settlement to futures delivery</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">DayCount for rate</param>
    /// <returns>Implied repo rate</returns>
    public double ImpliedRepoRate(DayCount dayCount)
    {
      double currentPayment = CtdBondPricer.FullPrice();
      double futurePayment = CashEquivalentPrice() + CtdForwardAccrued();
      return ImpliedRepoRate(Settle, CtdBondPricer.BondCashflowAdapter, currentPayment, futurePayment, BondFuture.LastDeliveryDate, dayCount);
    }

    /// <summary>
    /// Repo rate from CTD term repo curve
    /// </summary>
    /// <remarks>
    ///   <para>The market repo rate is rate of return from the buy and forward sale of the CTD bond where the foward sale price of the CTD
    ///   bond is calculated from the term repo curve.</para>
    /// </remarks>
    /// <param name="dayCount">Daycount for repo rate</param>
    /// <returns>Market term repo rate</returns>
    public double MarketRepoRate(DayCount dayCount)
    {
      // Calculate approximate (exact if no coupons before futures delivery)
      double currentPayment = CtdBondPricer.FullPrice();
      double futurePayment = CtdForwardFullPrice();
      return ImpliedRepoRate(Settle, CtdBondPricer.BondCashflowAdapter, currentPayment, futurePayment, BondFuture.LastDeliveryDate, dayCount);
    }

    /// <summary>
    /// Sum of coupon income from holding bond from settlement to futures delivery (cash and carry)
    /// </summary>
    /// <remarks>
    ///   <para>The coupon income is:</para>
    ///   <formula>
    ///     I_c = N * \left( \sum_{i=1}^n c_i + AI_T - AI_t \right)
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">I_c</formula> is the coupon income</description></item>
    ///			<item><description><formula inline="true">N</formula> is the notional (face) amount</description></item>
    ///     <item><description><formula inline="true">c_i</formula> is the ith coupon income amount</description></item>
    ///     <item><description><formula inline="true">AI_t</formula> is the CTD accrued interest</description></item>
    ///     <item><description><formula inline="true">AI_T</formula> is the CTD accrued interest at delivery</description></item>
    ///   </list>
    ///   <para>The coupon income is not present valued.</para>
    /// </remarks>
    /// <returns>Sum of bond coupon income</returns>
    public double CouponIncome()
    {
      var income = 0.0;
      for (var i = 0; i <= CtdBondPricer.Cashflow.Count - 1; i++)
        if (Dt.Cmp(CtdBondPricer.Cashflow.GetDt(i), BondFuture.LastDeliveryDate) <= 0)
          income += CtdBondPricer.Cashflow.GetAccrued(i);
      return (income + CtdForwardAccrued() - CtdBondPricer.Accrued()) * Notional;
    }

    /// <summary>
    /// Cost of funding bond cash and carry
    /// </summary>
    /// <remarks>
    ///   Cost of funding is the dollar cost of funding the purchase of a bond and holding till the futures delivery date
    ///   (for a cash and carry trade).
    ///   <para>The cost of funding is:</para>
    ///   <formula>
    ///     C_f = N * ( P_t + AI_t ) * r * \frac {\text{t}}{365}
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">C_f</formula> is the cost of funding</description></item>
    ///			<item><description><formula inline="true">N</formula> is the notional (face) amount</description></item>
    ///     <item><description><formula inline="true">P_t</formula> is the CTD spot clean price</description></item>
    ///     <item><description><formula inline="true">AI_t</formula> is the CTD accrued interest</description></item>
    ///			<item><description><formula inline="true">r</formula> is the repo rate</description></item>
    ///     <item><description><formula inline="true">t</formula> is the number of days from settlement to futures delivery</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="repoRate">Repo rate</param>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Cost of cash and carry funding</returns>
    public double CostOfFunding(double repoRate, DayCount dayCount)
    {
      double df = RateCalc.PriceFromRate(repoRate, Settle, BondFuture.LastDeliveryDate, dayCount, Frequency.None);
      return CtdBondPricer.FullPrice() * (1/df - 1.0) * Notional;
    }

    /// <summary>
    /// Cost of funding bond cash and carry
    /// </summary>
    /// <remarks>
    ///   Cost of funding is the dollar cost of funding the purchase of a bond and holding till the futures delivery date
    ///   (for a cash and carry trade).
    ///   <para>The cost of funding is:</para>
    ///   <formula>
    ///     C_f = N * ( P_t + AI_t ) * r * \frac {\text{t}}{365}
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">C_f</formula> is the cost of funding</description></item>
    ///			<item><description><formula inline="true">N</formula> is the notional (face) amount</description></item>
    ///     <item><description><formula inline="true">P_t</formula> is the CTD spot clean price</description></item>
    ///     <item><description><formula inline="true">AI_t</formula> is the CTD accrued interest</description></item>
    ///			<item><description><formula inline="true">r</formula> is the repo rate</description></item>
    ///     <item><description><formula inline="true">t</formula> is the number of days from settlement to futures delivery</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Cost of cash and carry funding</returns>
    public double CostOfFunding(DayCount dayCount)
    {
      var repoRate = MarketRepoRate(dayCount);
      return CostOfFunding(repoRate, dayCount);
    }

    /// <summary>
    /// Carry basis or cost of carry.
    /// </summary>
    /// <remarks>
    ///   <para>Carry basis is the difference between the coupon income from holding the CTD
    ///   bond and the cost of financing the bond as a percentage of notional.
    ///   Typically this if positive reflecting the higher coupon income in a positive sloping
    ///   yield curve environment.</para>
    ///   <para>The carry basis is:</para>
    ///   <formula>
    ///     B_c = \frac{I_c - C_f}{N}
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">B_c</formula> is the carry basis</description></item>
    ///			<item><description><formula inline="true">I_c</formula> is the coupon income</description></item>
    ///			<item><description><formula inline="true">C_f</formula> is the cost of funding</description></item>
    ///			<item><description><formula inline="true">N</formula> is the notional (face) amount</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="repoRate">Repo rate</param>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Carry basis</returns>
    public double CarryBasis(double repoRate, DayCount dayCount)
    {
      return (CouponIncome() - CostOfFunding(repoRate, dayCount))/Notional;
    }

    /// <summary>
    /// Carry basis or cost of carry.
    /// </summary>
    /// <remarks>
    ///   <para>Carry basis is the difference between the coupon income from holding the CTD
    ///   bond and the cost of financing the bond as a percentage of notional.
    ///   Typically this if positive reflecting the higher coupon income in a positive sloping
    ///   yield curve environment.</para>
    ///   <para>The carry basis is:</para>
    ///   <formula>
    ///     B_c = \frac{I_c - C_f}{N}
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">B_c</formula> is the carry basis</description></item>
    ///			<item><description><formula inline="true">I_c</formula> is the coupon income</description></item>
    ///			<item><description><formula inline="true">C_f</formula> is the cost of funding</description></item>
    ///			<item><description><formula inline="true">N</formula> is the notional (face) amount</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Carry basis</returns>
    public double CarryBasis(DayCount dayCount)
    {
      var repoRate = MarketRepoRate(dayCount);
      return CarryBasis(repoRate, dayCount);
    }

    /// <summary>
    /// Gross basis
    /// </summary>
    /// <remarks>
    ///   <para>The gross basis is the difference between the current clean price of the
    ///   CDT bond and the forward clean price implied by the futures price and conversion factor.</para>
    ///   <para>The gross basis is:</para>
    ///   <formula>
    ///     B_g = P_t - F * CF
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">B_g</formula> is the gross basis</description></item>
    ///			<item><description><formula inline="true">P_t</formula> is the CTD flat price</description></item>
    ///			<item><description><formula inline="true">F</formula> is the futures price</description></item>
    ///			<item><description><formula inline="true">CF</formula> is the CTD conversion factor</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Gross basis</returns>
    public double GrossBasis()
    {
      return CtdFlatPrice() - CashEquivalentPrice();
    }

    /// <summary>
    /// Net basis
    /// </summary>
    /// <remarks>
    ///   <para>The net basis is the difference between the cost of funds and the implied
    ///   repo rate from a cash and carry trade. Usually this basis is negative indicating
    ///   no arbitrage and the value of the delivery option.</para>
    ///   <para>The net basis is:</para>
    ///   <formula>
    ///     B_n = B_g - B_c
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">B_n</formula> is the net basis</description></item>
    ///			<item><description><formula inline="true">B_g</formula> is the gross basis</description></item>
    ///			<item><description><formula inline="true">B_c</formula> is the carry basis</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="repoRate">Repo rate</param>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Net basis</returns>
    public double NetBasis(double repoRate, DayCount dayCount)
    {
      return GrossBasis() - CarryBasis(repoRate, dayCount);
    }

    /// <summary>
    /// Net basis
    /// </summary>
    /// <remarks>
    ///   <para>The net basis is the difference between the cost of funds and the implied
    ///   repo rate from a cash and carry trade. Usually this basis is negative indicating
    ///   no arbitrage and the value of the delivery option.</para>
    ///   <para>The net basis is:</para>
    ///   <formula>
    ///     B_n = B_g - B_c
    ///   </formula>
    ///   <para>where:</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">B_n</formula> is the net basis</description></item>
    ///			<item><description><formula inline="true">B_g</formula> is the gross basis</description></item>
    ///			<item><description><formula inline="true">B_c</formula> is the carry basis</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Repo Daycount</param>
    /// <returns>Net basis</returns>
    public double NetBasis(DayCount dayCount)
    {
      var repoRate = MarketRepoRate(dayCount);
      return NetBasis(repoRate, dayCount);
    }

    #endregion Basis Calculations

    #region Sensitivity Calculations

    /// <summary>
    ///   Model implied change in futures contract value for a 1 dollar increase (0.01) in the underlying bond price
    /// </summary>
    /// <remarks>
    ///   <para>The price value of an 01 is the change in the value of the futures contract implied by
    ///   a one dollar change in the underlying bond.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P+0.01/2)] - V[F_{t}(P-0.01/2)]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V(f)</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p)</formula> is the futures model price given a underlying bond price p</description></item>
    ///     <item><description><formula inline="true">P</formula> is the underlying bond price</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price divided by the conversion factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   underlying nominal bond forward yield given a 1 dollar increase in the current price.</para>
    /// </remarks>
    /// <returns>Price value of the underlying bond price 01 as a percentage of notional</returns>
    public double Price01()
    {
      if (DiscountCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      var ctdFullPrice = CtdBondPricer.FullPrice();
      const double bump = 0.01;
      var futPriceUp = ModelPrice(ctdFullPrice + bump / 2.0);
      var futPriceDn = ModelPrice(ctdFullPrice - bump / 2.0);
      // Change in futures value
      return (PercentageMarginValue(futPriceUp) - PercentageMarginValue(futPriceDn));
    }

    /// <summary>
    ///   Model implied change in futures value for a 1 dollar increase in the underlying bond price
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Price01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underying bond price 01</returns>
    public double Price01Value()
    {
      return Price01() * Notional;
    }

    /// <summary>
    ///   Model implied change in futures value for a 1bp drop in underlying bond yield
    /// </summary>
    /// <remarks>
    ///   <para>The price value of an 01 is the change in the value of the futures contract implied by
    ///   a one bp change in the underlying bond yield.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P + \frac{\textup{pv01}^{ul}}{2})] - V[F_{t}(P-\frac{\textup{pv01}^{ul}}{2})]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V[f]</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p)</formula> is the futures model price given a underlying bond price p</description></item>
    ///     <item><description><formula inline="true">P</formula> is the underlying bond price</description></item>
    ///     <item><description><formula inline="true">pv01^{ul}</formula> is the underlying bond pv01</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price from a 1bp drop in the CTD yield divided by the conversion
    ///   factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   underlying nominal bond forward yield given a 1bp drop in the bond yield.</para>
    ///   <note>The Pv01 is expressed as a percentage of notional.</note>
    /// </remarks>
    /// <returns>Price value of the underlying bond yield 01 as a percentage of notional</returns>
    public double Pv01()
    {
      if (DiscountCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      var ctdFullPrice = CtdBondPricer.FullPrice();
      var bump = CtdBondPricer.PV01();
      var futPriceUp = ModelPrice(ctdFullPrice + bump / 2.0);
      var futPriceDn = ModelPrice(ctdFullPrice - bump / 2.0);
      // Change in futures value
      return (PercentageMarginValue(futPriceUp) - PercentageMarginValue(futPriceDn));
    }

    /// <summary>
    ///   Model implied change in futures value for a 1bp drop in the underlying bond yield
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Pv01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underlying yield 01</returns>
    public double Pv01Value()
    {
      return Pv01() * Notional;
    }

    /// <summary>
    ///   Model implied change in futures value for a 1bp drop in underlying bond forward yield
    /// </summary>
    /// <remarks>
    ///   <para>The forward value of an 01 is the change in the value of the futures contract implied by
    ///   a one bp change in the underlying bond forward yield.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P_T + \frac{\textup{pv01}^{ul}_T}{2})] - V[F_t(P_T - \frac{pv01^{ul}_T}{2}]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V(f)</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p_T)</formula> is the futures model price given a underlying bond forward price p_T</description></item>
    ///     <item><description><formula inline="true">P_T</formula> is the underlying bond forward price</description></item>
    ///     <item><description><formula inline="true">\textup{pv01}^{ul}_T</formula> is the underlying bond forward pv01</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price from a 1bp drop in the CTD forward yield divided by the conversion
    ///   factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from a 1bp drop in the
    ///   underlying nominal bond forward yield.</para>
    /// </remarks>
    /// <returns>Price value of the underlying forward yield 01</returns>
    public double Fv01()
    {
      if (DiscountCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      var fwdSettle = BondFuture.LastDeliveryDate;
      var ctdFullPrice = CtdBondPricer.FullPrice();
      var ctdFwdFullPrice = CtdBondPricer.FwdFullPrice(fwdSettle, ctdFullPrice);
      var cdtFwdYield = CtdBondPricer.FwdYield(ctdFwdFullPrice, fwdSettle, 0.0, YieldCAMethod.None);
      double futPriceUp, futPriceDn;
      if (BondFuture.QuotedOnIndexedYield)
      {
        futPriceUp = (1.0 - (cdtFwdYield - 0.0001 / 2.0));
        futPriceDn = (1.0 - (cdtFwdYield + 0.0001 / 2.0));
      }
      else
      {
        var ctdFwd01 = CtdBondPricer.FwdPv01(fwdSettle, ctdFwdFullPrice, cdtFwdYield);
        futPriceUp = (ctdFwdFullPrice - CtdForwardAccrued() + ctdFwd01 / 2.0) / CtdConversionFactor;
        futPriceDn = (ctdFwdFullPrice - CtdForwardAccrued() - ctdFwd01 / 2.0) / CtdConversionFactor;
      }
      // Change in futures value
      return (PercentageMarginValue(futPriceUp) - PercentageMarginValue(futPriceDn));
    }

    /// <summary>
    ///   Model implied change in futures value for a 1bp drop in underlying bond forward yield
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Pv01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underlying forward yield 01</returns>
    public double Fv01Value()
    {
      return Fv01() * Notional;
    }

    /// <summary>
    /// Modified duration
    /// </summary>
    /// <remarks>
    ///   <para>This is the price sensitivity to a 1bp yield change divided by the price.</para>
    ///   <para>The modified duration is calculated as:</para>
    ///   <formula>
    ///     DUR_mod = \frac{Pv01}{P}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">DUR_mod</formula> is the modified duration</description></item>
    ///			<item><description><formula inline="true">Pv01</formula> is the price change for a 1bp drop in the CTD yield</description></item>
    ///     <item><description><formula inline="true">P</formula> is the price</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Modified duration</returns>
    public double ModDuration()
    {
      return 10000.0 * Pv01() / QuotedPrice;
    }

    /// <summary>
    /// Convexity
    /// </summary>
    /// <remarks>
    ///   <para>This is the Pv01 sensitivity to a 1bp yield change divided by the price.</para>
    ///   <para>The convexity is calculated as:</para>
    ///   <formula>
    ///     C = \frac{C^{ul}}{Cf}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">c</formula> is the futures convexity</description></item>
    ///			<item><description><formula inline="true">C^{ul}</formula> is the convexity of the nomial deliverable bond</description></item>
    ///     <item><description><formula inline="true">Cf</formula> is the conversion factor</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Convexity</returns>
    public double Convexity()
    {
      // Note: Should really be using bond forward convexity
      // Note: Bond convexity scaled 100 more than Bloomberg. Adjust spreadsheet now but maybe change function later.
      return CtdBondPricer.Convexity() / CtdConversionFactor;
    }

    /// <summary>
    ///  Calculate the theoretical change in futures value for a 1bp drop in the term repo rate
    /// </summary>
    /// <remarks>
    ///   <para>The Repo 01 is the theoretical change in the futures value given a 1bp drop
    ///   in the term repo rate (calculated on a 25bp shift and scaled).</para>
    ///   <para>The Repo 01 assumes no change in the bond price.</para>
    ///   <para>The Repo01 is expressed as a percentage of notional.</para>
    /// </remarks>
    /// <param name="dayCount">Repo rate daycount</param>
    /// <returns>Repo 01 as a percentage of notional</returns>
    public double Repo01(DayCount dayCount)
    {
      // Change in forward price
      var repoRate = ImpliedRepoRate(dayCount);
      // Forward price implied by repoRate.
      var fp = (CtdBondPricer.FullPrice() -
        CashflowPv(CtdBondPricer.Cashflow, Settle, BondFuture.LastDeliveryDate, repoRate, dayCount)) /
        RateCalc.PriceFromRate(repoRate, Settle, BondFuture.LastDeliveryDate, dayCount, Frequency.None);
      // Forward price implied by bumped repoRate
      var fpdn = (CtdBondPricer.FullPrice() -
        CashflowPv(CtdBondPricer.Cashflow, Settle, BondFuture.LastDeliveryDate, repoRate - 0.0025, dayCount)) /
        RateCalc.PriceFromRate(repoRate - 0.0025, Settle, BondFuture.LastDeliveryDate, dayCount, Frequency.None);
      // Change in forward price
      var pdelta = (fpdn - fp);
      double delta;
      if (BondFuture.QuotedOnIndexedYield)
      {
        // Change in forward yield
        double ctdModelFullPrice = CtdBondPricer.FullModelPrice();
        double ctdModelFwdFullPrice = CtdBondPricer.FwdFullPrice(BondFuture.LastDeliveryDate, ctdModelFullPrice);
        delta = CtdBondPricer.FwdYield(ctdModelFwdFullPrice, BondFuture.LastDeliveryDate, 0.0, YieldCAMethod.None) -
                CtdBondPricer.FwdYield(ctdModelFwdFullPrice + pdelta, BondFuture.LastDeliveryDate, 0.0, YieldCAMethod.None);
      }
      else
      {
        // Calculated change in margin from change futures price implied by change in forward price / conversion factor
        delta = pdelta / CtdConversionFactor;
      }
      // Change in futures value
      return (PercentageMarginValue(QuotedPrice + delta) - PercentageMarginValue(QuotedPrice))/25.0;
    }

    /// <summary>
    /// Theoretical change in futures value for a 1bp drop in the term repo rate
    /// </summary>
    /// <remarks>
    ///   <para>The Repo 01 is the theoretical change in the futures value given a 1bp drop
    ///   in the term repo rate.</para>
    ///   <para>The Repo 01 is calculated without changing the bond price.</para>
    /// </remarks>
    /// <param name="dayCount">Repo rate daycount</param>
    /// <returns>Repo 01</returns>
    public double Repo01Value(DayCount dayCount)
    {
      return Repo01(dayCount) * Notional;
    }

    #endregion Sensitivity Calculations

    #region Utility Methods

    /// <summary>
    /// PV cashflows at fixed repo rate
    /// </summary>
    private static double CashflowPv(CashflowAdapter cf, Dt asOf, Dt toDate, double rate, DayCount dayCount)
    {
      double pv = 0.0;
      for (var i = 0; i <= cf.Count - 1; i++)
        if (Dt.Cmp(cf.GetDt(i), toDate) <= 0)
        {
          double accrual = cf.GetAccrued(i);
          double amount = cf.GetAmount(i);
          double T = Dt.Fraction(asOf, cf.GetDt(i), dayCount);
          double discountFactor = RateCalc.PriceFromRate(rate, T, Frequency.None);
          pv += discountFactor*(amount + accrual);
        }
      return pv;
    }

    /// <summary>
    /// Calculate implied repo rate
    /// </summary>
    private static double ImpliedRepoRate(Dt asOf, CashflowAdapter cf, double currentPayment, double futurePayment, Dt futureDate, DayCount dayCount)
    {
      if (asOf >= futureDate)
        return 0.0;
      // Calculate approximate (exact if no coupons before futures delivery)
      double T = Dt.Fraction(asOf, futureDate, dayCount);
      double r = (currentPayment / futurePayment - 1.0) / T;
      // Solve for exact repo rate
      var fn = new CashAndCarryPv
      {
        AsOf = asOf,
        CurrentPayment = currentPayment,
        CfAdapter = cf,
        FuturePayment = futurePayment,
        FuturePaymentDate = futureDate,
        T = T,
        DayCount = dayCount
      };
      var rf = new Brent2();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      return rf.solve(fn, 0.0, r);
    }

    /// <summary>
    ///   Solver Fn function for implying repo for bond future cash and carry
    /// </summary>
    private class CashAndCarryPv : SolverFn
    {
      public override double evaluate(double x)
      {
        var couponPv = CashflowPv(CfAdapter, AsOf, FuturePaymentDate, x, DayCount);
        return CurrentPayment - couponPv - FuturePayment / (1.0 + x * T);
      }
      public Dt AsOf { private get; set; }
      public double CurrentPayment { private get; set; }
      public CashflowAdapter CfAdapter { private get; set; }
      public double FuturePayment { private get; set; }
      public Dt FuturePaymentDate { private get; set; }
      public double T { private get; set; }
      public DayCount DayCount { private get; set; }
    }

    #endregion Utility Methods

    /// <summary>
    /// Present value of any additional payment associated with the pricer.
    /// </summary>
    /// <remarks>
    /// The reason that a "payment" pv is needed is that in risk measure term, Pv of rate futures are defined as contract value reflecting
    /// market implied price compared against exchange closing price, while in curve calibration or some other matters, 
    /// Pv of rate futures is purely the contract value reflecting market implied price; thus putting the comparing reference into "payment" pv
    /// is an effective workaround to address the double needs in toolkit pricer
    /// </remarks>
    ///<returns>Payment PV</returns>
    public override double PaymentPv()
    {
      double pv = 0.0;
      if (Payment != null)
      {
        return Payment.Amount;
      }
      return pv;
    }

    #endregion Methods

    #region Data

    private BondPricer _ctdBondPricer;

    #endregion Data

  }
}
