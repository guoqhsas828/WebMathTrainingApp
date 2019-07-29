using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  ///  Collection of the shifts to a specific curve in various scenarios.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class CurveShifts
  {
    #region Type
    [Serializable]
    internal class BumpSpec
    {
      public double BumpSize { get; private set; }
      public BumpFlags BumpFlags { get; private set; }
      public BumpSpec(double size, BumpFlags flags)
      {
        BumpSize = size;
        BumpFlags = flags & (BumpFlags.BumpRelative | BumpFlags.BumpDown | BumpFlags.BumpInPlace);
      }
      public override bool Equals(object obj)
      {
        var b = obj as BumpSpec;
        if (b == null) return base.Equals(obj);
        return BumpFlags == b.BumpFlags && Math.Abs(BumpSize - b.BumpSize) < 1E-16;
      }
      public override int  GetHashCode()
      {
        return BumpSize.GetHashCode()+BumpFlags.GetHashCode();
      }
    }
    #endregion

    #region Construtors
    /// <summary>
    /// Initializes a new instance of the <see cref="CurveShifts"/> class.
    /// </summary>
    /// <param name="tenors">The tenors.</param>
    /// <remarks></remarks>
    public CurveShifts(IEnumerable<CurveTenor> tenors)
    {
      if (tenors == null)
        throw new ToolkitException("Tenors cannot be null");
      int tenorCount = 0;
      allTenors_ = new Dictionary<CurveTenor, int>(CurveTenorComparer.Default);
      var dates = new UniqueSequence<Dt>();
      foreach (var t in tenors)
      {
        if (t == null) continue;
        if (allTenors_.ContainsKey(t)) continue;
        allTenors_.Add(t, tenorCount++);
        dates.Add(t.CurveDate);
      }
      byTenorShifts_ = new Dictionary<BumpSpec, double[][]>();
      scenarioShifts_=new Dictionary<CurveBumpScenario, double[]>(
        CurveBumpScenario.EqualityComparer);
    }

    #endregion

    #region Methods
    /// <summary>
    /// Determines whether the specified tenor contains tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor contains tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public bool ContainsTenor(CurveTenor tenor)
    {
      return allTenors_.ContainsKey(tenor);
    }

    /// <summary>
    /// Resets the by tenor shifts.
    /// </summary>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <remarks></remarks>
    public void ResetByTenorShifts(double bumpSize, BumpFlags bumpFlags)
    {
      byTenorShifts_.Remove(new BumpSpec(bumpSize,bumpFlags));
    }
    #endregion

    #region Properties

    /// <summary>
    /// Gets the shifts with the specified bump tenor.
    /// </summary>
    /// <remarks>The design allows to set values for different tenors simultaneously.</remarks>
    public double[] this[CurveTenor bumpTenor, double bumpSize, BumpFlags bumpFlags]
    {
      get { return this[bumpTenor, new BumpSpec(bumpSize, bumpFlags)]; }
      set { this[bumpTenor, new BumpSpec(bumpSize, bumpFlags)] = value; }
    }

    internal double[] this[CurveTenor bumpTenor, BumpSpec spec]
    {
      get
      {
        double[][] shifts;
        if (bumpTenor == null || !byTenorShifts_.TryGetValue(spec, out shifts))
        {
          return null;
        }
        int idx;
        if (allTenors_.TryGetValue(bumpTenor, out idx))
        {
          return shifts[idx];
        }
        return null;
      }
      set
      {
        if (bumpTenor == null) return;
        double[][] shifts;
        if (!byTenorShifts_.TryGetValue(spec, out shifts))
        {
          shifts = new double[allTenors_.Count][];
          byTenorShifts_.Add(spec, shifts);
        }
        int idx;
        if (allTenors_.TryGetValue(bumpTenor, out idx))
        {
          shifts[idx] = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets the double array with the specified scenario.
    /// </summary>
    /// <remarks></remarks>
    public double[] this[CurveBumpScenario scenario]
    {
      get
      {
        double[] shifts;
        if (scenario == null || !scenarioShifts_.TryGetValue(
          scenario, out shifts))
        {
          return null;
        }
        return shifts;
      }
      set
      {
        if (scenario == null) return;
        scenarioShifts_[scenario] = value;
      }
    }

    /// <summary>
    /// Gets all tenors.
    /// </summary>
    /// <remarks></remarks>
    public IEnumerable<CurveTenor> AllTenors
    {
      get { return allTenors_.Keys; }
    }

    /// <summary>
    /// Gets the overlay interp.
    /// </summary>
    /// <remarks></remarks>
    public Interp OverlayInterp
    {
      get { return overlayInterp_; }
    }
    #endregion Properties

    #region Data
    private readonly Dictionary<BumpSpec, double[][]> byTenorShifts_;
    private readonly Dictionary<CurveBumpScenario, double[]> scenarioShifts_;
    private readonly Dictionary<CurveTenor, int> allTenors_;
    private readonly Interp overlayInterp_ = new Weighted(new Const(), new Const());
    #endregion
  }
}
