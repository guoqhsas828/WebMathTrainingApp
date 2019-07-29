// 
//  -2013. All rights reserved.
// 

using System;
using System.Data;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Factor copula correlation with external defaults
  /// </summary>
  /// <remarks>
  ///   This class contains data structure for
  ///   totally external default model, which adds an external factor to
  ///   conventional one factor model.  The structure of the model is
  ///   described by the following equations:
  ///   <formula>
  ///     X_i = \rho_i Y + \sqrt{1 - \rho^2_i} \xi_{i,1}
  ///   </formula>
  ///   <formula>
  ///     Z_i = \mu_i + \sigma_i \xi_{i,2}
  ///   </formula>
  ///   where <c>i</c> denotes individual credit names,
  ///   <formula inline="true"> Y </formula>,
  ///   <formula inline="true"> \xi_{i,1} </formula>
  ///   and
  ///   <formula inline="true"> \xi_{i,2} </formula>
  ///   are all independent random variables.
  ///   The distribution of default times is mapped by the relationship
  ///   <formula>
  ///      \tau_i \leq t  \iff \min\{X_i, Z_i\} \leq \chi_i(t)
  ///    </formula>
  ///   where <formula inline="true"> \chi_i(t) </formula> is called default
  ///   threshold.
  ///   This class provides the data for 
  ///   <formula inline="true"> \rho_i </formula>,
  ///   <formula inline="true"> \mu_i </formula> and
  ///   <formula inline="true"> \sigma_i </formula>.
  /// </remarks>
  [Serializable]
  public class ExternalFactorCorrelation : FactorCorrelation
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ExternalFactorCorrelation));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Names for basket</param>
    /// <param name="data">Copula correlation data</param>
    public ExternalFactorCorrelation(
      string[] names,
      double[] data
      )
      : base(names, data, 1)
    {
      // sanity checks
      int dataSizeNeeded = 3 * names.Length;
      if (data.Length != dataSizeNeeded)
        throw new ArgumentException(String.Format("Invalid data length {0}, should be {1}", data.Length, dataSizeNeeded));
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// The factor of name i
    /// </summary>
    /// <param name="i">Index of the name</param>
    public double GetFactor(int i)
    {
      return Correlations[i];
    }

    /// <summary>
    /// The external mean of name i
    /// </summary>
    /// <param name="i">Index of the name</param>
    public double GetMu(int i)
    {
      int N = BasketSize;
      return Correlations[N + i];
    }

    /// <summary>
    /// The external standard deviation of name i
    /// </summary>
    /// <param name="i">Index of the name</param>
    public double GetSigma(int i)
    {
      int N = BasketSize;
      return Correlations[2 * N + i];
    }

    /// <summary>
    ///   Set all the factors to the same value
    /// </summary>
    /// <param name="factor">Factor to set</param>
    public override void SetFactor(double factor)
    {
      double[] data = Correlations;
      int N = BasketSize;
      for (int i = 0; i < N; ++i)
        data[i] = factor;
    }

    /// <summary>
    /// Set parameters for all the names
    /// </summary>
    /// <param name="factor">Factor to set</param>
    /// <param name="mu">Mean shift to set</param>
    /// <param name="sigma">Standard deviation to set</param>
    public void SetParameters(double factor, double mu, double sigma)
    {
      double[] data = Correlations;
      int N = BasketSize;
      for (int i = 0; i < N; ++i)
      {
        data[i] = factor;
        data[N + i] = mu;
        data[N + N + i] = sigma;
      }
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
      dataTable.Columns.Add(new DataColumn("Mu", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Sigma", typeof(double)));

      int N = this.BasketSize;
      string[] names = this.Names;
      double[] data = this.Correlations;
      for (int i = 0; i < N; i++)
      {
        DataRow row = dataTable.NewRow();
        row["Name"] = names[i];
        row["Factor"] = data[i];
        row["Mu"] = data[N + i];
        row["Sigma"] = data[N + N + i];
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    #endregion Methods
  }
}