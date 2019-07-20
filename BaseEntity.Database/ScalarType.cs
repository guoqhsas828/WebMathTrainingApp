/*
 * ScalarType.cs
 *
 * Copyright (c) WebMathTraining Inc 2008. All rights reserved.
 *
 */

using NHibernate;
using NHibernate.Type;
using BaseEntity.Database.Types;

namespace BaseEntity.Database
{

  /// <summary>
  /// Used to specify the type of a value in a parameterized query.
  /// The valid value types include all types exposed by NHibernateUtil
  /// as well as any custom WebMathTraining types.
  /// </summary>
  public static class ScalarType
  {
		/// <summary>Boolean</summary>
    public static readonly NullableType Boolean = NHibernateUtil.Boolean;

		/// <summary>DateTime</summary>
    public static readonly NullableType DateTime = NHibernateUtil.DateTime;

		/// <summary>Double</summary>
    public static readonly NullableType Double = NHibernateUtil.Double;

		/// <summary>Int32</summary>
    public static readonly NullableType Int32 = NHibernateUtil.Int32;

		/// <summary>Int64</summary>
    public static readonly NullableType Int64 = NHibernateUtil.Int64;

		/// <summary>String</summary>
    public static readonly NullableType String = NHibernateUtil.String;
  }

}


