using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///   Abstract interface of the rate calculator
  /// </summary>
  public interface IRateCalculator
  {
    /// <summary>
    ///   Calculate the rate specified by the index at the given date.
    /// </summary>
    /// <param name="resetDate">The reset date</param>
    /// <param name="rateIndex">The rate index</param>
    /// <returns>The specified rate at the reset date</returns>
    double GetRateAt(Dt resetDate, ReferenceIndex rateIndex);
  }
}
