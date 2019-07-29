// 
//  -2013. All rights reserved.
// 

using System;
using System.Data;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Single factor-based copula correlation
  /// </summary>
  /// <remarks>
  ///   This can either be a factor copula correlation where all names have the same factor,
  ///   or a Archimedean copula where the factor is interpreted as Kendall <formula inline="true">\tau</formula>.
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a correlation object.</para>
  /// <code language="C#">
  ///   string [] names = { "IBM", "HP", "DELL" };                  // List of underlying names
  ///
  ///   // Construct a single factor correlation
  ///   SingleFactorCorrelation correlation =
  ///     new SingleFactorCorrelation( names,                       // List of underlying names
  ///                                  0.30 );                      // Single correlation factor of 30%
  /// </code>
  /// </example>
  [Serializable]
  public class SingleFactorCorrelation : FactorCorrelation
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SingleFactorCorrelation));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="factor">Single factor for all names</param>
    public SingleFactorCorrelation(string[] names, double factor)
      : base(names, new double[] {factor}, 1)
    {}

    #endregion Constructors

    #region Methods

    /// <summary>
    /// The the single factor
    /// </summary>
    public double GetFactor()
    {
      return Correlations[0];
    }

    /// <summary>
    /// The fth factor of name i
    /// </summary>
    /// <param name="f">Index of the factor</param>
    /// <param name="i">Index of the name</param>
    public override double GetFactor(int f, int i)
    {
      return Correlations[0];
    }

    /// <summary>
    /// Correlation of defaults between name i and j
    /// </summary>
    /// <param name="i">Index of name i</param>
    /// <param name="j">Index of name j</param>
    public override double GetCorrelation(int i, int j)
    {
      if (i == j) return 1.0;
      return (Correlations[0] * Correlations[0]);
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
      return BumpCorrelations(bump, relative, factor);
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
      double orig = (factor) ? Correlations[0] : Correlations[0] * Correlations[0];
      double corr = (relative)
                      ? corr = orig * ((bump > 0.0) ? (1.0 + bump) : (1.0 / (1.0 - bump)))
                      : corr = orig + bump;
      if (corr > 1)
        corr = 1;
      else if (corr < 0)
        corr = 0;
      Correlations[0] = (factor) ? corr : Math.Sqrt(corr);

      return corr - orig;
    }

    /// <summary>
    /// Convert correlation to a data table
    /// </summary>
    /// <returns>Content orgainzed in a data table</returns>
    public override DataTable Content()
    {
      DataTable dataTable = new DataTable("Factors");
      dataTable.Columns.Add(new DataColumn("Name", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Factor", typeof(double)));

      int basketSize = this.BasketSize;
      string[] names = this.Names;
      double[] data = this.Correlations;
      for (int i = 0; i < basketSize; i++)
      {
        DataRow row = dataTable.NewRow();
        row["Name"] = names[i];
        row["Factor"] = data[0];
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    #endregion Methods
  }
}