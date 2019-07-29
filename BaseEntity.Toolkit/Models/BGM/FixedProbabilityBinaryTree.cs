/*
 * FixedProbabilityBinaryTree.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System.Collections.Generic;

using System.Diagnostics;
using BaseEntity.Toolkit.Util.Collections;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Models.BGM
{
  internal static class FixedProbabilityBinaryTree
  {
    /// <summary>
    ///  Calculate the probability of a state at the specified step.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="levelIndex">Index of the level.</param>
    /// <returns>Probability</returns>
    public static double Probability(
      uint stepIndex, uint levelIndex)
    {
      return QMath.BinomialPdf(levelIndex, stepIndex, 0.5);
    }

    /// <summary>
    ///  Calculate the probability of a state at the specified step.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="levelIndex">Index of the level.</param>
    /// <param name="upJumpProbability">The probability of one step up jumping.</param>
    /// <returns>Probability</returns>
    public static double Probability(
      uint stepIndex, uint levelIndex,
      double upJumpProbability)
    {
      return QMath.BinomialPdf(levelIndex, stepIndex,
        upJumpProbability);
    }

    /// <summary>
    ///  Calculate the probability distribution at the specified step.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <returns></returns>
    public static IList<double> Distribution(int stepIndex)
    {
      int n = stepIndex + 1; 
      var list = new double?[n];
      return ListUtil.CreateList(n, (k) => list[k]
        ?? QMath.BinomialPdf((uint) n, (uint) k, 0.5));
    }

    /// <summary>
    ///  Calculate the probability distribution at the specified step.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="upJumpProbability">The probability of one step up jumping.</param>
    /// <returns></returns>
    public static IList<double> Distribution(int stepIndex,
      double upJumpProbability)
    {
      double p = upJumpProbability;
      int n = stepIndex + 1;
      var list = new double?[n];
      return ListUtil.CreateList(n, (k) => list[k]
        ?? QMath.BinomialPdf((uint)n, (uint)k, p));
    }

    /// <summary>
    ///  Calculate the conditional probability.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="levelIndex">Index of the level.</param>
    /// <param name="baseStepIndex">Index of the base step.</param>
    /// <param name="baseLevelIndex">Index of the base level.</param>
    /// <returns>Conditional probability.</returns>
    public static double ConditionalProbability(
      uint stepIndex, uint levelIndex,
      uint baseStepIndex, uint baseLevelIndex)
    {
      if (stepIndex < baseStepIndex)
      {
        return LookBackConditional(baseStepIndex,
          baseLevelIndex, stepIndex, levelIndex);
      }
      if (stepIndex>baseStepIndex)
      {
        return TransitionProbability(baseStepIndex,
          baseLevelIndex, stepIndex, levelIndex, 0.5);
      }
      // the case baseStepIndex == stepIndex
      return levelIndex == baseLevelIndex ? 1 : 0;
    }

    /// <summary>
    ///  Calculate the conditional probability.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="levelIndex">Index of the level.</param>
    /// <param name="baseStepIndex">Index of the base step.</param>
    /// <param name="baseLevelIndex">Index of the base level.</param>
    /// <param name="upJumpProbability">The probability of one-step up jumping.</param>
    /// <returns>Conditional probability.</returns>
    public static double ConditionalProbability(
      uint stepIndex, uint levelIndex,
      uint baseStepIndex, uint baseLevelIndex,
      double upJumpProbability)
    {
      if (stepIndex < baseStepIndex)
      {
        return LookBackConditional(baseStepIndex,
          baseLevelIndex, stepIndex, levelIndex);
      }
      if (stepIndex > baseStepIndex)
      {
        return TransitionProbability(baseStepIndex,
          baseLevelIndex, stepIndex, levelIndex,
          upJumpProbability);
      }
      // the case baseStepIndex == stepIndex
      return levelIndex == baseLevelIndex ? 1 : 0;
    }

    private static double TransitionProbability(
      uint fromStepIndex, uint fromLevelIndex,
      uint toStepIndex, uint toLevelIndex,
      double probability)
    {
      Debug.Assert(toStepIndex >= fromStepIndex);
      if (toLevelIndex < fromLevelIndex)
        return 0;
      uint k = toLevelIndex - fromLevelIndex;
      uint n = toStepIndex - fromStepIndex;
      if (k > n)
        return 0;
      return QMath.BinomialPdf(k, n, probability);
    }

    private static double LookBackConditional(
      uint fromStepIndex, uint fromLevelIndex,
      uint backStepIndex, uint backLevelIndex)
    {
      Debug.Assert(backStepIndex <= fromStepIndex);
      if (backLevelIndex > fromLevelIndex)
        return 0;
      return QMath.HypergeometricPdf(
        backStepIndex, backLevelIndex,
        fromStepIndex - backStepIndex,
        fromLevelIndex - backLevelIndex);
    }
  }
}
