using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Numerics;
using OptStatus = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.OptimizerStatus; 

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Utility class for optimizing 
  /// </summary>  
  internal static class OptimizeUtil
  {
    internal static OptStatus RunOptimizer(Optimizer opt, 
      DelegateOptimizerFn optFn)
    {
      try
      {
        opt.Minimize(optFn);
      }
      catch (Exception)
      {
        if (opt.getNumEvaluations() >= opt.getMaxEvaluations())
          return OptStatus.MaximumEvaluationsReached;
        if (opt.getNumIterations() > opt.getMaxIterations())
          return OptStatus.MaximumIterationsReached;
        return OptStatus.FailedForUnknownException;
      }
      return OptStatus.Converged;
    }

  }
}
