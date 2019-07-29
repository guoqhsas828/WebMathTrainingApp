/*
 * ArrayPool.cs - 
 *
 *   2004-2008. All rights reserved.
 *
 * $Id $
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BaseEntity.Toolkit.Concurrency
{
  /// <summary>
  ///   A fixed size pool of objects.
  /// </summary>
  /// <typeparam name="T">Type of the objects in the pool</typeparam>
  /// <remarks>
  ///   This class allows multiple threads to take objects from the pool concurrently.
  ///   It can be used to represent a simple queue containing a fixed number of tasks.
  /// </remarks>
  public class ArrayPool<T>
  {
    /// <summary>
    ///   Constructor.
    /// </summary>
    /// <param name="objects">an array of objects to be placed in the pool.</param>
    public ArrayPool(T[] objects)
    {
      if (objects == null)
        objects = new T[0];
      data_ = objects;
      current_ = 0;
    }

    /// <summary>
    ///   Take an object from the pool.
    /// </summary>
    /// <remarks>
    ///   This function thread-safe, allowing multiple threads to take objects
    ///   from the pool.  When an object taken by one thread, it is conceptually
    ///   removed from the pool and it is no longer available to other threads.
    /// </remarks>
    /// <param name="t">A place to receive the object taken.</param>
    /// <returns>
    ///   True if the pool is not empty and an object is taken;
    ///   False if the pool is empty and no object taken.
    /// </returns>
    public bool Take(ref T t)
    {
      if (current_ >= data_.Length)
        return false;
      int idx = Interlocked.Increment(ref current_) - 1;
      if (idx >= data_.Length)
        return false;
      t = data_[idx];
      return true;
    }

    private int current_;
    private readonly T[] data_;
  }
}
