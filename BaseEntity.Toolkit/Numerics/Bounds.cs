
namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///  Represents the lower and upper bounds
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public struct Bounds<T>
  {
    private readonly T _lower, _upper;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bounds{T}"/> struct.
    /// </summary>
    /// <param name="lower">The lower.</param>
    /// <param name="upper">The upper.</param>
    public Bounds(T lower, T upper)
    {
      _lower = lower;
      _upper = upper;
    }
    /// <summary>
    /// Gets the lower bound.
    /// </summary>
    /// <value>The lower bound.</value>
    public T Lower { get { return _lower; } }
    /// <summary>
    /// Gets the upper bound.
    /// </summary>
    /// <value>The upper bound.</value>
    public T Upper { get { return _upper; } }
  }
}
