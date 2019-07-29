//
// BondFuture.cs
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Bond Futures
  /// </summary>
  /// <remarks>
  ///   <para>A bond future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   bond on a specified future expiration date at a predetermined price.</para>
  ///   <para>Bond futures come in two flavours - those quoted on a price (like CME TBond Futures, Euro-Bund Futures, etc)
  ///   and those quoted on an indexed yield (like ASX TBond Futures).</para>
  ///
  ///   <para><b>Price Quoted Futures</b></para>
  ///   <i>Deliverable Bonds</i>
  ///   <para>The bond purchased or sold can be selected from a pool of deliverable bonds.</para>
  ///   <i>Conversion factor</i>
  ///   <para>A conversion factor is used to 'normalise' the deliverable bonds and is the price factor such that
  ///   the yield to maturity of the bond on the delivery date of the futures contract equals the notional coupon
  ///   of the contract. The conversion factor times the futures price is the price of the bond delivered.</para>
  ///   <i>Cheapest to Deliver</i>
  ///   <para>The bond with the cheapest market price relative to the delivery price on expiration is the cheapest
  ///   to deliver bond. Prior to expiration, the cheapest to deliver bond is calculated as the bond that gives the
  ///   greatest return from buying and holding the bond and simultaneously selling the futures contract (cash and
  ///   carry).</para>
  ///   <i>Settlement</i>
  ///   <para>At expiration, the bond futures contract is settled at the flat price of the delivered bond
  ///   divided by the conversion factor for that bond.</para>
  ///
  ///   <para><b>Indexed Yield Quoted Futures</b></para>
  ///   <para>In this case the futures price is an indexed yield. There is no deliverable bond and no conversion
  ///   factors</para>
  ///   <i>Futures price</i>
  ///   <para>The quoted futures price is 100 - yield.</para>
  ///   <i>Margin</i>
  ///   <para>The margin is calculated from a futures value based on the price/yield formula for the underlying
  ///   bond. This results in a price per tick that is not constant but varies with the level of yield.</para>
  ///   <i>Settlement</i>
  ///   <para>At expiration the bond futures contract is settled (as for ASX AUD and NZD Bond Futures) at the average
  ///   yield of a specified basket of underlying bonds.</para>
  ///   <para>Common bond futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/ultra-tbond-futures.html">US Ultra Bond Future (CBT)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/us-treasury/30-year-us-treasury-bond.html">US Treasury Bond Future (CBT)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/us-treasury/10-year-us-treasury-note.html">US 10Yr Treasury Note Future (CBT)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/us-treasury/2-year-us-treasury-note.html">US 2Yr Treasury Note Future (CBT)</a></description></item>
  ///   <item><description><a href="http://www.m-x.ca/produits_taux_int_cgb_en.php">CGB 10Yr Bond Future (MSE)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/FGBL_en.html">Euro-Bund Future (EUX)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/FGBX_en.html">Euro-Buxl Future (EUX)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/FGBM_en.html">Euro-Bobl Future (EUX)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/FGBS_en.html">Euro-Schatz (Future EUX)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/CONF_en.html">Swiss Fed Bond CONF Future (EUX)</a></description></item>
  ///   <item><description><a href="http://www.eurexchange.com/trading/products/INT/FIX/FBTP_en.html">BTP Future (EUX)</a></description></item>
  ///   <item><description><a href="http://www.euronext.com/trader/contractspecifications/derivative/wide/contractspecifications-3640-EN.html?euronextCode=R-LON-FUT">Long Gilt Future (LIF)</a></description></item>
  ///   <item><description><a href="http://www.tse.or.jp/english/rules/derivatives/jgbf/index.html">Japan 10Yr Bond Future (TSE)</a></description></item>
  ///   <item><description><a href="http://eng.krx.co.kr/m3/m3_3/m3_3_3/m3_3_3_1/UHPENG03003_03_01.html">Korea 3Yr Bond Future (KRX)</a></description></item>
  ///   <item><description><a href="http://www.asx.net.au/products/australian-bond-futures-and-options.htm">Aus 10Yr Bond Future (AXS)</a></description></item>
  ///   <item><description><a href="http://www.asx.net.au/products/australian-bond-futures-and-options.htm">Aus 3Yr Bond Future (ASX)</a></description></item>
  ///   <item><description><a href="http://www.sfe.com.au/content/aboutsfe/brochures/016_z3and10.pdf">NZ 10Yr Bond Future (ASX)</a></description></item>
  ///   <item><description><a href="http://www.sfe.com.au/content/aboutsfe/brochures/016_z3and10.pdf">NZ 3Yr Bond Future (ASX)</a></description></item>
  ///   </list>
  ///
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <seealso cref="FutureBase"/>
  /// <example>
  /// <para>The following example demonstrates constructing a bond future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new BondFuture(
  ///    expirationDate,                          // Expiration
  ///    0.06,                                    // 6pc nominal coupon
  ///    10000,                                   // Contract size
  ///    0.01                                     // Tick size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class BondFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor for price quoted Bond Futures
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    /// The SettlementType is Physical. QuotingConvention is Price.</para>
    /// <para>The tick value defaults to the <paramref name="contractSize"/> * <paramref name="tickSize"/>.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="nominalCoupon">Nominal coupon of futures contract</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public BondFuture(Dt lastDeliveryDate, double nominalCoupon, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      NominalCoupon = nominalCoupon;
      QuotingConvention = FuturesQuotingConvention.Price;
      SettlementType = SettlementType.Physical;
    }

    /// <summary>
    /// Constructor for ASX Bond Futures
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    /// The SettlementType is Cash. QuotingConvention is IndexYield.</para>
    /// <para>The tick value defaults to the <paramref name="contractSize"/> * <paramref name="tickSize"/>.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="nominalCoupon">Nominal coupon of futures contract</param>
    /// <param name="nominalTerm">Nominal term of underlying bond</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public BondFuture(Dt lastDeliveryDate, double nominalCoupon, int nominalTerm, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      NominalCoupon = nominalCoupon;
      NominalTerm = nominalTerm;
      SettlementType = SettlementType.Cash;
      QuotingConvention = FuturesQuotingConvention.IndexYield;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (NominalCoupon <= 0 || NominalCoupon > 2.0)
        InvalidValue.AddError(errors, this, "NominalCoupon", "Invalid nominal coupon");
      if (NominalTerm < 0 || NominalTerm > 100)
        InvalidValue.AddError(errors, this, "NominalTerm", "Invalid nominal term");
    }

    /// <summary>
    /// Scaling factor for Point Value.
    /// </summary>
    /// <returns></returns>
    protected override double PointValueScalingFactor()
    {
      return 1e4; // 1e4 for IR products
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Nominal coupon of futures contract
    /// </summary>
    public double NominalCoupon { get; set; }

    /// <summary>
    /// Nominal term of underlying bond. Required for ASX bond futures only
    /// </summary>
    public int NominalTerm { get; set; }

    /// <summary>
    /// Futures quoting convention
    /// </summary>
    public FuturesQuotingConvention QuotingConvention { get; set; }

    #region Informational

    /// <summary>
    /// Future is quoted on a price
    /// </summary>
    public bool QuotedOnPrice { get { return QuotingConvention == FuturesQuotingConvention.Price; } }

    /// <summary>
    /// Future is quoted as an indexed yield
    /// </summary>
    public bool QuotedOnIndexedYield { get { return QuotingConvention == FuturesQuotingConvention.IndexYield; } }

    #endregion Informational

    #endregion Properties
  }
}
