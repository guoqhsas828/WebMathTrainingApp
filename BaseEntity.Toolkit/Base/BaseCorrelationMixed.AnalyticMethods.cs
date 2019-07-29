using System;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;


namespace BaseEntity.Toolkit.Base
{
    ///
    /// <summary>
    ///   A class for mixing base correlation data
    /// </summary>
    ///
    /// <remarks>
    ///   This class provides basic data structures and defines basic interface
    ///   for base correlation term structures.  Conceptually, the term structure
    ///   can be considered as a sequence of base correlations estimated at the same
    ///   as-of date but with different horizons, such as 3 years, 5 years, 7 years,
    ///   10 years, etc..  Each of these base correlations has an associated maturity
    ///   date. 
    /// </remarks>
    ///
    public abstract partial class BaseCorrelationMixed 
    {
        #region Semianalytic Sensitivities methods
        /// <summary>
        /// Compute derivatives of the equity tranche correlation arising from 
        /// a change in the underlying survival curve ordinates, default events and change in recovery via the strike maps
        /// </summary>
        ///<param name="basketPricer">BasketPricer object</param>
        ///<param name="discountCurve">Discount curve object</param>
        ///<param name="cdo">Cdo specifications</param>
        ///<param name="weights">Weight to assign to each base correlation object</param>
        /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
        /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
        /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
        /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
        /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
        /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
        internal double CorrelationDerivatives(BasketPricer basketPricer, DiscountCurve discountCurve, SyntheticCDO cdo, double[] weights, double[] retVal)
        {
            int nBc = BaseCorrelations.Length;
            double[][] res = new double[nBc][];
            double[][] grad = new double[nBc][];
            double totWeight = 0.0;
            double[] nWeights = new double[res.Length];
            for (int j = 0; j < res.Length; j++)
            {
                res[j] = new double[retVal.Length];
                totWeight += weights[j];
            }
            double factor = 0;
            double[] correlations = new double[nBc];
            double[] factors = new double[nBc];
            for (int n = 0; n < nBc; n++)
            {
                nWeights[n] = weights[n] / totWeight;
                correlations[n] = BaseCorrelations[n].CorrelationDerivatives(basketPricer, discountCurve, cdo, res[n]);
                factors[n] = Math.Sqrt(correlations[n]);
                factor += nWeights[n] * factors[n];
            }
            double corr = factor * factor;
            int idx = 0;
            for (int i = 0; i < basketPricer.SurvivalCurves.Length; i++)
            {
                int len = basketPricer.SurvivalCurves[i].Count;
                for (int n = 0; n < nBc; n++)
                    grad[n] = new double[len];
                double[] tmp = new double[len];
                for (int j = 0; j < len; j++)
                {
                    for (int n = 0; n < nBc; n++)
                    {
                        grad[n][j] = res[n][idx];
                        tmp[j] += nWeights[n] / factors[n] * res[n][idx];
                    }
                    retVal[idx] = factor * tmp[j];
                    idx++;
                }
                for (int j = 0; j < len; j++)
                {
                    for (int k = 0; k <= j; k++)
                    {
                        retVal[idx] = 0.5 * tmp[j] * tmp[j];
                        for (int n = 0; n < nBc; n++)
                            retVal[idx] += factor *
                                           (-0.5 * nWeights[n] / (correlations[n] * factors[n]) * grad[n][j] * grad[n][k] +
                                            nWeights[n] / factors[n] * res[n][idx]);
                        idx++;
                    }
                }
                double factorP = 0;
                for (int n = 0; n < nBc; n++)
                    factorP += nWeights[n] * Math.Sqrt(correlations[n] + res[n][idx]);
                retVal[idx] = factorP * factorP - corr;
                idx++;
                for (int n = 0; n < nBc; n++)
                    retVal[idx] += nWeights[n] / factors[n] * factor * res[n][idx];
                idx++;
            }
            return corr;
        }
        #endregion
    }
}