/*
 * TrancheEvaluator.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
	internal class TrancheEvaluator : BaseEvaluator
	{
    public TrancheEvaluator()
      : base()
    {
    }

    protected override double evaluate(double factor)
    {
      double dpv = base.evaluate(factor);
      double apv = apIndex_ < 0 ? 0.0 :
        evaluate(factor);
      return dpv - apv;
    }

    public int AttachmentIndex
    {
      get { return apIndex_; }
      set { apIndex_ = value; }
    }

    private int apIndex_;
	}
}
