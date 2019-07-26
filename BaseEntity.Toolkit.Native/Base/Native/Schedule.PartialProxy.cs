using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base.Native
{
  [Serializable]
  abstract partial class Schedule : INativeSerializable
  {
    /// <exclude />
    public abstract void GetObjectData(SerializationInfo info, StreamingContext context);
  }
}
