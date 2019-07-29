/*
 * BermudanBgmTreeCalculor.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.BGM
{
  /// <summary>
  ///   Bermudan pricer
  /// </summary>
  /// <exclude />
  internal abstract class BermudanBgmTreePricer : PricerBase, IPricer
  {
    // Logger
    private static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(BermudanBgmTreePricer));

    #region Tree methods and properties
    /// <summary>
    /// Initializes a new instance of the <see cref="BermudanBgmTreePricer"/> class.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <param name="asOf">The pricing date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="tree">The BGM binomial tree calculator.</param>
    protected BermudanBgmTreePricer(
      IProduct product, Dt asOf, Dt settle,
      BgmTreeCalculator tree)
      : base(product, asOf, settle)
    {
      tree_ = tree;
    }

    /// <summary>
    /// Gets the exercise dates.
    /// </summary>
    /// <value>The exercise dates.</value>
    public abstract Dt[] ExerciseDates { get; }

    /// <summary>
    ///  Calculate the exercises payoff on a forward date.
    /// </summary>
    /// <param name="exerciseDateIndex">The index of the exercise date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The payoff.</returns>
    public abstract double ExercisePayoff(
      int exerciseDateIndex, DiscountCurve discountCurve);

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    /// 	<para>There are some pricers which need to remember some internal state
    /// in order to skip redundant calculation steps. This method is provided
    /// to indicate that all internate states should be cleared or updated.</para>
    /// 	<para>Derived Pricers may implement this and should call base.Reset()</para>
    /// </remarks>
    public override void Reset()
    {
      indexMap_ = null;
      exercisable_ = null;
      tree_.Reset();
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if(!IsActive())
        return;

      base.Validate(errors);

      if (tree_ == null)
        InvalidValue.AddError(errors, this, "BgmTreeCalculator",
          String.Format("Invalid BGM tree. Cannot be null"));
      return;
    }
    #endregion

    #region IPricer Members

    /// <summary>
    /// Present value (including accrued) for product to pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value</returns>
    public override double ProductPv()
    {
      if (exercisable_ == null)
        UpdateExercisable();
      return tree_.RateDistributions.EvaluateBermudan(exercisable_,
        (k, dc) => ExercisePayoff(indexMap_[k], dc));
    }

    /// <summary>
    /// Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    double IPricer.Accrued()
    {
      return 0;
    }

    private void UpdateExercisable()
    {
      Dt[] exerciseDates = ExerciseDates;
      if (tree_.NodeDates != exerciseDates)
      {
        IncludeExerciseDatesInTree();
      }
      Dt[] nodeDates = tree_.RateDistributions.NodeDates;
      int[] indexMap = new int[nodeDates.Length];
      bool[] exercisable = new bool[nodeDates.Length];
      for (int i = 0, j =0; i < nodeDates.Length;++i)
      {
        if (j >= exerciseDates.Length)
          break; // no more exercise date
        if (exerciseDates[j] > nodeDates[i])
          continue; // go to the next node date

        // This date is exercisable
        exercisable[i] = true;
        indexMap[i] = j;
        // increase j and skip the invalid dates
        while (++j < exerciseDates.Length
          && exerciseDates[j] <= nodeDates[i])
        {
        }
      }
      indexMap_ = indexMap;
      exercisable_ = exercisable;
      return;
    }

    /// <summary>
    ///  Make sure all the exercise dates are included in the tree nodes.
    /// </summary>
    private void IncludeExerciseDatesInTree()
    {
      var exerDates = ExerciseDates;
      var treeDates = tree_.NodeDates;
      if (treeDates == null)
      {
        tree_.NodeDates = exerDates; // this also reset the tree.
        return;
      }
      for (int i = 0; i < exerDates.Length; ++i)
      {
        if (!treeDates.Contains(exerDates[i]))
        {
          var dates = new UniqueSequence<Dt> {treeDates, exerDates};
          tree_.NodeDates = dates; // this also reset the tree.
        }
      }
      return;
    }
    #endregion

    #region Properties

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return tree_.DiscountCurve; }
    }

    /// <summary>
    ///   Volatility Curve
    /// </summary>
    public VolatilityCurve[] VolatilityCurves
    {
      get { return tree_.VolatilityCurves; }
    }

    #endregion Properties

    #region Data

    private bool[] exercisable_;
    private int[] indexMap_;
    private BgmTreeCalculator tree_;

    #endregion // Data
  }
}
