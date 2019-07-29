using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///   Utilities to analyze and optimize single trade and portfolio evaluations,
  ///   especially the evaluations during simulations.
  /// </summary>
  /// <remarks>
  ///   <h2>Introduction</h2>
  ///   <para>In quantitative finance, we often have to run a large number of 
  ///    repetitive evaluations on trades and portfolios, as in simulations, 
  ///    sensitivity analysis, calibrations and others, where a few variables
  ///    keep changing while all the others are invariant.  This is the areas
  ///    in which code optimization can play significant roles.  However, since
  ///    different computation scenarios have vastly different sets of the changing
  ///    and invariant variables, it is extremely difficult, if not impossible,
  ///    to hand write pricing codes which are optimal in all the different
  ///    scenarios.  Furthermore, it is often undesirable to heavily optimize
  ///    the codes by hand, for such codes are hard to write, hard to understand,
  ///    hard to maintain, and more prone to bugs than the codes that plainly
  ///    mirror the business logic without optimization.</para>
  ///    
  ///   <para>Ideally, we would like to have both advantages: our hand-written
  ///    codes should follow closely the plain financial and business logic,
  ///    easy to understand, easy to maintain, and less bug prone, while in the
  ///    large number of repetitive evaluations, the running codes should be
  ///    heavily optimized, efficient and fast.  In modern world, we normally 
  ///    leave the job of code optimization to compilers.  Unfortunately in our
  ///    case, regular code compilers do not work, because the required
  ///    information are not available at compile time and the compilers simply
  ///    lack the basic knowledges intrinsic to our pricing problems.  We need
  ///    a tool which understands our problems and which is able to kick in 
  ///    optimizing our codes just before the repetitive evaluations when all
  ///    the relevant information are available.</para>
  ///    
  ///   <para><c>Toolkit</c> is built with such a tool, an analysis and
  ///    optimization engine to speed up the repetitive evaluations of a single
  ///    trades or the whole portfolio.  When the engine is fed with a list of 
  ///    trades and a set of varying objects (curves, FX rates, prices, etc.),
  ///    it runs through a sequence of transformations on the pricing codes to
  ///    make them efficient for repetitive evaluations.</para>
  ///    
  ///   <para>Basically, it first turns into constants all the variables known
  ///    invariant across evaluations.  Then it walks through the pricing
  ///    expressions and performs constant folding, redundancy elimination and
  ///    some elementary forms of symbolic manipulations to simplify them
  ///    as much as possible. After that it performs global optimization with
  ///    common expression eliminations and loop variable movements.  The end
  ///    results are the codes heavily optimized for repetitive computations.</para>
  ///    
  ///   <h2>Two examples</h2>
  /// 
  ///   <para>To explain what the tool does, let us look at some examples.</para>
  ///   <para>
  ///     First, some notations. Let
  ///   </para>
  ///   <list type="bullet">
  ///     <item><description><m>P</m> denote the projection curve and <m>D</m> the discount curve;
  ///       </description></item>
  ///     <item><description><m>P(t)</m> and <m>D(t)</m> being the interpolated curve values
  ///       (projection and discount factors) on date <m>t</m>, respectively;
  ///       </description></item>
  ///     <item><description><m>(T_1, T_2)</m>, a projection or discounting period from date <m>T_1</m> to <m>T_2</m>;
  ///       </description></item>
  ///     <item><description><m>\delta_P(T_1, T_2)</m>, the year fraction based on the projection convention (day count);
  ///       </description></item>
  ///     <item><description><m>\delta(T_1, T_2)</m>, the fraction based on the payment convention, which
  ///       may be different than the projection convention.
  ///       </description></item>
  ///   </list>
  ///   <para><b>Example 1: A simple floating interest payment</b></para>
  ///   <para>
  ///     To evaluate a simple floating interest payment on the period <m>(T_1, T_2)</m>
  ///     with payment date on <m>T_2</m>, we go through three steps.
  ///   </para>
  ///   <list type="bullet">
  ///     <item><description><i>Step 1</i>: calculate the floating rate<math>
  ///         R = \frac{P(T_1)/P(T_2) - 1}{\delta_P(T_1, T_2)}\tag{1a}
  ///       </math></description></item>
  ///     <item><description><i>Step 2</i>: calculate the accrual using the rate<math>
  ///         A = \delta(T_1, T_2)\, R\tag{1b}
  ///       </math></description></item>
  ///     <item><description><i>Step 3</i>: calculate the present value of accrual by discounting<math>
  ///         V = D(T_2)\, A\tag{1c}
  ///       </math></description></item>
  ///   </list>
  ///   <para>
  ///     If we only evaluate the payment once, then it makes sense to run through
  ///     all the three steps and calculate all the values, <m>\delta_p(T_1, T_2)</m>,
  ///     <m>\delta(T_1,T_2)</m>, <m>P(T_1)</m>, <m>P(T_2)</m>, <m>D(T_2)</m>, etc.,
  ///     on the fly.
  ///   </para>
  ///   <para>
  ///     What if we have to calculate it thousands of times, with different curves
  ///     <m>P</m> and <m>D</m>, but all the others being invariant?  Furthermore,
  ///     suppose we know that the ratio <m>P(t)/D(t)</m> never changes across different evaluations.
  ///     Is there a way to speed it up and cut the computation cost significantly?
  ///   </para>
  ///   <para>
  ///     Mathematically, the answer is <em>yes</em>.  We can reduce the whole evaluation expression into
  ///     the following formula:</para>
  ///   <math>
  ///     V = c_1\, D(T_1) - c_2\,D(T_2)\tag{2}
  ///   </math>where <m>c_1</m> and <m>c_2</m> are pre-calculated constants such that<math>
  ///     c_1 \equiv \frac{P(T_1)\,D(T_2)\,\delta(T_1,T_2)}
  ///     {D(T_1)\,P(T_2)\,\delta_P(T_1,T_2)}
  ///     ,\qquad
  ///     c_2 \equiv \frac{\delta(T_1,T_2)}{\delta_P(T_1,T_2)}
  ///   </math>
  ///   <para></para>
  ///   <para>
  ///    It is obvious that the reduced formula (2) is much more efficient in repetitive
  ///    evaluations.
  ///   </para>
  ///   <para>
  ///    But we don't want to write our codes in the reduced form, for it
  ///    obfuscates the finance and business logic.  Looking at the reduced
  ///    formula, it is hard to figure out the meanings of <m>c_1</m> and <m>c_2</m>.
  ///    Furthermore, the reduced form does not extend easily to other common
  ///    cases such as compounding rates, averaging rates, or payment dates
  ///    different than the projection end dates.  It would be real mess if we
  ///    do derive and write by hand the reduced forms for each of these cases.
  ///   </para>
  ///   <para>
  ///    Fortunately we will write codes in the complete form with nicely organized
  ///    steps, easy to understand and easy to maintain.
  ///    Then just before the repetitive evaluations, our optimization tool kicks
  ///    in automatically, analyzing our codes and reducing them into the forms
  ///    most efficient for computation.
  ///   </para>
  ///   <para>
  ///    Conceptually, our tool performs the following steps to reach the reduced form.
  ///   </para>
  ///   <list type="bullet">
  ///     <item><description><i>Step 1</i>: invariants are turned into constants<math>
  ///       \delta_1 = \delta_p(T_1, T_2)
  ///       ,\quad
  ///       \delta_2 = \delta(T_1,T_2)
  ///       ,\quad
  ///       s_1 = \frac{P(T_1)}{D(T_1)}
  ///       ,\quad
  ///       s_2 = \frac{P(T_2)}{D(T_2)}
  ///       </math></description></item>
  ///     <item><description><i>Step 2</i>: pricing formula (1a) through (1c) are
  ///      rewritten in terms of the constants, which leads to<math>
  ///         V = D(T_2)\, \delta_2 \frac{1}{\delta_1}
  ///         \left(\frac{s_1\,D(T_1)}{s_2\,D(T_2)} - 1 \right)
  ///       </math></description></item>
  ///     <item><description><i>Step 3</i>: constants are combined together<math>
  ///         c_1 = \frac{\delta_2\,s_1}{\delta_1\,s_2}
  ///         ,\quad
  ///         c_2 = \frac{\delta_2}{\delta_1}
  ///         \quad\rightarrow\quad
  ///         V = D(T_2)\, \left(c_1\frac{D(T_1)}{D(T_2)} - c_2\right)
  ///       </math></description></item>
  ///     <item><description><i>Step 4</i>: apply the rules <m>x(y-z)=xy-xz</m> and <m>xy/x = y</m>
  ///       to get the reduced form<math>
  ///         V = c_1\,D(T_1) - c_2\,D(T_2)
  ///       </math></description></item>
  ///   </list>
  ///   <para>The last step involves a little bit symbolic algebra, where we apply the basic
  ///    rules to reduce the number of operations in the pricing expressions.
  ///   </para>
  ///
  ///   <para><b>Example 2: Summation over many cash flows</b></para>
  ///   <para>
  ///     Let <m>C_i = C_i\left(D(T_{i,1}), D(T_{i,2}),\cdots, D(T_{i,n})\right)</m>,
  ///     <m>i=1,\ldots,N</m>,
  ///     be a sequence of discounted
  ///     cash flows, each of which depends on a number of discount factors,
  ///     <m>D(T_{i,1}), D(T_{i,2}),\cdots, D(T_{i,n})</m>, for either rate projections and/or discounting,
  ///     where the dates belong to a common set <m>S</m>, i.e.,
  ///     <m>T_{i,j}\in S</m> for all <m>i</m> and <m>j</m>.
  ///   </para>
  ///   <para>
  ///     The present value of the whole cash flows is evaluated by simple summation</para>
  ///   <para></para>
  ///   <para></para>
  ///   <math>
  ///     V = \sum_{i=1}^N C_i\left(D(T_{i,1}), D(T_{i,2}),\cdots, D(T_{i,n})\right)
  ///   </math>
  ///   <para>.</para>
  ///   <para>
  ///     We know that some dates <m>T_{i,j}</m> may be common across several different <m>i</m>'s.
  ///     For example, it is common to have <m>T_{i+1,1} = T_{i,2}</m> if <m>(T_{i,1}, T_{i,2})</m>'s
  ///     are rate projection periods of the same floating rate leg.  As another example,
  ///     suppose the summation is over the cash flows generated from thousands trades,
  ///     then it is even more common for many of them
  ///     to share the same payment dates, because there are only about 200 business days in a year.  
  ///   </para>
  ///   <para>
  ///     In the case with many shared dates, it is more efficient to evaluate the cash flows
  ///     by moving the calculation of discount factors, <m>D(T_{i,j})</m> outside the loop.
  ///   </para>
  ///   <math env="align*">
  ///     &amp;\text{Step 1:}&amp;\text{let } D_t &amp;= D(t),\;\forall t \in S
  ///     \\&amp;\text{Step 2:}&amp;V &amp;= \sum_{i=1}^N C_i\left(D_{i,1}, D_{i,2},\cdots, D_{i,n}\right)
  ///   </math>
  ///   <para>where all the discount factors are calculated exact once outside
  ///   the summation loop, and <m>D_{i,j}</m> simply dereferences the pre-calculated
  ///   discount factor at date <m>T_{i,j}</m>.</para>
  ///   <para></para>
  ///   <para>
  ///     In compiler terminology, this is a type of well known loop optimization.  Only here we do it at
  ///     a much higher level than a regular compiler is able to handle.
  ///   </para>
  ///   <para>
  ///     In addition to actually performing such optimization, our optimizer engine
  ///     can also output analysis tables to show how much gains can be obtained from such optimization.
  ///   </para>
  ///   <para></para>
  ///   <h2>Further extensions</h2>
  ///   <para></para>
  ///   <list type="number">
  ///     <item><description><i>Long-term</i>.
  ///       The analysis engine knows all the evaluation expressions, which opens the door
  ///       to automatic (algorithmic) differentiation for fast sensitivity calculation
  ///       in simulations;
  ///       </description></item>
  ///     <item><description><i>Long-term</i>.
  ///       The analysis engine knows the exact set of all the dates and curves on which
  ///       the interpolations are required, making it possible to push them into batch
  ///       execution in native codes, or in massively parallel computation devices;
  ///       </description></item>
  ///   </list>
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}
