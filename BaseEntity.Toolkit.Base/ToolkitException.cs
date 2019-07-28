/*
 * ToolkitException.cs
 *
 * 
 */

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Toolkit exception class
  /// </summary>
  [Serializable]
  public class ToolkitException : Exception
  {
    #region Constructors
    /// <summary>
    /// Default constructor
    /// </summary>
    public ToolkitException() : base()
    {
    }

    /// <summary>
    /// Exception with a new message caused by another exception
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ToolkitException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Exception with a message
    /// </summary>
    /// <param name="message">The message.</param>
    public ToolkitException(string message): base(message)
    {
    }

    /// <summary>
    /// Exception that builds a message from a formatted string and parameters
    /// </summary>
    /// <param name="formattedMessage">The formatted message.</param>
    /// <param name="parameters">The parameters.</param>
    public ToolkitException(string formattedMessage, params object[] parameters) : base(String.Format(formattedMessage, parameters))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolkitException"/> class.
    /// </summary>
    /// <param name="info">The information.</param>
    /// <param name="context">The context.</param>
    protected ToolkitException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
    #endregion
  }
}
