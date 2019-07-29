/*
 * ConvertibleBondParams.cs
 *

 */
using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Container class to hold parameters used by convertible bond
  /// </summary>
  [Serializable]
	public class ConvertibleBondParams
	{
    // initial stock price
    private double S0_ = 0;
    // volatility of stock
    private double sigmaS_ = 0;
    // stock continuous yield rate
    private double yield_ = 0;
    // mean reversion speed for Black-Karasinski model
    private double kappa_ = 0;
    // volatility of rate
    private double sigmaR_ = 0;
    // yield over risk-free rate for corparate bond
    private double spread_ = 0;
    // correlation between rate and stock driven Brownain motions
	  private double rho_ = 0;
    // start dates for call schedule
    private Dt[] callStartDates_ = null;
    // end dates for call schedule
	  private Dt[] callEndDates_ = null;
    // call prices
    private double[] callPrices_ = null;
    // start dates for put schedule
    private Dt[] putStartDates_ = null;
    // end dates for put schedule
	  private Dt[] putEndDates_ = null;
    // put prices
    private double[] putPrices_ = null;
    // conversion ratio: number of stock shares one bond can convert to
    private double conversionRatio_ = 0;
    // start date of conversion
    private Dt conversionStartDate_ = Dt.Empty;
    // end date of conversion
    private Dt conversionEndDate_ = Dt.Empty;
    // number of tree layers
    private int n_;

    /// <summary>
    ///  Constructor 
    /// </summary>
    /// <param name="conversionRatio">Conversion ratio</param>
    /// <param name="convertStartDate">Conversion start date</param>
    /// <param name="convertEndDate">Conversion end date</param>
    public ConvertibleBondParams(double conversionRatio, Dt convertStartDate, Dt convertEndDate)
    {
      conversionRatio_ = conversionRatio;
      conversionStartDate_ = convertStartDate;
      conversionEndDate_ = convertEndDate;
    }
  
    /// <exclude/>
    [Browsable(false)]    
    public int N
    {
      get { return n_; }
      set{ n_ = value;}
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double S0
    {
      get { return S0_; }
      set { S0_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double SigmaS
    {
      get { return sigmaS_; }
      set { sigmaS_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double Yield
    {
      get { return yield_; }
      set { yield_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double Kappa
    {
      get { return kappa_; }
      set { kappa_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double SigmaR
    {
      get { return sigmaR_; }
      set { sigmaR_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double Spread
    {
      get { return spread_; }
      set { spread_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double Rho
    {
      get { return rho_; }
      set { rho_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt[] CallStartDates
    {
      get { return callStartDates_; }
      set { callStartDates_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt[] CallEndDates
    {
      get { return callEndDates_; }
      set { callEndDates_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double[] CallPrices
    {
      get { return callPrices_; }
      set { callPrices_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt[] PutStartDates
    {
      get { return putStartDates_; }
      set { putStartDates_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt[] PutEndDates
    {
      get { return putEndDates_; }
      set { putEndDates_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double[] PutPrices
    {
      get { return putPrices_; }
      set { putPrices_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public double ConversionRatio
    {
      get { return conversionRatio_; }
      set { conversionRatio_ = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt ConversionStartDate
    {
      get { return conversionStartDate_; }
      set { ConversionStartDate = value; }
    }
    
    /// <exclude/>
    [Browsable(false)]
    public Dt ConversionEndDate
    {
      get { return conversionEndDate_; }
      set { conversionEndDate_ = value; }
    }
	}
}