/*
 * CouponPeriod.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
	///   Coupon period definition for a single coupon payment.
	/// </summary>
	///
	/// <remarks>
	///   <para>This class defines a coupon period of a step-up coupon schedule for
  ///   a fixed income security such as a Bond or a swap.</para>
  ///
  ///   <para>A coupon schedule is represented by a list of CouponPeriods (IList\langle CouponPeriod \rangle)
  ///   and utility methods are provided for standard calculations in
  ///   <see cref="CouponPeriodUtil"/></para>
  ///
  ///   <para>A coupon schedule defines the date that a coupon rate becomes effective. In other
  ///   words the coupon period date is the date the new coupon rate starts accruing and typically
  ///   the date the prior coupon pays.</para>
  ///
  ///   <para>CouponPeriod is an immutable class.</para>
	/// </remarks>
	///
  /// <seealso cref="CouponPeriodUtil"/>
	///
  [Component]
  [Serializable]
  public class CouponPeriod : BaseEntityObject, IDate, IComparable<CouponPeriod>
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		protected CouponPeriod()
		{}

    /// <summary>
    ///   Constructor
    /// </summary>
    public CouponPeriod(Dt date, double coupon)
    {
      date_ = date;
      coupon_ = coupon;
    }
    
		#endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate
    /// </summary>
    ///
    public override void Validate(ArrayList errors)
    {
      Validate(errors, true);
    }

    /// <summary>
    ///   Validate
    /// </summary>
    ///
    public void Validate(ArrayList errors, bool validateCoupon)
    {
      if (!date_.IsValid())
        InvalidValue.AddError(errors, this, "Date", $"Coupon end date {date_} is invalid");
      if (validateCoupon && (coupon_ < -2.0 || coupon_ > 2.0))
        InvalidValue.AddError(errors, this, "Coupon", $"Invalid Coupon. Must be between -2.0 and 2.0, not ({coupon_})");
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Coupon end date
    /// </summary>
    [DtProperty]
    public Dt Date
		{
			get { return date_; }
		}

    /// <summary>
    ///   Coupon
    /// </summary>
    [NumericProperty]
    public double Coupon
		{
			get { return coupon_; }
		}

    #endregion Properties

    #region IComparable<CouponPeriod> Members

    /// <summary>
    /// Compares 2 CallPeriods
    /// </summary>
    /// <param name="other">CouponPeriod to compare to</param>
    /// <returns></returns>
    public int CompareTo(CouponPeriod other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }

    int IComparable<IDate>.CompareTo(IDate other)
    {
      //check start dates
      return Dt.Cmp(this.Date, other.Date);
    }
    #endregion IComparable<CouponPeriod> Methods

		#region Data

		private readonly Dt date_;
		private readonly double coupon_;
		#endregion Data

  } // CouponPeriod
}
