// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///
  /// </summary>
  public class EntityContextBinder : IDisposable
  {
    #region Data

    private readonly IEntityContext _thisContext;
    private readonly IEntityContext _prevContext;
    private readonly bool _isOwned;

    #endregion

    #region Constructors

    /// <summary>
    /// Bind specified session
    /// </summary>
    /// <param name="context"></param>
    public EntityContextBinder(IEntityContext context)
    {
      _thisContext = context;
      _prevContext = EntityContext.Bind(context);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="isOwned"></param>
    public EntityContextBinder(IEntityContext context, bool isOwned)
    {
      _thisContext = context;
      _prevContext = EntityContext.Bind(context);
      _isOwned = isOwned;
    }

    /// <summary>
    /// Create a new <see cref="IEntityContext"/> and bind to current thread.
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="setValidFrom"></param>
    /// <returns></returns>
    public EntityContextBinder(DateTime asOf, bool setValidFrom)
      : this(EntityContextFactory.Create(asOf, ReadWriteMode.ReadWrite, setValidFrom), true)
    {
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// A flag to indicate if <c>Dispose()</c> has been called.
    /// </summary>
    private bool _isAlreadyDisposed;

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
    }

    /// <summary>
    /// Takes care of freeing the managed and unmanaged resources that this class is responsible for.
    /// </summary>
    /// <param name="isDisposing">Indicates if this Session is being Disposed of or Finalized.</param>
    /// <remarks>
    /// If this Session is being Finalized (<c>isDisposing==false</c>) then make sure not
    /// to call any methods that could potentially bring this Session back to life.
    /// </remarks>
    private void Dispose(bool isDisposing)
    {
      if (_isAlreadyDisposed)
        return;

      // Free managed resources that are being managed by the session if we know this call came through Dispose()
      if (isDisposing)
      {
        var boundContext = EntityContext.Bind(_prevContext);
        if (boundContext == null)
        {
          throw new MetadataException("No current session");
        }
        if (!ReferenceEquals(boundContext, _thisContext))
        {
          throw new MetadataException("Invalid current session");
        }

        if (_isOwned)
          _thisContext.Dispose();
      }

      _isAlreadyDisposed = true;
      GC.SuppressFinalize(this);
    }

    #endregion
  }
}