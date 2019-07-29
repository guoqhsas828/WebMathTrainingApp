using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  ///  The interface to represent an exposure set
  /// </summary>
  public interface IExposureSet
  {
    /// <summary>
    /// Identifier for this set of exposures
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Number of simulated paths
    /// </summary>
    int PathCount { get; }

    /// <summary>
    /// Number of trades in the set
    /// </summary>
    int TradeCount { get; }

    /// <summary>
    /// Gets the exposure dates of the specified trade
    /// </summary>
    /// <param name="tradeIndex">The index of the trade</param>
    /// <returns>A list of the exposure dates</returns>
    IReadOnlyList<Dt> GetExposureDates(int tradeIndex);

    /// <summary>
    /// Gets the exposures of the specified trade
    /// </summary>
    /// <param name="tradeIndex">The index of the trade</param>
    /// <returns>The exposure object</returns>
    object GetExposures(int tradeIndex);
  }

  /// <inheritdoc />
  /// <summary>
  ///  The interface to represent an exposure set
  /// </summary>
  /// <typeparam name="T">
  ///   The exposure data type, currently it should
  ///   be either <see cref="float"/> or <see cref="double"/>.
  /// </typeparam>
  public interface IExposureSet<out T> : IExposureSet
  {
    /// <summary>
    /// Gets the exposures of the specified trade
    /// </summary>
    /// <param name="tradeIndex">The index of the trade</param>
    /// <returns>A matrix with each row represening a path</returns>
    new IReadOnlyMatrix<T> GetExposures(int tradeIndex);
  }

  /// <summary>
  /// Represents a set of simulated exposures for a single trade or netting set
  /// </summary>
  [Serializable]
  public class ExposureSet : IExposureSet<double>
  {
    /// <summary>
    /// Identifier for this set of exposures
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Number of simulated paths
    /// </summary>
    public int PathCount => Exposures.GetLength(0);

    /// <summary>
    /// Number of exposure dates along each path
    /// </summary>
    public int DateCount => ExposureDates.Length;

    /// <summary>
    /// Exposure dates
    /// </summary>
    public Dt[] ExposureDates { get; set; }

    /// <summary>
    /// the exposures
    /// </summary>
    public double[,] Exposures { get; set; }

    /// <summary>
    /// the simulated discount factors corresponding to exposures
    /// </summary>
    public double[,] DiscountFactors { get; set; }

    #region IExposureSet members

    int IExposureSet.TradeCount => 1;

    IReadOnlyList<Dt> IExposureSet.GetExposureDates(int tradeIndex)
      => ExposureDates;

    object IExposureSet.GetExposures(int tradeIndex) => Exposures;

    IReadOnlyMatrix<double> IExposureSet<double>.GetExposures(int tradeIndex)
      => Exposures.ToMatrix();

    #endregion
  }

  /// <summary>
  /// Represents a set of simulated exposures for a single trade, optionally including exposures after 1bp increase in coupon. 
  /// The Coupon01 is used to convert incremental XVA charge into a running spread
  /// </summary>
  [Serializable]
  public class IncrementalExposureSet : ExposureSet, IExposureSet<double>
  {
    /// <summary>
    /// the exposures for same trade after 1bp increase in coupon
    /// </summary>
    public double[,] Coupon01Exposures { get; set; }

    #region IExposureSet members

    int IExposureSet.TradeCount => 2;

    IReadOnlyList<Dt> IExposureSet.GetExposureDates(int tradeIndex)
      => ExposureDates;

    object IExposureSet.GetExposures(int tradeIndex)
      => tradeIndex == 0 ? Exposures : Coupon01Exposures;

    IReadOnlyMatrix<double> IExposureSet<double>.GetExposures(int tradeIndex)
      => (tradeIndex == 0 ? Exposures : Coupon01Exposures).ToMatrix();

    #endregion
  }

  /// <summary>
  ///  The exposure set
  /// </summary>
  [Serializable]
  public class ExposureMultiSet : IExposureSet
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="singlePrecision"></param>
    /// <param name="pointer"></param>
    /// <param name="pathCount"></param>
    /// <param name="tradeCount"></param>
    /// <param name="exposureDts"></param>
    public ExposureMultiSet(
      bool singlePrecision, IntPtr pointer,
      int pathCount, int tradeCount, Dt[][] exposureDts)
    {
      IsSinglePrecision = singlePrecision;
      _pointer = pointer;
      PathCount = pathCount;
      TradeCount = tradeCount;
      _exposureDts = exposureDts;
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsSinglePrecision { get; }

    /// <inheritdoc />
    public string Id { get; set; }

    /// <inheritdoc />
    public int PathCount { get; set; }

    /// <inheritdoc />
    public int TradeCount { get; set; }

    /// <summary>
    /// Number of exposure dates along each path for trade i
    /// </summary>
    public int GetDateCount(int i)
    {
      return GetExposureDates(i).Length;
    }

    /// <summary>
    /// Exposure dates for trade i
    /// </summary>
    public Dt[] GetExposureDates(int i) => _exposureDts[i];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tradeIndex"></param>
    /// <returns></returns>
    public object GetExposures(int tradeIndex)
    {
      return IsSinglePrecision
        ? (object) ReadOnlyMatrix.Create<float>(
          GetPointer(tradeIndex), PathCount, GetDateCount(tradeIndex))
        : ReadOnlyMatrix.Create<double>(
          GetPointer(tradeIndex), PathCount, GetDateCount(tradeIndex));
    }

    /// <summary>
    ///  Get the pointer to the first value in the exposures
    ///  of the specified trade.
    /// </summary>
    /// <param name="tradeIndex">The index of the specified trade</param>
    /// <returns></returns>
    public IntPtr GetPointer(int tradeIndex)
    {
      var offset = 0;
      for (int j = 0; j < tradeIndex; j++)
      {
        offset += PathCount*_exposureDts[j].Length;
      }

      var elemSize = IsSinglePrecision ? sizeof(float) : sizeof(double);
      return IntPtr.Add(_pointer, offset*elemSize);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IntPtr GetPointerToDicountFactors()
    {
      throw new NotImplementedException();
    }

    /// <summary>
    ///  Get the total number of exposures of the specified trade
    /// </summary>
    /// <param name="tradeIndex">The index of the specified trade</param>
    /// <returns></returns>
    public int GetLength(int tradeIndex)
    {
      return _exposureDts[tradeIndex].Length*PathCount;
    }

    /// <summary>
    /// the simulated discount factors corresponding to exposures
    /// </summary>
    public double[,] DiscountFactors { get; set; }

    IReadOnlyList<Dt> IExposureSet.GetExposureDates(int tradeIndex)
      => _exposureDts[tradeIndex];

    object IExposureSet.GetExposures(int i) => GetExposures(i);

    private readonly Dt[][] _exposureDts;
    private readonly IntPtr _pointer;
  }

}