// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

using System;
using BaseEntity.Configuration;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Returns the name of the Windows user this application or batch process is running as
  /// </summary>
  public class DesktopIdentityContext : IIdentityContext
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(DesktopIdentityContext));

    /// <summary>
    /// Returns the UserName determined by the <see cref="Environment"/>
    /// </summary>
    /// <returns></returns>
    public string GetUserName()
    {
      string userName = Environment.UserName;

      Logger.Verbose("GetUserName: " + userName);

      return userName;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String"/> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return GetUserName();
    }
  }
}
