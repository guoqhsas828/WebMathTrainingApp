// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public static class CascadeUtil
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="lockType"></param>
    /// <param name="cascade"></param>
    /// <returns></returns>
    public static bool ShouldCascade(LockType lockType, string cascade)
    {
      switch (cascade)
      {
        case "none":
          return false;

        case "save-update":
          return (lockType == LockType.Insert || lockType == LockType.Update);

        case "all":
        case "all-delete-orphan":
          return true;

        default:
          throw new ArgumentException(String.Format("Invalid cascade [{0}]", cascade));
      }
    }
  }
}