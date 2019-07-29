/*
 * CDSIndex.cs
 *
 *
 */

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{

  ///
	/// <summary>
	///   CDS Index basket
	/// </summary>
	///
	/// <remarks>
	///   <para>This represents a basket of uniform credit default swaps, typically
	///   the common CDS Indices such as CDX and Tracxx</para>
	/// </remarks>
	///
  [Obsolete("Replaced by CDX")]
  [Serializable]
	public class CDSIndex : CreditBasket
	{
		#region Constructors

		/// <summary>
		///   constructor
		/// </summary>
    public
		CDSIndex()
		{}


    /// <summary>
    ///   Constructor
		/// </summary>
		///
		/// <param name="effective">Effective date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="ccy">Currency</param>
		/// <param name="firstPrem">First premium payment date</param>
		/// <param name="premium">Annualised premium</param>
		/// <param name="dayCount">Daycount of premium</param>
		/// <param name="freq">Frequency of premium payment</param>
		/// <param name="roll">Coupon roll method (business day convention)</param>
		/// <param name="cal">Calendar for coupon rolls</param>
		///
		public
		CDSIndex( Dt effective, Dt maturity, Currency ccy, Dt firstPrem,
							double premium, DayCount dayCount, Frequency freq,
							BDConvention roll, Calendar cal )
		{
			// Use properties for assignment for data validation
			Effective = effective;
			Maturity = maturity;
			Ccy = ccy;
			FirstPrem = firstPrem;
			Premium = premium;
			DayCount = dayCount;
			Freq = freq;
			BDConvention = roll;
			Calendar = cal;
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object
		Clone()
		{
			return (CDSIndex)base.Clone();
		}

		#endregion // Constructors

		#region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid First Premium date
      if (!firstPrem_.IsEmpty() && !firstPrem_.IsValid())
        InvalidValue.AddError(errors, this, "FirstPrem", String.Format("Invalid first premium date. Must be empty or valid date, not {0}", firstPrem_));

      // Invalid Maturity date
      if (!maturity_.IsEmpty() && !maturity_.IsValid())
        InvalidValue.AddError(errors, this, "Maturity", String.Format("Invalid maturity date. Must be empty or valid date, not {0}", maturity_));


      // Invalid Effective date
      if (!effective_.IsEmpty() && !effective_.IsValid())
        InvalidValue.AddError(errors, this, "Effective", String.Format("Invalid effective date. Must be empty or valid date, not {0}", effective_));

      // Premium has to be within a [0.0 - 200.0] range
      if (premium_ < 0.0 || premium_ > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));

      // Invalid days to settle
      if (daysToSettle_ < 0 || daysToSettle_ > 365)
        InvalidValue.AddError(errors, this, "DaysToSettle", String.Format("Invalid Days to settlement {0}. Must be between 0 and 365", daysToSettle_));

      return;
    }


		/// <summary>
		///   Construct CDS matching the general pricing terms of the index
		/// </summary>
		public CDS
		CDS()
		{
			return new CDS(Effective, Maturity, Ccy, FirstPrem, Premium, DayCount,
										 Freq, BDConvention, Calendar);
		}


		/// <summary>
		///   Construct CDS matching specified credit and the terms of the basket.
		/// </summary>
		public CDS
		CDS(int index)
		{
			CDS cds = new CDS(Effective, Maturity, Ccy, FirstPrem, Premium, DayCount, Freq, BDConvention, Calendar);
			return cds;
		}

		#endregion Methods

		#region Properties

    /// <summary>
    ///  Annex date of index series
    /// </summary>
    public Dt AnnexDate
    {
      get { return annexDate_; }
      set { annexDate_ = value; }
    }

    /// <summary>
    ///   Index family (eg. CDX)
    /// </summary>
		public string Family
		{
			get { return family_; }
			set { family_ = value; }
		}


    /// <summary>
    ///   Series number
    /// </summary>
		public int Series
		{
			get { return series_; }
			set { series_ = value; }
		}


    /// <summary>
    ///   Version number
    /// </summary>
		public int Version
		{
			get { return version_; }
			set { version_ = value; }
		}


    /// <summary>
    ///   Product primary currency
    /// </summary>
		public Currency	Ccy
		{
			get { return ccy_; }
			set { ccy_ = value; }
		}


    /// <summary>
    ///   Effective date
    /// </summary>
		public Dt	Effective
		{
			get { return effective_; }
			set { effective_ = value; }
		}


    /// <summary>
    ///   Maturity or maturity date
    /// </summary>
		public Dt	Maturity
		{
			get { return maturity_; }
			set { maturity_ = value; }
		}


    /// <summary>
    ///   Days to settlement
    /// </summary>
		public int DaysToSettle
		{
			get { return daysToSettle_; }
      set {	daysToSettle_ = value; }
		}


    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
		public double Premium
		{
			get { return premium_; }
			set { premium_ = value; }
		}


    /// <summary>
    ///   Daycount of premium
    /// </summary>
		public DayCount DayCount
		{
			get { return dayCount_; }
			set { dayCount_ = value; }
		}


    /// <summary>
    ///   Premium payment frequency (per year)
    /// </summary>
		public Frequency Freq
		{
			get { return freq_; }
			set { freq_ = value; }
		}


    /// <summary>
    ///   First premium payment date
    /// </summary>
		public Dt FirstPrem
		{
			get { return firstPrem_; }
			set { firstPrem_ = value; }
		}


    /// <summary>
    ///   Calendar for premium payment schedule
    /// </summary>
		public Calendar Calendar
		{
			get { return cal_; }
			set { cal_ = value; }
		}


    /// <summary>
    ///   Roll convention for premium payment schedule
    /// </summary>
		public BDConvention BDConvention
		{
			get { return roll_; }
			set { roll_ = value; }
		}

    #endregion

		#region Data

    private Dt annexDate_;
		private string family_;
		private int series_;
		private int version_;

    private Currency ccy_;

    private Dt effective_;
    private Dt maturity_;
    private int daysToSettle_;

		private double premium_;
		private DayCount dayCount_;
		private Frequency freq_;
		private Dt firstPrem_;
		private Calendar cal_;
		private BDConvention roll_;

		#endregion

	} // CDSIndex
}
