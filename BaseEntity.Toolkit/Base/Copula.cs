/*
 * Copula.cs
 *
 * A class defines copula objects
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   Class representing choice of Copula
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Copula functions are numerical methods that provide an efficient
  ///   way of combining several single-name probability distributions into
  ///   a joint probability distribution.</para>
  ///
  ///   <para>There are many different types of copulas with different
  ///   parameterizations varying from one single parameter to serveral parameters
  ///   for each single-name distribution.</para>
  ///
  ///   <para>Common copulas in finance include Gaussian, Student-t,
  ///   Double-t, Marshall-Olkin, Clayton, Frank and Gumbel. Each of these
  ///   has different characteristics in terms of dealing with characteristics
  ///   of the individual distributions such as skewness or fat tails (kurtosis).
  ///   They also have different tradeoffs in terms of analytical tractability.
  ///    Toolkit has implemented many of them.  Here is a brief list. </para>
  ///   <para></para>
  ///
  ///   <list type="number">
  ///   <item><term><b>Factor Copulae</b></term><description>
  ///
  ///   <para>Let <formula inline="true">Q_i(t)</formula> be the cumulative risk-neutral probability
  ///   that name i will default before time t.
  ///   In factor copula models the probability
  ///   <formula inline="true">Q_i(t)</formula> is mapped in a percentile-to-percentile manner
  ///   to the cumulative distribution function <formula inline="true">F_i(x)</formula>
  ///   of another random variable <formula inline="true">x_i</formula>, such that
  ///   <formula inline="true">Q_i(t) = F_i(x)</formula>.  The random variable
  ///   <formula inline="true">x_i</formula> is defined as</para>
  ///
  ///   <formula>
  ///     x_i = a_{i 1} Y_1 + a_{i 2} Y_2 + \ldots + a_{i m} Y_m
  ///      + Z_i \sqrt{1 - a_{i 1}^2 - \ldots - a_{i m}^2}
  ///   </formula>
  ///
  ///   <para>where <formula inline="true">Y_1, \ldots, Y_m, Z_i</formula> are uncorrelated random variables with
  ///   zero-mean, unit-variance distributions.  The equation defines a correlation
  ///   structure among the <formula inline="true">x_i</formula>'s, which depends on the m
  ///   common factors <formula inline="true">Y_1, \ldots, Y_m</formula>.  The correlation
  ///   between <formula inline="true">x_i</formula> and <formula inline="true">x_j</formula>
  ///   is</para>
  ///
  ///   <formula>
  ///     \mathrm{corr}(x_i,x_j) = a_{i 1} a_{j 1} + a_{i 2} a_{j 2} + \ldots
  ///       + a_{i m} a_{j m}
  ///   </formula>
  ///
  ///   <para>This function implements factor copulae for
  ///   <formula inline="true">m = 1, 2, 3</formula> and for three different distributions.</para>
  ///   <list type="bullet">
  ///     <item><term>Gaussian distribution</term>
  ///       <description><formula inline="true">Y_1, \ldots, Y_m, Z_i</formula> have independent normal
  ///       distribution <formula inline="true">N(0,1)</formula></description></item>
  ///     <item><term>Student t distribution</term>
  ///       <description><formula inline="true">Z_i</formula> and
  ///       <formula inline="true">Y_j</formula> for
  ///       <formula inline="true">j = 1,\ldots,m</formula> are independent normal variables divided by
  ///       a common chi-square variable
  ///       with k degrees of freedom.</description></item>
  ///     <item><term>Double t distribution</term>
  ///       <description><formula inline="true">Y_1, \ldots, Y_m, Z_i</formula> have independent student t
  ///       distribution with the same degree of freedom <formula inline="true">k</formula>.</description></item>
  ///  </list>
  ///
  ///   <para>In  Toolkit, the factors are stored in
  ///   <see cref="FactorCorrelation">FactorCorrelation</see> objects.
  ///   If the factors are the same for all names,
  ///   <see cref="SingleFactorCorrelation">SingleFactorCorrelation</see> class
  ///   can be used to store a single factor value.</para>
  ///
  ///   </description></item>
  ///
  ///   <item><term><b>Random Factor Loading (RFL) Copulae</b></term><description>
  ///   <para>This type of copula is based on one factor Gauss copula where the factor
  ///   is assumed a random variable following a discrete distribution.
  ///   To be precisely, in the Gauss factor equation
  ///
  ///   <formula>
  ///     x_i = a_i Y + Z_i \sqrt{1 - a^2_i}
  ///   </formula>
  ///   The factor loading
  ///   <formula inline="true">a_i</formula> for name <formula inline="true">i</formula>
  ///   is a random variable following
  ///   a discrete distribution given by
  ///   <formula>
  ///     a_i^2 = \bar{a_i}^2 + b_i (c_k - \bar{c})
  ///     \quad\mbox{ with probability }p_k
  ///   </formula>
  ///   where
  ///   <formula inline="true">
  ///     k = 0, \ldots, K
  ///   </formula> and
  ///   <formula>
  ///     \bar{c} = \sum_{k=0}^K p_k c_k
  ///   </formula> and
  ///   <formula>
  ///     b_i = \min\{ \frac{\bar{a_i}^2}{\bar{c}}, \frac{1 - \bar{a_i}^2}{1 - \bar{c}} \}
  ///   </formula>
  ///   </para>
  ///
  ///   <para>In  Toolkit, the array of parameters <formula inline="true"> \bar{a_i} </formula>
  ///   should be specified in a
  ///   <see cref="FactorCorrelation">FactorCorrelation</see> object,
  ///   while the factor distribution
  ///   <formula inline="true">\{(p_k, c_k), k = 0, \ldots, K\} </formula> is specified in
  ///   the property <c>Data</c> of <c>Copula</c> object as an array of probability/correlation pairs.
  ///   </para>
  ///
  ///   </description></item>
  /// 
  /// 
  ///   <item><term><b>Normal Inverse Gaussian (NIG) Copulae</b></term><description>
  ///   <para>This type of copula is based on normal inverse Gaussian distributions.
  ///   In the factor equation
  ///
  ///   <formula>
  ///     x_i = a_i Y + Z_i \sqrt{1 - a^2_i}
  ///   </formula>
  /// 
  ///   the variables 
  ///   <formula inline="true">Y</formula>
  ///   and
  ///   <formula inline="true">Z_i</formula>
  ///   are independent NIG random variables with the following parameters
  ///   <formula>
  ///     Y \sim \cal{NIG} \left(
  ///     \alpha,\,
  ///     \beta,\,
  ///    - \frac{\alpha\beta}{\sqrt{\alpha^2 - \beta^2}},\,
  ///    \alpha
  ///    \right)
  ///   </formula>
  ///   <formula>
  ///     Z_i \sim \cal{NIG} \left(
  ///     \frac{\sqrt{1-a^2_i}}{a_i} \alpha,\,
  ///     \frac{\sqrt{1-a^2_i}}{a_i} \beta,\,
  ///    - \frac{\sqrt{1-a^2_i}}{a_i} \frac{\alpha\beta}{\sqrt{\alpha^2 - \beta^2}},\,
  ///    \frac{\sqrt{1-a^2_i}}{a_i} \alpha
  ///    \right)
  ///   </formula>
  ///   </para>
  ///
  ///   <para>In  Toolkit, the array of parameters <formula inline="true"> a_i </formula>
  ///   should be specified in a
  ///   <see cref="FactorCorrelation">FactorCorrelation</see> object,
  ///   while the distribution parameters
  ///   <formula inline="true">\alpha</formula> and
  ///   <formula inline="true">\beta</formula> are specified in
  ///   the property <c>Data</c> of <c>Copula</c> object as an array in the form of <c>{alpha, beta}</c>.
  ///   </para>
  ///
  ///   </description></item>
  /// 
  /// 
  ///   <item><term><b>External Factor Copulae</b></term><description>
  ///   <para>This type of copula adds an external factor to one factor copula.  The structure of the model is
  ///   described by the following equation
  ///
  ///   <formula>
  ///     x_i = \rho_i Y + \sqrt{1 - \rho^2_i} \xi_{i,1}
  ///   </formula>
  ///   <formula>
  ///     z_i = \mu_i + \sigma_i \xi_{i,2}
  ///   </formula>
  ///
  ///   where <c>i</c> denotes individual credit names,
  ///   <formula inline="true"> Y </formula>,
  ///   <formula inline="true"> \xi_{i,1} </formula>
  ///   and
  ///   <formula inline="true"> \xi_{i,2} </formula>
  ///   are all independent random variables.</para>
  ///
  ///   <para>The distribution of default times is mapped by the relationship
  ///   <formula inline="true">Q_i(t) = \mathrm{Prob}( \min\{x_i, z_i\} \leq \chi )
  ///   </formula>.</para>
  ///
  ///   <para>This model can capture correlation smile better than usual factor copulae.  At this moment
  ///   the distributions of <formula inline="true"> Y </formula>,
  ///   <formula inline="true"> \xi_{i,1} </formula>
  ///   and
  ///   <formula inline="true"> \xi_{i,2} </formula> have to be Gaussian.</para>
  ///
  ///   <para>In  Toolkit, the arrays of parameters <formula inline="true"> \rho </formula>,
  ///   <formula inline="true"> \mu </formula> and <formula inline="true"> \sigma </formula>
  ///   are specified in
  ///   <see cref="ExternalFactorCorrelation">ExternalFactorCorrelation</see> classes.</para>
  ///   <para> </para>
  ///
  ///   </description></item>
  /// 
  ///   <item><term><b>Archimedean Copulae</b></term><description>
  ///
  ///   <para>An Archimedean copula can be represented by a function</para>
  ///
  ///   <formula>
  ///     \mathrm{c}(u_1, u_2, \ldots, u_n) = \phi\left( \psi(u_1) + \psi(u_2) + \ldots
  ///       + \psi(u_n) \right)
  ///   </formula>
  ///
  ///   <para>where <formula inline="true">n</formula> is the number of credits,
  ///   <formula inline="true">\phi()</formula> is the Laplace transformation
  ///   of some random variable and <formula inline="true">\psi = \phi^{[-1]}</formula>
  ///   is the inverse function.</para>
  ///
  ///   <para>The available Archimedean copulae are</para>
  ///   <list type="bullet">
  ///     <item><term>Gumbel copula</term>
  ///       <description><formula inline="true">\psi(t) = (-\ln(t))^\theta</formula> and
  ///       <formula inline="true">\phi(s) = \exp(-s^{1/\theta})</formula> for
  ///       <formula inline="true">\theta \geq 1</formula></description></item>
  ///     <item><term>Clayton copula</term>
  ///       <description><formula inline="true">\psi(t) = t^{-\theta} - 1</formula> and
  ///       <formula inline="true">\phi(s) = (1 + s)^{-1/\theta}</formula> for
  ///       <formula inline="true">\theta \geq 0</formula></description></item>
  ///     <item><term>Frank copula</term><description>
  ///       <formula inline="true">\psi(t) = \ln \frac{\exp(-\theta t) - 1}{\exp(-\theta)-1}</formula> and
  ///       <formula inline="true">\phi(s) = -\frac{1}{\theta}\ln[1 - e^{-s}(1-e^{-\theta})]</formula> for
  ///       <formula inline="true">\theta \neq 0</formula></description></item>
  ///   </list>
  ///
  ///   <para>In  Toolkit, the user does not have to specify the parameter <formula inline="true"> \theta </formula>
  ///   and a measure of correlation called Kendall's <formula inline="true">\tau</formula> is used.
  ///   <formula inline="true"> \tau = 1 - \theta^{-1} </formula> for Gumbel copula and
  ///   <formula inline="true"> \tau = \theta / (2 + \theta) </formula> for Clayton copula.
  ///   For Frank copula, Kendall <formula inline="true">\tau</formula> is complicated and we use a simple approximate
  ///   <formula inline="true"> \tau = \theta / (10 + \theta) </formula>.
  ///   </para>
  ///
  ///   <para>Kendall's <formula inline="true">\tau</formula> should be specified in the
  ///   <see cref="SingleFactorCorrelation">SingleFactorCorrelation</see> class.</para>
  ///
  ///   </description></item>
  ///
  ///   <item><term><b>General Correlation Copulae</b></term><description>
  ///
  ///   <para>For some distributions, the correlation structure can be represented by a general
  ///   <formula inline="true">n \times n</formula> matrix instead of arrays of factors.</para>
  ///
  ///   <para><list type="bullet">
  ///     <item><term>General Gaussian copula</term>
  ///       <description>The distribution of <formula inline="true">(z_1, \ldots, z_n)</formula> is given by
  ///       <formula>
  ///          \mathrm{c}(u_1, \ldots, u_n) = R^{-\frac{1}{2}}\exp\left[
  ///            - \frac{1}{2} x' (R^{-1} - I) x 
  ///          \right]
  ///       </formula>
  ///       where <formula inline="true">R</formula> is an <formula inline="true">n \times n</formula> correlation
  ///       matrix, <formula inline="true">x = (\Phi^{-1}(u_1),\ldots,\Phi^{-1}(u_1))</formula>, and
  ///       <formula inline="true">\Phi^{-1}(\cdot)</formula> is the inverse of standard normal distribution function.
  ///       </description></item>
  ///     <item><term>General student t copula</term>
  ///       <description>The distribution of <formula inline="true">(u_1, \ldots, u_n)</formula> is given by
  ///       <formula>
  ///          \mathrm{c}(u_1, \ldots, u_n) = R^{-\frac{1}{2}}\frac{\Gamma\left(\frac{\nu+n}{2}\right)}
  ///          {\Gamma\left(\frac{\nu}{2}\right)} \left[ \frac{\Gamma\left(\frac{\nu}{2}\right)}
  ///          {\Gamma\left(\frac{\nu+1}{2}\right)} \right]^n
  ///          \frac{ \left( 1 + \frac{x' R^{-1} x}{\nu}\right )^{-\frac{\nu+n}{2}} }
  ///          {\prod_{i=1}^n (1 + \frac{x_i^2}{\nu}) ^ {-\frac{\nu+1}{2}}}
  ///       </formula>
  ///       where <formula inline="true">R</formula> is an <formula inline="true">n \times n</formula> correlation
  ///       matrix, <formula inline="true">x = (t_\nu^{-1}(u_1),\ldots,t_\nu^{-1}(u_1))</formula>, and
  ///       <formula inline="true">t_\nu^{-1}(\cdot)</formula> is the inverse of student t distribution function
  ///       with degree of freedom <formula inline="true">\nu</formula>.
  ///       </description></item>
  ///   </list></para>
  ///
  ///   <para>The class <see cref="GeneralCorrelation">GeneralCorrelation</see> is provided
  ///   to store correlation matrix for this kind of copulae.</para>
  ///
  ///  </description></item>
  ///  </list>
  /// </remarks>
  ///
  /// <example>
  /// <para>The following example demonstrates constructing a copula</para>
  /// <code language="C#">
  ///   // Construct Gaussian copula
  ///   Copula copula1 = new Copula(CopulaType.Gauss);
  ///
  ///   // Construct another Gaussian copula
  ///   Copula copula2 = new Copula();
  ///
  ///   // Construct a Student-t copula with 3 degrees of freedom for the common factors
  ///   // and 4 degrees of freedom for individual factors.
  ///   Copula copula3 = new Copula(CopulaType.StudentT, 3, 4);
  /// </code>
  /// </example>
  ///
  [Serializable]
  public class Copula : BaseEntityObject
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Copula));

    #region Constructors

    /// <summary>
    ///   Construct a Gaussian copula
    /// </summary>
    public Copula()
    {
      type_ = CopulaType.Gauss;
      dfCommon_ = 0;
      dfIdiosyncratic_ = 0;
      data_ = new double[2] { 1.0, 0.0 };
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="type">Copula type</param>
    ///
    public Copula(CopulaType type)
    {
      type_ = type;
      dfCommon_ = 0;
      dfIdiosyncratic_ = 0;
      data_ = new double[2] { 1.0, 0.0 };
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="type">Copula type</param>
    /// <param name="parameters">Other parameters</param>
    ///
    public Copula(CopulaType type, double[] parameters)
    {
      type_ = type;
      dfCommon_ = 0;
      dfIdiosyncratic_ = 0;
      data_ = parameters;
      if (parameters == null || parameters.Length == 0)
        data_ = new double[] { 1.0, 0.0 };
      else if (type == CopulaType.NormalInverseGaussian)
      {
        if (parameters.Length < 2)
          data_ = new double[2]{
            parameters.Length==1 ? parameters[0] : 1.0,
            0.0};
        CheckNIGParameters(parameters);
      }
      else if (type == CopulaType.RandomFactorLoading)
      {
        CheckDistribution(parameters);
      }
      return;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="type">Copula type</param>
    /// <param name="dfCommon">Degrees of freedom of common factors</param>
    /// <param name="dfIdiosyncratic">Degrees of freedom of individual factors</param>
    ///
    public Copula(CopulaType type,
                   int dfCommon,
                   int dfIdiosyncratic)
    {
      type_ = type;
      DfCommon = dfCommon;
      DfIdiosyncratic = dfIdiosyncratic;
      data_ = new double[2] { 1.0, 0.0 };
    }

    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Check distribution for random factor loading copula
    /// </summary>
    /// <param name="parameters">Array of probability/value pairs</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="parameters"/>
    ///   do not represent a valid distribution of correlations.
    /// </exception>
    private static void CheckNIGParameters(double[] parameters)
    {
      if (parameters[0] <= 0)
        throw new System.ArgumentException(String.Format(
          "NIG parameter alpha must be positive, not {0}", parameters[0]));
      if (Math.Abs(parameters[0]) - Math.Abs(parameters[1]) < 1E-12)
        throw new System.ArgumentException(String.Format(
          "Absolute value of NIG parameter beta {0} must be less than alpha {1}",
          parameters[1], parameters[0]));
    }

    /// <summary>
    ///   Check distribution for random factor loading copula
    /// </summary>
    /// <param name="parameters">Array of probability/value pairs</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="parameters"/>
    ///   do not represent a valid distribution of correlations.
    /// </exception>
    private static void CheckDistribution(double[] parameters)
    {
      if (parameters.Length % 2 != 0)
        throw new System.ArgumentException(String.Format(
          "For random loading, the length of parameters ({0}) must be even", parameters.Length));
      double sumP = 0.0;
      int N = parameters.Length / 2;
      for (int i = 0; i < N; ++i)
      {
        int j = 2 * i;
        if (parameters[j] < 0 || parameters[j] > 1)
          throw new System.ArgumentException(String.Format(
            "Probability at {0} must be between 0 and 1, not {1}", i, parameters[j]));
        sumP += parameters[j];
        if (parameters[j + 1] < 0 || parameters[j + 1] > 1)
          throw new System.ArgumentException(String.Format(
            "Correlation value at {0} must be between 0 and 1, not {1}", i + 1, parameters[j + 1]));
      }
      if (Math.Abs(sumP - 1) > 1E-12)
        throw new System.ArgumentException(String.Format(
          "Probability must sum up to 1, not {0}", sumP));
      return;
    }
    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Copula type used for pricing
    /// </summary>
    public CopulaType CopulaType
    {
      get { return type_; }
      set { type_ = value; }
    }


    /// <summary>
    ///   Degrees of freedom for common factor
    /// </summary>
    public int DfCommon
    {
      get {
        if (type_ == CopulaType.RandomFactorLoading)
          return data_.Length / 2; // size of random factor distribution
        return dfCommon_;
      }
      set
      {
        if (value < 0)
          throw new System.ArgumentException(String.Format("Invalid degree of freedom (dfCommon = {0})", value));
        dfCommon_ = value;
      }
    }


    /// <summary>
    ///   Degrees of freedom for idiosyncratic factor
    /// </summary>
    public int DfIdiosyncratic
    {
      get { return dfIdiosyncratic_; }
      set
      {
        if (value < 0)
          throw new ArgumentException(String.Format("Invalid degree of freedom (dfIdiosyncratic = {0})", value));
        dfIdiosyncratic_ = value;
      }
    }

    /// <summary>
    ///   Array of parameter data
    /// </summary>
    public double[] Data
    {
      get { return data_; }
      set { data_ = value; }
    }

    #endregion Properties

    #region Data

    private CopulaType type_;
    private int dfCommon_;
    private int dfIdiosyncratic_;
    private double[] data_;

    #endregion Data

  }	// class Copula

}
