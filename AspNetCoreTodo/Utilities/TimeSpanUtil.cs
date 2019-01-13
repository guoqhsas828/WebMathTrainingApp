using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Utilities
{
  public static class TimeSpanUtil
  {
    public static string Display(this TimeSpan ts)
    {
      return String.Format("{0:%d}day {0:%h}hr {0:%m}min", ts);
    }
  }
}
