/*
 * TRSPricer.cs
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="TRS">total return swap</see> using the value of
  ///   the underlying asset.</para>
  /// </summary>
  /// <details>
  ///   <para>At maturity, the total return swap pays:</para>
  ///   <math>(\mathrm{factor} \times min(\mathrm{Maximum}, Quote_T) - \mathrm{targetFactor} \times \mathrm{targetQuote}) \times \mathrm{Notional}</math>
  ///   <para>Where Quote can be full price, flat price, or yield.</para>
  ///   <para>The pricing formula is:</para>
  ///   <math>\mathrm{factor} \times DF_T \times E[min(\mathrm{Maximum}, Quote_T)] - DT_T \times \mathrm{targetFactor} \times \mathrm{targetQuote}</math>
  ///   <para>For a price based TRS this simplifies to:</para>
  ///   <math>\mathrm{factor} \times min(\mathrm{Maximum}, Quote_t) - DT_T \times \mathrm{targetFactor} \times \mathrm{targetQuote}</math>
  /// </details>
  /// <seealso cref="BaseEntity.Toolkit.Products.TRS">Total return swap</seealso>
  [Serializable]
  public class TRSPricer : PricerBase, IPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TRSPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rates are taken from the survival curve.</para>
    /// </remarks>
    ///
    /// <param name="product">TRS to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    ///
    public TRSPricer(
      TRS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve
      )
      : base(product, asOf, settle)
    {
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      TRSPricer obj = (TRSPricer)base.Clone();

      obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;
      obj.survivalCurve_ = (survivalCurve_ != null) ? (SurvivalCurve)survivalCurve_.Clone() : null;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Calculate the present value (<formula inline="true">Full price \times Notional</formula>) of the TRS
    /// </summary>
    ///
    /// <returns>Present value to the pricing as-of date of the TRS</returns>
    ///
    public override double
    ProductPv()
    {
      return this.Pv(this.ULPricer.Pv());
    }

    /// <summary>
    ///   Calculate the present value (<formula inline="true">Full price \times Notional</formula>) of the TRS
    /// </summary>
    ///
    /// <param name="fullPrice">Full price of TRS underlying</param>
    ///
    /// <returns>Present value to the pricing as-of date of the TRS</returns>
    ///
    public double
    Pv(double fullPrice)
    {
      // Already matured
      if( Dt.Cmp(this.AsOf, TRS.Maturity) > 0 )
        return 0.0;

      // Days to maturity
      if (Dt.Cmp(this.Settle, TRS.Maturity) >= 0)
        // Pricing where settlement on or after maturity so final payout set
        this.dtm_ = 0;
      else if (Dt.Cmp(this.Settle, TRS.Effective) < 0)
        // Pricing before issue date
        this.dtm_ = Dt.Diff(TRS.Effective, TRS.Maturity);
      else
        this.dtm_ = Dt.Diff(this.Settle, TRS.Maturity);

      // Discount factor
      double df = DiscountCurve.DiscountFactor(TRS.Maturity);
      df = RateCalc.PriceBump(df, this.FinSpread, DiscountCurve.AsOf, TRS.Maturity, this.FinSpreadDayCount, this.FinSpreadFreq);

      // Price
      double pv = 0.0;
      // TBD: Need to calculate Fwd and PV for case where Maximum exist
      // TBD: Need to test for UL only when we need it.
      switch (this.TRS.Type)
      {
        case TRSType.Flat:
          // flat price
          double flatPrice = fullPrice - this.ULPricer.Accrued();
          pv = TRS.Factor * Math.Min(TRS.Maximum, flatPrice) - TRS.TargetFactor * TRS.Target * df;
          break;
        case TRSType.Price:
          // full price
          pv = TRS.Factor * Math.Min(TRS.Maximum, fullPrice) - TRS.TargetFactor * TRS.Target * df;
          break;
        case TRSType.Yield:
          // yield
          double fwdCAYield = this.GetFwdYield(fullPrice);
          pv = TRS.Factor * df * Math.Min(TRS.Maximum, fwdCAYield) - TRS.TargetFactor * TRS.Target * df;
          break;
      }

      return pv * this.Notional;
    }

    /// <summary>
    ///   Reset the pricer
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Clear any internal state.</para>
    /// </remarks>
    ///
    public override void Reset()
    {
      ulPricer_ = null;
      return;
    }

    /// <summary>
    ///   Construct pricer for underlying product
    /// </summary>
    /// <returns>Pricer for underlying product</returns>
    private IPricer GetULPricer()
    {
      if( TRS.Underlying is Bond )
      {
        Bond bond = TRS.Underlying as Bond;
        BondPricer pricer = new BondPricer(bond, this.AsOf, this.Settle);
        pricer.DiscountCurve = this.DiscountCurve;
        pricer.SurvivalCurve = this.SurvivalCurve;
        if (bond.Index != null)
          throw new ArgumentException("Currently don't support floating rate bonds for TRS");
        return pricer;
      }
      else
        throw new ArgumentException("Only Bond TRS currently supported");
    }

    /// <summary>
    ///   Calculate forward (convexity adjusted) yield for underlying product
    /// </summary>
    ///
    /// <param name="fullPrice">Full price of TRS Underlying</param>
    ///
    /// <returns>Forward quote</returns>
    ///
    private double GetFwdYield(double fullPrice)
    {
      if( this.TRS.Underlying is Bond )
      {
        BondPricer pricer = (BondPricer)ULPricer;
        pricer.MarketQuote = fullPrice;
        pricer.QuotingConvention = QuotingConvention.FullPrice;
        return pricer.FwdYield(this.TRS.Maturity, this.YieldVolatility, this.CAMethod);
      }
      else
        throw new ArgumentException("Only TRS on Bonds currently supported");
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Product
    /// </summary>
    public TRS TRS
    {
      get { return (TRS)Product; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; this.Reset(); }
    }

    /// <summary>
    ///   Survival curve used for pricing
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set { survivalCurve_ = value; this.Reset(); }
    }

    /// <summary>
    ///   Financing Spread
    /// </summary>
    public double FinSpread
    {
      get { return finSpread_; }
      set { finSpread_ = value; }
    }

    /// <summary>
    ///   Daycount of financing spread
    /// </summary>
    public DayCount FinSpreadDayCount
    {
      get { return finSpreadDayCount_; }
      set { finSpreadDayCount_ = value; }
    }

    /// <summary>
    ///   Compounding frequency of financing spread
    /// </summary>
    public Frequency FinSpreadFreq
    {
      get { return finSpreadFreq_; }
      set { finSpreadFreq_ = value; }
    }

    /// <summary>
    ///   Yield volatility (required for yield base TRS)
    /// </summary>
    public double YieldVolatility
    {
      get { return yieldVol_; }
      set { yieldVol_ = value; }
    }

    /// <summary>
    ///   Yield convexity adjustment method (required for yield base TRS)
    /// </summary>
    public YieldCAMethod CAMethod
    {
      get { return caMethod_; }
      set { caMethod_ = value; }
    }

    /// <summary>
    ///   Days to maturity for pv calculation
    /// </summary>
    public int DaysToMaturity
    {
      get { return dtm_; }
    }

    /// <summary>
    ///   Pricer for underlying asset
    /// </summary>
    private IPricer ULPricer
    {
      get
      {
        if (ulPricer_ == null)
          ulPricer_ = this.GetULPricer();
        return ulPricer_;
      }
    }

    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private SurvivalCurve survivalCurve_;

    private double finSpread_;
    private DayCount finSpreadDayCount_;
    private Frequency finSpreadFreq_;
    private double yieldVol_;
    private YieldCAMethod caMethod_ =  YieldCAMethod.Hull;

    private int dtm_;
    private IPricer ulPricer_ = null;

    #endregion Data

  } // class TRSPricer
}