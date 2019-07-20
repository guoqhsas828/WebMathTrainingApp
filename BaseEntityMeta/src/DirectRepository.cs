// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// A repository for items controlled by an NHibernate Session
  /// </summary>
  /// <typeparam name="T">
  /// The type of objects in the repository
  /// </typeparam>
  public class DirectRepository<T> : IRepository<T>
    where T : PersistentObject
  {
    #region Constants and Fields

    /// <summary>
    /// The class meta.
    /// </summary>
    // ReSharper disable StaticFieldInGenericType
    private static readonly ClassMeta ClassMeta;

    // ReSharper restore StaticFieldInGenericType

    /// <summary>
    /// The session.
    /// </summary>
    private readonly IQueryableAndEditableEntityContext _context;

    #endregion

    #region Constructors and Destructors

    /// <summary>
    /// Initializes static members of the <see cref="DirectRepository{T}" /> class.
    /// </summary>
    static DirectRepository()
    {
      ClassMeta = ClassCache.Find(typeof(T));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRepository{T}"/> class.
    /// </summary>
    public DirectRepository()
      : this((IQueryableAndEditableEntityContext)EntityContext.Current)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRepository{T}"/> class.
    /// </summary>
    /// <param name="entityContext">
    /// The session.
    /// </param>
    public DirectRepository(IQueryableAndEditableEntityContext entityContext)
    {
      _context = entityContext;
    }

    #endregion

    #region Public Properties

    /// <summary>
    ///   Gets the items in the repository (query).
    /// </summary>
    /// <value>The items (query).</value>
    public IQueryable<T> Items
    {
      get { return _context.Query<T>(); }
    }

    /// <summary>
    ///   Gets the session.
    /// </summary>
    /// <value>The session.</value>
    public IEntityContext Context
    {
      get { return _context; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Begin updates to the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void BeginUpdate(T item)
    {
      using (new EntityContextBinder(_context))
      {
        item.RequestUpdate();
      }
    }

    /// <summary>
    /// Creates an item.
    /// </summary>
    /// <returns>
    /// The newly created item
    /// </returns>
    public T Create()
    {
      return (T)ClassMeta.CreateInstance();
    }

    /// <summary>
    /// Deletes the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void Delete(T item)
    {
      _context.Delete(item);
    }

    /// <summary>
    /// Saves the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void Save(T item)
    {
      _context.Save(item);
    }

    #endregion
  }
}