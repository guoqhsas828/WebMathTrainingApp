using System;
using System.Collections;
using System.Linq;
using log4net.Core;
using NHibernate;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  ///
  /// </summary>
  internal class DefaultPurgeHandler : IPurgeHandler
  {
    /// <summary>
    /// Find all references to @object from the specified entity
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="po"></param>
    /// <param name="reporter"></param>
    /// <returns>IList</returns>
    public virtual IList FindReferences(ClassMeta cm, PersistentObject po, Reporter reporter)
    {
      var allRefs = new ArrayList();

      foreach (PropertyMeta pm in cm.PropertyList.Where(cm.IsOwner))
      {
        // ToDo: Replace with Snapshot mechanism
        if (pm.ExtendedData)
        {
          IList objRefs = FindReferencesOnExtendedProperty(cm, pm, po, reporter);
          allRefs.AddRange(objRefs);
          continue;
        }

        if (pm is ManyToOnePropertyMeta)
        {
          // Check for references from this property
          var manyToOnePropMeta = (ManyToOnePropertyMeta) pm;
          if (!manyToOnePropMeta.Clazz.IsInstanceOfType(po))
            continue;

          IList objRefs = Session.CreateQuery($"from {cm.Name} o where o.{pm.Name}={po.ObjectId}").List();
          reporter(Level.Debug, "Found {0} references from {1}.{2}", objRefs.Count, cm.Name, pm.Name);
          allRefs.AddRange(objRefs);
        }
        else if (pm is ComponentPropertyMeta)
        {
          // Check for references from component properties
          var compPropMeta = (ComponentPropertyMeta) pm;
          IEnumerable propertiesToCheck =
            compPropMeta.ChildEntity.PropertyList.OfType<ManyToOnePropertyMeta>().Where(
              mtopm => mtopm.Clazz.IsInstanceOfType(po));

          foreach (ManyToOnePropertyMeta manyToOnePropMeta in propertiesToCheck)
          {
            IList objRefs = Session.Find($"from {cm.Name} a where a.{pm.Name}.{manyToOnePropMeta.Name} = {po.ObjectId}");
            reporter(Level.Debug, "Found {0} references from {1}.{2}.{3}", objRefs.Count, cm.Name, pm.Name, manyToOnePropMeta.Name);
            allRefs.AddRange(objRefs);
          }
        }
        else if (pm is ComponentCollectionPropertyMeta)
        {
          // Check for references from our component collection
          var compColPropMeta = pm as ComponentCollectionPropertyMeta;

          IEnumerable propertiesToCheck =
            compColPropMeta.ChildEntity.PropertyList.OfType<ManyToOnePropertyMeta>().Where(
              mtopm => mtopm.Clazz.IsInstanceOfType(po));

          foreach (ManyToOnePropertyMeta manyToOnePropMeta in propertiesToCheck)
          {
            IList objRefs = Session.Find($"from {cm.Name} a join a.{pm.Name} b where b.{manyToOnePropMeta.Name} = {po.ObjectId}");
            reporter(Level.Debug, "Found {0} references from {1}.{2}.{3}", objRefs.Count, cm.Name, pm.Name, manyToOnePropMeta.Name);
            allRefs.AddRange(objRefs);
          }
        }
        else if (pm is ManyToManyPropertyMeta)
        {
          var manyToManyPropMeta = pm as ManyToManyPropertyMeta;
          if (manyToManyPropMeta.Clazz.IsInstanceOfType(po))
          {
            IList objRefs = Session.Find($"Select a from {cm.Name} a join a.{pm.Name} b where b = {po.ObjectId}");
            reporter(Level.Debug, "Found {0} references from {1}.{2}.{3}", objRefs.Count, cm.Name, pm.Name, manyToManyPropMeta.Name);
            allRefs.AddRange(objRefs);
          }
        }
      }

      return allRefs;
    }

    /// <summary>
    ///   Finds references for a given PersistentObject on an Extended Property 
    /// </summary>
    /// <remarks>
    ///   This method does not work for nested components/component collections.
    /// </remarks>
    /// <param name="cm"></param>
    /// <param name="pm"></param>
    /// <param name="po"></param>
    /// <param name="reporter"></param>
    /// <returns></returns>
    private static IList FindReferencesOnExtendedProperty(ClassMeta cm, PropertyMeta pm, PersistentObject po, Reporter reporter)
    {
      var allRefs = new ArrayList();
      ICriteria rootObjCriteria = Session.CreateCriteria(cm.Type);

      if (pm is ManyToOnePropertyMeta)
      {
        // Check for references from this property
        var manyToOnePropMeta = (ManyToOnePropertyMeta) pm;
        if (manyToOnePropMeta.Clazz.IsInstanceOfType(po))
        {
          IList rootObjs = rootObjCriteria.List();
          IList objRefs = new ArrayList();
          foreach (object rootObj in rootObjs)
          {
            var refObj = (PersistentObject) manyToOnePropMeta.GetValue(rootObj);
            if (refObj != null && refObj.ObjectId == po.ObjectId)
              objRefs.Add(rootObj);
          }

          reporter(Level.Debug, "Found {0} references from {1}.{2}", objRefs.Count, cm.Name, manyToOnePropMeta.Name);
          allRefs.AddRange(objRefs);
        }
      }
      else if (pm is ComponentPropertyMeta)
      {
        // Check for references from component properties
        var compPropMeta = (ComponentPropertyMeta)pm;
        var propertiesToCheck =
          compPropMeta.ChildEntity.PropertyList.OfType<ManyToOnePropertyMeta>().Where(
            mtopm => mtopm.Clazz.IsInstanceOfType(po));

        if (propertiesToCheck.Count() > 0)
        {
          IList rootObjs = rootObjCriteria.List();

          foreach (ManyToOnePropertyMeta manyToOnePropMeta in propertiesToCheck)
          {
            IList objRefs = new ArrayList();
            foreach (object rootObj in rootObjs)
            {
              var childObj = compPropMeta.GetValue(rootObj);
              if (childObj == null)
                continue;

              var refObj = (PersistentObject) manyToOnePropMeta.GetValue(childObj);
              if (refObj != null && refObj.ObjectId == po.ObjectId)
                objRefs.Add(rootObj);
            }

            reporter(Level.Debug, "Found {0} references from {1}.{2}.{3}", objRefs.Count, cm.Name, pm.Name,
                     manyToOnePropMeta.Name);
            allRefs.AddRange(objRefs);
          }
        }
      }
      else if (pm is ComponentCollectionPropertyMeta)
      {
        var compColPropMeta = pm as ComponentCollectionPropertyMeta;

        // Select all ManyToOne properties on the component object which match the type of the Persistent Object to be deleted
        var propertiesToCheck = compColPropMeta.ChildEntity.PropertyList.OfType<ManyToOnePropertyMeta>().Where(
               mtopm => mtopm.Clazz.IsInstanceOfType(po));

        if (propertiesToCheck.Count() > 0)
        {
          IList rootObjs = rootObjCriteria.List();

          foreach (ManyToOnePropertyMeta manyToOnePropMeta in propertiesToCheck)
          {
            IList objRefs = new ArrayList();
            foreach (object rootObj in rootObjs)
            {
              var compCollection = compColPropMeta.GetValue(rootObj) as IEnumerable ?? new ArrayList();
              foreach (object compObj in compCollection)
              {
                var refObj = (PersistentObject)manyToOnePropMeta.GetValue(compObj);
                if (refObj != null && refObj.ObjectId == po.ObjectId)
                  objRefs.Add(rootObj);
              }
            }

            reporter(Level.Debug, "Found {0} references from {1}.{2}.{3}", objRefs.Count, cm.Name, pm.Name, manyToOnePropMeta.Name);
            allRefs.AddRange(objRefs);
          }
        }
      }

      return allRefs;
    }
  }
}