namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Type of bumping algorithm to apply.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Specifies the way sensitivity bumping is applied and how and when
  ///   recalculation happens.</para>
  ///
  ///   <para>Note that all bumping types may not be valid in all situations.</para>
  /// </remarks>
  ///
  public enum BumpType
  {
    /// <summary>Bump all tenors of a particular curve or name simultaneously</summary>
    ///
    /// <remarks>
    ///   All points for a particular curve or name are bumped uniformly (in parallel)
    ///   then recalculation takes place.
    /// </remarks>
    ///
    Parallel,

    /// <summary>Bump each tenor of each curve or name individually</summary>
    ///
    /// <remarks>
    ///   All points are bumped individually and recalculation takes place.
    /// </remarks>
    ///
    ByTenor,

    /// <summary>Bump all curves of a particular category simultaneously</summary>
    ///
    /// <remarks>
    ///   All points are bumped uniformly within a particular category and then
    ///   recalculation takes place.
    /// </remarks>
    ///
    [System.ComponentModel.Browsable(false)]
    ByCategory,

    /// <summary>Bump all points simultaneously</summary>
    ///
    /// <remarks>
    ///   All points are bumped uniformly and then recalculation takes place.
    /// </remarks>
    ///
    Uniform
  }

}
