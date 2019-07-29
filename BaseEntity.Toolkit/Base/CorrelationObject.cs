// 
//  -2013. All rights reserved.
// 

using System;
using System.Data;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Base class for all correlation and base correlation objects
  /// </summary>
  /// <remarks>
  ///   <para>This class is a pure abstract class used to provide a common base for
  ///   all correlation and base correlation object, in order to help compiling time
  ///   type check.</para>
  ///   <para>Common types of correlations include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="BaseCorrelation">Base correlation surfaces</see></description></item>
  ///     <item><description><see cref="SingleFactorCorrelation">Single factor correlation</see></description></item>
  ///     <item><description><see cref="GeneralCorrelation">General pairwise correlation</see></description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public abstract class CorrelationObject : BaseEntityObject
  {
    // Logger
    //private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CorrelationObject));

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <returns>Created base correlation object</returns>
    protected CorrelationObject()
      : this(0.0, 1.0)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationObject"/> class.
    /// </summary>
    /// <param name="min">The minimum correlation allowed.</param>
    /// <param name="max">The maximum correlation allowed.</param>
    protected CorrelationObject(double min, double max)
    {
      Modified = false;
      MinCorrelation = min;
      MaxCorrelation = max;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Bump correlations by index
    /// </summary>
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    /// </remarks>
    /// <param name="i">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    /// <returns>The average change in correlation</returns>
    public abstract double BumpCorrelations(int i, double bump, bool relative, bool factor);

    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    /// <returns>The average change in correlations</returns>
    public abstract double BumpCorrelations(double bump, bool relative, bool factor);

    /// <summary>
    /// Convert correlation to a data table
    /// </summary>
    /// <returns>Content orgainzed in a data table</returns>
    public abstract DataTable Content();

    /// <summary>
    /// Set the correlation data from another correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal abstract void SetCorrelations(CorrelationObject source);

    /// <summary>
    /// Set the correlation ready state
    /// </summary>
    internal void SetReadyState(bool ready)
    {
      Modified = !ready;
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      var obj = (CorrelationObject)base.Clone();
      obj.Name = Name;
      obj.Modified = Modified;
      return obj;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Name of correlation object
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Indicator that the state is modified
    /// </summary>
    /// <exclude />
    public bool Modified { get; set; }

    /// <summary>
    /// The maximum correlation allowed
    /// </summary>
    public double MaxCorrelation { get; set; }

    /// <summary>
    /// The minimum correlation allowed
    /// </summary>
    public double MinCorrelation { get; set; }

    /// <summary>
    /// The minimum factor (the square root of the minimum correlation)
    /// </summary>
    internal double MinFactor
    {
      get { return Math.Sqrt(MinCorrelation); }
    }

    /// <summary>
    /// The maximum factor (the square root of the maximum correlation)
    /// </summary>
    internal double MaxFactor
    {
      get { return Math.Sqrt(MaxCorrelation); }
    }

    #endregion Properties
  }
}