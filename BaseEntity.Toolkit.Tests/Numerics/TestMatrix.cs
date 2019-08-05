//
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  [TestFixture]
  public class TestMatrix
  {
    [Test, Smoke]
    public void TestConstructFrom1dArray()
    {
      int m = 4;
      int n = 3;
      double[] a = new double[]{ 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0 };
      MatrixOfDoubles A = new MatrixOfDoubles(m, n, a);
      Assert.AreEqual( 9.0, A.at(2, 2) );
    }

    [Test, Smoke]
    public void TestConstructFrom2dArray()
    {
      double[,] a = new double[,]{ {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, {7.0, 8.0, 9.0}, {10.0, 11.0, 12.0} };
      MatrixOfDoubles A = new MatrixOfDoubles(a);
      Assert.AreEqual( 9.0, A.at(2, 2) );
    }

    [Test, Smoke]
    public void TestTranspose()
    {
      double[,] a = new double[,]{ {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, {7.0, 8.0, 9.0}, {10.0, 11.0, 12.0} };
      MatrixOfDoubles A = new MatrixOfDoubles(a);
      MatrixOfDoubles B = LinearAlgebra.Transpose(A);
      Assert.AreEqual( A.dim1(), B.dim2() );
      Assert.AreEqual( A.dim2(), B.dim1() );
    }
  }
}


