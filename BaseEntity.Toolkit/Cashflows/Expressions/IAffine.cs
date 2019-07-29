using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  Representing an affine expression: a*x + b
  /// </summary>
  public interface IAffine
  {
    double A { get; }
    double B { get; }
    Evaluable X { get; }
  }
}
