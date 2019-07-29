// 
//  -2013. All rights reserved.
// 

using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Base class for all basket correlation objects
  /// </summary>
  /// <remarks>
  /// This class provides basic data structures and defines basic interface
  /// for all basket correlation objects.
  /// </remarks>
  [Serializable]
  public abstract class Correlation : CorrelationObject, ICorrelationBump, ICorrelationSetFactor
  {
    // Logger
    //private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Correlation));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Correlated names</param>
    /// <param name="data">Copula correlation data</param>
    protected Correlation(string[] names, double[] data)
      : this(names, data, 0.0, 1.0)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="Correlation"/> class.
    /// </summary>
    /// <param name="names">Correlated names</param>
    /// <param name="data">Copula correlation data</param>
    /// <param name="min">The minimum correlation allowed.</param>
    /// <param name="max">The maximum correlation allowed.</param>
    protected Correlation(string[] names, double[] data, double min, double max)
      : base(min, max)
    {
      // sanity checks
      if (data == null || data.Length == 0)
        throw new ArgumentException("Null correlations data array");
      if (names == null || (data.Length != 1 && names.Length == 0))
        throw new ArgumentException("Null names array");
      Correlations = data;
      Names = names;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (Correlation)base.Clone();
      obj.Correlations = new double[Correlations.Length];
      for (int i = 0; i < Correlations.Length; i++)
        obj.Correlations[i] = Correlations[i];
      if (Names != null)
      {
        obj.Names = new string[Names.Length];
        for (int i = 0; i < Names.Length; i++)
          obj.Names[i] = Names[i];
      }
      else
        obj.Names = null;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Correlation of defaults between name i and j
    /// </summary>
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    public abstract double GetCorrelation(int i, int j);

    /// <summary>
    /// Set the correlation between any pair of credits to be the same number
    /// </summary>
    /// <remarks>This correlation equals = factor*factor.</remarks>
    /// <param name="factor">factor to set</param>
    public abstract void SetFactor(double factor);

    /// <summary>
    /// Set the correlation between any pair of credits to be the same number
    /// </summary>
    /// <param name="factor">Factor (square root of correlation) to set</param>
    /// <param name="fromDate">
    ///   Maturity date to set, effective only when the correlation is a term structure,
    ///   in which case all the factors with maturity dates on or later than the from
    ///   date are set to the specified value.
    /// </param>
    public abstract void SetFactor(Dt fromDate, double factor);

    /// <summary>
    /// Correlation of defaults between two names
    /// </summary>
    /// <param name="name1">Index of first name</param>
    /// <param name="name2">Index of second name</param>
    public double GetCorrelation(string name1, string name2)
    {
      int i = Index(name1);
      if (i < 0)
        throw new ArgumentException(String.Format("Name {0} not in correlation matrix", name1));
      int j = Index(name2);
      if (j < 0)
        throw new ArgumentException(String.Format("Name {0} not in correlation matrix", name2));
      return GetCorrelation(i, j);
    }

    /// <summary>
    /// Test if name exists in correlation matrix
    /// </summary>
    /// <param name="name">Name to search for</param>
    /// <returns>True if correlation for this name exists</returns>
    public bool HaveCorrelation(string name)
    {
      return (Index(name) >= 0);
    }

    /// <summary>
    /// Get index matching name
    /// </summary>
    /// <param name="name">Name to search for</param>
    /// <returns>index of name or -1 if not found</returns>
    public int Index(string name)
    {
      // TBD. Speed up this access. Use indexed names.
      for (int i = 0; i < Names.Length; i++)
        if (Names[i] == name)
          return i;
      return -1;
    }

    /// <summary>
    /// Get name
    /// </summary>
    /// <param name="i">index</param>
    /// <returns>name</returns>
    public string GetName(int i)
    {
      if (i < 0 || i >= Names.Length)
        throw new ArgumentOutOfRangeException("i", String.Format("index {0} is out of range", i));
      return Names[i];
    }

    /// <summary>
    /// Set the correlation data from another correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal override void SetCorrelations(CorrelationObject source)
    {
      if (source == null)
        throw new ArgumentException("The source object can not be null.");
      var other = source as Correlation;
      if (other == null)
        throw new ArgumentException("The source object is not a correlation object.");
      if (Correlations == null)
        throw new NullReferenceException("The correlation data is null.");
      if (other.Correlations == null || other.Correlations.Length != Correlations.Length)
        throw new ArgumentException("The source correlation data does not match this data.");
      for (int i = 0; i < Correlations.Length; ++i)
        Correlations[i] = other.Correlations[i];
    }

    /// <summary>
    /// Correlations as 2D Array
    /// </summary>
    /// <returns>Array of correlations</returns>
    public virtual double[,] ToMatrix()
    {
      throw new ArgumentException(String.Format("ToMatrix not supported for {0} {1}", GetType().FullName, Name));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Number of names (read only)
    /// </summary>
    public int NameCount
    {
      get { return Names.Length; }
    }

    /// <summary>
    /// Number of names (read only)
    /// </summary>
    public int BasketSize
    {
      get { return Names.Length; }
    }

    /// <summary>
    /// Names correlations refer to
    /// </summary>
    public string[] Names { get; private set; }

    /// <summary>
    /// Array of correlation data
    /// </summary>
    public double[] Correlations { get; set; }

    /// <exclude />
    public double StdError { get; set; }

    /// <exclude />
    public double MaxError { get; set; }

    #endregion Properties
  }
}