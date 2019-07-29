/*
 * ABSBasketPricer.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 *
 * TBD: Add ABSCDO specific stuffs. HJ Feb07
 */
using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers
{
  ///
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.ABSCDO">CDO of ABS</see> using the
  ///   <see cref="BasketPricer">Basket Pricer Model</see>.</para>
  /// </summary>
  ///
  /// <seealso cref="BaseEntity.Toolkit.Products.ABSCDO">CDO of ABS Tranche Product</seealso>
  /// <seealso cref="BasketPricer">Basket Pricer</seealso>
  [Serializable]
  public class ABSCDOPricer : SyntheticCDOPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ABSCDOPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO of ABS tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///
    public ABSCDOPricer(
      ABSCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve)
      : base(product, basket, discountCurve, 1.0, null)
    {
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="rateResets">List of reset rates</param>
    ///
    public ABSCDOPricer(
      ABSCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double notional,
      List<RateReset> rateResets)
      : base(product, basket, discountCurve, notional, rateResets)
    {
    }

    #endregion // Constructors

    #region Properties
    /// <summary>
    ///   CDO of ABS product
    /// </summary>
    public ABSCDO ABSCDO
    {
      get { return (ABSCDO)this.CDO; }
      set { this.CDO = value; }
    }
    #endregion // Properties

  } // class ABSCDOPricer

}
