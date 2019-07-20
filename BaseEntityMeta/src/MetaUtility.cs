// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Linq;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Utilities for accessing objects based on metadata
  /// </summary>
  public static class MetaUtility
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof (MetaUtility));

    /// <summary>
    /// Taking a datalink such as Product.Name and a root object such as an instance of Trade
    /// This method will return the object, classmeta, and propertymeta for that property
    /// So in the example of Product.Name the returned object will be the product within the Trade
    /// and the property meta for the Name property in the product
    /// </summary>
    /// <param name="datalink">string representation of the property searched for</param>
    /// <param name="oRoot">parent object this property is within</param>
    /// <param name="oRet">returned object for the datalink property</param>
    /// <param name="pmRet">returned property meta for the datalink property</param>
    /// <param name="cmRet">returned class meta for the datalink object</param>
    public static void ConvertDataLinkToObjectAndMeta(string datalink, object oRoot, ref object oRet, ref PropertyMeta pmRet, ref ClassMeta cmRet)
    {
      // SPLIT THE DATALINK INTO LEVELS OF PROPERTIES
      string[] sLevels = datalink.Split('.');

      var currentobject = oRoot;
      var currentmeta = ClassCache.Find(currentobject.GetType());
      PropertyMeta pm = null;

      if (currentmeta == null)
        return;

      for (int level = 0; level < sLevels.Length; level++)
      {
        if (level > 0)
        {
          currentobject = pm.GetValue(currentobject);

          if (currentobject != null)
            currentmeta = ClassCache.Find(currentobject.GetType());
        }

        if (currentobject == null)
        {
          if (Logger.IsDebugEnabled)
            Logger.Debug("Cannot find object for property " + pm.DisplayName + " parsing datalink " + datalink);
          return;
        }

        if (currentmeta == null)
        {
          if (Logger.IsDebugEnabled)
            Logger.Debug("Cannot find meta for object " + currentobject.GetType().ToString() + " parsing datalink " +
                         datalink);
          return;
        }

        pm = currentmeta.GetProperty(sLevels[level]);
        if (pm == null)
        {
          if (Logger.IsDebugEnabled)
            Logger.Debug("Cannot find property " + sLevels[level] + " in object " + currentobject.GetType().ToString() +
                         " parsing datalink " + datalink);
          return;
        }
      }

      // RETURN PARAMETERS
      oRet = currentobject;
      cmRet = currentmeta;
      pmRet = pm;
    }

    /// <summary>
    /// This method will allocate all properties of an object.
    /// GUI Screens need to know up front all the fields on an object
    /// This allows us to dynamically create controls based on the object
    /// and all its property types.
    /// </summary>
    /// <param name="obj"></param>
    public static int ResolveAll(object obj)
    {
      return ResolveAll(new Hashtable(), obj);
    }

    /// <summary>
    /// Resolves all.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj">The object.</param>
    /// <returns></returns>
    public static T ResolveAll<T>(this T obj)
    {
      ResolveAll((object) obj);
      return obj;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="resolvedCache"></param>
    /// <param name="obj"></param>
    private static int ResolveAll(Hashtable resolvedCache, object obj)
    {
      int count = 0;

      if (resolvedCache.ContainsKey(obj))
      {
        // If we have already called ResolveAll on this object before,
        // then we return immediately (otherwise we will infinitely
        // recurse in the case of circular references).
        return count;
      }

      // Add to list of resolved objects
      resolvedCache[obj] = obj;

      var cm = ClassCache.Find(obj);
      if (cm == null)
      {
        return count;
      }

      foreach (var pm in cm.PropertyList)
      {
        var propValue = pm.GetValue(obj);
        var collValue = propValue as ICollection;
        var listValue = collValue as IList;

        // TODO: Virtual method on PropertyMeta?
        if (pm is ManyToOnePropertyMeta)
        {
          if (propValue != null)
          {
            count += ResolveAll(resolvedCache, propValue);
          }
        }
        else if (pm is ComponentPropertyMeta)
        {
          if (propValue == null)
          {
            var cpm = pm as ComponentPropertyMeta;
            propValue = cpm.ChildEntity.CreateInstance();
            pm.SetValue(obj, propValue);
          }
          else
          {
            count += ResolveAll(resolvedCache, propValue);
          }
        }
        else if (pm is ComponentCollectionPropertyMeta)
        {
          var dict = collValue as IDictionary;

          if (listValue != null)
          {
            count += listValue.Cast<Object>().Sum(t => ResolveAll(resolvedCache, t));
          }
          else if (dict != null)
          {
            count += dict.Values.Cast<BaseEntityObject>().Sum(ro => ResolveAll(resolvedCache, ro));
          }
        }
        else if (pm is ElementCollectionPropertyMeta)
        {
          // For now, we assume that element collections do
          // not contain any many-to-one references, so all we
          // need to do is make sure the collection is loaded.
          if (collValue != null)
          {
            count += collValue.Count;
          }
        }
        else if (pm is OneToManyPropertyMeta)
        {
          if (listValue != null)
          {
            count += listValue.Cast<Object>().Sum(t => ResolveAll(resolvedCache, t));
          }
        }
        else if (pm is OneToOnePropertyMeta)
        {
          if (propValue != null)
          {
            count += ResolveAll(resolvedCache, propValue);
          }
        }
      }

      return count;
    }
  }
}