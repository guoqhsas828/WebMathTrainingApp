using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Interface to be implemented by all the binomial short rate tree model
  /// </summary>
  public interface IBinomialShortRateTreeModel
  {
    /// <summary>
    /// Gets the short rate tree.
    /// </summary>
    /// <returns>IReadOnlyList&lt;System.Double[]&gt;.</returns>
    IReadOnlyList<double[]> GetRateTree();

    /// <summary>
    /// Gets the discount factor tree.
    /// </summary>
    /// <returns>IReadOnlyList&lt;System.Double[]&gt;.</returns>
    IReadOnlyList<double[]> GetDiscountFactorTree();

    /// <summary>
    /// Sets the short rate tree.
    /// </summary>
    /// <param name="rateTree">The rate tree.</param>
    void SetRateTree(IReadOnlyList<double[]> rateTree);

    /// <summary>
    /// Create a new tree with the short rate volatility
    ///  bumped uniformly by the specified size
    /// </summary>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <returns>IBinomialShortRateTreeModel.</returns>
    IBinomialShortRateTreeModel BumpSigma(double bumpSize);

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    DiscountCurve DiscountCurve { get; }
  }

  internal static class BinomialRateTreeModelExtensions
  {
    public static IReadOnlyList<double[]> CloneRateTree(
      this IBinomialShortRateTreeModel rateTreeModel)
    {
      var rateTree = rateTreeModel.GetRateTree();
      var count = rateTree.Count;
      var clonedRateTree = new double[count][];
      for (int i = 0; i < count; i++)
      {
        clonedRateTree[i] = (double[])rateTree[i].Clone();
      }
      return clonedRateTree;
    }
  }

  /// <summary>
  ///  Types of the short rate models
  /// </summary>
  public enum ShortRateModelType
  {
    /// <summary>
    /// Method not specified
    /// </summary>
    None,

    /// <summary>
    ///  Evaluate callable bond assuming the short rate follows Black-Karasinski process
    /// </summary>
    BlackKarasinski,

    /// <summary>
    ///  Evaluate callable bond assuming the short rate follows Hull-White process
    /// </summary>
    HullWhite
  }
}
