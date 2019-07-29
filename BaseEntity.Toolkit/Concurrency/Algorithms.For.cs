/*
 * Algorithms.For.cs - 
 *
 *   2004-2008. All rights reserved.
 *
 * Implement simple form of parallel for functions.
 *
 */
using System;
using System.Collections.Generic;
using System.Threading;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Concurrency
{
  ///
  public static partial class Algorithms
  {
    #region Public Interfaces

    #region Parallel_For

    /// <summary>
    ///   Converts an array of one type to an array of another type.
    /// </summary>
    /// <typeparam name="TInput">The type of the elements of the source array.</typeparam>
    /// <typeparam name="TOutput">The type of the elements of the target array.</typeparam>
    /// <param name="array">The one-dimensional, zero-based Array to convert to a target type.</param>
    /// <param name="converter">A Converter that converts each element from one type to another type.</param>
    /// <returns>An array of the target type containing the converted elements from the source array.</returns>
    /// <exception cref="System.ArgumentNullException">array or converter is null.</exception>
    public static TOutput[] ConvertAll<TInput, TOutput>(
      TInput[] array, Converter<TInput, TOutput> converter)
    {
      if (array == null || converter == null)
        throw new ArgumentNullException("Null array or converter");
      TOutput[] output = new TOutput[array.Length];
      For<int>(0, array.Length, null,
        delegate(int i, int u) { output[i] = converter(array[i]); }, null);
      return output;
    }

    /// <summary>
    ///   Performs actions on individual elements of an array.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the array.</typeparam>
    /// <param name="array">The one-dimensional, zero-based Array on whose elements the action is to be performed.</param>
    /// <param name="action">The Action to perform on each element of array.</param>
    /// <exception cref="System.ArgumentNullException">array or action is null.</exception>
    public static void ForEach<T>(T[] array, Action<T> action)
    {
      if (array == null)
        throw new ArgumentNullException("array is null");
      For<int>(0, array.Length, null,
        delegate(int i, int u) { action(array[i]); }, null);
    }

    /// <summary>
    ///   Performs actions on individual elements of an array.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="U">Thread local state type.</typeparam>
    /// <param name="array">An input array.</param>
    /// <param name="init">A delegate creating thread local states.</param>
    /// <param name="action">Action on each element.</param>
    /// <exception cref="System.ArgumentNullException">array or action is null.</exception>
    public static void ForEach<T, U>(
      T[] array, Func<U> init, Action<T,U> action)
    {
      if (array == null)
        throw new ArgumentNullException("array is null");
      For<U>(0, array.Length, init,
        delegate(int i, U u) { action(array[i], u); }, null);
    }

    /// <summary>
    ///   Invoke for loop in parallel
    /// </summary>
    /// <remarks>
    ///   This function executes the following loop in parallel
    ///   <code language="C#">
    ///     for (int i = start; i &lt; stop; ++i)
    ///       body(i);
    ///   </code>
    /// </remarks>
    /// <param name="start">Start index</param>
    /// <param name="stop">Stop index</param>
    /// <param name="action">
    ///   Action performed on index.  It must be safe to call concurrently
    ///   with different indices.
    /// </param>
    /// <exception cref="System.ArgumentNullException">action is null.</exception>
    public static void For(int start, int stop, Action<int> action)
    {
      For<int>(start, stop, null,
        delegate(int i, int u) { action(i); }, null);
    }

    /// <summary>
    ///   Invoke for loop in parallel with thread local data.
    /// </summary>
    /// <typeparam name="U">Type of thread local data</typeparam>
    /// <param name="start">Start index</param>
    /// <param name="stop">Stop index</param>
    /// <param name="init">Thread local states initializer</param>
    /// <param name="action">Action at each step</param>
    public static void For<U>(int start, int stop,
      Func<U> init, Action<int, U> action)
    {
      For<U>(start, stop, init, action, null);
    }

    #endregion Parallel_For

    #region Parallel_Sum
    /// <summary>
    ///   Calculate the sum of values.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="eval">
    ///   Evaluation function returning the value at step <c>i</c>. 
    /// </param>
    /// <returns>Sum of values.</returns>
    /// <remarks>
    ///   This function returns <m>\sum_{\mathrm{start}\leq i \lt \mathrm{stop}} \mathrm{eval}(i)</m>,
    ///   where <c>eval</c> is
    ///   a delegate evaluating the value at step <c>i</c>.
    ///   Since the delegate is invoked concurrently, the caller must
    ///   make sure that it is thread safe.
    /// <example>
    ///   The following example aggregates the Value values of a portfolio.
    ///   <code>
    ///     static double SumPv(IPricer[] pricers)
    ///     {
    ///       double sumPv = 0;
    ///       for (int i = 0; i &lt; pricers.Length; ++i)
    ///       sumPv += pricers[i].Pv();
    ///       return sumPv;
    ///     }
    ///   </code>
    ///   Suppose all the individual pricers in the portfolio 
    ///   are independent such that the calls to pricer Pv() functions 
    ///   are thread safe.  We can parallelize the calculation easily 
    ///   with <c>Parallel.Sum</c>: 
    ///   <code>
    ///     static double SumPvParallel(IPricer[] pricers)
    ///     {
    ///       return Algorithms.Sum(0, pricers.Length,
    ///         delegate(int i) { return pricers[i].Pv(); });
    ///     }
    ///   </code>
    ///   In the above codes the third argument of <c>Parallel.Sum</c> is a delegate
    ///   returning the value at step <c>i</c>.
    /// </example>
    /// </remarks>
    public static double Sum(int start, int stop, Func<int, double> eval)
    {
      double result = 0;
      For<double[]>(start, stop,
        delegate() { return new double[] { 0.0 }; },
        delegate(int i, double[] s) { s[0] += eval(i); },
        delegate(double[] s) { result += s[0]; });
      return result;
    }

    /// <summary>
    ///  Stable summation in the sense that the round-off errors
    ///  does not depend on the threading schedule.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="eval">
    ///   Evaluation function returning the value at step <c>i</c>. 
    /// </param>
    /// <returns>Sum of values.</returns>
    public static double StableSum(int start, int stop, Func<int, double> eval)
    {
      double[] results = new double[stop - start];
      For(start, stop, (i) => { results[i - start] = eval(i); });
      double sum = 0;
      for (int i = 0; i < results.Length; ++i) sum += results[i];
      return sum;
    }

    /// <summary>
    ///   Calculate the sum of value pairs in parallel.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="eval">
    ///   Evaluation function calculating a pair of values at step <c>i</c>.
    /// </param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <remarks>
    ///   This function calculates the sum of value pair
    ///   <m>(x,y) = \sum_{\mathrm{start}\leq i \lt \mathrm{stop}} (x_i, y_i)</m>,
    ///   where <m>(x_i, y_i)</m> are calculated by <c>eval(i, out <m>x_i</m>, out <m>y_i</m>)</c>.
    ///   Since the delegate is invoked concurrently, the caller must
    ///   make sure that it is thread safe.
    /// <example>
    ///  <para>Suppose we want to calculate the duration weighted spread for a CDX pricer.
    ///  The non-parallel codes are:</para>
    ///  <code>
    ///  static double DurationWeightedSpread(CDXPricer pricer)
    ///  {
    ///    double durSpreadSum = 0.0, durationSum = 0.0;
    ///    SurvivalCurve[] curves = pricer.SurvivalCurves;
    ///    for (int i = 0; i &lt; curves.Length; i++)
    ///    {
    ///      CDX cdx = pricer.CDX;
    ///      CDSCashflowPricer cdsPricer = CurveUtil.ImpliedPricer(
    ///        curves[i], cdx.Maturity, cdx.DayCount, cdx.Freq,
    ///        cdx.BDConvention,  cdx.Calendar);
    ///      double duration = cdsPricer.RiskyDuration();
    ///      double spread = cdsPricer.BreakEvenPremium();
    ///      durSpreadSum += duration * spread;
    ///      durationSum += duration;
    ///    }
    ///    return durSpreadSum / durationSum;
    ///  }
    ///  </code>
    ///  <para>The main loop above calculates two sums simultaneously
    ///  and the results are taken ratio to obtain the weighted average.
    ///  The summation part can be efficiently parallelized by <c>Sum2</c>c>.
    /// </para>
    ///  <code>
    ///  static double DurationWeightedSpreadParallel(CDXPricer pricer)
    ///  {
    ///    double durSpreadSum, durationSum;
    ///    SurvivalCurve[] curves = pricer.SurvivalCurves;
    ///    Parallel.Sum2(0, curves.Length,
    ///      delegate(int i, out double durSpread, out double duration)
    ///      {
    ///        CDX cdx = pricer.CDX;
    ///        CDSCashflowPricer cdsPricer = CurveUtil.ImpliedPricer(
    ///          curves[i], cdx.Maturity, cdx.DayCount, cdx.Freq,
    ///          cdx.BDConvention, cdx.Calendar);
    ///        duration = cdsPricer.RiskyDuration();
    ///        durSpread = duration * cdsPricer.BreakEvenPremium();
    ///      },
    ///      out durSpreadSum, out durationSum);
    ///    return durSpreadSum / durationSum;
    ///  }
    ///  </code> 
    /// </example>
    /// </remarks>
    public static void Sum2(int start, int stop,
      Eval<int, double, double> eval,
      out double result1, out double result2)
    {
      double first = 0, second = 0;
      For<double[]>(start, stop,
        delegate()
        {
          return new double[] { 0.0, 0.0 };
        },
        delegate(int i, double[] s)
        {
          double v0, v1; eval(i, out v0, out v1);
          s[0] += v0; s[1] += v1;
        },
        delegate(double[] s)
        {
          first += s[0]; second += s[1];
        });
      result1 = first;
      result2 = second;
      return;
    }

    /// <summary>
    ///   Calculate the sum of values in parallel with thread local data.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="init">Thread local data initializer.</param>
    /// <param name="eval">
    ///   Evaluation function returning the value at step <c>i</c>. 
    /// </param>
    /// <returns>Sum of values.</returns>
    /// <example>
    ///   <para>In this example, we calculate the intrinsic value 
    ///   of a CDS index.  Here is a non-parallel implementation
    ///   with a single CDS pricer shared among steps:</para>
    ///   <code>
    ///   static double IntrinsicValue(CDXPricer pricer)
    ///   {
    ///     double totalPv = 0;
    ///     CDSCashflowPricer cdsPricer = GetCDSPricer(pricer);
    ///     SurvivalCurve[] curves = pricer.SurvivalCurves;
    ///     for (int i = 0; i &lt; curves.Length; i++)
    ///     {
    ///       cdsPricer.SurvivalCurve = curves[i];
    ///       cdsPricer.Reset();
    ///       totalPv += cdsPricer.Pv();
    ///     }
    ///     return totalPv;
    ///   }
    ///   </code>
    ///   <para>To parallelize this, we need to make the CDS pricer thread local
    ///   for the sake of thread safety.  
    ///   Here is a parallel version:</para>
    ///   <code>
    ///   static double IntrinsicValueParallel(CDXPricer pricer)
    ///   {
    ///     SurvivalCurve[] curves = pricer.SurvivalCurves;
    ///     return Parallel.Sum&lt;CDSCashflowPricer&gt;(
    ///       0, curves.Length,
    ///       delegate() // create thread local pricer
    ///       {
    ///         return GetCDSPricer(pricer);
    ///       },
    ///       // main loop step using thread local pricer
    ///       delegate(int i, CDSCashflowPricer cdsPricer)
    ///       {
    ///         cdsPricer.SurvivalCurve = curves[i];
    ///         cdsPricer.Reset();
    ///         return cdsPricer.Pv();
    ///       });
    ///   }
    ///   </code>
    ///   <para>Please note how the thread local pricers are created and used.</para>
    /// </example>
    public static double Sum<T>(int start, int stop,
      Func<T> init, Func<int, T, double> eval)
    {
      double result = 0;
      For<Pair<T, double>>(start, stop,
        delegate()
        {
          return new Pair<T, double>(
            init == null ? default(T) : init(), 0.0);
        },
        delegate(int i, Pair<T, double> p)
        {
          p.Second += eval(i, p.First);
        },
        delegate(Pair<T, double> p)
        {
          result += p.Second;
        });
      return result;
    }

    /// <summary>
    ///   Calculate the sum of values in parallel with thread local data.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="init">Thread local data initializer.</param>
    /// <param name="eval">
    ///   Evaluation function calculating a pair of values at step <c>i</c>.
    /// </param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    public static void Sum2<T>(int start, int stop,
      Func<T> init, Eval<int, T, double, double> eval,
      out double result1, out double result2)
    {
      double first = 0, second = 0;
      For<Triple<T, double, double>>(start, stop,
        delegate()
        {
          return new Triple<T, double, double>(
            init == null ? default(T) : init(), 0.0, 0.0);
        },
        delegate(int i, Triple<T, double, double> p)
        {
          double v0, v1; eval(i, p.First, out v0, out v1);
          p.Second += v0; p.Third += v1;
        },
        delegate(Triple<T, double, double> p)
        {
          first += p.Second; second += p.Third;
        });
      result1 = first;
      result2 = second;
      return;
    }

    private class Pair<T, U>
    {
      public Pair(T t, U u) { First = t; Second = u; }
      public T First;
      public U Second;
    };
    private class Triple<T, U, V>
    {
      public Triple(T t, U u, V v) 
      { First = t; Second = u; Third=v;}
      public T First;
      public U Second;
      public V Third;
    };
    #endregion Parallel_Sum

    #region Parallel_Reduce
    /// <summary>
    ///   Invoke for loop in parallel with thread local data
    ///   and a final reduction stage.
    /// </summary>
    /// <typeparam name="U">Type of thread local data</typeparam>
    /// <param name="start">Start index</param>
    /// <param name="stop">Stop index</param>
    /// <param name="init">Thread local states initializer</param>
    /// <param name="action">Action at each step</param>
    /// <param name="reduce">Thread local data reducer</param>
    public static void For<U>(int start, int stop,
      Func<U> init, Action<int, U> action, Action<U> reduce)
    {
      if (stop <= start)
        return;
      if (action == null)
        throw new ArgumentNullException("action");

      // Find the number of workers and create an array of ranges
      ArrayPool<Range> pool = null;
      int nworkers = GetWorkersAndRange(start, stop, ref pool);

      // If only one worker, do it single threaded.
      if (nworkers <= 1)
      {
        U state = (init == null ? default(U) : init());
        for (int i = start; i < stop; ++i)
          action(i, state);
        if (reduce != null)
          reduce(state);
        return;
      }

      // Start parallel execution
      try
      {
        IncreaseWorkerCount();

        // Do work with multiple threads:
        //  We run one worker on main thread
        //  and (n - 1) workers on other threads.
        logger.InfoFormat("Parallel.For.WorkerThreadCount: {0}", nworkers);
        int n = nworkers - 1;

        // Create an completion event to wait
        CountDownEvent evnt = new CountDownEvent(n);

        // Create n worker threads
        ForWorker<U>[] items = new ForWorker<U>[n];
        for (int i = 0; i < n; ++i)
        {
          //- Queue an item to thread pool
          items[i] = new ForWorker<U>(init, action, pool, evnt);
          ThreadPool.QueueUserWorkItem(items[i].Proc);
        }

        // Run one worker on our thread
        ForWorker<U> thisItem =
          new ForWorker<U>(init, action, pool, null);
        thisItem.Proc(null);

        List<Exception> workerExceptions = new List<Exception>();
        if (thisItem.Exception != null)
          workerExceptions.Add(thisItem.Exception);

        // Wait for other workers
        evnt.Wait();

        // Check for exceptions
        for (int i = 0; i < n; ++i)
        {
          if (items[i].Exception != null)
            workerExceptions.Add(items[i].Exception);
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
    #endregion Parallel_Reduce

    #endregion Public Interfaces

    #region Implementation Details
    /// <summary>
    ///   Implementation of a worker thread
    ///   which performs for loop in subranges.
    /// </summary>
    private class ForWorker<TState> : IWorkerThread
    {
      public ForWorker(
        Func<TState> init, Action<int,TState> body,
        ArrayPool<Range> pool, CountDownEvent evnt)
      {
        init_ = init;  body_ = body;
        pool_ = pool; evnt_ = evnt;
      }
      public void Proc(object unused)
      {
        try
        {
          IncreaseWorkerCount();
          done_ = false;

          state_ = (init_ == null ? default(TState) : init_());

          Range r = Range.Empty;
          while (pool_.Take(ref r))
          {
            for (int i = r.Begin; i < r.End; ++i)
              body_(i, state_);
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
      public TState State { get { return state_; } }

      private ArrayPool<Range> pool_;
      private Action<int, TState> body_;
      private Func<TState> init_;
      private TState state_;
      private CountDownEvent evnt_;
      private bool done_ = false;
      private Exception exception_ = null;
    }
    #endregion Implementation Details

  } // partial class Algorithms
}
