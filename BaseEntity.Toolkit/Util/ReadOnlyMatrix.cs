using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Util
{
  /// <inheritdoc />
  /// <summary>
  ///  Matrix view of data
  /// </summary>
  public interface IReadOnlyMatrix<out T> : IReadOnlyList<T>
  {
    /// <summary>
    /// Gets the length in the specified dimension
    /// </summary>
    /// <param name="dimension">0 for row, 1 for columns</param>
    /// <returns></returns>
    int GetLength(int dimension);

    /// <summary>
    /// Gets the element at the specified row/column position.
    /// </summary>
    /// <param name="rowIndex">The row index</param>
    /// <param name="columnIndex">The column index</param>
    /// <returns>The element</returns>
    T this[int rowIndex, int columnIndex] { get; }
  }

  /// <summary>
  ///  Extension methods to create matrix view of data
  /// </summary>
  internal static class ReadOnlyMatrix
  {
    /// <summary>
    ///  Create a matrix view of the specified contiguous memory block.
    /// </summary>
    /// <param name="pointer">Pointer to the memory block</param>
    /// <param name="rowCount">The number of rows</param>
    /// <param name="columnCount">The number of columns</param>
    /// <typeparam name="T">The element type</typeparam>
    /// <returns>A read only matrix view</returns>
    public static IReadOnlyMatrix<T> Create<T>(
      IntPtr pointer, int rowCount, int columnCount)
    where T: struct
    {
      return new MatrixViewFromPointer<T>(pointer, rowCount, columnCount);
    }

    /// <summary>
    ///  Create a matrix view of the specified list.
    /// </summary>
    /// <param name="data">The list</param>
    /// <param name="rowCount">The number of rows</param>
    /// <param name="columnCount">The number of columns</param>
    /// <typeparam name="T">The element type</typeparam>
    /// <returns>A read only matrix view</returns>
    public static IReadOnlyMatrix<T> ToMatrix<T>(
      this IReadOnlyList<T> data, int rowCount, int columnCount)
    {
      return new MatrixViewOfList<T>(data, rowCount, columnCount);
    }

    /// <summary>
    ///  Create a matrix view of the specified 2-dimensional array.
    /// </summary>
    /// <param name="data">The array</param>
    /// <typeparam name="T">The element type</typeparam>
    /// <returns>A read only matrix view</returns>
    public static IReadOnlyMatrix<T> ToMatrix<T>(this T[,] data)
    {
      return new MatrixViewOfArray2D<T>(data);
    }

    #region Nested private types

    private class MatrixViewOfList<T> : IReadOnlyMatrix<T>
    {
      private readonly int _nrow, _ncol;
      private readonly IReadOnlyList<T> _data;

      public MatrixViewOfList(IReadOnlyList<T> data,
        int rowCount, int columnCount)
      {
        Debug.Assert(rowCount*columnCount == data.Count);
        _nrow = rowCount;
        _ncol = columnCount;
        _data = data;
      }

      public IEnumerator<T> GetEnumerator() =>
        (_data ?? Enumerable.Empty<T>()).GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }

      public int Count => _data?.Count ?? 0;

      public T this[int index] => _data[index];

      public int GetLength(int dimension)
      {
        if (dimension < 0 || dimension > 1)
          throw new IndexOutOfRangeException("Dimension must be 0 or 1");
        return dimension == 0 ? _nrow : _ncol;
      }

      public T this[int row, int col] => _data[row*_ncol + col];
    }

    private class MatrixViewOfArray2D<T> : IReadOnlyMatrix<T>
    {
      private readonly T[,] _data;

      public MatrixViewOfArray2D(T[,] data)
      {
        _data = data;
      }

      public IEnumerator<T> GetEnumerator()
      {
        var d = _data;
        if (d != null)
        {
          for (int i = 0, m = d.GetLength(0); i < m; ++i)
          for (int j = 0, n = d.GetLength(0); j < n; ++j)
            yield return d[i, j];
        }
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }

      public int Count => _data?.Length ?? 0;

      public T this[int index]
      {
        get
        {
          var cols = GetLength(1);
          if (cols <= 0)
          {
            throw new IndexOutOfRangeException("Matrix is empty");
          }

          return _data[index/cols, index%cols];
        }
      }

      public int GetLength(int dimension)
        => _data?.GetLength(dimension) ?? 0;

      public T this[int row, int col] => _data[row, col];
    }

    private class MatrixViewFromPointer<T> : IReadOnlyMatrix<T>
    {
      private readonly int _nrow, _ncol;
      private readonly IntPtr _data;

      public MatrixViewFromPointer(IntPtr data, int rowCount, int columnCount)
      {
        _nrow = rowCount;
        _ncol = columnCount;
        _data = data;
      }

      public IEnumerator<T> GetEnumerator()
      {
        var p = _data;
        if (p != IntPtr.Zero)
        {
          var stepSize = Marshal.SizeOf<T>();
          for (int i = 0, n = Count; i < n; ++i)
          {
            yield return Marshal.PtrToStructure<T>(p);
            p = IntPtr.Add(p, stepSize);
          }
        }
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }

      public int Count => _ncol*_nrow;

      public T this[int index]
      {
        get
        {
          if (index < 0 || index >= Count)
          {
            throw new IndexOutOfRangeException();
          }

          var p = _data;
          if (p == IntPtr.Zero)
          {
            throw new NullReferenceException();
          }

          p = IntPtr.Add(p, index*Marshal.SizeOf<T>());
          return Marshal.PtrToStructure<T>(p);
        }
      }

      public int GetLength(int dimension)
      {
        if (dimension == 1) return _ncol;
        if (dimension == 0) return _nrow;
        throw new IndexOutOfRangeException();
      }

      public T this[int row, int col] => this[row*_ncol + col];
    }

    #endregion
  }

}
