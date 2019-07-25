//
// Copyright (c)    2017. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Base.Serialization
{
  internal static class CustomSerializers
  {
    private static readonly IList<ISimpleXmlSerializer> Serializers = new List<ISimpleXmlSerializer>
    {
      NativeObjectSerializer.Instance
    };

    internal static void Register(ISimpleXmlSerializer serializer)
    {
      Debug.Assert(serializer != null);
      Serializers.Add(serializer);
    }

    internal static ISimpleXmlSerializer TryGet(Type type)
    {
      var list = Serializers;
      for (int i = list.Count; --i >= 0;)
      {
        var serializer = list[i];
        if (serializer.CanHandle(type))
          return serializer;
      }
      return null;
    }
  }
}
