/*
 * Copyright (c)    2002-2014. All rights reserved.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Base.Serialization
{
  class ArrayIndexEnumerator : IEnumerator<int[]>, IEnumerable<int[]>
  {
    private readonly int[] _idx;
    private readonly int[] _dim;

    public ArrayIndexEnumerator(int[] dim)
    {
      _dim = dim;
      _idx = new int[dim.Length];
      _idx[_idx.Length - 1] = -1;
    }

    public int[] Current
    {
      get { return _idx; }
    }

    public void Dispose()
    {
    }

    object IEnumerator.Current
    {
      get { return Current; }
    }

    public bool MoveNext()
    {
      int[] idx = _idx, dim = _dim;
      for (int i = idx.Length - 1; i >= 0; --i)
      {
        if (++idx[i] < dim[i]) return true;
        idx[i] = 0;
      }
      return false;
    }

    public void Reset()
    {
      var idx = _idx;
      for (int i = 0, n = idx.Length; i < n; ++i)
        idx[i] = 0;
      idx[idx.Length - 1] = -1;
    }

    public IEnumerator<int[]> GetEnumerator()
    {
      return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return this;
    }
  }
}
