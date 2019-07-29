// 
//  -2013. All rights reserved.
// 

using System;
using System.Data;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// General pairwise correlation matrix
  /// </summary>
  /// <remarks>
  ///   In this class the correlation is specified by a general
  ///   (pairwise) correlation matrix.
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a correlation object.</para>
  /// <code language="C#">
  ///   string [] names = { "IBM", "HP", "DELL" };                  // List of underlying names
  ///   // Construct a pairwise correlation matrix with all correlations a single number
  ///   GeneralCorrelation correlation1 =
  ///     new GeneralCorrelation( names,                            // List of underlying names
  ///                             0.30 );                           // Single correlation factor of 30%
  ///   // Construct a 3x3 pairwise correlation matrix ( in row then column order )
  ///   double [] correlations = { 1.00, 0.30, 0.20,
  ///                              0.30, 1.00, 0.10,
  ///                              0.20, 0.10, 1.00 };
  ///   GenralCorrelation correlation1 =
  ///     new GeneralCorrelation( names,                            // List of underlying names
  ///                             correlations );                   // Array of pairwise correlations
  /// </code>
  /// </example>
  [Serializable]
  public class GeneralCorrelation : Correlation
  {
    // Logger
    //private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(GeneralCorrelation));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="data">Copula correlation data</param>
    public GeneralCorrelation(
      string[] names,
      double[] data
      )
      : base(names, data)
    {
      // sanity checks
      if (names == null || names.Length == 0)
        throw new ArgumentException("Null names array");
      int dataSizeNeeded = names.Length * names.Length;
      if (data.Length != dataSizeNeeded)
        throw new ArgumentException(String.Format("Invalid data length {0}, should be {1}", data.Length, dataSizeNeeded));
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="data">Copula correlation data</param>
    public GeneralCorrelation(
      string[] names,
      double[,] data
      )
      : base(names, new double[names.Length * names.Length])
    {
      // sanity checks
      if (names == null || names.Length == 0)
        throw new ArgumentException("Null names array");
      int dataSizeNeeded = names.Length * names.Length;
      if (data.Length != dataSizeNeeded)
        throw new ArgumentException(String.Format("Invalid data length {0}, should be {1}", data.Length, dataSizeNeeded));
      Buffer.BlockCopy(data, 0, Correlations, 0, data.Length * sizeof(double));
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="corr">correlation between any pair</param>
    public GeneralCorrelation(
      string[] names,
      double corr
      )
      : base(names, new double[names.Length * names.Length])
    {
      if (names == null || names.Length == 0)
        throw new ArgumentException("Null names array");
      int n = BasketSize;
      double[] data = Correlations;
      for (int i = 0; i < n; ++i)
      {
        for (int j = 0; j < i; ++j)
          data[n * i + j] = data[n * j + i] = corr;
        data[n * i + i] = 1.0;
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Correlation of defaults between name i and j
    /// </summary>
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    public override double GetCorrelation(int i, int j)
    {
      if (i == j) return 1.0;
      return Correlations[BasketSize * i + j];
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
      double delta = (relative ? 0.0 : bump);
      int count = 0;

      double[] data = Correlations;
      int n = BasketSize;
      for (int j = 0; j < n; j++)
      {
        if (j != i)
        {
          if (relative)
          {
            double orig = data[i * n + j];
            double corr = data[i * n + j] =
                          (data[n * j + i] *= ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump))));
            delta += (corr - orig) / (++count);
          }
          else
            data[i * n + j] = (data[n * j + i] += bump);
        }
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
      double delta = 0;
      for (int i = 0; i < Correlations.Length; ++i)
      {
        double orig = Correlations[i];
        double corr = relative ? orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump))) : orig + bump;
        if (corr > 1.0)
          corr = 1.0;
        else if (corr < 0.0)
          corr = 0.0;
        Correlations[i] = corr;
        delta += corr - orig;
      }
      return delta / Correlations.Length;
    }

    /// <summary>
    /// Set correlation between any pair to the same number
    /// </summary>
    /// <param name="correlation">Correlation to set</param>
    public void SetCorrelation(double correlation)
    {
      double[] data = Correlations;
      for (int i = 0; i < data.Length; ++i)
        data[i] = correlation;
      int n = BasketSize;
      for (int i = 0; i < n; ++i)
        data[i * (n + 1)] = 1.0;
    }

    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    /// <remarks>This correlation equals = factor*factor.</remarks>
    /// <param name="factor">factor to set</param>
    public override void SetFactor(double factor)
    {
      SetCorrelation(factor * factor);
    }

    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    /// <remarks>This correlation equals = factor*factor.</remarks>
    /// <param name="factor">factor to set</param>
    /// <param name="fromDate">this parameter is ignored</param>
    public override void SetFactor(Dt fromDate, double factor)
    {
      SetCorrelation(factor * factor);
    }

    /// <summary>
    ///   Convert correlation to a data table
    /// </summary>
    /// <returns>Content orgainzed in a data table</returns>
    public override DataTable Content()
    {
      var dataTable = new DataTable("Correlations");
      dataTable.Columns.Add(new DataColumn("Name", typeof(string)));
      for (int i = 0; i < BasketSize; ++i)
        dataTable.Columns.Add(new DataColumn(Names[i], typeof(double)));
      for (int i = 0; i < BasketSize; ++i)
      {
        DataRow row = dataTable.NewRow();
        row["Name"] = Names[i];
        for (int j = 0; j < BasketSize; ++j)
          row[Names[j]] = GetCorrelation(i, j);
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
      var corrs = new double[NameCount,NameCount];
      Buffer.BlockCopy(Correlations, 0, corrs, 0, Correlations.Length * sizeof(double));
      return corrs;
    }

    #endregion Methods
  }
}