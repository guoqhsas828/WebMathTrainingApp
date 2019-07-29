/*
 * BaseCorrelationCombined.cs
 *
 * A class for mixing base correlation data
 *
 *  . All rights reserved.
 *
 */
using System;
using System.Collections;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Base
{
    /// <summary>
    ///   Combined base correlation object
    /// </summary>
    /// <remarks>
    ///   <para>This is simply a wrapper of the base correlation combining method prior to release 8.7.</para>
    /// </remarks>
    public partial class BaseCorrelationJointSurfaces : BaseCorrelationMixed
    {
        #region Semianalytic Sensitivities methods
        /// <summary>
        /// Compute derivatives of the equity tranche correlation arising from 
        /// a change in the underlying survival curve ordinates, default events and change in recovery via the strike maps
        /// </summary>
        ///<param name="basketPricer">BasketPricer object</param>
        ///<param name="discountCurve">Discount curve object</param>
        ///<param name="cdo">Cdo specifications</param>
        /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
        /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
        /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
        /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
        /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
        /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
        public override double CorrelationDerivatives(BasketPricer basketPricer, DiscountCurve discountCurve, SyntheticCDO cdo, double[] retVal)
        {
            return this.CorrelationDerivatives(basketPricer, discountCurve, cdo, this.Weights, retVal);
        }
        #endregion
    }
}
