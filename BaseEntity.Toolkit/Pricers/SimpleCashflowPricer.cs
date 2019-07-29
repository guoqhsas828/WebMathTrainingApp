/*
 * SimpleCashflowPricer.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{

  ///
	/// <summary>
	///   A simple cashflow pricer
	/// </summary>
	///
	/// <remarks>
	///   <para>A simple cashflow is an array of dates coupled with
  ///   fee, accrued, loss, balance, year fraction and premium on
  ///   that date.
	///   A simple cashflow pricer calculates prices based on these
  ///   information.</para>
	/// </remarks>
	///
	[Serializable]
  public class SimpleCashflowPricer : PricerBase, IPricer, ICollection
  {
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(SimpleCashflowPricer));

		#region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
		public SimpleCashflowPricer( IProduct product,
																 Dt asOf,
																 Dt settle,
																 DiscountCurve discountCurve,
																 DiscountCurve referenceCurve )
			: base(product, asOf, settle)
		{
		  data_ = new ArrayList();
			this.DiscountCurve = discountCurve;
			this.referenceCurve_ = referenceCurve;
		}

		#endregion Constructors

		#region Methods


    /// <summary>
    /// Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="paymentSchedule"></param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      if(paymentSchedule == null)
        paymentSchedule = new PaymentSchedule();

      int start = FindStartIndex(from, 0);
      int stop = Count;
      
      for (int i = start; i < stop; ++i)
      {
        DateNode current = this[i];
        if (!current.Fee.AlmostEquals(0.0))
        {
          var payment = new BasicPayment(current.Date, current.Fee, Product.Ccy);
          paymentSchedule.AddPayment(payment);
        }
      }
      return paymentSchedule; 
    }

    /// <summary>
    /// Generate Cashflow nodes for AMC from PaymentSchedule
    /// </summary>
    /// <param name="scalingFactor"></param>
    /// <returns></returns>
    public IList<ICashflowNode> ToCashflowNodeList(double scalingFactor = 1.0)
    {
      return GetPaymentSchedule(null, AsOf).ToCashflowNodeList(scalingFactor, 1, DiscountCurve, null, null);
    }

    /// <summary>
    ///   Calculate Pv
    /// </summary>
    public override double ProductPv()
		{
			double pv = Pv( this.AsOf, this.Settle, this.Product.Maturity );
			pv *= this.Notional;
			return pv;
		}

    /// <summary>
    ///   Calculate Pv
    /// </summary>
    public double ProtectionPv()
		{
			double pv = ProtectionPv( this.AsOf, this.Settle, this.Product.Maturity );
			pv *= this.Notional;
			return pv;
		}

    /// <summary>
    ///   Calculate Pv
    /// </summary>
    public double
		FeePv()
		{
			double pv = FeePv( this.AsOf, this.Settle, this.Product.Maturity );
			pv *= this.Notional;
			return pv;
		}

    /// <summary>
    ///   Return IEnumerator for date nodes.
    /// </summary>
    public IEnumerator
    GetEnumerator()
    {
      return data_.GetEnumerator();
    }

		/// <summary>
		///   Add a date node
		/// </summary>
		public void Add( Dt date )
		{
		  data_.Add( new DateNode(date) );
		}

		/// <summary>
		///   Add a date node
		/// </summary>
		public void Add( Dt date, double fee, double accrued, double loss )
		{
		  data_.Add( new DateNode(date, fee, accrued, loss) );
		}

		/// <summary>
		///   Add a date node
		/// </summary>
		public void Add( Dt date, double fee, double accrued, double loss,
										 double balance, double fraction, double premium )
		{
		  data_.Add( new DateNode(date, fee, accrued, loss, balance, fraction, premium) );
		}

		#endregion Methods

		#region Properties

    /// <summary>
    ///   number of date nodes
    /// </summary>
    public int Count
    {
      get { return data_.Count; }
    }

    /// <summary>
    ///   Get date node by index
    /// </summary>
    public DateNode this[ int index ]
    {
      get { return (DateNode)data_[index]; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
		public DiscountCurve DiscountCurve
		{
			get { return discountCurve_; }
			set {
			  // allow null curve, meaning no discount (zero discount rate)
			  discountCurve_ = value;
			}
		}

    #endregion Properties

		#region ICollection Members

    /// <exclude />
		public bool IsSynchronized
		{
			get	{	return false;	}
		}

		/// <exclude />
		public object SyncRoot
		{
			get {	return null;}
		}

		/// <exclude />
		public void CopyTo(Array array, int index)
		{
			for( int i = 0; i < Count; i++ ) 
			{
				array.SetValue(data_[i], index + i);
			}
		}

		#endregion ICollection Members

		#region Data

    private ArrayList data_;
    private DiscountCurve discountCurve_;
		private DiscountCurve referenceCurve_;

    #endregion Data

    #region DateNode

    /// <summary>
		///   Class representing a snapshot of cashflow at a date
		/// </summary>
		[Serializable]
		public class DateNode {
			/// <summary>
			///   Constructor
			/// </summary>
			public DateNode( Dt date )
			{
			  Date = date;
				Fee = Accrued = Loss = 0.0;
				Balance = Fraction = Premium = 0.0;
			}

			/// <summary>
			///   Constructor
			/// </summary>
			public DateNode( Dt date, double fee, double accrued, double loss )
			{
			  Date = date;
				Fee = fee;
				Accrued = accrued;
				Loss = loss;
				Balance = Fraction = Premium = 0.0;
			}

			/// <summary>
			///   Constructor
			/// </summary>
			public DateNode( Dt date, double fee, double accrued, double loss,
											 double balance, double fraction, double premium )
			{
			  Date = date;
				Fee = fee;
				Accrued = accrued;
				Loss = loss;
				Balance = balance;
				Fraction = fraction;
				Premium = premium;
			}

			/// <summary>Date</summary>
			public Dt Date;
			/// <summary>Fee</summary>
			public double Fee;
			/// <summary>Accrued</summary>
			public double Accrued;
			/// <summary>Loss</summary>
			public double Loss;
			/// <summary>Balance</summary>
			public double Balance;
			/// <summary>Year fraction</summary>
			public double Fraction;
			/// <summary>Premium</summary>
			public double Premium;
		};

		#endregion DateNode

		#region Helpers

		private double Pv( Dt asOf, Dt settle, Dt maturity )
		{
		  return CalcPrice( asOf, settle, maturity, true, true );
		}

		private double FeePv( Dt asOf, Dt settle, Dt maturity )
		{
		  return CalcPrice( asOf, settle, maturity, true, false );
		}

		private double ProtectionPv( Dt asOf, Dt settle, Dt maturity )
		{
		  return CalcPrice( asOf, settle, maturity, false, true );
		}

    /// <summary>
    /// Calculate price of cashflows
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="maturity"></param>
    /// <param name="includeFee"></param>
    /// <param name="includeProtection"></param>
    /// <returns></returns>
		protected virtual double CalcPrice( Dt asOf, Dt settle, Dt maturity,
															bool includeFee, bool includeProtection )
		{
		  DiscountCurve dc = DiscountCurve;
		  int start = FindStartIndex( settle, 0 );
			int stop = FindStopIndex( maturity, start);

		  double pv = 0;

    	for( int i = start; i < stop; ++i )
		  {
			  DateNode current = this[i];
				double df = ( dc == null ? 1.0 : dc.DiscountFactor(current.Date) );
				if( includeFee && current.Fee != 0.0 )
					pv += (current.Fee + current.Accrued) * df;
				if( includeProtection && current.Loss != 0.0 )
				  pv += current.Loss * df;
		  }
			if( dc != null )
				pv /= dc.DiscountFactor( asOf );

			return pv;
		}

    /// <summary>
    /// Find index of first cashflow to consider
    /// </summary>
    /// <param name="date"></param>
    /// <param name="start"></param>
    /// <returns></returns>
		protected int FindStartIndex( Dt date, int start )
		{
		  int N = Count;
			for( int i = start; i < N; ++i )
				if( Dt.Cmp( this[i].Date, date ) >= 0 )
				  return i;
			return N;
		}

    /// <summary>
    /// Find index of last cashflow to consider
    /// </summary>
    /// <param name="date"></param>
    /// <param name="start"></param>
    /// <returns></returns>
    protected int FindStopIndex(Dt date, int start)
		{
		  int N = Count;
			for( int i = start; i < N; ++i )
				if( Dt.Cmp( this[i].Date, date ) > 0 )
				  return i;
			return N;
		}

		#endregion Helpers

    
  } // SimpleCashflowPricer

  /// <summary>
  /// Simple cashflow pricer for one-time fee valuation.
  /// </summary>
  [Serializable]
  public class OneTimeFeePricer : SimpleCashflowPricer
  {
    //It seems the way SimpleCashflowPricer calculates the Pv is wrong, 
    //this is just a cautious way to fix the Fee Pricer without affecting the other products using SimpleCashflowPricer for payment pv calculation
		#region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public OneTimeFeePricer(IProduct product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      Dt feeSettle, double feeAmount)
      : base(product, asOf, settle, discountCurve, referenceCurve)
    {
      Add(feeSettle, feeAmount, 0.0, 0.0);
    }

    #endregion Constructors

    /// <summary>
    ///   Calculate Pv
    /// </summary>
    public override double ProductPv()
    {
      var feePayDt = Count > 0 ? this[0].Date : Dt.Empty;
      return feePayDt > Settle ? base.ProductPv() : 0.0;
    }
  }
}
