// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Data;
using NHibernate;
using NHibernate.Engine;
using NHibernate.Type;
using BaseEntity.Database.Engine;
using BaseEntity.Metadata;
#if NETSTANDARD2_0
using IDbCommand = System.Data.Common.DbCommand;
using IDataReader = System.Data.Common.DbDataReader;
#endif

namespace BaseEntity.Database.Types
{
  /// <summary>
  /// 
  /// </summary>
  internal class ObjectRefType : ManyToOneType
  {
    /// <summary>
    /// 
    /// </summary>
    public ObjectRefType(string name)
      : base(name, null, false, false, false, false)
    {}

    public override object ResolveIdentifier(object value, ISessionImplementor session, object owner)
    {
      if (value == null) return null;

      var id = (long)value;
      var interceptor = (AuditInterceptor)session.Interceptor;
      return new ObjectRef(id, interceptor.EntityContext);
    }

    public override object Assemble(object child, ISessionImplementor session, object owner)
    {
      var objectRef = child as ObjectRef;
      if (objectRef == null || objectRef.IsNull) return null;
      return objectRef.Id == 0 ? objectRef.Obj : ResolveIdentifier(objectRef.Id, session);
    }

    public object GetResolvedInstance(object value)
    {
      var objectRef = value as ObjectRef;
      return objectRef == null ? null : objectRef.Obj;
    }

    //protected override object GetIdentifier(object value, ISessionImplementor session)
    //{
    //  return ForeignKeys.GetEntityIdentifierIfNotUnsaved(GetAssociatedEntityName(), value, session); //tolerates nulls
    //}

    /// <summary> Two entities are considered the same when their instances are the same. </summary>
    /// <param name="x">One entity instance </param>
    /// <param name="y">Another entity instance </param>
    /// <param name="entityMode">The entity mode. </param>
    /// <returns> True if x == y; false otherwise. </returns>
    public override bool IsSame(object x, object y) //, EntityMode entityMode
    {
      if (ReferenceEquals(x, y))
      {
        return true;
      }

      long oldId = GetObjectId(x);
      long newId = GetObjectId(y);
      return oldId == newId;
    }

    public override bool IsDirty(object old, object current, ISessionImplementor session)
    {
      return !IsSame(old, current); //, session.EntityMode
    }

    public override bool IsDirty(object old, object current, bool[] checkable, ISessionImplementor session)
    {
      if (IsAlwaysDirtyChecked)
      {
        return IsDirty(old, current, session);
      }
      return !IsSame(old, current); //, session.EntityMode
    }

    private static long GetObjectId(object obj)
    {
      var objectRef = (ObjectRef)obj;
      if (objectRef == null || objectRef.IsNull)
      {
        return 0;
      }
      var po = (PersistentObject)objectRef.Obj;
      return po == null ? objectRef.Id : po.ObjectId;
    }

    public override object NullSafeGet(IDataReader rs, string name, ISessionImplementor session, object owner)
    {
      throw new NotImplementedException();
    }

    public override void NullSafeSet(IDbCommand cmd, object value, int index, ISessionImplementor session)
    {
      long? id;
      if (value == null)
      {
        id = null;
      }
      else
      {
        var po = value as PersistentObject;
        if (po != null)
        {
          id = po.ObjectId;
        }
        else
        {
          var objectRef = value as ObjectRef;
          if (objectRef != null)
          {
            id = objectRef.IsNull ? null : (objectRef.IsUninitialized ? (long?)objectRef.Id : ((PersistentObject)objectRef.Obj).ObjectId);
          }
          else
          {
            id = Convert.ToInt64(value);
          }
        }
      }

      _objectIdType.NullSafeSet(cmd, id, index, session);
    }

    public override void NullSafeSet(IDbCommand st, object value, int index, bool[] settable, ISessionImplementor session)
    {
      NullSafeSet(st, value, index, session);
    }

    public override object Replace(object original, object target, ISessionImplementor session, object owner, IDictionary copyCache)
    {
      return target;
    }

    private readonly IType _objectIdType = new ObjectIdType();
  }
}