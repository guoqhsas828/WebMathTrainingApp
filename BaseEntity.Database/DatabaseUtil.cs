/*
 * DatabaseUtil.cs -
 *
 * Copyright (c) WebMathTraining Inc 2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using NHibernate;
using NHibernate.Criterion;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// General purpose database utility functions
  /// </summary>
  public static class DatabaseUtil
  {
    private static readonly Regex ToUtcRege = new Regex(@"(.+?)ToUtc\('(.+?)'\)");

    /// <summary>
    /// Load a named object and set the property to this object
    /// </summary>
    /// <param name="po"></param>
    /// <returns>PersistentObject</returns>
    public static PersistentObject FindByKey(PersistentObject po)
    {
      var cm = ClassCache.Find(po);

      var keyList = cm.KeyPropertyList.Select(keyProp => keyProp.GetValue(po)).ToList();

      string keyStr = PersistentObjectUtil.FormKey(cm, keyList);

      // Lookup in database
      var keyPropList = cm.KeyPropertyList;
      ICriteria criteria = Session.CreateCriteria(cm.Type);
      for (int i = 0; i < keyPropList.Count; i++)
      {
        PropertyMeta keyProp = keyPropList[i];
        if (keyList[i] == null)
        {
          // Can happen when AllowNullableKey = true
          if (!keyProp.IsNullable)
            throw new DatabaseException($"{keyProp.Name} cannot be null");
          criteria.Add(Restrictions.IsNull(keyProp.Name));
        }
        else
        {
          var key = keyList[i] as PersistentObject;
          // If it is a persistent object compare on the ObjectId
          // otherwise compare on the object
          criteria.Add(key == null
            ? Restrictions.Eq(keyProp.Name, keyList[i])
            : Restrictions.Eq(keyProp.Name + ".id", key.ObjectId));
        }
      }

      try
      {
        IList list = criteria.List();
        if (list.Count == 0)
          return null;
        if (list.Count == 1)
          return (PersistentObject)list[0];
        throw new DatabaseException("Unique key violation");
      }
      catch (Exception ex)
      {
        throw new DatabaseException($"Error querying {cm.Name} with criteria [{keyStr}] : {ex}");
      }
    }

    /// <summary>
    /// Look up an item with the specified (unique) criteria. Throws a <see cref="DatabaseException"/> if no item, 
    /// or more than one item, matches the criteria.
    /// </summary>
    /// <typeparam name="T">The type of the item to lookup</typeparam>
    /// <param name="criteria">The criteria.</param>
    /// <returns>The single item matching the criteria</returns>
    public static T Lookup<T>(Expression<Func<T, bool>> criteria)
    {
      var items = Session.Linq<T>().Where(criteria);
      try
      {
        return items.Single();
      }
      catch (InvalidOperationException ioe)
      {
        // TODO: partially reduce the criteria for a more user-friendly exception message
        throw new DatabaseException(
          String.Format(
            "No unique {1} found matching {0}. The specified criteria should probably include predicates on the key properties of {1}, and must ultimately identify exactly one item.",
            criteria,
            typeof(T).Name),
          ioe);
      }
    }

    /// <summary>
    /// Look up an item with the specified (unique) criteria, or return null if none found.
    /// Throws a <see cref="DatabaseException"/> if more than one item matches the criteria.
    /// </summary>
    /// <typeparam name="T">The type of the item to lookup</typeparam>
    /// <param name="criteria">The criteria.</param>
    /// <returns>The single item matching the criteria, or the default value for the type if no items match the criteria.</returns>
    public static T LookupOrDefault<T>(Expression<Func<T, bool>> criteria)
    {
      try
      {
        return Session.Linq<T>().Where(criteria).SingleOrDefault();
      }
      catch (InvalidOperationException ioe)
      {
        // TODO: partially reduce the criteria for a more user-friendly exception message
        throw new DatabaseException(
          String.Format(
            "Multiple items found matching {0}. The specified criteria should probably include predicates on the key properties of {1}, and must ultimately identify at most one item.",
            criteria,
            typeof(T)),
          ioe);
      }
    }

    /// <summary>
    /// Replace any localtime values in the provided HQL query to UTC time.
    /// </summary>
    /// <param name="hql">An HQL query with zero or more datetime criteria encapsulated in a ToUtc() function.</param>
    /// <remarks>
    /// A localtime value is recognized by being encapsulated inside a ToUtc() function.
    /// </remarks>
    public static string ConvertToUtc(string hql)
    {
      var sb = new StringBuilder();

      int startIdx = 0;
      Match match = ToUtcRege.Match(hql);
      while (match.Success)
      {
        var groups = match.Groups;

        // Parse and (if necessary) convert to UTC
        DateTime result;
        sb.Append(groups[1]);
        if (!DateTime.TryParse(groups[2].Value, out result))
        {
          throw new DatabaseException("Invalid datetime [" + groups[2].Value + "]");
        }
        sb.Append(string.Format("'{0:s}'", result.ToUniversalTime()));

        // Keep track of where we are in the original text
        startIdx += match.Length;

        // See if any more matches
        match = match.NextMatch();
      }

      // Append any text after last match
      sb.Append(hql.Substring(startIdx));

      return sb.ToString();
    }

    public static int AddUser()
    {
      using (new SessionBinder())
      {
        var existingUser = UserFactory.FindByName(EntityContextFactory.UserName);
        if (existingUser != null) return 1;

        var user = (User)Activator.CreateInstance(typeof(User), true);

        user.Name = EntityContextFactory.UserName;


        try
        {
          EntityContextFactory.AddUser(user);

          Session.Save(user);

          // Create Administrator Role for this User
          var role = UserRoleFactory.CreateInstance();
          role.Name = "Administrator";
          role.Administrator = true;

          Session.Save(role);

          user.Role = role;

          Session.CommitTransaction();
        }
        catch (Exception ex)
        {
          Console.WriteLine("Could not create user [{0}]: {1}\n{2}", user.Name, ex.Message, ex);
          Session.RollbackTransaction();
          return 3;
        }
      }

      return 0;
    }

    public static int SaveObject(PersistentObject po)
    {
      using (new SessionBinder())
      {
        try
        {
          Session.Save(po);
          Session.CommitTransaction();
        }
        catch (Exception ex)
        {
          Session.RollbackTransaction();
          return -1;
        }
      }

      return 1;
    }
  }
}

