// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Exception thrown by <see cref="JSonException"/> class.
  /// It indicates the token, that caused it and place inside the input string.
  /// </summary>
  [Serializable]
  public sealed class JSonException : Exception
  {
    /// <summary>
    /// Init constructor.
    /// </summary>
    public JSonException(string message)
      : base(message)
    {}

    /// <summary>
    /// Init constructor.
    /// </summary>
    public JSonException(string message, Exception exception)
      : base(message, exception)
    {}

    /// <summary>
    /// Init constructor.
    /// </summary>
    public JSonException(string message, int line, int offset, Exception innerException)
      : base(PrepareMessage(message, line, offset), innerException)
    {
      Line = line;
      Offset = offset;
    }

    /// <summary>
    /// Init constructor.
    /// </summary>
    public JSonException(string message, int line, int offset)
      : base(PrepareMessage(message, line, offset))
    {}

    #region ISerializable Implementation

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public JSonException(SerializationInfo info, StreamingContext context)
    {
      if (info == null)
      {
        throw new ArgumentNullException("info");
      }

      Line = info.GetInt32("Line");
      Offset = info.GetInt32("Offset");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);

      info.AddValue("Line", Line);
      info.AddValue("Offset", Offset);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Line in input source, where invalid token was spot.
    /// </summary>
    public int Line { get; private set; }

    /// <summary>
    /// Char offset inside the line of source code, where invalid token was spot.
    /// </summary>
    public int Offset { get; private set; }

    #endregion

    #region Message Preparation

    private static string PrepareMessage(string message, int line, int offset)
    {
      return String.Format(CultureInfo.InvariantCulture, "{0} ({1}:{2})", message, line, offset);
    }

    #endregion
  }
}