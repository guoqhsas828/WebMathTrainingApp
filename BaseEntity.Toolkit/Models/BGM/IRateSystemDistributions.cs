using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///   Class representing the distributions of rate systems
  ///   over time and states.
  /// </summary>
  public interface IRateSystemDistributions
  {
    /// <summary>
    /// Gets the as-of date.
    /// </summary>
    /// <value>The as-of date.</value>
    Dt AsOf { get; }

    /// <summary>
    /// Gets the tenor dates of the forward rates.
    /// </summary>
    /// <value>The tenor dates.</value>
    Dt[] TenorDates { get; }

    /// <summary>
    /// Gets the time grid node dates.
    /// </summary>
    /// <value>The time grid node dates.</value>
    Dt[] NodeDates { get; }

    /// <summary>
    /// Gets the number of rates at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The number of rates.</returns>
    int GetRateCount(int dateIndex);

    /// <summary>
    /// Gets the number of state nodes at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The number of nodes.</returns>
    int GetStateCount(int dateIndex);

    /// <summary>
    /// Gets the probability of a given state at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The probability.</returns>
    double GetProbability(int dateIndex, int stateIndex);

    /// <summary>
    /// Gets the conditional probability of a date/state pair
    /// given that the system is in the <c>baseState</c>
    /// at the <c>baseDate</c>.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <param name="baseDateIndex">Index of the base date.</param>
    /// <param name="baseStateIndex">Index of the base state.</param>
    /// <returns>The conditional probability.</returns>
    double GetConditionalProbability(
      int dateIndex, int stateIndex,
      int baseDateIndex, int baseStateIndex);

    /// <summary>
    /// Gets the annuity, which normally should be the zero bond
    /// price corresponding to a rate.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The annuity.</returns>
    double GetAnnuity(int rateIndex, int dateIndex, int stateIndex);

    /// <summary>
    /// Gets the rate.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The rate.</returns>
    double GetRate(int rateIndex, int dateIndex, int stateIndex);

    /// <summary>
    /// Gets the index of the last reset rate.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The rate index.</returns>
    int GetLastResetIndex(int dateIndex);

    /// <summary>
    /// Gets the fraction.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <returns>The fraction</returns>
    double GetFraction(int rateIndex);
  }
}
