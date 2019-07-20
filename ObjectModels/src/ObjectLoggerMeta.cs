// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using log4net;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Class which encapsulates an object loggers meta
  /// </summary>
  public class ObjectLoggerMeta
  {
    /// <summary>
    /// 
    /// </summary>
    public ObjectLoggerMeta(ObjectLoggerAttribute loggerInfo, ILog logger)
    {
      Attribute = loggerInfo;
      Logger = logger;
    }

    /// <summary>
    /// Object logger attribute instance
    /// </summary>
    public ObjectLoggerAttribute Attribute { get; private set; }

    /// <summary>
    /// Copy of the object logger instance
    /// </summary>
    public ILog Logger { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return string.Format("{0}.{1}", Attribute.Name,  Attribute.Category).GetHashCode();
    }

    /// <summary>
    /// Compares a pair of ObjectLoggerMeta classes based on the keys of the ObjectLoggerAttribute
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public override bool Equals(object other)
    {
      var otherObjectInfoInstance = other as ObjectLoggerMeta;
      if (otherObjectInfoInstance == null)
      {
        return false;
      }
      // check if there equal
      return (otherObjectInfoInstance.Attribute.Name != null && Attribute.Name.Equals(otherObjectInfoInstance.Attribute.Name) &&
              otherObjectInfoInstance.Attribute.Category != null && Attribute.Category.Equals(otherObjectInfoInstance.Attribute.Category));
    }     
  }
}