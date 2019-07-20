using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace BaseEntity.Core.Logging
{

  public static class QLogManager
  {
    public static ILog GetLogger(Type type)
    {
      return LogManager.GetLogger(type);
    }

  }
}
