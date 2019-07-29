using System;
using System.Collections.Generic;
using System.Threading;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Concurrency
{
  public static partial class Algorithms
  {
    #region Parallel Enumerate

    /// <summary>
    ///   Parallel For each loops.
    /// </summary>
    /// <typeparam name="T">The type of state</typeparam>
    /// <param name="data">The state data.</param>
    /// <param name="action">The action on the data.</param>
    public static void Enumerate<T>(
      IEnumerable<T> data, Action<T> action)
    {
      Enumerate<T,int>(data, null, (t,i)=>action(t), null);
    }

    /// <summary>
    ///   Parallel For each loops.
    /// </summary>
    /// <typeparam name="T">The type of state</typeparam>
    ///<typeparam name="U">The type of the local data</typeparam>
    /// <param name="data">The state data.</param>
    /// <param name="init">Thread local data initializer.</param>
    /// <param name="action">The action on the data.</param>
    /// <param name="reduce">Reduction function.</param>
    public static void Enumerate<T,U>(IEnumerable<T> data,
      Func<U> init, Action<T,U> action, Action<U> reduce)
    {
      if (data == null)
        throw new ArgumentNullException("data");
      if (action == null)
        throw new ArgumentNullException("action");

      int nworkers = GetAvailableWorkers();
      if (nworkers <= 1)
      {
        var u = init == null ? default(U) : init();
        foreach (var t in data)
          action(t,u);
        if (reduce != null)
          reduce(u);
        return;
      }

      // Create a synchtonized data pool
      var pool = new DataPool<T>(data);

      // Start parallel execution
      
      try
      {
        IncreaseWorkerCount();

        // Do work with multiple threads:
        //  We run one worker on main thread
        //  and (n - 1) workers on other threads.
        logger.InfoFormat("Parallel.ForEach.WorkerThreadCount: {0}", nworkers);
        int n = nworkers - 1;

        // Create an completion event to wait
        CountDownEvent evnt = new CountDownEvent(n);

        // Create n worker threads
        EnumerateWorker<T,U>[] items = new EnumerateWorker<T,U>[n];
        for (int i = 0; i < n; ++i)
        {
          //- Queue an item to thread pool
          items[i] = new EnumerateWorker<T,U>(init, action, pool, evnt);
          ThreadPool.QueueUserWorkItem(items[i].Proc);
        }

        var workerExceptions = new List<Exception>();

        // Run one worker on our thread
        EnumerateWorker<T,U> thisItem =
          new EnumerateWorker<T,U>(init, action, pool, null);
        thisItem.Proc(null);
        if (thisItem.Exception != null)
          workerExceptions.Add(thisItem.Exception);

        // Wait for other workers
        evnt.Wait();

        // Check for exceptions
        for (int i = 0; i < n; ++i)
        {
          if (items[i].Exception != null)
            workerExceptions.Add( items[i].Exception );
        }

        // Throw exception if any
        if (workerExceptions.Count>0)
          throw new AggregateException(workerExceptions);

        // Perform reduction if any
        if (reduce != null)
        {
          for (int i = 0; i < n; ++i)
            reduce(items[i].State);
          reduce(thisItem.State);
        }
      }
      finally
      {
        DecreaseWorkerCount();
      }

      return;
    }

    #endregion Parallel Enumerate

    #region Implementation Details
    /// <summary>
    ///   Implementation of a worker thread
    ///   which performs for loop in subranges.
    /// </summary>
    private class EnumerateWorker<T,U> : IWorkerThread
    {
      public EnumerateWorker(
        Func<U> init, Action<T,U> body,
        DataPool<T> pool, CountDownEvent evnt)
      {
        init_ = init;  body_ = body;
        pool_ = pool; evnt_ = evnt;
        state_ = default(U);
      }
      public void Proc(object unused)
      {
        try
        {
          IncreaseWorkerCount();
          done_ = false;

          var data = default(T);
          if (pool_.Take(ref data))
          {
            var state = state_ =
              init_ == null ? default(U) : init_();
            do
            {
              body_(data, state);
            } while (pool_.Take(ref data));
          }
        }
        catch (Exception e)
        {
          exception_ = e;
        }
        finally
        {
          if (evnt_ != null)
            evnt_.Set();
          done_ = true;
          DecreaseWorkerCount();
        }
        return;
      }
      public bool Done { get { return done_; } }
      public Exception Exception { get { return exception_; } }
      public U State { get { return state_; } }

      private DataPool<T> pool_;
      private Func<U> init_;
      private U state_;
      private Action<T, U> body_;
      private CountDownEvent evnt_;
      private bool done_ = false;
      private Exception exception_ = null;
    }

    private class DataPool<T>
    {
      internal DataPool(IEnumerable<T> data)
      {
        data_ = data.GetEnumerator();
      }
      internal bool Take(ref T t)
      {
        lock (data_)
        {
          if (!data_.MoveNext())
            return false;
          t = data_.Current;
          return true;
        }
      }
      private IEnumerator<T> data_;
    }
    #endregion Implementation Details

  } // class Algorithms
}
