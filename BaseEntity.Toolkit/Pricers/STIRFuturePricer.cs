//
// RateFuturePricer.cs
//   2010-2014. All rights reserved.
//
using System;
using System.Collections;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.StirFuture">STIR Future</see>
  /// </summary>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StirFuture" />
  /// <remarks>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StirFuture">STIR future</seealso>
  [Serializable]
  public class StirFuturePricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Future contract</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="contracts">Number of contracts</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceCurve">Forward curve (or null to use discount curve)</param>
    public StirFuturePricer(StirFuture future, Dt asOf, Dt settle, double contracts, DiscountCurve discountCurve, DiscountCurve referenceCurve = null)
      : base(future, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve ?? discountCurve;
      Contracts = contracts;
      ApproximateForFastCalculation = false;
    }

    #endregion Constructors

    #region Utilities

    /// <summary>
    ///   Validate pricer inputs
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
      // Allow empty DiscountCurve and ReferenceCurve for market calculations
    }

    #endregion Utilities

    #region Properties

    /// <summary>
    /// Quoted futures price
    /// </summary>
    public double QuotedPrice { get; set; }

    /// <summary>
    /// Previous close futures price
    /// </summary>
    public double? PrevClosePrice { get; set; }

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
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Reference (projection) curve
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; private set; }

    /// <summary>
    /// Rate model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters RateModelParameters { get; set; }

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
      get { return Notional / StirFuture.ContractSize; }
      set { Notional = StirFuture.ContractSize * value; }
    }

    /// <summary>
    /// Rate future being priced
    /// </summary>
    protected internal StirFuture StirFuture => Product as StirFuture;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value of any additional payment.
    /// </summary>
    /// <remarks>
    ///   <para>For rate futures this is the number of contracts times the value of each
    ///   futures contract given the model price minus the model basis.</para>
    ///   <formula>
    ///     V = \text{Margin}( F^m - B_m ) * N
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price from rates</description></item>
    ///     <item><description><formula inline="true">B_m</formula> is the basis between the quoted futures price and the model futures price</description></item>
    ///     <item><description><formula inline="true">N</formula> is the number of futures contracts</description></item>
    ///   </list>
    ///   <para>Note that the model basis must be explicitly set. There are methods for calculating the model basis.</para>
    ///   <seealso cref="ImpliedModelBasis"/>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <seealso cref="ModelPrice"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      var p = (FutureBase)Product;
      if (p.LastTradingDate != Dt.Empty && Settle > p.LastTradingDate)
        return 0.0;

      return ContractModelMarginValue(ModelPrice() - ModelBasis) * Contracts;
    }

    /// <summary>
    ///   Implied futures price
    /// </summary>
    /// <remarks>
    ///   <para>This is 1 minus the calculated <see cref="ModelRate()">model rate</see></para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double ModelPrice()
    {
      return 1.0 - ModelRate();
    }

    /// <summary>
    ///   Implied forward rate of the underlying deposit
    /// </summary>
    /// <remarks>
    ///   <para>This is the forward rate implied by the interest rate curves. For money market rate based futures
    ///   this includes a convexity adjustment.</para>
    /// </remarks>
    /// <returns>The implied forward rate</returns>
    public double ModelRate()
    {
      if (DiscountCurve == null || ReferenceCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      double res = 0.0;
      switch (StirFuture.RateFutureType)
      {
        case RateFutureType.TBill:
          var reference = (DiscountCurve)ReferenceCurve;
          res = reference.F(StirFuture.DepositSettlement, StirFuture.DepositMaturity, StirFuture.ReferenceIndex.DayCount, Frequency.None);
          break;
        case RateFutureType.ASXBankBill:
          reference = (DiscountCurve)ReferenceCurve;
          var df = reference.DiscountFactor(StirFuture.DepositMaturity);
          var prevDf = reference.DiscountFactor(StirFuture.DepositSettlement);
          res = (prevDf / df - 1.0) * 365.0 / 90.0;
          break;
        case RateFutureType.ArithmeticAverageRate:
        case RateFutureType.GeometricAverageRate:
        case RateFutureType.MoneyMarketCashRate:
        default:
          var ps = GetPaymentSchedule(null, AsOf);
          var fip = ps.First() as FloatingInterestPayment;
          if( fip != null )
            res = fip.EffectiveRate;
          break;
      }
      return res;
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    ///   <para>The model basis is the difference between the model implied futures price and the quoted futures price.</para>
    ///   <formula>
    ///     Basis = F^m - Price
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price</description></item>
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
    /// The total nominal value of the future contract based on the current market quote
    /// </summary>
    /// <returns>Total value</returns>
    public double Value()
    {
      return Contracts * ContractMarginValue();
    }

    /// <summary>
    /// Futures rate from quoted price
    /// </summary>
    /// <remarks>
    /// <para>For rate futures this is simply <m>R_Q = 1 - P_Q</m>
    /// where <m>P_Q</m> is the quoted futures price.</para>
    /// </remarks>
    /// <returns>Rate implied by quoted market futures price</returns>
    public double QuotedRate()
    {
      return 1.0 - QuotedPrice;
    }

    /// <summary>
    /// Convexity adjusted futures rate
    /// </summary>
    /// <returns>Convexity adjusted futures rate</returns>
    public double AdjustedQuotedRate()
    {
      return QuotedRate() + ConvexityAdjustment();
    }

    /// <summary>
    /// Convexity adjustment
    /// </summary>
    /// <returns>Convexity adjustment to forward rate</returns>
    public double ConvexityAdjustment()
    {
      double res = 0.0;
      switch (StirFuture.RateFutureType)
      {
        case RateFutureType.TBill:
        case RateFutureType.ASXBankBill:
          res = 0.0;
          break;
        case RateFutureType.ArithmeticAverageRate:
        case RateFutureType.GeometricAverageRate:
        case RateFutureType.MoneyMarketCashRate:
        default:
          var ps = GetPaymentSchedule(null, AsOf);
          var fip = ps.First() as FloatingInterestPayment;
          if (fip != null)
            res = fip.ConvexityAdjustment;
          break;
      }
      return res;
    }

    /// <summary>
    /// Change in value for a 1bp change in the futures quoted rate
    /// </summary>
    /// <returns>Change in value for a 1bp change in the quoted rate</returns>
    public double Pv01()
    {
      return -PointValue() * Contracts;
    }

    #endregion Methods

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
    /// <returns>The calculated margin</returns>
    public double Margin(double prevPrice)
    {
      return (ContractMarginValue(QuotedPrice) - ContractMarginValue(prevPrice))*Contracts;
    }

    /// <inheritdoc cref="Margin(double)" select="summary|remarks|returns" />
    public double Margin()
    {
      if( !PrevClosePrice.HasValue )
        throw new Exception("PrevClosePrice must be set to calculate the Margin.");
      return (ContractMarginValue(QuotedPrice) - ContractMarginValue(PrevClosePrice.Value)) * Contracts;
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
      return Math.Round(ContractModelMarginValue(price), 2, MidpointRounding.AwayFromZero);
    }

    /// <inheritdoc cref="ContractMarginValue(double)" />
    public double ContractMarginValue()
    {
      return ContractMarginValue(QuotedPrice);
    }

    /// <summary>
    /// Value of each futures contract for margin calculation (without any rounding)
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="ContractMarginValue(double)" />
    /// </remarks>
    /// <param name="price">Futures price</param>
    /// <returns>Futures contract value</returns>
    private double ContractModelMarginValue(double price)
    {
      switch (StirFuture.RateFutureType)
      {
        case RateFutureType.ASXBankBill:
          return StirFuture.ContractSize / (1.0 + (1.0 - price) * 90.0/365.0);
        case RateFutureType.TBill:
          return  1.0 - (1.0 - price) / StirFuture.TickSize * StirFuture.TickValue;
        case RateFutureType.ArithmeticAverageRate:
        case RateFutureType.GeometricAverageRate:
        case RateFutureType.MoneyMarketCashRate:
        default:
          // CME Eurodollars
          return StirFuture.ContractSize - (1.0 - price) * StirFuture.PointValue * 1e4;
      }
    }

    /// <summary>
    /// Margin value as a percentage of notional
    /// </summary>
    /// <param name="price">Futures price</param>
    /// <returns>Margin value as a percentage of notional</returns>
    public double PercentageMarginValue(double price)
    {
      return ContractMarginValue(price) / StirFuture.ContractSize;
    }

    /// <inheritdoc cref="PercentageMarginValue(double)" />
    public double PercentageMarginValue()
    {
      return PercentageMarginValue(QuotedPrice);
    }

    /// <summary>
    /// Value of a single tick per contract. For most futures this is a fixed amount.
    /// </summary>
    /// <returns>Value of a single tick</returns>
    public double TickValue()
    {
      if (StirFuture.RateFutureType == RateFutureType.ASXBankBill)
      {
        return ContractMarginValue(QuotedPrice) - ContractMarginValue(QuotedPrice-0.0001);
      }
      else
        return StirFuture.TickValue;
    }

    /// <summary>
    /// Value of a single point. This is the tick value divided by the tick size
    /// </summary>
    /// <returns>Value of a point</returns>
    public double PointValue()
    {
      return StirFuture.PointValue;
    }

    #endregion Margin Calculations

    #region Utility Methods

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <remarks>
    ///   <para>Because of the marking to the market mechanism the cashflows of a future are really daily settlements reflecting the 
    ///   changes in the market rate/price. The generated payment schedule is an equivalent one time interest payment that pays the value of the 
    ///   futures at maturity, i.e. the expected value of its payoff under the risk neutral measure.</para>
    /// </remarks>
    /// <param name="ps">payment schedule</param>
    /// <param name="from">Start date for generation of payments</param>
    /// <returns>Payment schedule for the ed future</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if( DiscountCurve == null || ReferenceCurve == null )
        throw new ArgumentException("Discount/Funding curve required");

      // For the time being, this is used only in the curve calibration and it is
      // intended to work for all the rate futures, including bank bill futures.
      var future = StirFuture;
      var accrualStart = StirFuture.DepositSettlement;
      var accrualEnd = StirFuture.DepositMaturity;
      var settlement = !StirFuture.SettlementDate.IsEmpty() ? StirFuture.SettlementDate : StirFuture.Maturity;
      if (from > settlement)
        return null;
      var flags = ProjectionFlag.MarkedToMarket;
      if (ApproximateForFastCalculation)
        flags |= ProjectionFlag.ApproximateProjection;
      var projectionType = ProjectionType.SimpleProjection;
      if (StirFuture.RateFutureType == RateFutureType.ArithmeticAverageRate)
        projectionType = ProjectionType.ArithmeticAverageRate;
      else if (StirFuture.RateFutureType == RateFutureType.GeometricAverageRate)
        projectionType = ProjectionType.GeometricAverageRate;
      var projParams = new ProjectionParams { ProjectionType = projectionType, ProjectionFlags = flags};
      var rateProjector = CouponCalculator.Get(AsOf, StirFuture.ReferenceIndex, ReferenceCurve, DiscountCurve, projParams);

      // Get convexity adjustent. No convexty adjustment for bank bill futures because they are marked
      // to the present value, not to the future rates.
      var forwardAdjustment = (future.RateFutureType == RateFutureType.TBill || future.RateFutureType == RateFutureType.ASXBankBill)
        ? null : ForwardAdjustment.Get(AsOf, DiscountCurve, RateModelParameters, projParams);

      if (ps == null)
        ps = new PaymentSchedule();
      else
        ps.Clear();
      var ip = new FloatingInterestPayment(Dt.Empty, settlement, StirFuture.Ccy, accrualStart,
                                           accrualEnd, accrualStart, accrualEnd, Dt.Empty, 1.0, 0.0,
                                           future.ReferenceIndex.DayCount, Frequency.None, CompoundingConvention.None, rateProjector,
                                           forwardAdjustment) { AccrueOnCycle = true };
      ps.AddPayment(ip);
      return ps;
    }

    #endregion Utility Methods
  }
}
