/*
 * IOptionPeriod.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Represent a period during which an option is exercisable
  ///   with a specified strike or price.
  /// </summary>
  public interface IOptionPeriod : IPeriod, ICloneable
  {
    /// <summary>Exercise price</summary>
    double ExercisePrice { get; }

    /// <summary>Option style</summary>
    OptionStyle Style { get; }

    /// <summary>Option type</summary>
    OptionType Type { get; }

    /// <summary>
    /// Adjusted exercise date
    /// </summary>
    Dt NotificationDate { get; }
  }
}
