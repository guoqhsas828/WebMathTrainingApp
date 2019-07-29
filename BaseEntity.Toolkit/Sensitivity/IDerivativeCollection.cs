using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Sensitivity
{
    /// <summary>
    /// Collection of semi-analytic derivative results
    /// </summary>
    public interface IDerivativeCollection
    {
        /// <summary>
        /// Number of underlying reference curves
        /// </summary>
        int CurveCount { get; }
        /// <summary>
        /// Access reference curve indexed by i
        /// </summary>
        /// <param name="i">Curve index</param>
        /// <returns>Reference curve at index  i</returns>
        CalibratedCurve GetCurve(int i);
        /// <summary>
        /// Access the derivatives container at index i
        /// </summary>
        /// <param name="i">Reference curve index</param>
        /// <returns>DerivativeWrtCurve object</returns>
        DerivativesWrtCurve GetDerivatives(int i);
    }
   
}
