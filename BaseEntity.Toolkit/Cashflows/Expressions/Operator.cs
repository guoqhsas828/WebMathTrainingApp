using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///   Arithmetic operations
  /// </summary>
  public enum Operator
  {
    None,

    // Unary
    Drift, // c + x
    Scale, // c*x
    Inverse, // c/x
    Affine, // a*x + b
    Cap, // min(c, x)
    Floor, // max(c, x)

    // Binary
    Add, // x + y
    Subtract, // x - y
    Multiply, // x * y
    Divide, // x / y
  }

}
