/*
 * HeterogeneousBasketPricer.cs
 *
 *
 */
#define USE_OWN_BumpedPvs
// #define INCLUDE_EXTRA_DEBUG // Define to include exra debug output
using System;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  using System.ComponentModel;
  using System.Collections;
  using System.Runtime.Serialization;

  using Util;
  using BaseEntity.Toolkit.Base;
  using BaseEntity.Toolkit.Products;
  using Toolkit.Numerics;
  using BaseEntity.Shared;
  using Toolkit.Models;
  using Toolkit.Curves;

  ///
  /// <summary>
  ///   Pricing helper class for Heterogeneous basket pricer with bootstraping correlations
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class employs more efficient method to calculate the loss distributions
  ///   in base correlation calibration.  Its algorithm has some inconsistency with
  ///   the standard heterogeneous basket pricers.
  /// 
  ///   (1) After calling the Reset() function, only the loss distributions for the last tenor period
  ///       are recalculated.  In contrast, the standard pricers will
  ///       recalculate the whole distributions for all the tenor periods from the settlement to maturity.
  /// 
  ///   (2) Accordingly, the SetFactor() function only sets the correlation of the last tenor point
  ///       while keep all other tenor points unchanged.  In contrast, the standard pricers will reset
  ///       the correlations of all the tenor points.
  /// 
  ///    At this moment, this class is intented to be used internally.
  /// </remarks>
  ///
  /// <exclude />
  [Serializable]
  class BootstrapHeterogeneousBasketPricer : HeterogeneousBasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BootstrapHeterogeneousBasketPricer));

    #region Constructors

    /// <exclude />
    public BootstrapHeterogeneousBasketPricer()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Factor correlations for the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    /// <remarks>
    ///   <para>This is obsolete.</para>
    /// </remarks>
    public BootstrapHeterogeneousBasketPricer(
        Dt asOf,
        Dt settle,
        Dt maturity,
        SurvivalCurve[] survivalCurves,
        RecoveryCurve[] recoveryCurves,
        double[] principals,
        Copula copula,
        CorrelationTermStruct correlation,
        int stepSize,
        TimeUnit stepUnit,
        Array lossLevels
        )
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
            copula, correlation, stepSize, stepUnit, lossLevels)
    {
      lastIndex_ = FindTenorIndex(correlation.Dates, maturity);
    }

    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Find the index of the period which contains the maturity date
    /// </summary>
    /// <param name="dates">Array of end period dates</param>
    /// <param name="maturity">maturity date</param>
    /// <returns></returns>
    private int FindTenorIndex(Dt[] dates, Dt maturity)
    {
      int N = dates.Length;
      if (N > 1)
      {
        for (int i = 0; i < N; ++i)
          if (Dt.Cmp(maturity, dates[i]) <= 0)
            return i;
      }
      return N - 1;
    }

    /// <summary>
    ///   Set factor for the last period
    /// </summary>
    /// <param name="factor">factor to set</param>
    public override void SetFactor(double factor)
    {
      CorrelationTermStruct corr = (CorrelationTermStruct)this.Correlation;
      corr.SetFactorAtDate(lastIndex_, factor);
    }

    public void SetMaturity(Dt maturity)
    {
      this.Maturity = maturity;
      lastIndex_ = FindTenorIndex(((CorrelationTermStruct)this.Correlation).Dates, maturity);
    }
    #endregion // Methods

    #region Properties
    #endregion // Properties

    #region Data
    private int lastIndex_;
    #endregion // Data

  }// BootstrapHeterogeneousBasketPricer
}
