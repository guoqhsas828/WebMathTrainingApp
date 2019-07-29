/*
 * ProductUtil.cs
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{

	/// <summary>
	///   Product utility methods.
	/// </summary>
	///
	/// <remarks>
	///   <para>Collection of utility methods for products.</para>
	/// </remarks>
	///
	public static class ProductUtil
  {
		/// <summary>
		///   Return latest maturity from series of products
		/// </summary>
		///
		/// <param name="products">Array of products</param>
		///
		/// <returns>Latest maturity date</returns>
		///
		/// <exception cref="ArgumentOutOfRangeException">If verifyUniform is true and product maturities do not match</exception>
		///
		public static Dt LastMaturity( IProduct [] products )
		{
		  // Sanity check
		  if( null == products || products.Length < 1 )
				throw new ArgumentException( "products cannot be null or empty" );

			for( int i = 0; i < products.Length; ++i )
			{
			  // in case the first element is null
				if( null != products[i] )
				{
					Dt maturityDate = products[i].Maturity;
					for( int j = i + 1; j < products.Length; ++j )
					{
						if( null != products[j] && Dt.Cmp(maturityDate, products[j].Maturity) < 0 )
							maturityDate = products[j].Maturity;
					}
					return maturityDate;
				}
			}

			return Dt.Empty;
		}
    
		/// <summary>
		///   Verify all maturities for a series of products match
		/// </summary>
		///
		/// <param name="products">Array of products</param>
		///
		/// <returns>true if all maturities match, false otherwise</returns>
		///
		public static bool VerifyMaturitiesMatch( IProduct [] products )
		{
		  if( null == products || products.Length < 1 )
				return true;

		  Dt maturityDate = products[0].Maturity;
			for( int i = 1; i < products.Length; ++i )
				if( Dt.Cmp(maturityDate, products[i].Maturity) != 0 )
					return false;

			return true;
		}

		/// <summary>
		/// The effective annualized coupon for a Product at a given date.
		/// </summary>
		/// 
		/// <param name="p">The product</param>
		/// <param name="date">The date</param>
		/// <param name="indexResetRate">The last index reset rate for floating rate coupons</param>
		/// 
		/// <returns>Annualized coupon or 0 for non-coupon paying instruments.</returns>
		/// 
		internal static double CouponAt(Product p, Dt date, double indexResetRate)
		{
			double cpn = 0;

			// Handle different products
			if (p is CDS)
			{
				CDS cds = (CDS)p;
				cpn = CouponPeriodUtil.CouponAt(date, cds.Premium, cds.PremiumSchedule, cds.CdsType == CdsType.FundedFixed ? 0 : indexResetRate);
			}
			else if (p is Bond)
			{
				Bond bond = (Bond)p;
				cpn = CouponPeriodUtil.CouponAt(date, bond.Coupon, bond.CouponSchedule, bond.Floating ? indexResetRate : 0);
			}
			else if (p is LCDS)
			{
				LCDS lcds = (LCDS)p;
				cpn = CouponPeriodUtil.CouponAt(date, lcds.Premium, lcds.PremiumSchedule, lcds.CdsType == CdsType.FundedFixed ? 0 : indexResetRate);
			}
			else if (p is Loan)
			{
				Loan loan = (Loan)p;
				cpn = CouponPeriodUtil.CouponAt(date, 0/*loan.Spread*/, null, loan.IsFloating ? indexResetRate : 0);
			}
			else if (p is SwapLeg)
			{
				SwapLeg swap = (SwapLeg)p;
				cpn = CouponPeriodUtil.CouponAt(date, swap.Coupon, swap.CouponSchedule, swap.Floating ? indexResetRate : 0);
			}
			else if (p is SyntheticCDO)
			{
				SyntheticCDO cdo = (SyntheticCDO)p;
				cpn = CouponPeriodUtil.CouponAt(date, cdo.Premium, null, cdo.CdoType == CdoType.FundedFixed ? 0 : indexResetRate);
			}

			// Done
			return cpn;
		}

	  internal static ISchedule GetSchedule(this Product product)
	  {
	    if (product.CustomPaymentSchedule != null)
	    {
	      return product.CustomPaymentSchedule;
	    }
	    var withSchedule = product as ProductWithSchedule;
	    return withSchedule != null ? withSchedule.Schedule: null;
	  }
  } // class ProductUtil
}
