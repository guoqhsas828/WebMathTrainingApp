using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Base
{
  public interface ISchedule
  {
    Dt GetNextCouponDate(Dt date);
    Dt GetPrevCouponDate(Dt date);
    IList<Schedule.CouponPeriod> Periods { get; }
  }
}
