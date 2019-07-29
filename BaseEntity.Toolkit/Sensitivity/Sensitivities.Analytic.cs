using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Sensitivity
{
    /// <summary>
    /// Supported sensitivity calculation methods
    /// </summary>
    public enum SensitivityMethod
    {
        /// <summary>
        /// Bumping and recalculating. Suited to large bump size. It is supported by every IPricer 
        /// </summary>
        FiniteDifference,

        /// <summary>
        /// Compute the deltas and gamma by analytically differentiating the Pv with respect to the quantity of interest
        /// Suited to small bump size. It is supported by pricers that implement the IAnalyticDerivativesProvider interface and 
        /// produces significant speed improvements.
        /// </summary>
        SemiAnalytic
    }
    
    /// <summary>
    /// Helper class for analytic sensitivities
    /// </summary>
    internal class AnalyticSensitivities
    {
       private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AnalyticSensitivities));
        /// <summary>
        /// Computes the outer product between Pv derivatives wrt curve ordinates and derivatives of curve ordinates with respect to market quotes
        /// </summary>
        /// <param name="derivative">DerivativeWrtCurve object</param>
        /// <param name="retVal">Overwritten by derivatives wirth respect to curve tenor quotes</param>
        internal static void CalcDerivativesWrtTenorQuotes(DerivativesWrtCurve derivative, DerivativesWrtCurve retVal)
        {
            derivative.ReferenceCurve.EvaluateDerivativesWrtQuotes();
            int n = derivative.ReferenceCurve.Count;
            int nt = derivative.ReferenceCurve.Tenors.Count;
            double[] gradient = new double[nt];
            double[] hessian = new double[nt * (nt + 1) / 2];
            int kk, k = 0;
            for (int i = 0; i < nt; i++)
            {
                gradient[i] = 0.0;
                for (int ii = 0; ii < n; ii++)
                    gradient[i] += derivative.Gradient[ii] * derivative.ReferenceCurve.GradientsWrtQuotes[ii][i];
                for (int j = 0; j <= i; j++)
                {
                    kk = 0;
                    hessian[k] = 0.0;
                    for (int ii = 0; ii < n; ii++)
                    {
                        hessian[k] += derivative.Gradient[ii] * derivative.ReferenceCurve.HessiansWrtQuotes[ii][k];
                        for (int jj = 0; jj <= ii; jj++)
                        {
                            double mult = (jj == ii)
                                              ? derivative.ReferenceCurve.GradientsWrtQuotes[ii][i] *
                                                derivative.ReferenceCurve.GradientsWrtQuotes[jj][j]
                                              : derivative.ReferenceCurve.GradientsWrtQuotes[ii][i] *
                                                derivative.ReferenceCurve.GradientsWrtQuotes[jj][j] +
                                                derivative.ReferenceCurve.GradientsWrtQuotes[ii][j] *
                                                derivative.ReferenceCurve.GradientsWrtQuotes[jj][i];
                            hessian[k] += derivative.Hessian[kk] * mult;
                            kk++;
                        }

                    }
                    k++;
                }
            }
            retVal.Gradient = gradient;
            retVal.Hessian = hessian;
            retVal.RecoveryDelta = derivative.RecoveryDelta;
            retVal.Vod = derivative.Vod;
        }

        private static int BinarySearch(CurveTenorCollection tenors, Dt maturity)
        {
          int left = 0;
          int right = tenors.Count - 1;
          for (; ;)
          {
            int comp, mid = (left + right) / 2;
            if ((comp = Dt.Cmp(maturity, tenors[mid].Maturity)) >= 0)
            {
              if (comp == 0)
                return comp;
              else
                left = mid;
            }
            else
              right = mid;
            if(right - left == 1)
              break;
          }
          return ~right;
        }

        /// <summary>
        /// Create the hedging instrument for the given name. 
        /// Returns either one instrument (if name is not matching) or one instrument for each curve tenor (if name is matching)
        /// </summary>
        /// <param name="curve">Underlying CalibratedCurve</param>
        /// <param name="pricer">Pricer to be hedged</param>
        /// <param name="name">Name of the hedge tenor</param>
        ///<param name="hedgeNames">Name of the hedging instruments</param>
        /// <param name="hedgePos">Position of the hedging instrument in the collection of tenors within the curve</param>
        /// <returns>Array of hedging pricers</returns>
        internal static IPricer[] HedgingInstruments(CalibratedCurve curve, IPricer pricer, string name, out string[] hedgeNames, out int[] hedgePos)
        {
          string normalizedName = name.Trim().ToLower();
          if (String.Compare(normalizedName, "matching") == 0)
          {
            IPricer[] retVal = new IPricer[curve.Tenors.Count];
                hedgeNames = new string[retVal.Length];
                hedgePos = new int[retVal.Length];
                for (int i = 0; i < retVal.Length; i++)
                {
                    retVal[i] = curve.Calibrator.GetPricer(curve, (IProduct)curve.Tenors[i].Product.Clone());
                    hedgeNames[i] = curve.Tenors[i].Name;
                    hedgePos[i] = i;
                }
                return retVal;
            }
            else if (String.Compare(normalizedName, "maturity") == 0 || (name.Length >= 5 && Sensitivities.StringIsNum(name)))
            {
              Dt maturity;
              if (String.Compare(normalizedName, "maturity") == 0)
                maturity = pricer.Product.Maturity;
              else
                maturity = Dt.FromExcelDate(Double.Parse(name));
              int idx = BinarySearch(curve.Tenors, maturity);
              if (idx < 0)
              {
                idx = ~idx;
                if (idx > 0)
                  idx = (Dt.FractDiff(curve.Tenors[idx - 1].Maturity, maturity) <
                         Dt.FractDiff(maturity, curve.Tenors[idx].Maturity))
                          ? idx - 1
                          : idx;
              }
              int pos = idx;
              IPricer retVal = curve.Calibrator.GetPricer(curve, (IProduct) curve.Tenors[pos].Product.Clone());
              hedgePos = new int[] {pos};
              pricer.Product.Maturity = maturity;
              hedgeNames = new string[] { name };
              return new IPricer[] { retVal };
            }
            else
            {
              int pos = curve.Tenors.Index(normalizedName);
              if (pos == -1)
              {
                hedgeNames = null;
                hedgePos = null;
                logger.DebugFormat("Curve {0} missing tenor {1}. Hedge notional has not been computed", curve.Name, name);
                return null;
              }
              IPricer retVal = curve.Calibrator.GetPricer(curve, (IProduct)curve.Tenors[pos].Product.Clone());
              hedgeNames = new string[]{name};
              hedgePos = new int[]{pos};
              return new IPricer[] { retVal };
            }
        }
    }
     
}
