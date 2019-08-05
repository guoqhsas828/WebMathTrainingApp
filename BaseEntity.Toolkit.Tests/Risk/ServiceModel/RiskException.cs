using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Base class for all Risk exceptions
  /// </summary>
  [Serializable]
  public class RiskException : Exception
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the RiskException class with a
    /// specified error message.
    /// </summary>
    public RiskException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the RiskException class with a
    /// specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    public RiskException(string message, Exception inner)
      : base(message, inner)
    {
    }

    #endregion
  }
}
