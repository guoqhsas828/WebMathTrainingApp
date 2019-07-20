
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  ///   Utility methods for <see cref="BusinessEvent">BusinessEvent class</see>.
  /// </summary>
  public static class BusinessEventUtil
  {
    /// <summary>
    ///   Returns the latest Business Event Effective Date applied to the TargetId
    /// </summary>
    /// <param name="targetId"></param>
    /// <returns></returns>
    public static DateTime GetLastEventDate(long targetId)
    {
      return Session.Linq<BusinessEvent>()
        .Where(be => be.TargetId == targetId)
        .OrderByDescending(be => be.EffectiveDate)
        .Select(be => be.EffectiveDate)
        .FirstOrDefault();
    }


    /// <summary>
    ///   Returns all Business Events applied to the given TragetIds in Ascending Order of EffectiveDate and EventOrder
    /// </summary>
    /// <param name="targetIds"></param>
    /// <returns></returns>
    public static IList FindByTargetIds(IList targetIds)
    {
      IList businessEvents = DataLoader.GetObjects(typeof (BusinessEvent), "TargetId", targetIds);

      // Sort in Ascending order of Effective Date and Event Order
      List<BusinessEvent> list = businessEvents.Cast<BusinessEvent>().ToList();
      list.Sort();
      return list;
    }

    /// <summary>
    ///   Returns all Business Events against the given TragetId in Ascending Order of EffectiveDate and EventOrder
    /// </summary>
    /// <param name="targetId">Filter TargetId</param>
    /// <param name="fromDate">Filter start date. Can be null.</param>
    /// <param name="toDate">Filter end date. Can be null</param>
    /// <returns>List of all Business Events applied to TargetId basked on start and end effective date filters.</returns>
    public static IList FindByTargetId(long targetId, DateTime fromDate, DateTime toDate)
    {
      ICriteria criteria = Session.CreateCriteria(typeof (BusinessEvent))
        .Add(Restrictions.Eq("TargetId", targetId))
        .AddOrder(Order.Asc("EffectiveDate"))
        .AddOrder(Order.Asc("EventOrder"));

      if (fromDate != DateTime.MinValue)
        criteria.Add(Restrictions.Ge("EffectiveDate", fromDate));
      if (toDate != DateTime.MinValue)
        criteria.Add(Restrictions.Le("EffectiveDate", toDate));
      
      return criteria.List();
    }

    /// <summary>
    ///   Apply the given business events
    /// </summary>
    /// <param name="businessEvents">List of Business Events to rollback</param>
    public static void ReApplyBusinessEvents(List<BusinessEvent> businessEvents)
    {
      businessEvents.Sort();

      foreach (var be in businessEvents)
      {
        try
        {
          be.ReApply();
        }
        catch (Exception ex)
        {
          string msg = String.Format("Cannot rollback {0} [{1}] effective [{2:d}].\n{3}", be.GetType().Name,
                                     be.Description, be.EffectiveDate, ex.Message);
          throw new BusinessEventRollbackException(msg, ex);
        }
      }
    }

    /// <summary>
    ///   Rollback the given business events
    /// </summary>
    /// <param name="businessEvents">List of Business Events to rollback</param>
    public static IList<BusinessEvent> RollbackBusinessEvents(List<BusinessEvent> businessEvents)
    {
      var list = new List<BusinessEvent>();

      businessEvents.Sort();

      // Rollback BusinessEvents in the reverse 
      // order of Effective Date and Event Order
      for (int i = businessEvents.Count - 1; i >= 0; i--)
      {
        BusinessEvent be = businessEvents[i];
        try
        {
          be.Rollback();
          list.Add(be);
        }
        catch (Exception ex)
        {
          string msg = String.Format("Cannot rollback {0} [{1}] effective [{2:d}].\n{3}", be.GetType().Name,
                                     be.Description, be.EffectiveDate, ex.Message);
          throw new BusinessEventRollbackException(msg, ex);
        }
      }

      return list;
    }

    /// <summary>
    ///   Checks for BusinessEventRollbackException in the entire stack trace.
    /// </summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    public static BusinessEventRollbackException CheckForBusinessEventException(Exception ex)
    {
      Exception currentException = ex;
      while (currentException != null)
      {
        if (currentException is BusinessEventRollbackException)
          return currentException as BusinessEventRollbackException;

        currentException = currentException.InnerException;
      }
      return null;
    }
  }
}