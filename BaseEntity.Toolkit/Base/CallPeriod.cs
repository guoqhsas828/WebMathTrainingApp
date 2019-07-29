/*
 * CallPeriod.cs
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

	///
  /// <summary>
	///   Definition for an element of a call schedule.
	/// </summary>
	///
	/// <remarks>
	///   <para>This class defines the call schedule of a fixed income security such
  ///   as a Bond or a swap.</para>
	/// 
  ///   <para>CallPeriod is an immutable class.</para>
  /// </remarks>
	///
  [Serializable]
  public class CallPeriod : BaseEntityObject, IOptionPeriod, IComparable
  {
		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		protected CallPeriod()
		{}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="startDate">start of call period (inclusive)</param>
    /// <param name="endDate">end of call period (inclusive)</param>
    /// <param name="price">Call price</param>
    /// <param name="trigger">Call protect trigger</param>
    /// <param name="style">Option style (American, Bermudan, or European)</param>
    /// <param name="grace">Call protect days grace</param>
    ///
    public CallPeriod(Dt startDate, Dt endDate, double price,
               double trigger, OptionStyle style, int grace)
    {
      startDate_ = startDate;
      endDate_ = endDate;
      price_ = price;
      trigger_ = trigger;
      style_ = style;
      grace_ = grace;
      return;
    }

	  /// <summary>
	  /// Create the call period from notification dates
	  /// </summary>
	  /// <param name="notificationDate">Notification dates</param>
	  /// <param name="price">Call price</param>
	  /// <param name="trigger">Call protect trigger</param>
	  /// <param name="style">Option style(American, Bermudan, or European)</param>
	  /// <param name="grace">Call protect days grace</param>
	  /// <param name="underlyingStartDate">underlying start dates</param>
	  public CallPeriod(Dt notificationDate, double price,
	    double trigger, OptionStyle style, int grace, Dt underlyingStartDate)
	  {
      NotificationDate = notificationDate;
      startDate_ = underlyingStartDate;
	    endDate_ = underlyingStartDate;
	    price_ = price;
	    trigger_ = trigger;
	    style_ = style;
	    grace_ = grace;
	    return;
	  }

	  #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate things that cannot be tested in property set methods.
    /// </summary>
    public override void Validate(ArrayList errors)
		{
      if (!startDate_.IsValid())
        InvalidValue.AddError(errors, this, "StartDate", String.Format("Invalid start date {0}", startDate_));
      if (!endDate_.IsValid())
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Invalid end date {0}", endDate_));
      if (startDate_ > endDate_)
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Start date {0} cannot be after end date {1}", startDate_, endDate_));
			if( price_ < 0.0 )
				InvalidValue.AddError(errors, this, "CallPrice", String.Format("Invalid price {0}. Cannot be negative", price_));
			if( trigger_ < 0.0 )
				InvalidValue.AddError(errors, this, "TriggerPrice", String.Format("Invalid trigger {0}. Cannot be negative", trigger_));
      if (style_ == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", "Invalid option style. Must not be None");
			if( grace_ < 0.0 )
				InvalidValue.AddError(errors, this, "Grace", String.Format("Invalid grace period {0}. Must be +ve", grace_));

		  return;
		}

		#endregion Methods

		#region Properties

		/// <summary>
		///   Start of call period
		/// </summary>
    [DtProperty]
    public Dt StartDate
		{
			get { return startDate_; }
		}
    
    /// <summary>
    ///   End of call period
    /// </summary>
    [DtProperty]
    public Dt EndDate
		{
			get { return endDate_; }
		}
    
    /// <summary>
    ///   Call price
    /// </summary>
    [NumericProperty]
    public double CallPrice
		{
			get { return price_; }
		}
    
    /// <summary>
    ///   Trigger price
    /// </summary>
    [NumericProperty]
    public double TriggerPrice
		{
			get { return trigger_; }
		}
    
		/// <summary>
		///   Option style
		/// </summary>
    [EnumProperty]
    public OptionStyle Style
		{
			get { return style_; }
		}
    
		/// <summary>
		///   Call protection days grace
		/// </summary>
    [NumericProperty]
    public int Grace
		{
			get { return grace_; }
		}

    /// <summary>
    /// Notification date
    /// </summary>
	  public Dt NotificationDate { get; set; }

		#endregion Properties

    #region IComparable Members

    /// <summary>
    /// Compares 2 CallPeriods
    /// </summary>
    /// <param name="other">CallPeriod to compare to</param>
    /// <returns></returns>
    public int CompareTo(IPeriod other)
    {
      //check start dates
      return Dt.Cmp(this.StartDate, other.StartDate);
    }

    ///<summary>
    ///</summary>
    ///<param name="other"></param>
    ///<returns></returns>
    public int CompareTo(object other)
    {
      if ((other is IPeriod) == false)
        throw new ArgumentException("Compare to non-IPeriod type object not supported");

      return CompareTo((IPeriod)other);
    }

	  #endregion IComparable Methods

		#region Data

    private readonly Dt startDate_;
    private readonly Dt endDate_;
    private readonly double price_ = 1;
    private readonly double trigger_ = 1;
    private readonly OptionStyle style_ = OptionStyle.European;
    private readonly int grace_ = 1;

		#endregion Data

    #region IOptionPeriod Members

    double IOptionPeriod.ExercisePrice
    {
      get { return price_; }
    }

    OptionType IOptionPeriod.Type
    {
      get { return OptionType.Call; }
    }

    int IComparable<IPeriod>.CompareTo(IPeriod other)
    {
      return Dt.Cmp(this.StartDate, other.StartDate);
    }

    #endregion

   
    #region IPeriod Members

    bool IPeriod.ExclusiveEnd
    {
      get { return false; }
    }

    #endregion

  } // CallPeriod
}
