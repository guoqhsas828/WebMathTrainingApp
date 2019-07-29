using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   ByTenorSelection resprests a collection of tenors selected as
  ///   equivalents to each other.  This is a special internal class
  ///   used directly in sensitivity routine to keep codes clean and
  ///   consistent.
  /// </summary>
  internal class ByTenorSelection : ICurveTenorSelection
  {
    #region Nested Type
    private class BumpHandler : ICurveBumpHandler
    {
      private CurveTenor[] tenors_;
      private CurveShifts.BumpSpec spec_;

      public BumpHandler(CurveTenor[] tenors, BumpFlags flags, double size)
      {
        tenors_ = tenors;
        spec_ = new CurveShifts.BumpSpec(size, flags);
      }

      #region ICurveBumpHandler Members

      public bool HasAffected(CurveShifts shifts)
      {
        return tenors_.Any(shifts.ContainsTenor);
      }

      public double[] GetShiftValues(CurveShifts shifts)
      {
        return shifts[tenors_[0], spec_];
      }

      public void SetShiftValues(CurveShifts shifts, double[] values)
      {
        shifts[tenors_[0], spec_] = values;
      }

      #endregion
    }
    #endregion

    #region Data
    private readonly CurveTenor[] tenors_;
    private readonly IReEvaluator hedgePricer_;
    private readonly CalibratedCurve[] curves_;
    private DependencyGraph<CalibratedCurve> allCurves_;
    #endregion

    #region Methods and Properties
    public ByTenorSelection(DependencyGraph<CalibratedCurve> allCurves,
      CalibratedCurve[] curves, CurveTenor[] tenors, IReEvaluator hedgePricer)
    {
      allCurves_ = allCurves;
      Validate(tenors);
      tenors_ = tenors;
      hedgePricer_ = hedgePricer;
      curves_ = curves;
    }

    [Conditional("DEBUG")]
    private static void Validate(CurveTenor[] tenors)
    {
      if (tenors == null || tenors.Length == 0)
        throw new ToolkitException("Tenors cannot be empty");
    }

    /// <summary>
    ///  Gets all the tenors considered equivalent.
    /// </summary>
    /// <remarks></remarks>
    public CurveTenor[] EquivalentTenors { get { return tenors_; } }

    /// <summary>
    /// Gets the key tenor.
    /// </summary>
    /// <remarks></remarks>
    public CurveTenor KeyTenor { get { return tenors_[0]; } }

    /// <summary>
    /// Gets the hedge pricer.
    /// </summary>
    /// <remarks></remarks>
    public IReEvaluator HedgePricer { get { return hedgePricer_; }}

    /// <summary>
    /// Evaluates the hedge value.
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public double EvaluateHedge()
    {
      return hedgePricer_ == null ? 0.0 : hedgePricer_.ReEvaluate();
    }
    #endregion

    #region ICurveTenorSelector Members

    public IList<CurveTenor> Tenors
    {
      get { return tenors_; }
    }

    public string Name
    {
      get { return KeyTenor.Name; }
    }

    public IList<CalibratedCurve> Curves
    {
      get { return curves_; }
    }

    public IEnumerable<CalibratedCurve> AllCurves
    {
      get { return allCurves_.ReverseOrdered(); }
    }

    public ICurveBumpHandler GetBumpHandler(BumpFlags flags, params double[] bumpSizes)
    {
      if (bumpSizes == null || bumpSizes.Length == 0)
      {
        throw new ToolkitException("Bump sizes cannot be empty array");
      }
      return new BumpHandler(tenors_, flags, bumpSizes[0]);
    }

    #endregion
  }
}
