// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Optionally defined within a plugin library to provide a hook for initialization of that plugin
  /// </summary>
  public interface IPlugin
  {
    /// <summary>
    /// Perform license check (if any)
    /// </summary>
    void CheckLicense();

    /// <summary>
    /// Called at the completion of Risk initialization
    /// </summary>
    void Init();
  }
}