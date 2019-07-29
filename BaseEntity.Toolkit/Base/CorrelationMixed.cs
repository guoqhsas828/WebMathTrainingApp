/*
 * CorrelationMixed.cs
 *
 * Correlation term structure
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   Mixed correlations
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This class provides basic data structures and defines basic interface
  ///   for all correlation mix objects.</para>
  ///   <para>At this implementation, this class is for internal use only.</para>
  /// </remarks>
  ///
  /// <exclude />
  [Serializable]
  public class CorrelationMixed : CorrelationObject, ICorrelationBump
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CorrelationMixed));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <exclude />
    public CorrelationMixed(
      CorrelationObject[] correlations,
      double[] weights)
      : this(correlations,weights, 0.0, 1.0)
    { }

    internal CorrelationMixed(
      CorrelationObject[] correlations,
      double[] weights, double min, double max)
      : base(min, max)
    {
      // Sanity check
      if (correlations != null && (weights == null || correlations.Length != weights.Length))
        throw new ArgumentException(String.Format("correlations and wights not match"));
      correlations_ = correlations;
      weights_ = weights;
      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      CorrelationMixed obj = (CorrelationMixed)base.Clone();

      if (correlations_ != null)
      {
        obj.correlations_ = new CorrelationObject[correlations_.Length];
        for (int i = 0; i < correlations_.Length; ++i)
          if (correlations_[i] != null)
            obj.correlations_[i] = (CorrelationObject)correlations_[i].Clone();
      }
      obj.weights_ = CloneUtil.Clone(weights_);
      return obj;
    }

    #endregion // Constructors

    #region Methods
    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// 
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    public override double BumpCorrelations(double bump, bool relative, bool factor)
    {
      double avgBump = 0;
      if (correlations_ != null)
        for (int i = 0; i < correlations_.Length; ++i)
          avgBump += correlations_[i].BumpCorrelations(bump, relative, factor) / (1 + i);
      return avgBump;
    }

    ///
    /// <summary>
    ///   Bump correlations by index
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    /// </remarks>
    ///
    /// <param name="idx">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    public override double BumpCorrelations(int idx, double bump, bool relative, bool factor)
    {
      double avgBump = 0;
      if(correlations_ != null)
        for (int i = 0; i < correlations_.Length; ++i)
          avgBump += correlations_[i].BumpCorrelations(idx, bump, relative, factor) / (1 + i);
      return avgBump;
    }

    /// <summary>
    ///   Convert correlation to a data table
    /// </summary>
    ///
    /// <returns>Content orgainzed in a data table</returns>
    public override DataTable Content()
    {
      throw new ToolkitException("The method or operation is not implemented.");
    }

    /// <summary>
    ///   Get name of item i
    /// </summary>
    /// <param name="i">index</param>
    /// <returns>name</returns>
    public string GetName(int i)
    {
      if (correlations_ != null && correlations_.Length > 0)
        return ((ICorrelationBump)correlations_[0]).GetName(i);
      return null;
    }

    ///
    /// <summary>
    ///   Correlation of defaults between name i and j
    /// </summary>
    ///
    /// <param name="c">Index of correlation object</param>
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    ///
    public double GetCorrelation(int c, int i, int j)
    {
      if (correlations_.Length <= c || c < 0)
        throw new System.ArgumentException(String.Format(
          "Invalid correlation index {0} (must be less than {1})", c, correlations_.Length));
      return ((Correlation)correlations_[c]).GetCorrelation(i, j);
    }

    ///
    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    ///
    /// <param name="factor">factor to set</param>
    ///
    /// <remarks>
    ///   The correlation between pairs are set to the square of the factor.
    /// </remarks>
    public void SetFactor(double factor)
    {
      foreach (CorrelationObject correlation in correlations_)
      {
        ICorrelationSetFactor corr = (ICorrelationSetFactor)correlation;
        corr.SetFactor(factor);
      }
    }

    /// <summary>
    ///   Set the correlation data from another
    ///   correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal override void SetCorrelations(CorrelationObject source)
    {
      if (source == null)
        throw new ArgumentException("The source object can not be null.");

      CorrelationMixed other = source as CorrelationMixed;
      if (other == null)
        throw new ArgumentException("The source object is not a base correlation object.");

      if (this.correlations_ == null)
        throw new NullReferenceException("The correlation array is null.");

      if (other.correlations_ == null
        || other.correlations_.Length != this.correlations_.Length)
      {
        throw new ArgumentException("The source correlation array does not match this data.");
      }

      for (int i = 0; i < correlations_.Length; ++i)
        this.correlations_[i].SetCorrelations(other.correlations_[i]);

      return;
    }
    #endregion // Methods

    #region Properties
    /// <summary>
    ///    Number of names
    /// </summary>
    public int NameCount
    {
      get { return correlations_ != null && correlations_.Length > 0 
        ? ((ICorrelationBump)correlations_[0]).NameCount : 0; }
    }

    /// <summary>
    ///   Maturity dates
    /// </summary>
    public CorrelationObject[] CorrelationObjects
    {
      get { return correlations_; }
    }

    /// <summary>
    ///    Weights
    /// </summary>
    public double[] Weights
    {
      get { return weights_; }
    }
    #endregion Properties

    #region Data
    private CorrelationObject[] correlations_;
    private double[] weights_;
    #endregion Data

  } // class Correlation

}
