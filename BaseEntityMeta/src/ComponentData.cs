// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class ComponentData : PropertyMap
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="typeName"></param>
    public ComponentData(string typeName)
    {
      if (string.IsNullOrWhiteSpace(typeName))
      {
        throw new ArgumentException("typeName cannot be empty");
      }
      TypeName = typeName;
    }

    /// <summary>
    /// 
    /// </summary>
    public string TypeName { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public override void Write(BinaryEntityWriter writer)
    {
      var cm = ClassCache.Find(TypeName);
      if (cm == null)
      {
        throw new MetadataException(string.Format("Invalid TypeName [{0}] : not found", TypeName));
      }
      if (!cm.IsComponent)
      {
        throw new MetadataException(string.Format("Invalid TypeName [{0}] : not a component", TypeName));
      }
      writer.Write(TypeName);
      Write(writer, cm);
    }
  }
}