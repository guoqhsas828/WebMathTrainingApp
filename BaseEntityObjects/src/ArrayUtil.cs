/*
 * ArrayUtil.cs
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  ///   Small helper functions manipulating arrays.
  ///   For WebMathTraining internal use only.
  ///   <preliminary/>
  /// </summary>
  /// <exclude/>
  public static class ArrayUtil
  {
    /// <exclude/>
    public delegate T Generater<T>(int i);
    /// <exclude/>
    public delegate bool Selector(int i);
    /// <exclude/>
    public delegate int Counter(int i);
    /// <exclude/>
    public delegate OutT Converter<InT1, InT2, OutT>(InT1 t1, InT2 t2);
    

    /// <summary>
    /// Converts an array of one type into a List of another type using the given conversion function.
    /// </summary>
    /// 
    /// <typeparam name="InT">The input type to convert</typeparam>
    /// <typeparam name="OutT">The output type</typeparam>
    /// <param name="array">The array of objects to convert</param>
    /// <param name="convert">The conversion function</param>
    /// 
    /// <returns>A List of the output type</returns>
    /// 
    public static List<OutT> Convert<InT, OutT>(InT[] array, Converter<InT, OutT> convert)
    {
      List<OutT> results = new List<OutT>();

      if(!HasValue(array))
        return results;

      // Convert all
      for(int i = 0; i < array.Length; i++)
        results.Add(convert(array[i]));

      // Done
      return results;
    }

    /// <summary>
    /// Determines whether an Array has at least 1 non-null value in its elements. 
    /// </summary>
    /// 
    /// <typeparam name="T">The type</typeparam>
    /// <param name="array">The array to check</param>
    /// 
    /// <returns>Boolean. </returns>
    /// 
    public static bool HasValue<T>(T[] array)
    {
      // null or zero-length array
      if (array == null || array.Length == 0)
        return false;

      // Check array elements until a value is found
      if (typeof(T).IsValueType)
        return true;
      else
      {
        for (int i = 0; i < array.Length; i++)
        {
          if (array[i] != null)
            return true;
        }

        // No value found
        return false;
      }
    }

    /// <summary>
    ///   Create an array with all the elements initialized to a given value
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="n">Size of array</param>
    /// <param name="t">Value to initialize</param>
    /// <returns>An initialized array</returns>
    public static T[] NewArray<T>(int n, T t)
    {
      T[] a = new T[n];
      for (int i = 0; i < n; ++i)
        a[i] = t;
      return a;
    }

    /// <summary>
    ///  Create a new two dimensional array and initialize all elements with the specified value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="m">The m.</param>
    /// <param name="n">The n.</param>
    /// <param name="t">The t.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static T[,] New<T>(int m, int n, T t)
    {
      T[,] a = new T[m,n];
      for (int i = 0; i < m; ++i)
        for (int j = 0; i < n; ++j)
          a[i,j] = t;
      return a;
    }

    /// <summary>
    ///   Create an array and initialize the elements by a function
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="n">Size of array</param>
    /// <param name="generate">Initialization function</param>
    /// <returns>An initialized array of length n</returns>
    public static T[] Generate<T>(int n, Generater<T> generate)
    {
      T[] a = new T[n];
      for (int i = 0; i < n; ++i)
        a[i] = generate(i);
      return a;
    }

    /// <summary>
    ///   Create an array with selected elements and initializing them by a function
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="n">Maximum number of elements</param>
    /// <param name="select">Selector.  Element <c>i</c> is included in output array only when <c>select(i)</c> return true.</param>
    /// <param name="generate">Initializer.  Element <c>i</c> is initialized to value <c>generate(i)</c> when it is included.</param>
    /// <returns>an array of length less or equal to <c>n</c></returns>
    public static T[] GenerateIf<T>(int n, Selector select, Generater<T> generate)
    {
      List<T> list = new List<T>();
      for (int i = 0; i < n; ++i)
        if (select(i)) list.Add(generate(i));
      return list.ToArray();
    }

    /// <summary>
    ///   Create an sub-array 
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="array">The original array</param>
    /// <param name="select">Selector.  Element <c>i</c> is included in output array only when <c>select(i)</c> return true.</param>
    /// <returns>An sub-array</returns>
    public static T[] SubArray<T>(T[] array, Selector select)
    {
      List<T> list = new List<T>();
      for (int i = 0; i < array.Length; ++i)
        if (select(i)) list.Add(array[i]);
      return list.ToArray();
    }

    /// <summary>
    ///   Pick elements from an array according to non-default indicators.
    ///   <preliminary/>
    /// </summary>
    /// <typeparam name="T">Element type of the array to pick</typeparam>
    /// <typeparam name="U">Element type of the indicator array</typeparam>
    /// <param name="ary">The array to pick from</param>
    /// <param name="picks">The indicator array (non-default value means to pick)</param>
    /// <returns>An array of the elements picked.</returns>
    ///
    /// <remarks>
    /// <para>The number of the elements picked is the number of the non-default
    /// elements in the array <paramref name="picks"/>, while
    /// the array returned depends on input array <paramref name="ary"/>.
    /// <list type="bullet">
    /// <item><description>
    ///   If <paramref name="ary"/> is empty, it returns null;
    /// </description></item>
    /// <item><description>
    ///   If <paramref name="ary"/> has only one element, say the value of which is <c>v</c>,
    ///   then it returns an array of value <c>v</c> with the length equal to the number
    ///   of non-default valued picks;
    /// </description></item>
    /// <item><description>
    ///   If <paramref name="ary"/> has the same length as <paramref name="picks"/>,
    ///   it returns an array of the elements picked at the positions where the corresponding
    ///   picks are of the non-default values;
    /// </description></item>
    /// <item><description>
    ///   Otherwise, an <c>ArgumentException</c> is thrown.
    /// </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// 
    /// <exclude />
    public static T[] PickElements<T, U>(T[] ary, U[] picks) where U : struct
    {
      if (ary == null || ary.Length == 0)
        return null;
      if (ary.Length != 1 && ary.Length != picks.Length)
        throw new ArgumentException(String.Format(
          "the array (len={0}) and picks (len={1}) not match",
          ary.Length, picks.Length));

      // Find length of picks
      int nPicked = 0;
      for (int i = 0; i < picks.Length; ++i)
        if (!picks[i].Equals(default(U)))
          ++nPicked;

      if (nPicked == ary.Length)
      {
        // Special case 1: pick the whole array
        return (T[])ary.Clone();
      }
      else if (ary.Length == 1)
      {
        // Special case 2: extend a single element to an array
        // of the required length.
        return NewArray(nPicked, ary[0]);
      }

      // Now, need to pick up a real subset of elements.
      T[] result = new T[nPicked];
      for (int i = 0, idx = 0; i < picks.Length; ++i)
        if (!picks[i].Equals(default(U)))
          result[idx++] = ary[i];

      // The result
      return result;
    }

    /// <summary>
    /// Converts a read-only list of one type to an array of another type.
    /// </summary>
    /// <typeparam name="TIn">The type of the source items</typeparam>
    /// <typeparam name="TOut">The type of the out items</typeparam>
    /// <param name="source">The source list</param>
    /// <param name="converter">The converter function</param>
    /// <returns>TOut[].</returns>
    public static TOut[] ConvertAll<TIn, TOut>(
      IReadOnlyList<TIn> source, Func<TIn, TOut> converter)
    {
      if (source == null)
      {
        return null;
      }
      var n = source.Count;
      var array = new TOut[n];
      for (int i = 0; i < n; ++i)
        array[i] = converter(source[i]);
      return array;
    }

    /// <summary>
    /// Converts a read-only list of one type to an array of another type.
    /// </summary>
    /// <typeparam name="TIn">The type of the source items</typeparam>
    /// <typeparam name="TOut">The type of the out items</typeparam>
    /// <param name="source">The source list</param>
    /// <param name="converter">The converter function</param>
    /// <returns>TOut[].</returns>
    public static TOut[] ConvertAll<TIn, TOut>(
      IReadOnlyList<TIn> source, Func<TIn, int, TOut> converter)
    {
      if (source == null)
      {
        return null;
      }
      var n = source.Count;
      var array = new TOut[n];
      for (int i = 0; i < n; ++i)
        array[i] = converter(source[i], i);
      return array;
    }

    /// <summary>
    ///   Create an array from two arrays with the same lengths
    /// </summary>
    /// <typeparam name="InT1">Element type of the first input array</typeparam>
    /// <typeparam name="InT2">Element type of the second input array </typeparam>
    /// <typeparam name="OutT">Element type of the output array</typeparam>
    /// <param name="array1">The first input array</param>
    /// <param name="array2">The second input array</param>
    /// <param name="fn">Conversion function</param>
    /// <returns>An output array</returns>
    public static OutT[] ConvertAll<InT1, InT2, OutT>(
      InT1[] array1, InT2[] array2, Converter<InT1, InT2, OutT> fn)
    {
      if (array1.Length != array2.Length)
        throw new ArgumentException("Length of array1 and array2 not match");
      OutT[] array0 = new OutT[array1.Length];
      for (int i = 0; i < array1.Length; ++i)
        array0[i] = fn(array1[i], array2[i]);
      return array0;
    }

    /// <summary>
    ///   Create an array from two arrays with the same lengths
    /// </summary>
    /// <typeparam name="InT1">Element type of the first input array</typeparam>
    /// <typeparam name="InT2">Element type of the second input array </typeparam>
    /// <typeparam name="OutT">Element type of the output array</typeparam>
    /// <param name="array1">The first input array</param>
    /// <param name="array2">The second input array</param>
    /// <param name="select">Selection function</param>
    /// <param name="fn">Conversion function</param>
    /// <returns>An output array</returns>
    public static OutT[] ConvertAll<InT1, InT2, OutT>(
      InT1[] array1, InT2[] array2, Selector select, Converter<InT1, InT2, OutT> fn)
    {
      if (array1.Length != array2.Length)
        throw new ArgumentException("Length of array1 and array2 not match");
      OutT[] array0 = new OutT[array1.Length];
      for (int i = 0; i < array1.Length; ++i)
      {
        if (select(i))
          array0[i] = fn(array1[i], array2[i]);
      }
      return array0;
    }


    /// <summary>
    ///   Create an array from two arrays with the same lengths
    /// </summary>
    /// <param name="length">The number of array entries to count, starting from zero</param>
    /// <param name="count">The array delegate to count</param>
    /// <returns>count an array</returns>
    public static int Count(int length, Counter count)
    {
      int n = 0;
      for (int i = 0; i < length; ++i)
        n += count(i);
      return n;
    }

    /// <summary>
    ///   Create a matrix with all the elements set to a single value
    /// </summary>
    /// <param name="value">Value</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <returns>Created matrix</returns>
    public static T[,] CreateMatrixFromSingleValue<T>(
      T value, int rows, int cols)
    {
      T[,] mat = new T[rows, cols];
      for (int i = 0; i < rows; ++i)
        for (int j = 0; j < cols; ++j)
          mat[i, j] = value;
      return mat;
    }

    /// <summary>
    ///   Calculate the sum of values.
    /// </summary>
    /// <param name="start">Start index.</param>
    /// <param name="stop">Stop index.</param>
    /// <param name="eval">
    ///   Evaluation function returning the value at step <c>i</c>. 
    /// </param>
    /// <returns>Sum of values.</returns>
    public static double Sum(int start, int stop, Func<int, double> eval)
    {
      double sum = 0;
      for (int i = start; i < stop; ++i)
        sum += eval(i);
      return sum;
    }

    /// <summary>
    /// Determines whether the specified array is null or empty.
    /// </summary>
    /// <param name="a">A.</param>
    /// <returns><c>true</c> if the specified array is null or empty; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsNullOrEmpty(this Array a)
    {
      return a == null || a.Length == 0;
    }

    /// <summary>
    ///  Determine if any element of the specified array satisfies the predicate.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a">An array.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static bool Any<T>(this T[,] a, Func<T,bool> predicate)
    {
      int m = a.GetLength(0), n = a.GetLength(1);
      for (int i = 0; i < m; ++i)
        for (int j = 0; j < n; ++j)
          if (predicate(a[i, j])) return true;
      return false;
    }

    /// <summary>
    /// Sets the specified element of the array to the specified value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a">A.</param>
    /// <param name="i">The i.</param>
    /// <param name="j">The j.</param>
    /// <param name="value">The value.</param>
    /// <returns>``0[][].</returns>
    public static T[,] Set<T>(this T[,] a, int i, int j, T value)
    {
      a[i, j] = value;
      return a;
    }

    /// <summary>
    ///  Gets a read-only list of all the elements in the specified row.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The data.</param>
    /// <param name="rowIndex">Index of the row.</param>
    /// <returns>IReadOnlyList{``0}.</returns>
    /// <exception cref="System.IndexOutOfRangeException"></exception>
    public static IReadOnlyList<T> Row<T>(this T[,] data, int rowIndex)
    {
      return GetList(data, 0, rowIndex, false);
    }

    /// <summary>
    ///  Gets a read-only list of all the rows
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The data.</param>
    /// <returns>IReadOnlyList{``0}.</returns>
    /// <returns>IReadOnlyList&lt;IReadOnlyList&lt;T&gt;&gt;.</returns>
    public static IReadOnlyList<IReadOnlyList<T>> ToRows<T>(
      this T[,] data)
    {
      return new DelegateReadOnlyList<IReadOnlyList<T>>(
        data.GetLength(0),
        rowIndex => GetList(data, 0, rowIndex, false));
    }

    /// <summary>
    ///  Gets a read-only list of all the elements in the specified row,
    ///  or an empty list if row index is non-negative and out of range.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The data.</param>
    /// <param name="rowIndex">Index of the row.</param>
    /// <returns>IReadOnlyList{``0}.</returns>
    /// <exception cref="System.IndexOutOfRangeException"></exception>
    /// <remarks>It returns an empty list if row index is non-negative and out of range.</remarks>
    public static IReadOnlyList<T> OptionalRow<T>(this T[,] data, int rowIndex)
    {
      return GetList(data, 0, rowIndex, true);
    }

    /// <summary>
    ///  Gets a read-only list of all the elements in the specified column.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The data.</param>
    /// <param name="colIndex">Index of the column.</param>
    /// <returns>IReadOnlyList{``0}.</returns>
    /// <exception cref="System.IndexOutOfRangeException"></exception>
    public static IReadOnlyList<T> Column<T>(this T[,] data, int colIndex)
    {
      return GetList(data, 1, colIndex, false);
    }

    /// <summary>
    ///  Gets a read-only list of all the elements in the specified column,
    ///  or an empty list if column index is non-negative and out of range.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The data.</param>
    /// <param name="colIndex">Index of the column.</param>
    /// <returns>IReadOnlyList{``0}.</returns>
    /// <exception cref="System.IndexOutOfRangeException"></exception>
    /// <remarks>It returns an empty list if column index is non-negative and out of range.</remarks>
    public static IReadOnlyList<T> OptionalColumn<T>(this T[,] data, int colIndex)
    {
      return GetList(data, 1, colIndex, true);
    }

    private static IReadOnlyList<T> GetList<T>(T[,] data,
      int dim, int index, bool optional)
    {
      if (data == null) return EmptyArray<T>.Instance;

      int m = data.GetLength(dim);
      if (optional && index >= m) return EmptyArray<T>.Instance;


      if (index < 0 || index >= m)
      {
        throw new IndexOutOfRangeException(String.Format(
          "{0} index {1} out of the range [0..{2}).",
          dim == 0 ? "Row" : "Column", index, m));
      }

      return dim == 0
        ? new DelegateReadOnlyList<T>(data.GetLength(1), j => data[index, j])
        : new DelegateReadOnlyList<T>(data.GetLength(0), i => data[i, index]);
    }

    /// <summary>
    /// Convert the nested lists to a 2-dimensional array with the specified size.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <returns>``0[][].</returns>
    public static T[,] ToArray2D<T>(this IEnumerable<IEnumerable<T>> data, int rows, int columns)
    {
      var res = new T[rows, columns];
      int i = -1;
      foreach (var row in data)
      {
        if (++i >= rows) break;
        int j = -1;
        foreach (var v in row)
        {
          if (++j >= columns) break;
          res[i, j] = v;
        }
      }
      return res;
    }

    /// <summary>
    /// Convert the nested lists to a 2-dimensional array with the specified size.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <returns>``0[][].</returns>
    public static T[,] ToArray2DTransposed<T>(this IEnumerable<IEnumerable<T>> data, int rows, int columns)
    {
      var res = new T[rows, columns];
      int i = -1;
      foreach (var col in data)
      {
        if (++i >= columns) break;
        int j = -1;
        foreach (var v in col)
        {
          if (++j >= rows) break;
          res[j, i] = v;
        }
      }
      return res;
    }

    /// <summary>
    /// Transposes the specified data.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <returns>``0[][].</returns>
    public static T[,] Transpose<T>(this T[,] data)
    {
      if (data == null) return data;
      int m = data.GetLength(1), n = data.GetLength(0);
      var dst = new T[m, n];
      for (int i = 0; i < m; ++i)
        for (int j = 0; j < n; ++j)
          dst[i, j] = data[j, i];
      return dst;
    }

    /// <summary>
    ///  Extension method for one dimensional array to convert into a 2D array as a single row.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <returns></returns>
    public static T[,] ToRow<T>(this IEnumerable<T> target)
    {
      var array = target.ToArray();
      var output = new T[1, array.Length];
      foreach (var i in Enumerable.Range(0, array.Length))
      {
        output[0, i] = array[i];
      }
      return output;
    }

    /// <summary>
    ///  Extension method for one dimensional array to convert into a 2D array as a single column.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <returns></returns>
    public static T[,] ToColumn<T>(this IEnumerable<T> target)
    {
      var array = target.ToArray();
      var output = new T[array.Length, 1];
      foreach (var i in Enumerable.Range(0, array.Length))
      {
        output[i, 0] = array[i];
      }
      return output;
    }

    #region RemoveEmptyItems

    /// <summary>
    /// Remove empty objects from matching arrays of objects.
    /// </summary>
    /// <param name="objects">Array of objects</param>
    /// <typeparam name="T">Type of objects in array</typeparam>
    /// <exception cref="Exception"></exception>
    public static T[] RemoveEmptyItems<T>(this T[] objects)
    {
      var res = RemoveEmptyItems<T, object, object, object>(objects, null, null, null, false);
      return res.Item1;
    }

    /// <summary>
    /// Remove empty objects from matching arrays of objects.
    /// </summary>
    /// <param name="objects1">First array of objects</param>
    /// <param name="objects2">Second array of objects</param>
    /// <param name="testBoth">If true, exclude tuples where all objects are null, otherwise exclude pairs
    /// where object1 is null (and throw error if other objects are not null)</param>
    /// <typeparam name="T1">Type of first array</typeparam>
    /// <typeparam name="T2">Type of second array</typeparam>
    /// <returns>Tuple containing filtered objects</returns>
    /// <exception cref="Exception"></exception>
    public static Tuple<T1[], T2[]> RemoveEmptyItems<T1, T2>(this T1[] objects1, T2[] objects2, bool testBoth = true)
    {
      var res = RemoveEmptyItems<T1, T2, object, object>(objects1, objects2, null, null, testBoth);
      return new Tuple<T1[], T2[]>(res.Item1, res.Item2);
    }

    /// <summary>
    /// Remove empty objects from matching arrays of objects.
    /// </summary>
    /// <param name="objects1">First array of objects</param>
    /// <param name="objects2">Second array of objects</param>
    /// <param name="objects3">Third array of objects</param>
    /// <param name="testBoth">If true, exclude tuples where all objects are null, otherwise exclude pairs
    /// where object1 is null (and throw error if other objects are not null)</param>
    /// <typeparam name="T1">Type of first array</typeparam>
    /// <typeparam name="T2">Type of second array</typeparam>
    /// <typeparam name="T3">Type of third array</typeparam>
    /// <returns>Tuple containing filtered objects</returns>
    /// <exception cref="Exception"></exception>
    public static Tuple<T1[], T2[], T3[]> RemoveEmptyItems<T1, T2, T3>(
      this T1[] objects1, T2[] objects2, T3[] objects3, bool testBoth = true
      )
    {
      var res = RemoveEmptyItems<T1, T2, T3, object>(objects1, objects2, objects3, null, testBoth);
      return new Tuple<T1[], T2[], T3[]>(res.Item1, res.Item2, res.Item3);
    }

    /// <summary>
    /// Remove empty objects from matching arrays of objects.
    /// </summary>
    /// <remarks>
    /// <para>If object1 is null, returns tuple of nulls.</para>
    /// </remarks>
    /// <param name="objects1">First array of objects</param>
    /// <param name="objects2">Second array of objects</param>
    /// <param name="objects3">Third array of objects</param>
    /// <param name="objects4">Fourth array of objects</param>
    /// <param name="testBoth">If true, exclude tuples where all objects are null, otherwise exclude pairs
    /// where object1 is null (and throw error if other objects are not null)</param>
    /// <typeparam name="T1">Type of first array</typeparam>
    /// <typeparam name="T2">Type of second array</typeparam>
    /// <typeparam name="T3">Type of third array</typeparam>
    /// <typeparam name="T4">Type of fourth array</typeparam>
    /// <returns>Tuple containing filtered objects</returns>
    /// <exception cref="Exception"></exception>
    public static Tuple<T1[], T2[], T3[], T4[]> RemoveEmptyItems<T1, T2, T3, T4>(
      this T1[] objects1, T2[] objects2, T3[] objects3, T4[] objects4, bool testBoth = true
      )
    {
      if (objects1 == null)
        return new Tuple<T1[], T2[], T3[], T4[]>(null, null, null, null);

      // Verify objects are all same size if they are not null
      if ((objects2 != null && objects1.Length != objects2.Length) ||
          (objects3 != null && objects1.Length != objects3.Length) ||
          (objects4 != null && objects1.Length != objects4.Length))
        throw new Exception("Expected matching items to have the same length");

      // Count non-empty pairs
      var count = objects1.Where((t, i) => !IsEmpty(t) &&
        !(testBoth && (objects2 == null || IsEmpty(objects2[i])) &&
        (objects3 == null || IsEmpty(objects3[i])) &&
        (objects4 == null || IsEmpty(objects4[i])))).Count();
      // Warn if values exist where first object is null
      if (!testBoth)
      {
        if (objects1.Where((t, i) => IsEmpty(t) &&
                                     !((objects2 == null || IsEmpty(objects2[i])) &&
                                       (objects3 == null || IsEmpty(objects3[i])) &&
                                       (objects4 == null || IsEmpty(objects4[i])))).Any())
          throw new Exception("First item is empty but other items are not empty");
      }

      var objs1 = new T1[count];
      var objs2 = new T2[count];
      var objs3 = new T3[count];
      var objs4 = new T4[count];
      for (int i = 0, idx = 0; i < objects1.Length; i++)
        if (!IsEmpty(objects1[i]) &&
            !(testBoth &&
              (objects2 == null || IsEmpty(objects2[i])) &&
              (objects3 == null || IsEmpty(objects3[i])) &&
              (objects4 == null || IsEmpty(objects4[i]))))
        {
          objs1.SetValue(objects1[i], idx);
          if (objects2 != null) objs2.SetValue(objects2[i], idx);
          if (objects3 != null) objs3.SetValue(objects3[i], idx);
          if (objects4 != null) objs4.SetValue(objects4[i], idx);
          idx++;
        }
      return new Tuple<T1[], T2[], T3[], T4[]>(objs1, objs2, objs3, objs4);
    }

    /// <summary>
    /// Return true if object is empty in some sense.
    /// </summary>
    private static bool IsEmpty(Object obj)
    {
      return (obj == null ||
        (obj is Double && obj.Equals(0.0)) ||
        (obj is String && String.IsNullOrWhiteSpace((string)obj))
        /*|| (obj is Dt && (Dt)obj == Dt.Empty)*/);
    }

    #endregion RemoveEmptyItems
  }

  #region Type DelegateReadOnlyList<T>

  class DelegateReadOnlyList<T> : IReadOnlyList<T>
  {
    private readonly int _count;
    private readonly Func<int, T> _getter;

    public DelegateReadOnlyList(int count, Func<int, T> getter)
    {
      _count = count;
      _getter = getter;
    }

    public T this[int index]
    {
      get { return _getter(index); }
    }

    public int Count
    {
      get { return _count; }
    }

    public IEnumerator<T> GetEnumerator()
    {
      var fn = _getter;
      for (int i = 0, n = _count; i < n; ++i)
        yield return fn(i);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  #endregion
}
