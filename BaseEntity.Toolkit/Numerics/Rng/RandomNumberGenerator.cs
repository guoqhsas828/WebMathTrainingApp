/*
 * RandomNumberGenerator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  ///
  /// <summary>
  ///   Pseudo-random number generator
  /// </summary>
  ///
  /// <remarks>
  ///   This is a class to generate deviates with various univariate distributions,
  ///   including uniform, normal, exponential, gamma and beta distributions.
  /// </remarks>
  ///
  [Serializable]
  public class RandomNumberGenerator : BaseEntityObject
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (RandomNumberGenerator));

    #region Constructors

    /// <summary>
    ///   Constructor with core generator
    /// </summary>
    ///
    /// <remarks>
    ///   For internal use only
    /// </remarks>
    private RandomNumberGenerator(CoreGenerator core)
    {
      if (null == core) coreRng_ = new CoreGenerator();
      else coreRng_ = core;
    }

    /// <summary>
    ///   Default constructor
    /// </summary>
    public RandomNumberGenerator()
    {
      coreRng_ = new CoreGenerator();
    }

    /// <summary>
    ///   Constructor with a shared core generator
    /// </summary>
    /// <remarks>
    ///   If the input generator <c>rng</c> is null,
    ///   a new generator is constructed, otherwise,
    ///   the core generator in <c>rng</c> is shared.
    /// </remarks>
    /// <param name="rng">random number gnerator to share</param>
    public RandomNumberGenerator(RandomNumberGenerator rng)
    {
      if (null == rng) coreRng_ = new CoreGenerator();
      else coreRng_ = rng.coreRng_;
    }

    /// <summary>
    ///   Constructor with a given seed
    /// </summary>
    public RandomNumberGenerator(uint seed)
    {
      coreRng_ = new CoreGenerator(seed);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      RandomNumberGenerator obj = (RandomNumberGenerator) base.Clone();
      obj.coreRng_ = new CoreGenerator(coreRng_);
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///    Generate a variate with uniform distribution in [a, b]
    /// </summary>
    /// <param name="a">lower-bound on support of the distribution</param>
    /// <param name="b">upper-bound on support of the distribution</param>
    public double Uniform(double a, double b)
    {
      return (b - a)*coreRng_.StdUniform() + a;
    }

    /// <summary>
    ///   Generate a variate with normal distribution
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The normal density function is defined by:
    ///   <formula>
    ///   p(x) = \displaystyle{\frac{1}{\sigma\sqrt{2\pi}}e^{-\frac{(x-\mu)^2}{2\sigma^2}}}
    ///   </formula>
    ///   where <formula inline="true">\mu</formula> is the mean and <formula inline="true">\sigma^2</formula>
    ///   is the variance.</para>
    ///   <para>
    ///   The skewness and kurtosis are 0 and 3, respectively    
    ///   </para>
    /// </remarks>
    ///
    /// <param name="mu">mean of the distribution</param>
    /// <param name="sigma">standard deviation of distribution</param>
    public double Normal(double mu, double sigma)
    {
      return coreRng_.StdNormal()*sigma + mu;
    }

    /// <summary>
    ///   Generate a variate with exponential distribution
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The exponential density function is defined by:
    ///   <formula inline="true">
    ///   p(x) = \lambda e^{-\lambda x}
    ///   \qquad x \gt 0, \lambda \gt 0.</formula>
    ///   </para>
    ///   <para>
    ///   The mean, variance, skewness and kurtosis are
    ///   <formula>
    ///     \mu = \frac{1}{\lambda},\quad
    ///     \sigma^2 = \frac{1}{\lambda^2}, \quad
    ///     \gamma_1 = 2, \quad
    ///     \gamma_2 = 6
    ///   </formula>
    ///   </para>
    /// </remarks>
    /// <param name="lambda">the intensity of the distribution</param>
    public double Exponential(double lambda)
    {
      if (0 >= lambda) throw new ArgumentOutOfRangeException(String.Format("lambda ({0}) must be positive", lambda));
      return -1.0*Math.Log(coreRng_.StdUniform())/lambda;
    }

    /// <summary>
    ///   Generate a variate with gamma distribution
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The gamma density function is defined by:
    ///   <formula>
    ///   p(x) = \displaystyle{\frac{x^{a-1} e^{-x/b}}{\Gamma(a) b^a}}
    ///   \qquad x \gt 0, a \gt 0
    ///   </formula>
    ///   where <c>a</c> is the order parameter and <c>b</c> is the scale parameter.
    ///   If <c>X</c> and <c>Y</c> are independent gamma-distributed random
    ///   variables of order <c>a1</c> and <c>a2</c> with the same scale parameter <c>b</c>, then
    ///   <c>X + Y</c> has gamma distribution of order <c>a1 + a2</c>.</para>
    ///
    ///   <para>
    ///   The mean, variance, skewness and kurtosis are
    ///   <formula>
    ///     \mu = a b,\quad
    ///     \sigma^2 = a b^2, \quad
    ///     \gamma_1 = \frac{2}{\sqrt{a}}, \quad
    ///     \gamma_2 = \frac{4}{a}
    ///   </formula>
    ///   </para>
    /// </remarks>
    /// <param name="a">the order parameter of the distribution</param>
    /// <param name="b">the scale parameter of the distribution</param>
    public double Gamma(double a, double b)
    {
      if (0 >= a) throw new ArgumentOutOfRangeException(String.Format("a ({0}) must be positive", a));
      return coreRng_.Gamma(a, b);
    }

    /// <summary>
    ///   Generate a variate with beta distribution
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The beta density function is defined by:
    ///   <formula>
    ///   p(x) = \frac{\Gamma(a + b)}{\Gamma(a)\Gamma(b)} x^{a-1} (1-x)^{b-1}
    ///   \qquad 0 \lt x \lt 1
    ///   </formula></para>
    ///
    ///   <para>
    ///   The mean, variance, skewness and kurtosis are
    ///   <formula>
    ///     \mu = \frac{a}{a + b}
    ///   </formula>
    ///   <formula>
    ///     \sigma^2 = \frac{a b}{(a+b)^2(a+b+1)}
    ///   </formula>
    ///   <formula>
    ///     \gamma_1 = \frac{2(b-a)\sqrt{a+b+1}}{\sqrt{a b}(2+a+b)}
    ///   </formula>
    ///   <formula>
    ///     \gamma_2 = \frac{6[a^3 + a^2(1-2b) + b^2(1+b) - 2ab(2+b)]}{ab(a+b+2)(a+b+3)}
    ///   </formula>
    ///   The mode of the distribution is given by
    ///   <formula>
    ///     \hat{x} = \frac{a - 1} {a + b - 2}
    ///   </formula>
    ///   </para>
    /// </remarks>
    /// <param name="a">the first parameter of the distribution</param>
    /// <param name="b">the second parameter of the distribution</param>
    public double Beta(double a, double b)
    {
      if (0 >= a) throw new ArgumentOutOfRangeException(String.Format("a ({0}) must be positive", a));
      return coreRng_.Beta(a, b);
    }

    /// <summary>Generate a variate with standard uniform distribution</summary>
    /// <exclude />
    protected double StdUniform()
    {
      return coreRng_.StdUniform();
    }

    /// <summary>Generate a variate with standard normal distribution</summary>
    /// <exclude />
    protected double StdNormal()
    {
      return coreRng_.StdNormal();
    }

    /// <summary>Generate a variates with standard exponential distribution</summary>
    /// <exclude />
    protected double StdExponential()
    {
      return coreRng_.StdExponential();
    }

    /// <summary>Generate a variates with chi squared distribution</summary>
    /// <exclude />
    protected double ChiSquared(int df)
    {
      double chi2 = 0;
      for (int i = 0; i < df; ++i)
      {
        double f = coreRng_.StdNormal();
        chi2 += f*f;
      }
      return chi2;
    }

    /// <summary>Generate an array of variates with standard uniform distribution</summary>
    /// <param name="x">Array to received generated random numbers</param>
    /// <exclude />
    protected void StdUniform(double[] x)
    {
      coreRng_.StdUniform(x);
    }

    /// <summary>Generate an array of variates with standard normal distribution</summary>
    /// <param name="x">Array to received generated random numbers</param>
    /// <exclude />
    protected void StdNormal(double[] x)
    {
      coreRng_.StdNormal(x);
    }

    /// <summary>Generate an array of variates with standard exponential distribution</summary>
    /// <param name="x">Array to received generated random numbers</param>
    /// <exclude />
    protected void StdExponential(double[] x)
    {
      coreRng_.StdExponential(x);
    }

    /// <summary>Get the real underlying CoreGenerator</summary>
    /// <remarks>These are mainly used for interaction with C++ codes.
    ///  They should not be visible to the users</remarks>
    /// <exclude />
    protected internal CoreGenerator Internal_GetCoreGenerator()
    {
      return coreRng_;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   The core generator
    /// </summary>
    public RandomNumberGenerator CoreGenerator
    {
      get { return new RandomNumberGenerator(coreRng_); }
      set
      {
        if (null == value) throw new ArgumentOutOfRangeException(String.Format("null core generator"));
        coreRng_ = value.coreRng_;
      }
    }

    /// <summary>
    ///   Seed of random number generator
    /// </summary>
    public uint Seed { get { return coreRng_.Seed; } set { coreRng_.Seed = value; } }

    /// <summary>
    ///   Random seed
    /// </summary>
    /// <remarks>
    ///    The value is garanteed to be different everytime it is called.
    /// </remarks>
    public static uint RandomSeed
    {
      get
      {
        DateTime y2k = new DateTime(2000, 1, 1);
        DateTime now = DateTime.Now;
        uint seed = (uint) (now.Ticks - y2k.Ticks);
        return seed;
      }
    }

    #endregion Properties

    #region Data

    private CoreGenerator coreRng_;

    #endregion Data
  }
}
