// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{

  [TestFixture]
  public class VolatilityBumpTests
  {
    [Test]
    public static void BumpResult()
    {
      BumpResult empty = 0.0;
      Assert.IsTrue(empty.IsEmpty);
      Assert.AreEqual(0.0, empty.Amount);

      BumpResult nonEmpty = 1.2;
      Assert.IsFalse(nonEmpty.IsEmpty);
      Assert.AreEqual(1.2, nonEmpty.Amount);

      BumpResult defered = new BumpAccumulator();
      Assert.IsFalse(defered.IsEmpty);
      Assert.AreEqual(0.0, defered.Amount);
      ((BumpAccumulator)defered).Add(1);
      ((BumpAccumulator)defered).Add(2);
      Assert.AreEqual(1.5, defered.Amount);
    }
  }
}
