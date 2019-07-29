// 
//  -2013. All rights reserved.
// 

using System;
using System.Data;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Factor copula correlation object
  /// </summary>
  /// <remarks>
  /// In this class individual names have the different factors.
  /// At this implementation, the number of factors for an individual name
  /// should not exceed three.
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a correlation object.</para>
  /// <code language="C#">
  ///   string [] names = { "IBM", "HP", "DELL" };                  // List of underlying names
  ///
  ///   // Construct a one factor correlation set
  ///   double [] correlations1 = { 0.30, 0.20, 0.10 };
  ///   FactorCorrelation correlation1 =
  ///     new FactorCorrelation( names,                            // List of underlying names
  ///                            correlations1 );                  // Array of single factor correlations
  ///
  ///   // Construct a three factor correlation set (by factor order)
  ///   double [] correlations = { 0.30, 0.30, 0.20,
  ///                              0.20, 0.20, 0.20,
  ///                              0.10, 0.10, 0.10 };
  ///   double [] correlations3 = { 0.30, 0.20, 0.10 };
  ///   FactorCorrelation correlation3 =
  ///     new FactorCorrelation( names,                            // List of underlying names
  ///                            correlations3 );                  // Array of three factor correlations
  /// </code>
  /// </example>
  [Serializable]
  public class FactorCorrelation : Correlation
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(FactorCorrelation));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="numFactors">Number of factors for individual name</param>
    /// <param name="data">Copula correlation data</param>
    public FactorCorrelation(
      string[] names,
      int numFactors,
      double[] data
      )
      : base(names, data)
    {
      // sanity checks
      if (names == null || names.Length == 0)
        throw new ArgumentException("Null names array");
      int dataSizeNeeded = numFactors * names.Length;
      if (numFactors <= 0)
        throw new ArgumentOutOfRangeException("numFactors", @"Number of factor must be greater than 0");
      if (data.Length != dataSizeNeeded)
        throw new ArgumentException(String.Format("Invalid data length {0}, should be {1}", data.Length, dataSizeNeeded));
      NumFactors = numFactors;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="numFactors">Number of factors for individual name</param>
    /// <param name="data">Copula correlation data</param>
    public FactorCorrelation(
      string[] names,
      int numFactors,
      double[,] data
      )
      : base(names, new double[names.Length * numFactors])
    {
      if (names == null || names.Length == 0)
        throw new ArgumentException("Null names array");
      if (numFactors <= 0)
        throw new ArgumentOutOfRangeException("numFactors", @"Number of factor must be greater than 0");
      if (data.GetLength(0) != numFactors || data.GetLength(1) != names.Length)
        throw new ArgumentException(String.Format("Invalid data size {0}x{1} but expected {2}x{3}", data.GetLength(0), data.GetLength(1), numFactors, names.Length));
      Buffer.BlockCopy(data, 0, Correlations, 0, data.Length * sizeof(double));
      NumFactors = numFactors;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    ///   This function is designed to be used in derived classes and
    ///   it does not check for data consistency.
    /// </remarks>
    /// <param name="names">Names for basket</param>
    /// <param name="data">Copula correlation data</param>
    /// <param name="numFactors">Number of factors for individual name</param>
    protected FactorCorrelation(
      string[] names,
      double[] data,
      int numFactors
      )
      : base(names, data)
    {
      // sanity checks
      NumFactors = numFactors;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// The fth factor of name i
    /// </summary>
    /// <param name="f">Index of the factor</param>
    /// <param name="i">Index of the name</param>
    public virtual double GetFactor(int f, int i)
    {
      if (f < 0 || f >= NumFactors)
        throw new ToolkitException(String.Format("Invalid factor index {0} (number of factors {1})", f, NumFactors));
      return Correlations[BasketSize * f + i];
    }

    /// <summary>
    /// Correlation of defaults between name i and j
    /// </summary>
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    public override double GetCorrelation(int i, int j)
    {
      if (i == j) return 1.0;
      double res = 0.0;
      for (int f = 0; f < NumFactors; f++)
        res += GetFactor(f, i) * GetFactor(f, j);
      return res;
    }

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
    /// <returns>The average change in correlations</returns>
    public override double BumpCorrelations(int i, double bump, bool relative, bool factor)
    {
      double delta = 0;
      for (int f = 0; f < this.NumFactors; f++)
      {
        int idx = i + this.BasketSize * f;
        double orig = (factor) ? this.Correlations[idx] : this.Correlations[idx] * this.Correlations[idx];
        double corr = relative
                        ? orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
                        : orig + bump / this.NumFactors;
        if (corr > 1)
          corr = 1;
        else if (corr < 0)
          corr = 0;
        this.Correlations[idx] = (factor) ? corr : Math.Sqrt(corr);
        delta += corr - orig;
      }

      return delta;
    }

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
    /// <param name="basketSize">Basket size</param>
    /// <param name="numFactors">Number of factors</param>
    /// <param name="data">Array of factors</param>
    /// <param name="start">Position of the first factor in data array</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    /// <returns>The average change in correlations</returns>
    public static double BumpCorrelationsAt(
      int basketSize, int numFactors, double[] data,
      int start, double bump, bool relative, bool factor)
    {
      double delta = 0;
      for (int f = 0; f < numFactors; f++)
      {
        int idx = start + basketSize * f;
        double orig = (factor) ? data[idx] : data[idx] * data[idx];
        double corr = relative
                        ? orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
                        : orig + bump / numFactors;
        if (corr > 1)
          corr = 1;
        else if (corr < 0)
          corr = 0;
        data[idx] = (factor) ? corr : Math.Sqrt(corr);
        delta += corr - orig;
      }

      return delta;
    }

    /// <summary>
    /// Bump all the correlations simultaneously
    /// </summary>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    /// <returns>The average change in factors</returns>
    public override double BumpCorrelations(double bump, bool relative, bool factor)
    {
      double delta = 0.0;
      for (int i = 0; i < this.Correlations.Length; i++)
      {
        double orig = (factor) ? this.Correlations[i] : this.Correlations[i] * this.Correlations[i];
        double corr = relative
                        ? orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
                        : orig + bump / this.NumFactors;
        if (corr > 1)
          corr = 1;
        else if (corr < 0)
          corr = 0;
        this.Correlations[i] = (factor) ? corr : Math.Sqrt(corr);
        delta += corr - orig;
      }

      return delta / this.BasketSize;
    }

    /// <summary>
    /// Set all the factors to the same value
    /// </summary>
    /// <param name="factor">Factor to set</param>
    public override void
      SetFactor(double factor)
    {
      double[] data = Correlations;
      for (int i = 0; i < data.Length; ++i)
        data[i] = factor;
    }

    /// <summary>
    /// Set all the factors to the same value
    /// </summary>
    /// <param name="factor">Factor to set</param>
    /// <param name="fromDate">Date from which to set the factor (ignored)</param>
    public override void
      SetFactor(Dt fromDate, double factor)
    {
      SetFactor(factor);
    }

    /// <summary>
    /// Convert correlation to a data table
    /// </summary>
    /// <returns>Content orgainzed in a data table</returns>
    public override DataTable
      Content()
    {
      DataTable dataTable = new DataTable("Factors");
      dataTable.Columns.Add(new DataColumn("Name", typeof(string)));
      if (1 == NumFactors)
        dataTable.Columns.Add(new DataColumn("Factor", typeof(double)));
      else
      {
        for (int i = 1; i <= NumFactors; ++i)
        {
          string col = "Factor " + i;
          dataTable.Columns.Add(new DataColumn(col, typeof(double)));
        }
      }

      int basketSize = this.BasketSize;
      string[] names = this.Names;
      double[] data = this.Correlations;
      for (int i = 0; i < basketSize; i++)
      {
        DataRow row = dataTable.NewRow();
        row["Name"] = names[i];
        if (1 == NumFactors)
          row["Factor"] = data[i];
        else
        {
          for (int j = 1, idx = i; j <= NumFactors; idx += basketSize, ++j)
          {
            string col = "Factor " + j;
            row[col] = data[idx];
          }
        }
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    /// <summary>
    /// Correlations as 2D Array
    /// </summary>
    /// <returns>Array of correlations</returns>
    public override double[,] ToMatrix()
    {
      var corrs = new double[NameCount, NumFactors];
      Buffer.BlockCopy(Correlations, 0, corrs, 0, NumFactors * sizeof(double));
      return corrs;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Number of factors (read only)
    /// </summary>
    public int NumFactors { get; private set; }

    #endregion Properties
  }
}