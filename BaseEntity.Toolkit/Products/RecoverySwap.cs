/*
 * RecoverySwap.cs
 *
 */

using System;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  ///   Recovery Swap Product
  /// </summary>
  /// <remarks>
  ///   <para>Credit recovery swaps are OTC contracts which pay out only in the case of default
  ///   when the seller pays according to the promised recovery and receives based on the
  ///   realized recovery. There are two legs - a fixed leg (paying he series of premium payments)
  ///   and a floating leg (the loss paid by the protection seller on default).</para>
  ///   <h1 align="center"><img src="RecoverySwap.png"/></h1>
  ///   <para>The recovery swap effectively locks in a recovery rate in the event of default.
  ///   Recovery swaps are quoted in terms of a recovery strike level (eg 35%).</para>
  ///   <para>When trading a at-the-money recovery swap, a recovery strike is set
  ///   for no up front payment.</para>
  ///   <para>The buyer of the recovery swap pays the srike and receives or buys the
  ///   deliverable bond.</para>
  /// 
  ///   <para><b>Example recovery swap</b></para>
  ///   <para>An example of a typical recovery swap is:</para>
  ///   <list type="bullet">
  ///     <item><description>5 year at-market recovery swap on Goldman Sachs.</description></item>
  ///     <item><description>$100m notional</description></item>
  ///     <item><description>Reference asset is specific bond A.</description></item>
  ///     <item><description>Strike 35%</description></item>
  ///   </list>
  ///   <para>The protection buyer pays no fee to enter the contract.</para>
  ///   <para>If a credit event occurs, the protection seller surrender the bonds to the seller
  ///    and the seller pay the promised recovery (35%).</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.RecoverySwapPricer">Recovery Swap Pricer</seealso>
	///
	/// <example>
	/// <para>The following example demonstrates constructing a Credit Recovery Swap.</para>
	/// <code language="C#">
	///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
	///   Dt maturity = Dt.CDSMaturity(effectiveDate, 5, TimeUnit.Years); // Maturity date is standard 5Yr CDS maturity
	///
	///   RecoverySwap recoverySwap =
	///     new RecoverySwap( effectiveDate,                          // Effective date
	///                       maturityDate,                           // Maturity date
	///                       Currency.EUR,                           // Currency is Euros
	///                       DayCount.Actual360,                     // Acrual Daycount is Actual/360
	///                       Calendar.TGT,                           // Calendar is Target
	///                       0.4                                     // Promised recovery rate
	///                     );
	///
	///   // Alternate method of creating standard 5Yr Recovery Swap.
	///   CDS recoverySwap2 =
	///     new RecoverySwap( effectiveDate,                          // Effective date
	///                       5,                                      // Standard 5 Year maturity
	///                       Currency.EUR,                           // Currency is Euros
	///                       0.4,                                    // Promised recovery rate
	///                     );
  /// </code>
	/// </example>
	///
  [Serializable]
	[ReadOnly(true)]
  public class RecoverySwap : Product
	{
		#region Constructors

    /// <summary>
		///   Default Constructor
		/// </summary>
    protected
		RecoverySwap()
		{
		}
    
    /// <summary>
		///   Constructor for a standard CDS based on years to maturity
		/// </summary>
		///
		/// <remarks>
		///   <para>Standard terms include:</para>
		///   <list type="bullet">
		///     <item><description>Premium DayCount of Actual360.</description></item>
		///     <item><description>Premium payment frequency of Quarterly.</description></item>
		///     <item><description>Premium payment business day convention of Following.</description></item>
		///     <item><description>The calendar is None.</description></item>
		///   </list>
		/// </remarks>
		///
		/// <param name="effective">Effective date (date premium started accruing) </param>
		/// <param name="maturityInYears">Years to maturity or scheduled termination date from the effective date (eg 5)</param>
		/// <param name="ccy">Currency of premium and recovery payments</param>
		/// <param name="recovery">Annualised premium in percent (0.02 = 200bp)</param>
		///
		public
		RecoverySwap(Dt effective, int maturityInYears, Currency ccy, double recovery )
			: base(effective, Dt.CDSMaturity(effective, new Tenor(maturityInYears, TimeUnit.Years)), ccy)
		{
			// Use properties for assignment for data validation
			RecoveryRate = recovery;
			DayCount = DayCount.Actual360;
			Freq = Frequency.Quarterly;
			BDConvention = BDConvention.Following;
			Calendar = Calendar.None;
		}
    
    /// <summary>
		///   Constructor for a standard CDS based on maturity tenor
		/// </summary>
		///
		/// <remarks>
		///   <para>Standard terms include:</para>
		///   <list type="bullet">
		///     <item><description>Premium DayCount of Actual360.</description></item>
		///     <item><description>Premium payment frequency of Quarterly.</description></item>
		///     <item><description>Premium payment business day convention of Following.</description></item>
		///     <item><description>The calendar is None.</description></item>
		///   </list>
		/// </remarks>
		///
		/// <param name="effective">Effective date (date premium started accruing) </param>
		/// <param name="maturityTenor">Maturity or scheduled temination tenor from the effective date</param>
		/// <param name="ccy">Currency of premium and recovery payments</param>
		/// <param name="recovery">Annualised premium in percent (0.02 = 200bp)</param>
		///
		public
		RecoverySwap(Dt effective, Tenor maturityTenor, Currency ccy, double recovery )
			: base(effective, Dt.CDSMaturity(effective, maturityTenor), ccy)
		{
			// Use properties for assignment for data validation
			RecoveryRate = recovery;
			DayCount = DayCount.Actual360;
			Freq = Frequency.Quarterly;
			BDConvention = BDConvention.Following;
			Calendar = Calendar.None;
		}

    /// <summary>
    ///   Constructor
		/// </summary>
		///
		/// <param name="effective">Effective date (date premium started accruing) </param>
		/// <param name="maturity">Maturity or scheduled temination date</param>
		/// <param name="ccy">Currency of premium and recovery payments</param>
		/// <param name="recovery">Annualised premium in percent (0.02 = 200bp)</param>
		///
		public
		RecoverySwap(Dt effective, Dt maturity, Currency ccy, double recovery )
			: base(effective, maturity, ccy)
		{
			// Use properties for assignment for data validation
			RecoveryRate = recovery;
			DayCount = DayCount.Actual360;
			Freq = Frequency.Quarterly;
			BDConvention = BDConvention.Following;
			Calendar = Calendar.None;
		}
    
    #endregion Constructors

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

      if (recovery_ < 0.0 || recovery_ > 1.0)
        InvalidValue.AddError(errors, this, "Recovery", String.Format("Invalid recovery. Must be between 0 and 1, Not {0}", recovery_));

      if (fee_ < -1.0 || fee_ > 1.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between -1 and 1, Not {0}", fee_));

      // Fee settle has to be a valid date 
      if (!feeSettle_.IsEmpty() && !feeSettle_.IsValid())
        InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Invalid fee settlement date. Must be empty or valid date, not {0}", feeSettle_));
      
      return;
    }


		#endregion Methods

		#region Properties

    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
		[Category("Base")]
		public double RecoveryRate
		{
			get { return recovery_; }
			set 
      {
			  recovery_ = value;
			}
		}
    
    /// <summary>
    ///   Up-front fee in percent.
    /// </summary>
		[Category("Base")]
		public double Fee
		{
			get { return fee_; }
			set 
      {
				fee_ = value;
			}
		}
    
    /// <summary>
    ///   Fee payment date
    /// </summary>
		[Category("Base")]
		public Dt FeeSettle
		{
			get { return feeSettle_; }
			set 
      {
				feeSettle_ = value;
			}
		}
    
    /// <summary>
    ///   Daycount of premium
    /// </summary>
		[Category("Base")]
		public DayCount DayCount
		{
			get { return dayCount_; }
			set { dayCount_ = value; }
		}
    
    /// <summary>
    ///   Premium payment frequency (per year)
    /// </summary>
		[Category("Base")]
		public Frequency Freq
		{
			get { return freq_; }
			set { freq_ = value; }
		}

    /// <summary>
    ///   Calendar for premium payment schedule
    /// </summary>
		[Category("Base")]
		public Calendar Calendar
		{
			get { return cal_; }
			set { cal_ = value; }
		}
    
    /// <summary>
    ///   Roll convention for premium payment schedule
    /// </summary>
		[Category("Base")]
		public BDConvention BDConvention
		{
			get { return roll_; }
			set { roll_ = value; }
		}

		#endregion Properties

		#region Data

		private double fee_;
		private Dt feeSettle_;
		private double recovery_;
		private DayCount dayCount_;
		private Frequency freq_;
		private Calendar cal_;
		private BDConvention roll_;

		#endregion Data

	} // class CDS
}
