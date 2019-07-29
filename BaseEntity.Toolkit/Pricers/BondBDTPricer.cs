/*
 * BondBDTPricer.cs
 *
 */

using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  ///
	/// <summary>
	///   BDT Callable Bond pricer
	/// </summary>
	///
	/// <preliminary />
	///
	[Serializable]
  public class BondBDTPricer : BDTPricer, IPricer
	{
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(BondBDTPricer));

		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">CallableBond to price</param>
		///
		public
		BondBDTPricer(Bond product)
			: base(product)
		{}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="survivalCurve">Survival curve</param>
		/// <param name="recoveryRate">Recovery rate</param>
		/// <param name="volatilityCurve">Short rate volatility term structure</param>
		///
		public
		BondBDTPricer(Bond product,  Dt asOf, Dt settle,
									DiscountCurve discountCurve, SurvivalCurve survivalCurve, double recoveryRate,
									VolatilityCurve volatilityCurve)
			: base(product, asOf, settle, discountCurve, survivalCurve, recoveryRate, volatilityCurve)
		{}


    #endregion // Constructors

		#region Methods

		/// <summary>
		///   Calculates Present value of the Callable Bond
		/// </summary>
		///
		/// <returns>Pv of Callable Bond</returns>
		///
		public override double ProductPv()
		{
			return Price(true) * Notional;
		}

    /// <summary>
		///   Calculates Present value of the Callable Bond without option
		/// </summary>
		///
		/// <returns>Pv of Callable Bond without option</returns>
		///
		public double NOPv()
		{
			return Price(false) * Notional;
		}


		// Function do calculate pv (percentage of Notional) of bond with or without option.
		//
		private double
		Price(bool withCall)
		{
			// Generate BDT rate tree
			Generate(Bond.Maturity);

			// Fill in cashflow schedules matching tree nodes
			double [] payment = new double[Dates.Length];
			double [] accrued = new double[Dates.Length];

			Schedule sched = new Schedule(AsOf, Bond.Effective, Bond.FirstCoupon, Bond.Maturity,
																		Bond.Freq, Bond.BDConvention, Bond.Calendar);

			int cfIdx = 0;
			for( int i = 0; i < Dates.Length; i++ )
			{
				// Find next payment date
				while( cfIdx < (sched.Count-1) && Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i]) < 0 )
					cfIdx++;

				// Calculate accrued to this slice date
				accrued[i] = Dt.Fraction(sched.GetPeriodStart(cfIdx), sched.GetPeriodEnd(cfIdx),
																 sched.GetPeriodStart(cfIdx), Dates[i], Bond.DayCount, Bond.Freq) * Bond.Coupon;

				// Calculate payment if this is the last slice before payment
				if( i >= (Dates.Length-1) || Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i+1]) < 0 )
					payment[i] = Dt.Fraction(sched.GetPeriodStart(cfIdx), sched.GetPeriodEnd(cfIdx),
																	 sched.GetPeriodStart(cfIdx), sched.GetPaymentDate(cfIdx),
																	 Bond.DayCount, Bond.Freq) * Bond.Coupon ;
				else
					payment[i] = 0.0;
				//logger.Debug( String.Format(" {0}: accrued={1}, payment={2}, next cpn ({3})={4}", i, accrued[i], payment[i], cfIdx, sched.getPaymentDate(cfIdx)) );
			}

			// Do we have to worry about the call schedule dates?  In perverse cases there may be
			// a call period that isn't "hit" by dates on the BDT tree.  TBD: worry about this later MEF 10-22-2004

			// Step back through tree, pricing call option
			int n = Dates.Length;
			double [] P = new double[n+1];

			// Set up the final period price
			{
			  double lastprice = 1.0 ; // clean price
        double strike = withCall ? Bond.GetCallPriceByDate(Dates[n - 1]) : 0.0;
				if (strike > 0)
					lastprice = Math.Min(strike, lastprice);
				lastprice += payment[payment.Length-1]; // full price
				for( int k = 0; k <= n; k++ )
				{
				  P[k] = lastprice;
				}
			}

			// start from the next last period
			for( int slice = n - 2; slice >= 0; slice-- )
			{
				// First, get call price.
        double strike = withCall ? Bond.GetCallPriceByDate(Dates[slice]) : 0.0;
				double paymentSlice = payment[slice];

				// Discount values back to today
				// Note: When we go upward, we need only to keep one price array.
				// Otherwise we have to keep two price arrays, both current and forward.
				for (int k = 0; k <= slice; k++ )
				{
					double rateSliceK = Rate(slice, k);
					double thisDeltaT = DeltaT;
					double P_K = 0.5 * (P[k] + P[k+1]);

					// P[k] is the clean price without payment
					P[k] = P_K / (1.0 + rateSliceK*thisDeltaT);

					// Test if we are called.
					if (strike > 0)
					{
						double accruedSlice = accrued[slice];
						double currentK = P[k];
						P[k] = Math.Min(strike, currentK);
					}
					// Add in any cashflows -- note that if this is the maturity date,
					P[k] += paymentSlice;
				}
			}

			// Only one value remains: return it!
			return P[0];
		}

		// Function do calculate pv of bond with or without option.
		//
		private double
		Price_new(bool withCall)
		{
			// Generate BDT rate tree
			Generate(Bond.Maturity);

			// Fill in cashflow schedules matching tree nodes
			double [] payment = new double[Dates.Length];
			double [] mismatch = new double[Dates.Length];
			double [] accrued = new double[Dates.Length];

			Schedule sched = new Schedule(AsOf, Bond.Effective, Bond.FirstCoupon, Bond.Maturity,
																		Bond.Freq, Bond.BDConvention, Bond.Calendar);

			int cfIdx = 0;
			for( int i = 0; i < Dates.Length; i++ )
			{
				// Find next payment date
				while( cfIdx < (sched.Count-1) && Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i]) < 0 )
					cfIdx++;

				// Calculate accrued to this slice date
				Dt pStart = sched.GetPeriodStart(cfIdx);
				Dt pEnd = sched.GetPeriodEnd(cfIdx);
				accrued[i] = Dt.Fraction(pStart, pEnd, pStart, Dates[i], Bond.DayCount, Bond.Freq) * Bond.Coupon;
				// Calculate payment if this is the last slice before payment
				if( i >= (Dates.Length-1) || Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i+1]) < 0 )
				{
					// payment[i] = accrued[i];
					// Full coupon
					payment[i] = Dt.Fraction(pStart, pEnd, pStart, pEnd, Bond.DayCount, Bond.Freq) * Bond.Coupon;
					// Adjust for placement on tree
					mismatch[i] = Dt.Fraction(pStart, pEnd, Dates[i], pEnd, Bond.DayCount, Bond.Freq);
					// Reset accrued.
					accrued[i] = 0.0;
				}
				else
				{
					payment[i] = 0.0;
					mismatch[i] = 0.0;
				}
				//logger.Debug( String.Format(" {0}: accrued={1}, payment={2}, next cpn ({3})={4}", i, accrued[i], payment[i], cfIdx, sched.GetPaymentDate(cfIdx)) );
			}

			// Do we have to worry about the call schedule dates?  In perverse cases there may be
			// a call period that isn't "hit" by dates on the BDT tree.
			// TBD: worry about this later MEF 10-22-2004

			// Step back through tree, pricing call option
			int n = Dates.Length;
			double [] current = new double[n+1];
			double [] forward = new double[n+1];

			// Set up the final payment
			for( int k = 0; k < (n+1); k++ )
			{
				forward[k] = payment[payment.Length-1];
			}

			for( int slice = n - 1; slice >= 0; slice-- )
			{
				// First, get call price.
        double strike = withCall ? Bond.GetCallPriceByDate(Dates[slice]) : 0.0;
				double paymentSlice = payment[slice];
				double mismatchSlice = mismatch[slice];

				// Discount values back to today
				for (int k = 0; k <= slice; k++ )
				{
					double rateSliceK = Rate(slice, k);
					double thisDeltaT = DeltaT;
					double fK = forward[k];
					double fKplus1 = forward[k+1];
					double paymentK = paymentSlice/(1.0 + rateSliceK*mismatchSlice);
					// Value at current node is the roll-back plus the payment
					// due on this date, which we add in below after we check for
					// call provisions
					current[k] = 0.5 / (1.0 + rateSliceK*thisDeltaT) * (fK + fKplus1);

					// Test if we are called.
					if (strike > 0)
					{
						// We've had some discussion about what the right comparison is
						// here for determining exercise.  Call schedule prices are clean
						// prices, i.e. without accrued interest, while the tree roll-back
						// values are dirty.  Thus to compare to the tree value, which
						// should represent a true market trasaction price, we must add
						// the accrued interest to the call price, since this is the amount
						// due to the bond holder should exercise occur.
						//
						// See http://www.sec.gov/answers/callablebonds.htm:
						//
            // Callable or redeemable bonds are bonds that
            // can be redeemed or paid off by the issuer
            // prior to the bonds' maturity date. When an
            // issuer calls its bonds, it pays investors the
            // call price (usually the face value of the
            // bonds) together with accrued interest to date
            // and, at that point, stops making interest
            // payments. Call provisions are often part of
            // corporate and municipal bonds, but usually
            // not bonds issued by the federal government.
						//

						// Note that the payment for this node is included, but in that
						// case the accrued is zero.  Also note that the payment of accrued,
						// or a coupon proper, cannot be avoided by calling the bond, so this
						// is the appropriate comparison.
						double accruedSlice = accrued[slice];
						double currentK = current[k];
						current[k] = Math.Min(strike + accruedSlice, currentK) + paymentK;
					}
					else
					{
						// Still need to handle payments on this node.
						current[k] += paymentK;
					}
				}
				forward = current;
			}

			// Only one value remains: return it!
			return current[0];
		}

		// Function do calculate pv of bond option.
		//
		/// <summary>Test routine for Hehui</summary>
		public double
		OptionPv()
		{
			// Generate BDT rate tree
			Generate(Bond.Maturity);

			// Fill in cashflow schedules matching tree nodes
			double [] payment = new double[Dates.Length];
			double [] accrued = new double[Dates.Length];

			Schedule sched = new Schedule(AsOf, Bond.Effective, Bond.FirstCoupon, Bond.Maturity,
																		Bond.Freq, Bond.BDConvention, Bond.Calendar);

			int cfIdx = 0;
			for( int i = 0; i < Dates.Length; i++ )
			{
				// Find next payment date
				while( cfIdx < (sched.Count-1) && Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i]) < 0 )
					cfIdx++;

				// Calculate accrued to this slice date
				accrued[i] = Dt.Fraction(sched.GetPeriodStart(cfIdx), sched.GetPeriodEnd(cfIdx),
																 sched.GetPeriodStart(cfIdx), Dates[i], Bond.DayCount, Bond.Freq);
				//* Bond.Coupon;

				// Calculate payment if this is the last slice before payment
				if( i >= (Dates.Length-1) || Dt.Cmp(sched.GetPaymentDate(cfIdx), Dates[i+1]) < 0 )
					payment[i] = Dt.Fraction(sched.GetPeriodStart(cfIdx), sched.GetPeriodEnd(cfIdx),
																	 sched.GetPeriodStart(cfIdx), sched.GetPaymentDate(cfIdx),
																	 Bond.DayCount, Bond.Freq) * Bond.Coupon ;
				else
					payment[i] = 0.0;
				//logger.Debug( String.Format(" {0}: accrued={1}, payment={2}, next cpn ({3})={4}", i, accrued[i], payment[i], cfIdx, sched.GetPaymentDate(cfIdx)) );
			}

			// Do we have to worry about the call schedule dates?  In perverse cases there may be
			// a call period that isn't "hit" by dates on the BDT tree.  TBD: worry about this later MEF 10-22-2004

			// Step back through tree, pricing call option
			int n = Dates.Length;
			double [] P = new double[n+1];
			double [] C = new double[n+1];

			// Set up the final payment
			{
        double strike = Bond.GetCallPriceByDate(Dates[n - 1]);
				double lastprice = 1.0 ; // clean price
				double callValue = (strike > 0) ?  Math.Max(lastprice - strike, 0.0) : 0.0 ;
				lastprice += payment[payment.Length-1]; // full price
				for( int k = 0; k <= n; k++ )
				{
				  P[k] = lastprice ;
					C[k] = callValue ;
				}
			}

			for( int slice = n - 2; slice >= 0; slice-- )
			{
				// First, get call price.
        double strike = Bond.GetCallPriceByDate(Dates[slice]);
				double accruedSlice = accrued[slice];
				double paymentSlice = payment[slice];

				// Discount values back to today
				for (int k = 0; k <= slice; k++ )
				{
					double rateSliceK = Rate(slice, k);
					double thisDeltaT = DeltaT;
					double P_K = (P[k] + P[k+1]) / 2 ;
					double C_K = (C[k] + C[k+1]) / 2 ;

					//double d = Math.Pow(1.0 + rateSliceK*thisDeltaT, -thisDeltaT);
					double d = 1.0 / (1.0 + rateSliceK*thisDeltaT) ;
					P[k] = d * P_K;
					C[k] = d * C_K;

					// Test if we are called.
					if (strike > 0)
					{
						double currentK = C[k];
						C[k] = Math.Max(P[k] - strike, currentK);
					}

					P[k] += paymentSlice ;
				}
			}

			// Only one value remains: return it!
			return C[0];
		}


		/// <summary>
		///   Calculate OAS for Callable Bond given full price
		/// </summary>
		///
		/// <param name="fullPrice">Full price</param>
		///
		public double
		OAS(double fullPrice)
		{
			logger.Debug( String.Format("Trying to solve oas for full price {0}", fullPrice) );

			// This is almost a linear function of spread here so we can take a good first guess
			// based on the rate01
			double b = EvaluatePrice(0.0);
			double guess = (fullPrice - b)/((EvaluatePrice(0.001) - b)/0.001);
			logger.Debug( String.Format("Initial guess of oas is {0}", guess) );

			// Solve for implied rate spread
			Brent rf = new Brent();
			rf.setToleranceX(1e-4);
			rf.setLowerBounds(guess-0.01);
			rf.setUpperBounds(guess+0.01);

			fn_ = new Double_Double_Fn(this.Evaluate);
			solverFn_ = new DelegateSolverFn(fn_, null);

			try {
				rf.solve(solverFn_, fullPrice, guess-0.0002, guess+0.0002);
			}
			catch( Exception e )
			{
				throw new ToolkitException( String.Format("Unable to find oas matching price {0}. Last tried spread {1}", fullPrice, rf.getCurrentSolution()), e);
			}

			double result = rf.getCurrentSolution();

			// Tidy up
			fn_ = null;
			solverFn_ = null;

			logger.Debug( String.Format("Found oas {0}", result) );

			return result;
		}

    private double Evaluate(double x, out string exceptDesc)
    {
      double price = 0.0;
      exceptDesc = null;
      try {
        price = EvaluatePrice(x);
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      return price;
    }

    //
    // Function for root find evaluation
    // Prices product given rate spread
    //
    private double
    EvaluatePrice(double x)
    {
      double origSpread = DiscountCurve.Spread;

      // Update spread
      DiscountCurve.Spread = origSpread + x;

      // Re-price
      double price = Pv();

      logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);

      // Restore spread
      DiscountCurve.Spread = origSpread;

      return price;
    }

    #endregion // Methods

		#region Properties

		/// <summary>
		///   Product
		/// </summary>
		public Bond Bond
		{
			get { return (Bond)Product; }
		}

		#endregion // Properties

		#region Data

		// For solver
		[NonSerialized, NoClone]
    DelegateSolverFn solverFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
		[NonSerialized, NoClone]
    Double_Double_Fn fn_;

		#endregion // Data

	} // class BondBDTPricer

}
