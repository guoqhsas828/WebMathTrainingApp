using System;
using System.Collections.Generic;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Storage for realization of relevant quantities for each path  
  /// </summary>
  [Serializable]
  public class SimulatedPathValues : ISimulatedPathValues
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="capacity">Default capacity</param>
    /// <param name="nettingCount">Number of netting groups</param>
    public SimulatedPathValues(int capacity, int nettingCount)
    {
      nettingCount_ = nettingCount;
      portfolioData_ = new List<Data>(capacity);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Clear the data list
    /// </summary>
    internal void Clear()
    {
      portfolioData_.Clear();
    }

    /// <summary>
    /// Store simulated values by simulation dates 
    /// </summary>
    /// <param name="values">Undiscounted netted positions</param>
    /// <param name="discountFactor">Realized discount factor</param>
    /// <param name="numeraire">Realized numeraire process</param>
    /// <param name="weightCpty">Density to change measure to condition on counter party default at simulation date</param>
    /// <param name="weightOwn">Density to change measure to condition on own default at simulation date</param>
    /// <param name="weightSurvival">Density to change measure to condition on survival of both counter party and booking entity</param>
    /// <param name="cptySpread">CDS spread of the counter party</param>
    /// <param name="ownSpread">CDS spread of the booking entity</param>
    /// <param name="lendSpread">Lending spread</param>
    /// <param name="borrowSpread">Borrowing spread</param>
    /// <param name="pathId">Unique path identifier</param>
    /// <param name="pathWeight">Weight to apply to path</param>
    public void Add(double[] values, double discountFactor, double numeraire, double weightCpty, double weightOwn,
                    double weightSurvival,
                    double cptySpread, double ownSpread, double lendSpread, double borrowSpread, int pathId, double pathWeight)
    {
      weight_ = pathWeight;
      id_ = pathId;
      portfolioData_.Add(new Data
                           {
                             Values = values,
                             DiscountFactor = discountFactor,
                             Numeraire = numeraire,
                             WeightCpty = weightCpty,
                             WeightOwn = weightOwn,
                             WeightSurvival = weightSurvival,
                             CptySpread = cptySpread,
                             OwnSpread = ownSpread,
                             LendSpread = lendSpread,
                             BorrowSpread = borrowSpread
                           });
    }

    #endregion

    #region Methods

    /// <summary>
    /// Access the undiscounted mark to market of a netting group at a given simulation date
    /// </summary>
    /// <param name="dateIndex">Index of the simulation date</param>
    /// <param name="groupIndex">Index of the netting group</param>
    /// <returns>Value of the portfolio </returns>
    /// <remarks>
    /// <para>Suppose there are <m>N</m> instruments in the portfolio with the same netting set at time <m>t</m>.
    /// Let <m>V_i(t)</m> denote the value of the <m>i</m>th instrument from the booking entity's perspective. </para>
    /// The value of the portfolio with this netting group at <m>t</m> is give by
    /// <math env="align*">
    ///   V(t) = \sum_{i=1}^N V_i(t).
    ///  </math>
    /// <para>Note only exposures belonging to the same netting set are netted out.</para>
    /// </remarks>
    public double GetPortfolioValue(int dateIndex, int groupIndex)
    {
      return portfolioData_[dateIndex].Values[groupIndex];
    }

    /// <summary>
    /// Sets the undiscounted mark to market of a netting group at the given simulation date
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <param name="nettingGroupIndex">Index of the netting group</param>
    /// <param name="value">Portfolio value to set</param>
    /// <remarks>Portfolio value is discounted only to the future simulation date</remarks>
    public void SetPortfolioValue(int dateIndex, int nettingGroupIndex, double value)
    {
      portfolioData_[dateIndex].Values[nettingGroupIndex] = value;
    }

    /// <summary>
    /// Access the realized counter party CDS spread  
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Counter party spread</returns>
    /// <remarks>
    /// <para>
    /// Let <m>\tau_c</m> and <m>R_c</m> denote the default time and recovery rate of the counter party. 
    /// Let <m>S_t^c(T_k)</m> denote the conditional cumulative distribution function of <m>\tau_c</m>, i.e. 
    /// <m>S_t^c(T_k) = \mathbb{Q}(\tau_c \leq T_k | \mathbb{F}_t).</m>
    /// </para>
    /// <para>
    /// The counter party CDS spread at <m>T_k</m> is calculated by
    /// <math env="align*">
    ///   \text{CptySpread}(T_k) = (1 - R_c) \cdot \frac{\frac{S_t^c(T_k) - S_t^c(T_{k-1})}{\Delta t_k}}{1 - \frac{S_t^c(T_k) + S_t^c(T_{k-1})}{2}} ,
    ///  </math>
    /// where <m>T_k</m> is the <m>k</m>th simulation date, and <m>\Delta t_k = T_k - T_{k-1}</m>.
    /// </para>
    /// </remarks>
    public double GetCptySpread(int dateIndex)
    {
      return portfolioData_[dateIndex].CptySpread;
    }

    /// <summary>
    /// Access the realized booking entity CDS spread
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Booking entity spread</returns>
    /// <remarks>
    /// <para>
    /// Let <m>\tau_o</m> and <m>R_o</m> denote the default time and recovery rate of the booking entity. 
    /// Let <m>S_t^o(T_k)</m> denote the conditional cumulative distribution function of <m>\tau_o</m>, i.e. 
    /// <m>S_t^o(T_k) = \mathbb{Q}(\tau_o \leq T_k | \mathbb{F}_t).</m>
    /// </para>
    /// <para>
    /// The booking entity CDS spread at <m>T_k</m> is calculated by
    /// <math env="align*">
    ///   \text{OwnSpread}(T_k) = (1 - R_o) \cdot \frac{\frac{S_t^o(T_k) - S_t^o(T_{k-1})}{\Delta t_k}}{1 - \frac{S_t^o(T_k) + S_t^o(T_{k-1})}{2}} ,
    ///  </math>
    /// where <m>T_k</m> is the <m>k</m>th simulation date, and <m>\Delta t_k = T_k - T_{k-1}</m>.
    /// </para>
    /// <para>
    /// If the own survival curve doesn't exist, by default, <m>\text{Ownspread} = 0</m>.
    /// </para>
    /// </remarks>
    public double GetOwnSpread(int dateIndex)
    {
      return portfolioData_[dateIndex].OwnSpread;
    }

    /// <summary>
    /// Access the realized lending spread
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Lending spread</returns>
    /// <remarks>
    /// <para>
    /// Let <m>\tau_l</m> and <m>R_l</m> denote the default time and recovery rate of the lending party. 
    /// Let <m>S_t^l(T_k)</m> denote the conditional cumulative distribution function of <m>\tau_l</m>, i.e. 
    /// <m>S_t^l(T_k) = \mathbb{Q}(\tau_l \leq T_k | \mathbb{F}_t).</m>
    /// </para>
    /// <para>
    /// The lending spread at <m>T_k</m> is calculated by
    /// <math env="align*">
    ///   \text{LendSpread}(T_k) = (1 - R_l) \cdot \frac{\frac{S_t^l(T_k) - S_t^l(T_{k-1})}{\Delta t_k}}{1 - \frac{S_t^l(T_k) + S_t^l(T_{k-1})}{2}} ,
    ///  </math>
    /// where <m>T_k</m> is the <m>k</m>th simulation date, and <m>\Delta t_k = T_k - T_{k-1}</m>.
    /// </para>
    /// </remarks>
    public double GetLendSpread(int dateIndex)
    {
      return portfolioData_[dateIndex].LendSpread;
    }


    /// <summary>
    /// Access the realized borrowing spread
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Borrowing spread</returns>
    /// <remarks>
    /// <para>
    /// Let <m>\tau_b</m> and <m>R_b</m> denote the default time and recovery rate of the borrowing party. 
    /// Let <m>S_t^b(T_k)</m> denote the conditional cumulative distribution function of <m>\tau_b</m>, i.e. 
    /// <m>S_t^b(T_k) = \mathbb{Q}(\tau_b \leq T_k | \mathbb{F}_t).</m>
    /// </para>
    /// <para>
    /// The borrowing spread at <m>T_k</m> is calculated by
    /// <math env="align*">
    ///   \text{BorrowSpread}(T_k) = (1 - R_b) \cdot \frac{\frac{S_t^b(T_k) - S_t^b(T_{k-1})}{\Delta t_k}}{1 - \frac{S_t^b(T_k) + S_t^b(T_{k-1})}{2}} ,
    ///  </math>
    /// where <m>T_k</m> is the <m>k</m>th simulation date, and <m>\Delta t_k = T_k - T_{k-1}</m>.
    /// </para>
    /// </remarks>
    public double GetBorrowSpread(int dateIndex)
    {
      return portfolioData_[dateIndex].BorrowSpread;
    }

    /// <summary>
    /// Realized domestic discount factor at date index. 
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Domestic discount factor</returns>
    public double GetDiscountFactor(int dateIndex)
    {
      return portfolioData_[dateIndex].DiscountFactor;
    }

    /// <summary>
    /// Realized undiscounted numeraire asset at dateIndex
    /// </summary>
    /// <param name="dateIndex">Date Index</param>
    /// <returns>Realized undiscounted numeraire asset at dateIndex</returns>
    /// <remarks>
    /// 
    /// </remarks>
    public double GetNumeraire(int dateIndex)
    {
      return portfolioData_[dateIndex].Numeraire;
    }

    /// <summary>
    /// Radon Nikodym derivative to change the measure from simulation measure (corresponding to a convenient numeraire process) 
    /// to risk neutral measure (corresponding to bank account process)
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Realized change of measure</returns>
    /// <remarks>
    /// <para>
    /// Assume the simulation measure is <m>\mathbb{Q}^N</m> with corresponding numeraire asset <m>N</m>. Denote the risk neutral measure and corresponding bank account by <m>\mathbb{Q}^B</m> and <m>B</m>.
    /// </para>
    /// Then the Radon Nikodym derivative to change the measure from <m>\mathbb{Q}^N</m> to <m>\mathbb{Q}^B</m> is given by:
    /// <math env="align*">
    ///  \frac{d\mathbb{Q}^N}{d\mathbb{Q}^B} = \frac{N_tB_0}{N_0B_t}
    ///  </math>
    /// </remarks>
    internal double GetRadonNikodym(int dateIndex)
    {
      return portfolioData_[dateIndex].DiscountFactor*portfolioData_[dateIndex].Numeraire;
    }

    /// <summary>
    /// Radon Nikodym derivative to condition on event of the counter party defaulting at date index.   
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Realized change of measure</returns>
    /// <remarks>
    /// <para>
    /// Denote the default time of counter party and booking entity by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// Let RadonNikodymCpty denote the Radon Nikodym derivative (in simulation measure) to condition on 
    /// event of the counter party  defaulting at date <m>t</m>. 
    /// </para>
    /// Then for unilateral case, we only take into account counter party default at date <m>t</m>,
    /// <math env="align*">
    ///  \text{Unilateral RadonNikodymCpty} = \frac{dP(\cdot|\tau_c = t)}{dP(\cdot)}.
    ///  </math>
    /// For bilateral case, we take into account counter party default at date <m>t</m>, before the booking entity,
    /// <math env="align*">
    ///  \text{Bilateral RadonNikodymCpty} = \frac{dP(\cdot|\tau_c = t, \tau_o > t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    public double GetRadonNikodymCpty(int dateIndex)
    {
      return portfolioData_[dateIndex].WeightCpty;
    }

    /// <summary>
    /// Radon Nikodym derivative to condition on event of the booking entity defaulting at date index.
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Realized change of measure</returns>
    /// <remarks>
    /// <para>
    /// Denote the default time of counter party and booking entity by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// Let RadonNikodymOwn denote the Radon Nikodym derivative (in simulation measure) to condition on event of 
    /// the booking entity defaulting at date <m>t</m>.
    /// </para>
    /// Then for unilateral case, we only take into account booking entity default at date <m>t</m>,
    /// <math env="align*">
    ///  \text{Unilateral RadonNikodymOwn} = \frac{dP(\cdot|\tau_o = t)}{dP(\cdot)}.
    ///  </math>
    /// For bilateral case, we take into account booking entity default at date <m>t</m>, before the counter party,
    /// <math env="align*">
    ///  \text{Bilateral RadonNikodymOwn} = \frac{dP(\cdot|\tau_c > t, \tau_o = t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    public double GetRadonNikodymOwn(int dateIndex)
    {
      return portfolioData_[dateIndex].WeightOwn;
    }

    /// <summary>
    /// Radon Nikodym derivative to condition on event of both booking entity and counter party surviving at date index
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <returns>Realized change of measure</returns>
    /// <remarks>
    /// <para>
    /// Denote the default time of counter party and booking entity by <m>\tau_c</m> and <m>\tau_o</m> respectively. 
    /// Let RadonNikodymSurvival denote the Radon Nikodym derivative (in simulation measure) to condition 
    /// on the event of booking entity surviving at date <m>t</m>.
    /// </para>
    /// For unilateral case, we only take into account booking entity surviving at date <m>t</m>, 
    /// <math env="align*">
    ///  \text{Unilateral RadonNikodymSurvival} = \frac{dP(\cdot|\tau_o > t)}{dP(\cdot)}.
    ///  </math>
    /// For bilateral case, we take into account both booking entity and counter party surviving at date <m>t</m>, 
    /// <math env="align*">
    ///  \text{Bilateral RadonNikodymSurvival} = \frac{dP(\cdot|\tau_c > t, \tau_o > t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    public double GetRadonNikodymSurvival(int dateIndex)
    {
      return portfolioData_[dateIndex].WeightSurvival;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Reference to old path for incremental cva
    /// </summary>
    public ISimulatedPathValues OldPath
    {
      get { return oldPath_; }
      set { oldPath_ = value; }
    }

    ///<summary>
    /// Number of exposure dates
    ///</summary>
    public int DateCount
    {
      get { return portfolioData_.Count; }
    }

    /// <summary>
    /// Index identifying the path
    /// </summary>
    public int Id
    {
      get { return id_; }
    }


    /// <summary>
    /// Path weight
    /// </summary>
    public double Weight
    {
      get { return weight_; }
    }

    /// <summary>
    /// Number of netting groups
    /// </summary>
    public int NettingCount
    {
      get { return nettingCount_; }
    }

    #endregion

    #region Data class

    /// <summary>
    /// Data repository
    /// </summary>
    [Serializable]
    private struct Data
    {
      internal double CptySpread;
      internal double DiscountFactor;
      internal double Numeraire;
      internal double OwnSpread;
      internal double[] Values;
      internal double WeightCpty;
      internal double WeightOwn;
      internal double WeightSurvival;
      internal double LendSpread;
      internal double BorrowSpread;
    }

    #endregion

    #region Data

    private readonly int nettingCount_;
    private readonly IList<Data> portfolioData_;
    private int id_;
    private ISimulatedPathValues oldPath_;
    private double weight_;

    #endregion
  }
}