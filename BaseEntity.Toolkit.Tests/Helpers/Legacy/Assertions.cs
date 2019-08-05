using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace BaseEntity.Toolkit.Tests.Helpers.Legacy
{
  public class Assertions
  {
    public static void AssertEqual<T>(string label, T expect, T actual)
    {
      Assert.AreEqual(expect, actual, label);
    }

    public static void AssertEqual(string label, double expect, double actual, double epsilon)
    {
      Assert.AreEqual(expect, actual, epsilon, label);
    }

    internal static void AssertMatch(string label, object expect, object actual)
    {
      Assert.That(actual, To.Match(expect), label);
    }

    internal static void AssertDontMatch(string label, object expect, object actual)
    {
      Assert.That(actual, Is.Not.EqualTo(expect), label);
    }
  }
}
