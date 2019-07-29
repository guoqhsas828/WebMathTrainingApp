/*
 * SwaptionTrade.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */
using System.Collections.Generic;


namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Interface for products with embeded options.
  /// </summary>
  public interface ICallable
  {
    /// <summary>
    /// Gets the exercise schedule.
    /// </summary>
    /// <value>The exersie schedule.</value>
    IList<IOptionPeriod> ExerciseSchedule { get; }
    /// <summary>
    /// Gets the option right.
    /// </summary>
    /// <value>The option right.</value>
    OptionRight OptionRight { get; }
    /// <summary>
    /// Gets the notification days.
    /// </summary>
    /// <value>The notification days.</value>
    int NotificationDays { get; }
    /// <summary>
    /// Gets the indicator whether the exercise strikes is in full price
    /// </summary>
    /// <value>True for the full price, false for the flat price</value>
    bool FullExercisePrice { get; }
  }
}
