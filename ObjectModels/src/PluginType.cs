
/* Copyright (c) WebMathTraining 2012. All rights reserved. */

using System;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Indicates the types of plugins (corresponding to interfaces, base classes, or custom attributes)/>
  /// </summary>
  [Flags]
  public enum PluginType
  {
    /// <summary>
    /// None
    /// </summary>
    None = 0,

    /// <summary>
    /// Plugin contains entity or component definitions
    /// </summary>
    EntityModel = 1,

    /// <summary>
    /// Contains TradeBlotter line entry panels
    /// </summary>
    TradeBlotter = 2,

    /// <summary>
    /// Contains classes that implement IEntityFilter
    /// </summary>
    EntityFilter = 4,

    /// <summary>
    /// Custom Windows Forms views for entities/components
    /// </summary>
    DisplayFactory = 8,

    /// <summary>
    /// Contains custom reporting columns
    /// </summary>
    ReportEngine = 16,

    /// <summary>
    /// Contains custom saved cashflow setters
    /// </summary>
    SavedCashflow = 32
  }
}