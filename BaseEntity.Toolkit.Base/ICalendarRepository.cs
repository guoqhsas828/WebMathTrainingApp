using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Base
{
  /// <summary/>
  public interface ICalendarRepository
  {
    /// <summary/>
    void InitNativeCalendarCalc(string dir, Action<LoadCalendarCallback> calendarCalcInit);

    /// <summary/>
    void InitManagedCalendarCalc(string dir);
  }
}