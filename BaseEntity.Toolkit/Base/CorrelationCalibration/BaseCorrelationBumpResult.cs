/*
 * BaseCorrelationBumpResult.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
	internal class BaseCorrelationBumpResult
	{
    internal void AddBump(double bump)
    {
      avg_ += (bump - avg_) / (++count_);
    }
    internal void AddTenor(int index)
    {
      bumpedTenorIndices_.Add(index);
    }
    internal void AddDetachment(int index)
    {
      bumpedDpIndices_.Add(index);
    }

    internal double Average
    {
      get { return avg_; }
    }
    internal int Count
    {
      get { return count_; }
    }
    internal int FromTenorIndex
    {
      get
      {
        return bumpedTenorIndices_.Count > 0
          ? bumpedTenorIndices_[0] : Int32.MaxValue;
      }
    }
    internal int FromDpIndex
    {
      get
      {
        return bumpedDpIndices_.Count > 0
          ? bumpedDpIndices_[0] : Int32.MaxValue;
      }
    }
    internal int[] BumpedTenorIndices
    {
      get { return bumpedTenorIndices_.ToArray(); }
    }
    internal int[] BumpDpIndices
    {
      get { return bumpedDpIndices_.ToArray(); }
    }
    internal bool IsIndexBumped
    {
      get { return indexBumped_; }
      set { indexBumped_ = value; }
    }

    private double avg_ = 0;
    private int count_ = 0;
    private bool indexBumped_ = false;
    private UniqueSequence<int> bumpedTenorIndices_ = new UniqueSequence<int>();
    private UniqueSequence<int> bumpedDpIndices_ = new UniqueSequence<int>();
  }
}
