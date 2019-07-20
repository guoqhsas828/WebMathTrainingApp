// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System.Text;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityData : PropertyMap
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityId"></param>
    public EntityData(int entityId)
    {
      EntityId = entityId;
    }

    /// <summary>
    /// 
    /// </summary>
    public int EntityId { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public override void Write(BinaryEntityWriter writer)
    {
      var cm = ClassCache.Find(EntityId);
      if (cm == null)
      {
        throw new MetadataException(string.Format("Invalid EntityId [{0}] : not found", EntityId));
      }
      writer.Write(EntityId);
      Write(writer, cm);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string PrettyPrint()
    {
      var sb = new StringBuilder();
      sb.AppendLine();
      sb.AppendLine(string.Format("EntityId = {0}", EntityId));
      foreach (var kvp in this)
      {
        sb.AppendLine(string.Format("{0}={1}", kvp.Key, kvp.Value));
      }
      return sb.ToString();
    }
  }
}