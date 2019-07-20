// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NHibernate.Transform;
using BaseEntity.Database.Engine;

namespace BaseEntity.Database
{
  /// <summary>
  ///   Utility class to rollback Business Events Effective on or after the given AsOf date
  /// </summary>
  [Serializable]
  public class BusinessEventManager
  {
    #region Data

    private readonly DateTime _asOf;
    private readonly Lazy<Dictionary<long, List<BusinessEventInfo>>> _lazyBusinessEventsCache;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessEventManager"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    public BusinessEventManager(DateTime asOf)
    {
      _asOf = asOf;
      _lazyBusinessEventsCache = new Lazy<Dictionary<long, List<BusinessEventInfo>>>(InitBusinessEventsCache);
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets AsOf date.
    /// </summary>
    /// <value>As of.</value>
    public DateTime AsOf
    {
      get { return _asOf; }
    }

    internal Dictionary<long, List<BusinessEventInfo>> BusinessEventsCache
    {
      get { return _lazyBusinessEventsCache.Value; }
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Loads all Buiness Events from the database Effective on or after the given    
    ///   asOf date and builds a cache without resolving the Target Objects.
    /// </summary>
    private Dictionary<long, List<BusinessEventInfo>> InitBusinessEventsCache()
    {
      var businessEventsCache = new Dictionary<long, List<BusinessEventInfo>>();

      IList list = Session.CreateCriteria(typeof(BusinessEvent))
        .Add(Restrictions.Gt("EffectiveDate", AsOf))
        .SetProjection(
          Projections.ProjectionList()
            .Add(Projections.Property("ObjectId"), "ObjectId")
            .Add(Projections.Property("TargetId"), "TargetId"))
        .SetResultTransformer(Transformers.AliasToBean(typeof(BusinessEventInfo)))
        .List();

      foreach (BusinessEventInfo eventInfo in list)
      {
        long targetObjectId = eventInfo.TargetId;

        List<BusinessEventInfo> events;
        if (!businessEventsCache.TryGetValue(targetObjectId, out events))
        {
          events = new List<BusinessEventInfo>();
          businessEventsCache[targetObjectId] = events;
        }

        events.Add(eventInfo);
      }

      return businessEventsCache;
    }

    /// <summary>
    ///   Given the TargetObjectId, rollbacks the events applied on or after the AsOf date.
    /// </summary>
    /// <param name="objectId">The Target ObjectId.</param>
    /// <returns>
    /// <c>true</c> if one or more Business Events were rolled back; otherwise, <c>false</c>.
    /// </returns>
    public bool ReApplyEvents(long objectId)
    {
      // Rollback any "future" events for the given persistent object id
      List<BusinessEventInfo> events;
      if (!BusinessEventsCache.TryGetValue(objectId, out events))
        return false;

      IList objIds = new ArrayList();
      foreach (BusinessEventInfo eventInfo in events)
        objIds.Add(eventInfo.ObjectId);

      // Bulk load the related business events
      IList businessEventsToRollback = DataLoader.GetObjectsById(typeof(BusinessEvent), objIds);

      // roll back the business events
      BusinessEventUtil.RollbackBusinessEvents(businessEventsToRollback.OfType<BusinessEvent>().ToList());

      return businessEventsToRollback.Count > 0;
    }

    /// <summary>
    ///   Given the TargetObjectId, rollbacks the events applied on or after the AsOf date.
    /// </summary>
    /// <param name="objectId">The Target ObjectId.</param>
    /// <returns>
    /// <c>true</c> if one or more Business Events were rolled back; otherwise, <c>false</c>.
    /// </returns>
    public IList<BusinessEvent> RollbackEvents(long objectId)
    {
      // Rollback any "future" events for the given persistent object id
      List<BusinessEventInfo> events;
      if (!BusinessEventsCache.TryGetValue(objectId, out events))
        return new BusinessEvent[0];

      IList objIds = new ArrayList();
      foreach (BusinessEventInfo eventInfo in events)
        objIds.Add(eventInfo.ObjectId);

      // Bulk load the related business events
      IList businessEventsToRollback = DataLoader.GetObjectsById(typeof(BusinessEvent), objIds);

      // roll back the business events
      return BusinessEventUtil.RollbackBusinessEvents(businessEventsToRollback.OfType<BusinessEvent>().ToList());
    }

    #endregion

    #region Nested Types

    internal struct BusinessEventInfo
    {
      public long ObjectId { get; set; }
      public long TargetId { get; set; }
    }

    #endregion
  }
}