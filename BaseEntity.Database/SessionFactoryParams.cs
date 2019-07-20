// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Database
{
  /// <summary>
  /// Used to specify all configurable Database settings
  /// </summary>
  [Serializable]
  internal class SessionFactoryParams
  {
    public SessionFactoryParams()
    {
      Dialect = "MsSql2008";
      DefaultSchema = "";
      ConnectString = "";
      Password = "";
      AppRoleName = "";
      AppRolePassword = "";
    }

    public string ConnectString { get; set; }
    public string Password { get; set; }
    public string Dialect { get; set; }
    public string DefaultSchema { get; set; }
    public int CommandTimeout { get; set; }
    public string AppRoleName { get; set; }
    public string AppRolePassword { get; set; }
  }
}