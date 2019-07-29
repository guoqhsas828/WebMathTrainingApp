/*
 * CurvePoint.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Toolkit.Base;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Curves
{
	// CurvePointArrayConverter

	/// <exclude />
  [Serializable]
 	[TypeConverter(typeof(CurvePointArrayConverter))]
	public class CurvePointArray : ICollection
	{
		/// <exclude />
		public CurvePointArray( Curve curve )
		{
			this.curve = curve;
		}

		/// <exclude />
		public int Length
		{
			get { return curve.Count; }
		}
		
		/// <exclude />
		public CurvePoint
		this[ int index ]
		{
			get
			{
				return new CurvePoint( curve.GetDt(index), curve.GetVal(index) );
			}

			set
			{
				curve.SetDt( index, value.Date );
				curve.SetVal( index, value.Value );
			}
		}
		
		private Curve curve;

		#region ICollection Members
		/// <exclude />
		public int Add(object value)
		{
			CurvePoint point= (CurvePoint)value;
			curve.Add(point.Date,point.Value);
			return curve.Count-1;
		}


		/// <exclude />
		public bool IsSynchronized
		{
			get
			{
				return false;
			}
		}

		/// <exclude />
		public int Count
		{
			get
			{
				return Length;
			}
		}

		/// <exclude />
		public void CopyTo(Array array, int index)
		{
			for (int i=0;i<Count;i++ ) 
			{
				array.SetValue(this[i],index+i);
			}
		}

		/// <exclude />
		public object SyncRoot
		{
			get
			{
				return null;
			}
		}

		#endregion

		#region IEnumerable Members
		/// <exclude />
		class CurvePointArrayEnumerator: IEnumerator
		{
			/// <exclude />
			public CurvePointArrayEnumerator(CurvePointArray pointArray)
			{
				array=pointArray;
			}
			#region IEnumerator Members
			private int index = -1 ;
			private CurvePointArray array;
			/// <exclude />
			public void Reset()
			{
				index = -1;
			}
			/// <exclude />
			public object Current
			{
				get
				{
					if( index <= -1 || index>array.Length ) 
					{
						throw new InvalidOperationException() ;
					}
					return array[index];
				}
			}
			/// <exclude />
			public bool MoveNext()
			{
				index++ ;
				if( index < array.Length )
				{
					return true ;
				}
				else
				{
					index = -1;
					return false;
				}
			}

			#endregion
		}

		/// <exclude />
		public IEnumerator GetEnumerator()
		{
			return new CurvePointArrayEnumerator(this);
		}

		#endregion
	}
	
	/// <exclude />
	public class CurvePointArrayConverter : TypeConverter
	{

		// CurvePointArrayPropertyDescriptor
		/// <exclude />
		protected class CurvePointArrayPropertyDescriptor : SimplePropertyDescriptor
		{
			/// <exclude />
			public CurvePointArrayPropertyDescriptor( CurvePointArray points, int index )
				: base( typeof(CurvePointArray),
								String.Format("[ {0}]   {1:d}", FormatArrayIndex(index, points.Count) , new DateTime(points[index].Date.Year, points[index].Date.Month, points[index].Date.Day)),
								typeof(CurvePoint),
								null )
			{
				this.index = index;
			}

			private static string FormatArrayIndex(int index, int arraysize)
			{
				int numberOfDigitals = (int)Math.Ceiling(Math.Log10(arraysize));
				string format = string.Format("{{0:D{0}}}", numberOfDigitals);
				return string.Format(format, index);
			}

			/// <exclude />
			public override void
			SetValue( object instance, object value )
			{
				if( instance is CurvePointArray )
				{
					CurvePointArray points = (CurvePointArray)instance;
					points[index] = (CurvePoint)value;
				}

				base.OnValueChanged( instance, EventArgs.Empty );
			}

			/// <exclude />
			public override object
			GetValue( object instance )
			{
				if( instance is CurvePointArray )
				{
					CurvePointArray points = (CurvePointArray)instance;
					return points[index];
				}

				return null;
			}
		
			private int index;
		}
		
		/// <exclude />
		public CurvePointArrayConverter() : base() {}
		
		/// <exclude />
		public override object 
    ConvertTo( ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType )
		{
			if (destinationType == null)
			{
				throw new ArgumentNullException( "destinationType" );
			}
			if(( destinationType == typeof(string) ) && ((value as CurvePointArray) != null) )
			{
				return "CurvePointArray";
			}
			return base.ConvertTo(context, culture, value, destinationType); 
		}		

		/// <exclude />
		public override PropertyDescriptorCollection 
    GetProperties( ITypeDescriptorContext context, object value, Attribute[] attributes )
		{
			CurvePointArrayPropertyDescriptor[] propArray = null;
			if( value is CurvePointArray )
			{
				CurvePointArray points = (CurvePointArray)value;
				propArray = new CurvePointArrayPropertyDescriptor[ points.Length ];
				for( int i = 0; i < points.Length; i++ )
				{
					propArray[i] = new CurvePointArrayPropertyDescriptor( points, i );
				}
			}
			return new PropertyDescriptorCollection( propArray );
		}

		/// <exclude />
		public override bool
    GetPropertiesSupported( ITypeDescriptorContext context )
		{
			return true; 
		}
	}
}
