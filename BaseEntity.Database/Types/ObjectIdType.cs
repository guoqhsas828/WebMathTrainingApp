// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Data;
using NHibernate.SqlTypes;
using NHibernate.Type;
using BaseEntity.Metadata;
using NHibernate.Engine;
#if NETSTANDARD2_0
using IDataReader = System.Data.Common.DbDataReader;
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database.Types
{
  /// <summary>
  /// Used to map ObjectId values
  /// </summary>
  /// <remarks>
  /// ObjectId values are treated differently than other Int64 values due to the embedded EntityId.
  /// </remarks>
  [Serializable]
  public class ObjectIdType : PrimitiveType, IIdentifierType
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectIdType"/> class.
    /// </summary>
    public ObjectIdType()
      : base(SqlTypeFactory.Int64)
    {}

    /// <summary>
    /// Gets the default value.
    /// </summary>
    /// <value>The default value.</value>
    public override object DefaultValue
    {
      get { throw new NotImplementedException("No default value for an ObjectIdType"); }
    }

    /// <summary>
    /// Gets the primitive class.
    /// </summary>
    /// <value>The primitive class.</value>
    public override Type PrimitiveClass
    {
      get { return typeof(Int64); }
    }

    /// <summary>
    /// When implemented by a class, gets the object in the
    /// <see cref="IDataReader"/> for the Property.
    /// </summary>
    /// <param name="rs">The <see cref="IDataReader"/> that contains the value.</param>
    /// <param name="index">The index of the field to get the value from.</param>
    /// <returns>
    /// An object with the value from the database.
    /// </returns>
    public override object Get(IDataReader rs, int index, ISessionImplementor session)
    {
      return Convert.ToInt64(rs[index]);
    }

    /// <summary>
    /// When implemented by a class, gets the object in the
    /// <see cref="IDataReader"/> for the Property.
    /// </summary>
    /// <param name="rs">The <see cref="IDataReader"/> that contains the value.</param>
    /// <param name="name">The name of the field to get the value from.</param>
    /// <returns>
    /// An object with the value from the database.
    /// </returns>
    /// <remarks>
    /// Most implementors just call the <see cref="Get(IDataReader, int)"/>
    /// overload of this method.
    /// </remarks>
    public override object Get(IDataReader rs, string name, ISessionImplementor session)
    {
      return Convert.ToInt64(rs[name]);
    }

    /// <summary>
    /// See <see cref="NHibernate.Type.AbstractType.ReturnedClass"/>
    /// </summary>
    public override Type ReturnedClass
    {
      get { return typeof(Int64); }
    }

    /// <summary>
    /// Sets the specified st.
    /// </summary>
    /// <param name="st">The st.</param>
    /// <param name="value">The value.</param>
    /// <param name="index">The index.</param>
    public override void Set(IDbCommand st, object value, int index, ISessionImplementor session)
    {
      var id = (long)value;
      if (EntityHelper.IsTransient(id))
      {
        throw new DatabaseException("Attempt to save transient ObjectId [" + value + "] to the database");
      }
      var parm = (IDataParameter)st.Parameters[index];
      parm.Value = id;
    }

    /// <summary>
    /// See <see cref="NHibernate.Type.AbstractType.Name"/>
    /// </summary>
    public override string Name
    {
      get { return "ObjectId"; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance has nice equals.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance has nice equals; otherwise, <c>false</c>.
    /// </value>
    public bool HasNiceEquals
    {
      get { return true; }
    }

    /// <summary>
    /// When implemented by a class, converts the xml string from the
    /// mapping file to the .NET object.
    /// </summary>
    /// <param name="xml">The value of <c>discriminator-value</c> or <c>unsaved-value</c> attribute.</param>
    /// <returns>The string converted to the object.</returns>
    /// <remarks>
    /// This method needs to be able to handle any string.  It should not just
    /// call System.Type.Parse without verifying that it is a parsable value
    /// for the System.Type.
    /// </remarks>
    public object StringToObject(string xml)
    {
      return long.Parse(xml);
    }

    /// <summary>
    /// When implemented by a class, return a <see cref="String"/> representation
    /// of the value, suitable for embedding in an SQL statement
    /// </summary>
    /// <param name="value">The object to convert to a string for the SQL statement.</param>
    /// <param name="dialect"></param>
    /// <returns>
    /// A string that containts a well formed SQL Statement.
    /// </returns>
    public override string ObjectToSQLString(object value, NHibernate.Dialect.Dialect dialect)
    {
      return value.ToString();
    }

    /// <summary>
    /// Parse the XML representation of an instance
    /// </summary>
    /// <param name="xml">XML string to parse, guaranteed to be non-empty</param>
    /// <returns></returns>
    public override object FromStringValue(string xml)
    {
      throw new NotImplementedException();
    }

    #region IIdentifierWithDiscriminatorType Methods

    /// <summary>
    /// Get the .NET Type for the specified ObjectId
    /// </summary>
    public Type GetClass(object id)
    {
      return EntityHelper.GetClassFromObjectId((long)id);
    }

    #endregion

    /// <summary>
    /// Get the <see cref="Type"/> for the specified id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [Obsolete("Use EntityHelper.GetClassFromObjectId(id) instead")]
    public static Type GetClassFromObjectId(long id)
    {
      return EntityHelper.GetClassFromObjectId(id);
    }
  }
}