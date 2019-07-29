/*
 * MissingFixingException.cs
 *
 * 
 */

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// exception to be thrown from the toolkit when there is a missing fixing.
  /// </summary>
  [Serializable]
  public class MissingFixingException : ToolkitException
  {
    #region Constructors
    /// <summary>
    /// Default constructor
    /// </summary>
    public MissingFixingException() : base()
    {
    }

    /// <summary>
    /// Exception with a new message caused by another exception
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MissingFixingException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Exception with a message
    /// </summary>
    /// <param name="message">The message.</param>
    public MissingFixingException(string message): base(message)
    {
    }

    /// <summary>
    /// Exception that builds a message from a formatted string and parameters
    /// </summary>
    /// <param name="formattedMessage">The formatted message.</param>
    /// <param name="parameters">The parameters.</param>
    public MissingFixingException(string formattedMessage, params object[] parameters) : base(formattedMessage, parameters)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingFixingException"/> class.
    /// </summary>
    /// <param name="info">The information.</param>
    /// <param name="context">The context.</param>
    protected MissingFixingException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
    #endregion
  }
}
