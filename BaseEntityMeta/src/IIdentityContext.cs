// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Provides a common interface that applications can use to determine the name 
  /// of the current user.  This is required because the method used is different
  /// for desktop applications versus WAS hosted services.
  /// </summary>
  public interface IIdentityContext
  {
    /// <summary>
    /// Gets the name of the user.
    /// </summary>
    /// <returns></returns>
    string GetUserName();
  }
}
