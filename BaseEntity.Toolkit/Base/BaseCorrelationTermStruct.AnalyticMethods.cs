/*
 * BaseCorrelationTermStruct.cs
 *
 * A class for base correlation data with term structure
 *
 *  . All rights reserved.
 *
 */
#define Include_Old_Constructors

using System;
using System.ComponentModel;
using System.Collections;
using System.Data;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using BaseEntity.Toolkit.Calibrators.BaseCorrelation;

namespace BaseEntity.Toolkit.Base
{

    public partial class BaseCorrelationTermStruct : BaseCorrelationObject, ICorrelationBump, ICorrelationBumpTermStruct
    {
        #region Semianalytic sensitivities methods

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
          
          if(Dates.Length <= 1)
          {
            return this.BaseCorrelations[0].CorrelationDerivatives(basketPricer, discountCurve, cdo, retVal);
          }
          int idx = checkDates(dates_, cdo.Maturity);
          if (idx >= 0)
          {
            return this.BaseCorrelations[idx].CorrelationDerivatives(basketPricer, discountCurve, cdo, retVal);
           
          }
          if(this.InterpMethod != InterpMethod.Linear || this.ExtrapMethod != ExtrapMethod.Const)
              throw new ToolkitException("SemiAnalytic strike rescale derivatives only supported for Linear Tenor Interpolation and Const Tenor Extrapolation");
          if(Dt.Cmp(cdo.Maturity, dates_[0])<=0)
          {
            return this.BaseCorrelations[0].CorrelationDerivatives(basketPricer, discountCurve, cdo, retVal);
          }
          int n = Dates.Length;
          if(Dt.Cmp(cdo.Maturity, dates_[n-1]) >= 0)
          {
            return this.BaseCorrelations[n-1].CorrelationDerivatives(basketPricer, discountCurve, cdo, retVal);
          }
          int k;
          double h, b, a;
          int kLow = 0;
          int kHi = n-1;
          while (kHi - kLow > 1)
          {
            k = (kHi + kLow) >> 1;
            if (Dt.Cmp(Dates[k], cdo.Maturity)>0) 
              kHi = k;
            else 
              kLow = k;
          }
          h = (double)Dt.Diff(Dates[kLow],Dates[kHi]);
          a = (double)(Dt.Diff(cdo.Maturity, Dates[kHi])) / h;
          b = 1.0 - a;
          double[][] res = new double[2][];
          res[0] = new double[retVal.Length];
          res[1] = new double[retVal.Length];
          double c0 = this.BaseCorrelations[kLow].CorrelationDerivatives(basketPricer, discountCurve, cdo, res[0]);
          double c1 = this.BaseCorrelations[kHi].CorrelationDerivatives(basketPricer, discountCurve, cdo, res[1]);
          for (int i = 0; i < retVal.Length; i++)
            retVal[i] = a*res[0][i] + b*res[1][i];
          return a*c0 + b*c1;
        }
        #endregion
    }
}
