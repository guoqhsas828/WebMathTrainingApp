using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
    /// <summary>
    /// Container for derivatives w.r.t the ordinates of the underlying reference curve
    /// </summary>
    public class DerivativesWrtCurve
    {
        private CalibratedCurve referenceCurve_;
        private double[] gradient_;
        private double[] hessian_;
        private double vod_ = 0;
        private double recoveryDelta_ = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="referenceCurve">Underlying reference curve</param>
        public DerivativesWrtCurve(CalibratedCurve referenceCurve)
        {
            referenceCurve_ = referenceCurve;
        }


        /// <summary>
        /// Accessor for the underlying reference curve
        /// </summary>
        public CalibratedCurve ReferenceCurve
        {
            get { return referenceCurve_; }
        }

        /// <summary>
        /// Gradient of the pricing measure w.r.t the CommodityCurve ordinates
        /// </summary>
        public double[] Gradient
        {
            get { return gradient_; }
            set
            {
                gradient_ = (double[])value.Clone();
            }
        }

        /// <summary>
        /// Hessian of the pricing measure w.r.t the CommodityCurve ordinates. The lower diagonal part of the
        /// matrix is stored in a vector so that <m>H[i,j] = Hessian[i*(i+1)/2 + j]</m>
        /// </summary>
        public double[] Hessian
        {
            get { return hessian_; }
            set
            {
                hessian_ = (double[])value.Clone();
            }
        }

        /// <summary>
        /// Value of default (applicable if entity is subject to credit risk)
        /// </summary>
        public double Vod
        {
            get { return vod_; }
            set { vod_ = value; }
        }

        /// <summary>
        /// Derivative with respect to quoted recovery (applicable if the entity is subject to credit risk)
        /// </summary>
        public double RecoveryDelta
        {
            get { return recoveryDelta_; }
            set { recoveryDelta_ = value; }
        }

        /// <summary>
        /// Computes the sensitivity to joint movements of the market quotes 
        /// </summary>
        /// <param name="bumps">Array of bumps</param>
        /// <returns>Sensitivity to joint moves by bumps</returns>
        public double ComputeSensitivity(double[] bumps)
        {
          int nt = ReferenceCurve.Tenors.Count;
          int n = Gradient.Length;
          if(n != nt)
            throw new ToolkitException("This function can only be used to compute derivatives of the PV w.r.t to quotes");
          double[] pert = new double[nt];
            for (int i = 0; i < Math.Min(nt, bumps.Length); i++)
                pert[i] = bumps[i];
            double retVal = 0.0;
            int k = 0;
            for (int i = 0; i < nt; i++)
            {
                retVal += Gradient[i] * pert[i];
                for (int j = 0; j <= i; j++)
                {
                    double mult = (i == j) ? 0.5 * pert[i] * pert[j] : (pert[i] * pert[j]);
                    retVal += Hessian[k] * mult;
                    k++;
                }
            }
            return retVal;
        }

        /// <summary>
        /// Computes the sensitivity to joint movements of the market quotes 
        /// </summary>
        /// <param name="bumps">Array of bumps</param>
        /// <returns>Sensitivity to joint moves by bumps</returns>
        public double SecondOrderOnly(double[] bumps)
        {
          int N = ReferenceCurve.Tenors.Count;
          int n = Gradient.Length;
          if (n != N)
            throw new ToolkitException("This function can only be used to compute derivatives of the PV w.r.t to quotes");
          double[] pert = new double[N];
          for (int i = 0; i < Math.Min(pert.Length, bumps.Length); i++)
            pert[i] = bumps[i];
          double retVal = 0.0;
          int k = 0;
          for (int i = 0; i < N; i++)
          {
            for (int j = 0; j <= i; j++)
            {
              double mult = (i == j) ? pert[i] * pert[j] : 2*(pert[i] * pert[j]);
              retVal += Hessian[k] * mult;
              k++;
            }
          }
          return retVal;

        }
    }
}
