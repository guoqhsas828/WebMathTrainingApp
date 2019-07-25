/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Style of option (american, European, etc.).
  /// </summary>
  public enum OptionStyle
  {
    /// <summary>None</summary>
    None = 0,

    /// <summary>American - Exerciseable over life of option</summary>
    American,

    /// <summary>European - Exerciseable at maturity only</summary>
    European,

    /// <summary>Bermudan - Exerciseable on a series of dates</summary>
    Bermudan
  }
}
