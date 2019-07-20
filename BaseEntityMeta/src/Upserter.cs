// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Upserter.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   Updates (if found) or creates and saves (if not found) a repository item, according to the specified unique search criteria.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Linq.Expressions;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Updates (if found) or creates and saves (if not found) a repository item, according to the specified unique search criteria.
  /// Disposable, so when the upserter is disposed the appropriate action will be taken against the repository, depending upon whether
  /// or not the item was actually created by the upserter.
  /// </summary>
  /// <typeparam name="T">
  /// The type of the repository (item)
  /// </typeparam>
  public class Upserter<T> : IDisposable
    where T : class
  {
    #region Constants and Fields

    /// <summary>
    /// The repository.
    /// </summary>
    private readonly IRepository<T> _repository;

    /// <summary>
    /// Has the upserter been disposed?
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// Was the item created by the upserter?
    /// </summary>
    private bool _isNew;

    /// <summary>
    /// The item.
    /// </summary>
    private T _item;

    #endregion

    #region Constructors and Destructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Upserter{T}"/> class. 
    /// </summary>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    /// <param name="repository">
    /// The repository.
    /// </param>
    public Upserter(Expression<Func<T, bool>> criteria, IRepository<T> repository)
    {
      if (repository == null)
      {
        throw new ArgumentNullException(nameof(repository));
      }

      this._repository = repository;
      Load(criteria);
    }

    #endregion

    #region Public Properties

    /// <summary>
    ///   Gets a value indicating whether this item was newly created or was found in the repository.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this item is new; otherwise, <c>false</c>.
    /// </value>
    public bool IsItemNew
    {
      get
      {
        return _isNew;
      }
    }

    /// <summary>
    ///   Gets the item.
    /// </summary>
    /// <value>The item.</value>
    public T Item
    {
      get
      {
        return _item;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
      if (!_isDisposed)
      {
        if (disposing)
        {
          DisposeManagedResources();
        }

        // ReleaseUnmanagedResources();
        _isDisposed = true;
      }
    }

    /// <summary>
    /// The dispose managed resources.
    /// </summary>
    private void DisposeManagedResources()
    {
      if (_isNew)
      {
        _repository.Save(_item);
      }
    }

    /// <summary>
    /// The load.
    /// </summary>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    /// <exception cref="MetadataException">
    /// More than one item was found to be matching the specified criteria
    /// </exception>
    private void Load(Expression<Func<T, bool>> criteria)
    {
      var items = _repository.Items.Where(criteria);
      try
      {
        _item = items.SingleOrDefault();
      }
      catch (InvalidOperationException ioe)
      {
        throw new MetadataException(
          string.Format($"Upserter can only handle unique values. The specified criteria should probably include predicates on the key properties of {typeof(T)}, and must ultimately identify at most one item.", 
          ioe));
      }

      if (_item != null)
      {
        _repository.BeginUpdate(_item);
      }
      else
      {
        _item = _repository.Create();
        _isNew = true;
      }
    }

    #endregion

    // private void ReleaseUnmanagedResources()
    // {
    // }
  }
}