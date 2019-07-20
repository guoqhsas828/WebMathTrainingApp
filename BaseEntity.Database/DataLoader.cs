// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  ///   Utility class with methods to bulk load objects from the database.
  /// </summary>
  public static class DataLoader
  {
    /// <summary>
    ///   Given a Persistent Oject Type, Property name and a list of values to match 
    ///   for the property, this method bulk loads the objects from the database.
    /// </summary>
    /// <param name="objectType">Type of a Persistent Object</param>
    /// <param name="propertyName">Name of the Property on the Persistent Object</param>
    /// <param name="propertyValues">Values to match for the given Property</param>
    /// <returns>A list of matching Persistent objects</returns>
    public static IList GetObjects(Type objectType, string propertyName, IList propertyValues)
    {
      return GetObjectsByCriteria(Session.CreateCriteria(objectType), propertyName, propertyValues);
    }

    /// <summary>
    ///   Given an ICriteria for an IPersistent object, the Property name and a list of values 
    ///   to match for the property, this method bulk loads the objects from the database.
    /// </summary>
    /// <param name="criteriaQuery">Criteria for Persistent Object</param>
    /// <param name="propertyName">Name of the Property on the Persistent Object</param>
    /// <param name="propertyValues">Values to match for the given Property</param>
    /// <returns>A list of matching Persistent objects</returns>
    public static IList GetObjectsByCriteria(ICriteria criteriaQuery, string propertyName, IList propertyValues)
    {
      const int batchSize = 2000;

      IList results = new ArrayList();

      // Split property values collection into chunks 
      // of 2K because this is the limit for the numer 
      // of parameters for an ICriteria In query.
      for (int i = 0; i < propertyValues.Count; i++)
      {
        var clonedCriteria = (ICriteria)criteriaQuery.Clone();
        IList values = new ArrayList();
        for (int j = 0; j < batchSize && i < propertyValues.Count; j++, i++)
        {
          values.Add(propertyValues[i]);
        }
        clonedCriteria.Add(Restrictions.In(propertyName, values));
        clonedCriteria.List(results);
        i--;
      }

      return results;
    }

    /// <summary>
    ///   Given a Persistent Object Type and lits of ObjectId's,
    ///   this method bulk loads objects from the database
    /// </summary>
    /// <param name="objectType">Type of a Persistent Object</param>
    /// <param name="objectIds">List of ObjectIds</param>
    /// <returns>A list of matching Persistent objects</returns>
    public static IList GetObjectsById(Type objectType, IList objectIds)
    {
      return GetObjects(objectType, "ObjectId", objectIds);
    }
    
    /// <summary>
    ///   Given a Persistent Object Type and lits of ObjectId's,
    ///   this method bulk loads objects from the database
    /// </summary>
    /// <param name="objectIds">List of ObjectIds</param>
    /// <returns>A list of matching Persistent objects</returns>
    public static IList<T> GetObjectsById<T>(IList<long> objectIds) where T : PersistentObject
    {
      return GetObjects(typeof(T), "ObjectId", (IList)objectIds).OfType<T>().ToList();
    }
  }
}