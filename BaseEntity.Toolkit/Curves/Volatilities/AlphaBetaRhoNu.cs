using System;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///  Represents the SABR parameters <c>alpha (or zeta)</c>, <c>beta</c>, <c>rho</c> and <c>nu</c>.
  /// </summary>
  [Serializable]
  public struct AlphaBetaRhoNu
  {
    private readonly double[] _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaBetaRhoNu"/> struct.
    /// </summary>
    /// <param name="alpha">The alpha (or zeta).</param>
    /// <param name="beta">The beta.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="nu">The nu.</param>
    public AlphaBetaRhoNu(double alpha, double beta, double rho, double nu)
    {
      _data = new[] {alpha, beta, rho, nu};
    }

    /// <summary>
    /// Gets a value indicating whether this instance has value.
    /// </summary>
    /// <value><c>true</c> if this instance has value; otherwise, <c>false</c>.</value>
    public bool HasValue
    {
      get { return _data != null; }
    }

    /// <summary>
    /// Gets the alpha.
    /// </summary>
    /// <value>The alpha.</value>
    public double Alpha
    {
      get { return _data == null ? Double.NaN : _data[0]; }
    }

    /// <summary>
    /// Gets the beta.
    /// </summary>
    /// <value>The beta.</value>
    public double Beta
    {
      get { return _data == null ? Double.NaN : _data[1]; }
    }

    /// <summary>
    /// Gets the rho.
    /// </summary>
    /// <value>The rho.</value>
    public double Rho
    {
      get { return _data == null ? Double.NaN : _data[2]; }
    }

    /// <summary>
    /// Gets the nu.
    /// </summary>
    /// <value>The nu.</value>
    public double Nu
    {
      get { return _data == null ? Double.NaN : _data[3]; }
    }
  }
}
