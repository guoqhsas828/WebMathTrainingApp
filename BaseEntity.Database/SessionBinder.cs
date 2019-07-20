
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using NHibernate;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  ///
	/// </summary>
	public class SessionBinder : EntityContextBinder
	{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionBinder"/> class.
    /// Opens and binds a new session.
    /// </summary>
    public SessionBinder()
      : this(DateTime.MaxValue, ReadWriteMode.ReadWrite)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionBinder"/> class.
    /// All business events associated with an object and effective after 
    /// the asOf date will be rolled back when an object is loaded in this session.
    /// </summary>
    /// <param name="asOf"></param>
    public SessionBinder(DateTime asOf)
      : this(asOf, ReadWriteMode.ReadOnly)
    {
    }

	  /// <summary>
    /// Initializes a new instance of the <see cref="SessionBinder"/> class.
    /// </summary>
    /// <param name="readWriteMode">The read write mode.</param>
    public SessionBinder(ReadWriteMode readWriteMode)
      : this(DateTime.MaxValue, readWriteMode)
	  {
	  }

	  /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="readWriteMode"></param>
    public SessionBinder(DateTime asOf, ReadWriteMode readWriteMode)
      : base(new NHibernateEntityContext(asOf, readWriteMode), true)
    {
    }

	  /// <summary>
	  /// 
	  /// </summary>
	  /// <param name="asOf"></param>
	  /// <param name="readWriteMode"></param>
	  /// <param name="setValidFrom"></param>
	  public SessionBinder(DateTime asOf, ReadWriteMode readWriteMode, bool setValidFrom)
      : base(new NHibernateEntityContext(asOf, readWriteMode, setValidFrom), true)
    {
    }

	  /// <summary>
	  /// 
	  /// </summary>
	  /// <param name="asOf"></param>
	  /// <param name="readWriteMode"></param>
	  /// <param name="historizationPolicy"></param>
	  public SessionBinder(DateTime asOf, ReadWriteMode readWriteMode, HistorizationPolicy historizationPolicy)
	    : base(new NHibernateEntityContext(asOf, readWriteMode, historizationPolicy), true)
	  {
	  }

    /// <summary>
    /// Bind specified session
    /// </summary>
    /// <remarks>
    /// The provided context is bound to the current thread but is not owned by the SessionBinder
    /// and therefore will not be disposed of when the SessionBinder instance is disposed.
    /// </remarks>
    /// <param name="context"></param>
    public SessionBinder(IEntityContext context)
      : base(context, false)
    {
      var nhContext = context as NHibernateEntityContext;
      if (nhContext == null)
      {
        throw new DatabaseException("Invalid context [" + context + "]");
      }
      // Do a sanity check to make sure we have an active transaction
      var session = (ISession)nhContext;
      if (!session.Transaction.IsActive)
      {
        throw new DatabaseException(String.Format(
          "Session [{0}] has no active transaction!", context));
      }
    }

	  /// <summary>
    ///
    /// </summary>
    /// <returns>ISession</returns>
    /// <exclude />
    public static ISession GetCurrentSession()
	  {
	    var context = EntityContext.Current;
	    if (context == null)
	    {
	      throw new DatabaseException("No current EntityContext");
	    }
      var nhContext = EntityContext.Current as NHibernateEntityContext;
	    if (nhContext == null)
	    {
	      throw new DatabaseException("Invalid EntityContext [" + context.GetType() + "]");
	    }
      return nhContext;
    }
	}
}
