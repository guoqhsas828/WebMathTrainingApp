/*
 * CurveTenorCollection.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves
{
  ///
  /// <summary>
  ///   Typesafe array of CurveTenors.
  /// </summary>
  ///
	/// <remarks>
	///   <see cref="CurveTenor">CurveTenor</see>
  /// </remarks>
  ///
  [Serializable]
	public class CurveTenorCollection : BaseEntityObject, IList<CurveTenor>, IList
	{
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public
		CurveTenorCollection()
    {
			tenors_ = new List<CurveTenor>();
		}


		///
    /// <summary>
    ///   Clone object for CurveTenorCollection
    /// </summary>
    ///
		/// <remarks>
		///   <para>Due to embedded references within the clone object, a
		///   specialised clone method is required.</para>
		/// </remarks>
    ///
		public override object
		Clone()
		{
		  var clone = (CurveTenorCollection) base.Clone();

      clone.tenors_ = CloneUtil.Clone(tenors_);

			return clone;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Return IEnumerator for tenors.
    /// </summary>
    public IEnumerator
    GetEnumerator()
    {
      return tenors_.GetEnumerator();
    }


		/// <summary>
		///   Add tenor to array of tenors
		/// </summary>
		public void
		Add(object value)
		{
			CurveTenor tenor=(CurveTenor)value;
			tenors_.Add(tenor);
		}


		/// <summary>
		///   Get index based on tenor name. -1 if not found.
		/// </summary>
		public int
		Index(string name)
		{
			for( int i = 0; i < tenors_.Count; i++ )
				if( String.Compare(((CurveTenor)tenors_[i]).Name, name, true) == 0 )
					return i;

			return -1;
		}

    /// <summary>
    /// Sort tenors by increasing maturity
    /// </summary>
    public void Sort()
    {
      tenors_.Sort();
    }
    
    /// <summary>
    ///   Clear tenors
    /// </summary>
    public void
    Clear()
    {
      tenors_.Clear();
    }
    
    
    /// <summary>
		///   Convert to string
		/// </summary>
		public override string ToString()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append(String.Format("tenors_.Count = {0}; Tenor list:\n", tenors_.Count));
			for (int i=0; i < tenors_.Count; i++)
			{
				sb.Append(String.Format("tenors_[{0}]: {1}\n", i, tenors_[i]));
			}

			return sb.ToString();
		}
		
		#endregion // Methods

		#region Properties

		/// <summary>
    ///   number of tenors
    /// </summary>
    [Category("Base")]
    public int Count
    {
      get { return tenors_.Count; }
    }


    /// <summary>
    ///  Get tenor by index
    /// </summary>
    public CurveTenor
    this[ int index ]
    {
      get { return (CurveTenor)tenors_[index]; }
    }

    /// <summary>
    /// Determine if this collection holds a tenor
    /// </summary>
    /// <param name="name">tenor name</param>
    /// <returns>true if tenor is present in collection</returns>
    public bool ContainsTenor(string name)
    {
      for (int i = 0; i < tenors_.Count; i++)
        if (String.Compare(((CurveTenor)tenors_[i]).Name, name, true) == 0)
          return true;

      return false;
    }

   
    /// <summary>
    /// Remove a tenor from the collection using date as a criteria
    /// </summary>
    /// <param name="date">date</param>
    public void Remove(Dt date)
    {
      for (int i = 0; i < tenors_.Count; i++)
      {
        if (Dt.Cmp(((CurveTenor)tenors_[i]).CurveDate, date) == 0)
          tenors_.RemoveAt(i);
      }
    }

    /// <summary>
    /// Binary search
    /// </summary>
    /// <param name="ten">Curve tenor</param>
    /// <returns> index of the tenor in the collection</returns>
    public int BinarySearch(CurveTenor ten)
    {
      return tenors_.BinarySearch(ten);
    }

    /// <summary>
    ///  Get tenor by name
    /// </summary>
    public CurveTenor
    this[ string name ]
    {
			get {
				for( int i = 0; i < tenors_.Count; i++ )
					if( String.Compare(((CurveTenor)tenors_[i]).Name, name, true) == 0 )
						return (CurveTenor)tenors_[i];

				throw new ArgumentException( String.Format("Tenor {0} not in curve", name) );
			}
    }

		#endregion // Properties

		#region Data

    private List<CurveTenor> tenors_;

		#endregion // Data

    #region IEnumerable<CurveTenor> Members

    IEnumerator<CurveTenor> IEnumerable<CurveTenor>.GetEnumerator()
    {
      return tenors_.GetEnumerator();
    }

    #endregion

    #region IList<CurveTenor> Members

    int IList<CurveTenor>.IndexOf(CurveTenor item)
    {
      return tenors_.IndexOf(item);
    }

    void IList<CurveTenor>.Insert(int index, CurveTenor item)
    {
      tenors_.Insert(index, item);
    }

    void IList<CurveTenor>.RemoveAt(int index)
    {
      tenors_.RemoveAt(index);
    }

    CurveTenor IList<CurveTenor>.this[int index]
    {
      get { return tenors_[index]; }
      set { tenors_[index] = value; }
    }

    #endregion

    #region ICollection<CurveTenor> Members

    /// <summary>
    /// Add curve tenor
    /// </summary>
    /// <param name="item">Curve tenor to add</param>
    public void Add(CurveTenor item)
    {
      tenors_.Add(item);
    }

    /// <summary>
    /// True if contains specified curve tenor
    /// </summary>
    /// <param name="item">Curve tenor to query</param>
    /// <returns>true if contains specified curve tenor</returns>
    public bool Contains(CurveTenor item)
    {
      return tenors_.Contains(item);
    }

    /// <summary>
    /// Copy specified array of curve tenors starting a specified point
    /// </summary>
    /// <param name="array">Curve tenors to copy</param>
    /// <param name="arrayIndex">Index to start copy</param>
    public void CopyTo(CurveTenor[] array, int arrayIndex)
    {
      tenors_.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Is read only
    /// </summary>
    public bool IsReadOnly
    {
      get { return false; }
    }

    /// <summary>
    /// Remove curve tenor
    /// </summary>
    /// <param name="item">Curve tenor to remove</param>
    /// <returns>true if found</returns>
    public bool Remove(CurveTenor item)
    {
      return tenors_.Remove(item);
    }

    #endregion

    #region IList Members

    int IList.Add(object value)
    {
      return ((IList)tenors_).Add(value);
    }

    void IList.Clear()
    {
      ((IList)tenors_).Clear();
    }

    bool IList.Contains(object value)
    {
      return ((IList)tenors_).Contains(value);
    }

    int IList.IndexOf(object value)
    {
      return ((IList)tenors_).IndexOf(value);
    }

    void IList.Insert(int index, object value)
    {
      ((IList)tenors_).Insert(index, value);
    }

    bool IList.IsFixedSize
    {
      get { return ((IList)tenors_).IsFixedSize; }
    }

    bool IList.IsReadOnly
    {
      get { return ((IList)tenors_).IsReadOnly; }
    }

    void IList.Remove(object value)
    {
      ((IList)tenors_).Remove(value);
    }

    void IList.RemoveAt(int index)
    {
      ((IList)tenors_).RemoveAt(index);
    }

    object IList.this[int index]
    {
      get { return ((IList)tenors_)[index]; }
      set { ((IList)tenors_)[index] = value; }
    }

    #endregion

    #region ICollection Members

    void ICollection.CopyTo(Array array, int index)
    {
      ((IList)tenors_).CopyTo(array, index);
    }

    int ICollection.Count
    {
      get { return ((IList)tenors_).Count; }
    }

    bool ICollection.IsSynchronized
    {
      get { return ((IList)tenors_).IsSynchronized; }
    }

    object ICollection.SyncRoot
    {
      get { return ((IList)tenors_).SyncRoot; }
    }

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      return ((IList)tenors_).GetEnumerator();
    }

    #endregion
  } // class CurveTenorCollection

}
