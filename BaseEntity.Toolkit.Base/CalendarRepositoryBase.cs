using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Base
{
  /// <summary/>
  public abstract class CalendarRepositoryBase : ICalendarRepository
  {
    /// <summary/>
    public static string CalendarDir = string.Empty;

    /// <summary/>
    public abstract void InitNativeCalendarCalc(string dir, Action<LoadCalendarCallback> calendarCalcInit);

    /// <summary/>
    public abstract void InitManagedCalendarCalc(string dir);

  }
}