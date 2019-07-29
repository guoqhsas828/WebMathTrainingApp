// 
//  -2017. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Ccr
{
  internal interface ICCRMeasureAccumulator
  {
    void AddMeasureAccumulator(CCRMeasure measure, double ci);

    bool HasMeasureAccumulator(CCRMeasure measure, double ci);

    double GetMeasure(CCRMeasure measure, Dt dt, double ci);

    void AccumulateExposures(PathWiseExposure.ExposurePoint pathEE, PathWiseExposure.ExposurePoint pathNEE);

    void ReduceCumulativeValues();

    Tuple<Dt[], double[]>[] IntegrationKernels { set; }
  }
}