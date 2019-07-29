using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Utils
{
  public interface ITimeGridBuilder
  {
    IReadOnlyList<Dt> GetTimeGrids(Dt begin, Dt end);
  }

  public class TimeGridBuilder : ITimeGridBuilder
  {
    internal TimeGridBuilder(int stepSize, TimeUnit stepUnit)
    {
      StepSize = stepSize;
      StepUnit = stepUnit;
    }

    public IReadOnlyList<Dt> GetTimeGrids(Dt begin, Dt end)
    {
      var step = StepSize;
      var stepUnit = StepUnit;
      if (step == 0 || stepUnit == TimeUnit.None)
        return EmptyArray<Dt>.Instance;


      var timeGrids = new List<Dt>();
      var date = begin;
      while (date < end)
      {
        date = Dt.Add(date, step, stepUnit);
        if (date >= end)
        {
          date = end;
        }
        timeGrids.Add(date);
      }
      return timeGrids;
    }

    public int StepSize { get; }
    public TimeUnit StepUnit { get; }
  }
}
