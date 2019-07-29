using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.Note">Note</see>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Note" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Note">Note</seealso>
  [Serializable]
  public class NotePricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="note">Note to be priced</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    public NotePricer(Note note, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve)
      : base(note, asOf, settle)
    {
      DiscountCurve = discountCurve;
      Notional = notional;
    }

    #endregion Constructors

    #region Methods

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
      base.Validate(errors);
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
    }

    /// <summary>
    /// Generates payment schedule for the note
    /// </summary>
    /// <param name="ps">Payment schedule object</param>
    /// <param name="from">Start date for inclusion of payment</param>
    /// <returns>A payment schedule object</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (from > Note.Maturity)
        return null;
      if (ps == null)
        ps = new PaymentSchedule();
      else
        ps.Clear();
      Dt maturity = Dt.Roll(Note.Maturity, Note.BDConvention, Note.Calendar);
      if (Note.CompoundFreq == Frequency.Continuous)
      {
        var amt = Note.Notional * RateCalc.PriceFromRate(Note.ZeroCoupon, Note.Effective, Note.Maturity, Note.DayCount, Note.CompoundFreq) /
                  RateCalc.PriceFromRate(Note.Coupon, Note.Effective, Note.Maturity, Note.DayCount, Note.CompoundFreq);
        ps.AddPayment(new PrincipalExchange(maturity, amt, Note.Ccy));
      }
      else
      {
        var ip = new FixedInterestPayment(Note.Effective, maturity, Note.Ccy, Dt.Empty, Dt.Empty, Settle, maturity, Dt.Empty,
                                          Note.Notional, Note.Coupon, Note.DayCount, Note.CompoundFreq)
                   {AccrualFactor = Dt.Years(Settle, maturity, Note.DayCount)};
        var pe = new PrincipalExchange(maturity, Note.Notional, Note.Ccy);
        ps.AddPayment(ip);
        ps.AddPayment(pe);
      }
      return ps;
    }


    /// <summary>
    ///  Present value of a  forward rate agreement
    ///  </summary>
    ///  <returns>The present value of a money market instrument on XIbor rates</returns>
    public override double ProductPv()
    {
      PaymentSchedule paymentSchedule = GetPaymentSchedule(null, AsOf);
      double pv = 0.0;
      foreach (Payment p in paymentSchedule)
      {
        var ip = p as InterestPayment;
        if (ip != null)
          ip.FixedCoupon = Note.Coupon; //update coupon
        pv += p.DomesticAmount*DiscountCurve.Interpolate(AsOf, p.PayDt);
      }
      return pv;
    }

    /// <summary>
    /// Computes the par coupon of a Note 
    /// </summary>
    /// <returns>The par coupon of a note</returns>
    public double ParCoupon()
    {
      RateResetState state;
      return ForwardRateCalculator.CalculateForwardRate(AsOf, Settle, Settle, Dt.Roll(Note.Maturity, Note.BDConvention, Note.Calendar),
        DiscountCurve, Note.DayCount, Note.Freq, null, false, out state);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Accessor for the discount factor
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Accessor for the discount factor
    /// </summary>
    public Note Note
    {
      get { return (Note) Product; }
    }

    #endregion Properties
  }

  [Serializable]
  internal class DefaultableNotePricer : NotePricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="note">Note to be priced</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    public DefaultableNotePricer(Note note, Dt asOf, Dt settle, double notional, 
      DiscountCurve discountCurve, SurvivalCurve survivalCurve, double recoveryRate)
      : base(note, asOf, settle, notional, discountCurve)
    {
      SurvivalCurve = survivalCurve;
      RecoveryRate = recoveryRate;
    }

    #endregion Constructors

    #region Methods

    #region Overrides of NotePricer

    /// <summary>
    ///  Present value of a  forward rate agreement
    ///  </summary>
    ///  <returns>The present value of a money market instrument on XIbor rates</returns>
    public override double ProductPv()
    {
      Dt asOf = AsOf, maturity = Note.Maturity;
      var sc = SurvivalCurve;
      var endSp = sc == null ? 1.0 : sc.SurvivalProb(asOf, maturity);
      var paymentSchedule = GetPaymentSchedule(null, asOf);
      double pv = 0.0;
      foreach (var p in paymentSchedule)
      {
        var payDt = p.PayDt;
        var sp = endSp;
        if (sc != null && payDt != Note.Maturity)
        {
          sp = sc.SurvivalProb(asOf, payDt);
        }
        var ip = p as InterestPayment;
        if (ip != null)
          ip.FixedCoupon = Note.Coupon; //update coupon
        else if (p is PrincipalExchange)
          sp += RecoveryRate*(1 - sp);
        pv += sp*p.DomesticAmount*DiscountCurve.Interpolate(asOf, payDt);
      }
      return pv;
    }

    #endregion

    #endregion

    #region Properties
    public SurvivalCurve SurvivalCurve { get; private set; }
    public double RecoveryRate { get; private set; }
    #endregion
  }
}