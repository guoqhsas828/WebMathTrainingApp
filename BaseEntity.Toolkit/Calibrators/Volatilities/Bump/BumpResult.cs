/*
 * 
 */
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.Bump
{
  /// <summary>
  ///  Represent the bump result, with the properties
  ///   <c>IsEmpty</c> to indicate nothing bumped, and
  ///   <c>Amount</c> to get the actual bump amount (averaged).
  /// </summary>
  public class BumpResult
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BumpResult"/> class.
    /// </summary>
    /// <param name="amount">The amount.</param>
    public BumpResult(double amount)
    {
      Value = amount;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="System.Double"/> to <see cref="BumpResult"/>.
    /// </summary>
    /// <param name="a">a.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator BumpResult(double a)
    {
      return new BumpResult(a);
    }

    /// <summary>
    /// Gets the actual bump amount
    /// </summary>
    /// <value>The amount</value>
    public virtual double Amount => Value;

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value><c>true</c> if nothing is bumped; otherwise, <c>false</c>.</value>
    public virtual bool IsEmpty => Amount.AlmostEquals(0.0);

    /// <summary>
    ///   The bump amount
    /// </summary>
    internal double Value;
  }

  /// <summary>
  ///  Represent the deferred bump result, for the case where
  ///  the actual bump amount is not available until the repricing
  ///  is completed.
  /// </summary>
  /// <seealso cref="BumpResult" />
  public sealed class BumpAccumulator : BumpResult
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BumpAccumulator"/> class.
    /// </summary>
    public BumpAccumulator():base(0.0)
    {
      _count = 0;
    }

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value><c>true</c> if it is sure that nothing is bumped; otherwise, <c>false</c>.</value>
    public override bool IsEmpty => false;

    /// <summary>
    /// Gets the actual bump amount.
    /// </summary>
    /// <value>The amount.</value>
    public override double Amount => _count > 0 ? (Value/_count) : 0;

    /// <summary>
    /// Adds the specified value to the accumulator.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Add(double value)
    {
      Value += value;
      ++_count;
    }

    /// <summary>
    /// The bump count
    /// </summary>
    private int _count;
  }
}
