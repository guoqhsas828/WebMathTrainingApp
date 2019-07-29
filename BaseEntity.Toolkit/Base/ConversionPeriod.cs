/*
 * ConversionPeriod.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Base
{
  ///
	/// <summary>
	///   Definition for an element of a Convertable bond
	///   conversion schedule.
	/// </summary>
	///
	/// <remarks>
	///   This class defines the conversion schedule of a Convertable bond.
	/// </remarks>
	///
  [Serializable]
  public class ConversionPeriod : BaseEntityObject, IOptionPeriod
  {
    #region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
    protected ConversionPeriod()
		{}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="startDate">start of conversion period</param>
    /// <param name="endDate">end of conversion period</param>
    /// <param name="percent">Percentage of outstanding amount</param>
    /// <param name="price">Conversion price</param>
    /// <param name="trigger">Trigger</param>
    /// <param name="additionalPayment">additional payment</param>
    /// <param name="notificationDate">Underlying option exercise date</param>
    ///
    public
    ConversionPeriod(Dt startDate, Dt endDate, double percent, double price,
										 double trigger, double additionalPayment,
                     Dt notificationDate = new Dt())
		{
			// Use properties to get validation
			StartDate = startDate;
			EndDate = endDate;
			Percent = percent;
			ConversionPrice = price;
			TriggerPrice = trigger;
			AdditionalPayment = additionalPayment;
		  NotificationDate = notificationDate;

			Validate();
    }

    #endregion Constructors

    #region Methods

		/// <summary>
		///   Validate
		/// </summary>
    public override void Validate(ArrayList errors)
		{
      if (!startDate_.IsValid())
        InvalidValue.AddError(errors, this, "StartDate", String.Format("Invalid start date {0}", startDate_));
      if (!endDate_.IsValid())
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Invalid end date {0}", endDate_));
      if (startDate_ >= endDate_)
        InvalidValue.AddError(errors, this, "EndDate", String.Format("Start date {0} must be before end date {1}", startDate_, endDate_));
   		if( (percent_ < 0.0) || (percent_ > 1.0) )
        InvalidValue.AddError(errors, this, "Percent", String.Format("Invalid percentage {0}. Must be between 0 and 1", percent_));
			if( price_ <= 0.0 )
				InvalidValue.AddError(errors, this, "ConversionPrice", String.Format("Invalid price {0}. Must be +ve", price_));
			if( trigger_ <= 0.0 )
				InvalidValue.AddError(errors, this, "Trigger", String.Format("Invalid trigger {0}. Must be +ve", trigger_));
			if( additionalPayment_ <= 0.0 )
				InvalidValue.AddError(errors, this, "AdditionalPayment", String.Format("Invalid additional payment {0}. Must be +ve", additionalPayment_));

		  return;
		}

    #endregion Methods

    #region Properties

		/// <summary>
		///   Start of period
		/// </summary>
		public Dt StartDate
		{
			get { return startDate_; }
			set { startDate_ = value; }
		}

		/// <summary>
		///   End date of period
		/// </summary>
		public Dt EndDate
		{
			get { return endDate_; }
			set { endDate_ = value; }
		}
    
    /// <summary>
		///   Percentage of amount outstanding
		/// </summary>
		public double Percent
		{
			get { return percent_; }
			set { percent_ = value; }
		}
    
		/// <summary>
		///   Conversion price
		/// </summary>
		public double ConversionPrice
		{
			get { return price_; }
			set { price_ = value; }
		}
    
		/// <summary>
		///   Trigger
		/// </summary>
		public double TriggerPrice
		{
			get { return trigger_; }
			set { trigger_ = value; }
		}
    
		/// <summary>
		/// Additional Payments
		/// </summary>
		public double AdditionalPayment
		{
			get { return additionalPayment_; }
			set { additionalPayment_ = value; }
    }

    /// <summary>
    /// Notification date (Exercise date)
    /// </summary>
    public Dt NotificationDate { get; set; }

    #endregion Properties

    #region Data

		private Dt startDate_;
		private Dt endDate_;
		private double percent_;
		private double price_;
		private double trigger_;
		private double additionalPayment_;

    #endregion Data

    #region IPeriod Members

    bool IPeriod.ExclusiveEnd
    {
      get { return true; }
    }

    int IComparable<IPeriod>.CompareTo(IPeriod other)
    {
      return StartDate.CompareTo(other.StartDate);
    }
    #endregion

    #region IOptionPeriod Members

    double IOptionPeriod.ExercisePrice
    {
      get { return ConversionPrice; }
    }

    OptionStyle IOptionPeriod.Style
    {
      get { return OptionStyle.European; }
    }

    OptionType IOptionPeriod.Type
    {
      get { return OptionType.Call; }
    }

    #endregion

  } // ConversionPeriod

}
