using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Evaluator interface
  /// </summary>
  /// <typeparam name="TInput">The type of input data</typeparam>
  /// <typeparam name="TResult">The type of output result</typeparam>
  public interface IEvaluator<in TInput, out TResult>
  {
    /// <summary>
    ///  Evaluates the specified input and returns the result
    /// </summary>
    /// <param name="input">The input value</param>
    /// <returns>The result value</returns>
    TResult Evaluate(TInput input);
  }
}
