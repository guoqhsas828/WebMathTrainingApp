// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public class AuditException : Exception
  {
    /// <summary>
    /// 
    /// </summary>
    public AuditException()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public AuditException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public AuditException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected AuditException(SerializationInfo info, StreamingContext context)
      : base(info, context)
    {
    }
  }
}