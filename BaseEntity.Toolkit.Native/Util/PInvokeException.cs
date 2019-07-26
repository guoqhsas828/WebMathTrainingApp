//
// PInvokeException.cs
// Copyright (c)   2012-2013. All rights reserved.
//
using System;
using System.Text;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// PInvoke Exception class
  /// </summary>
  public static class PInvokeException
  {
    public static Exception Exception
    {
      get
      {
        return receiver_ == null || receiver_.Length == 0
          ? null : new ToolkitException(receiver_.ToString());
      }
    }

    public static bool Pending
    {
      get { return receiver_ != null && receiver_.Length != 0; }
    }

    public static StringBuilder Receiver
    {
      get
      {
        if (receiver_ == null)
          receiver_ = new StringBuilder(256);
        return receiver_;
      }
    }

    [ThreadStatic]
    private static StringBuilder receiver_ = new StringBuilder(256);
  }
}
