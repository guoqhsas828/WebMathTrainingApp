using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Toolkit.Concurrency
{
  ///
  public delegate TResult Func<TResult>();
  ///
  public delegate TResult Func<T, TResult>(T arg);
  ///
  public delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);

  ///
  public delegate void Action<T1, T2>(T1 arg1, T2 arg2);

  ///
  public delegate void Accumulator(int i, ref double sum);
  ///
  public delegate void Accumulator<T>(int i, T states,
    ref double sum);
  ///
  public delegate void Accumulator2(int i, ref double s1, ref double s2);
  ///
  public delegate void Accumulator2<T>(int i, T states,
    ref double s1, ref double s2);

  ///
  public delegate void Eval<T, TOut1, TOut2>(T i, out TOut1 o1, out TOut2 o2);
  ///
  public delegate void Eval<T, U, TOut1, TOut2>(T i, U u, out TOut1 o1, out TOut2 o2);
}
