using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Curves;


namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Container for a collection of sensitivities
  /// </summary>
  public class DerivativeCollection : IDerivativeCollection
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(DerivativeCollection));
    private string name_;
    private List<DerivativesWrtCurve> derivativesBin_;
    private Dictionary<string, int> map_;
    private bool rescaleStrikes_ = false;
    private int index_;

    /// <summary>
    /// Default constructor
    /// </summary>
    public DerivativeCollection()
    {
      derivativesBin_ = new List<DerivativesWrtCurve>();
      map_ = new Dictionary<string, int>();
      index_ = 0;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="capacity">Inital capacity of the collection</param>
    public DerivativeCollection(int capacity)
    {
      derivativesBin_ = new List<DerivativesWrtCurve>(capacity);
      map_ = new Dictionary<string, int>(capacity);
      index_ = 0;
    }

    #region Properties
    /// <summary>
    /// Number of curves in the collection
    /// </summary>
    public int CurveCount
    {
      get
      {
        return derivativesBin_.Count;
      }
    }

    /// <summary>
    /// Name of the pricer corresponding to the collection
    /// </summary>
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    /// True if sensitivities have been computed with rescale strikes option turned on (applicable to basket products)
    /// </summary>
    public bool RescaleStrikes
    {
      get { return rescaleStrikes_; }
      set { rescaleStrikes_ = value; }
    }

    #endregion

    #region Methods
    /// <summary>
    /// Access the ith curve in the collection
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <returns>Reference curve at index i</returns>
    public CalibratedCurve GetCurve(int i)
    {
      return derivativesBin_[i].ReferenceCurve;
    }

    /// <summary>
    /// Access the DerivativesWrtCurve object stored at index i
    /// </summary>
    /// <param name="i">Index</param>
    /// <returns>DerivativesWrtCurve stored at index i</returns>
    public DerivativesWrtCurve GetDerivatives(int i)
    {
      return derivativesBin_[i];
    }
    /// <summary>
    /// Access the derivatives by curve name
    /// </summary>
    /// <param name="curveName">Identifier</param>
    /// <returns>DerivativesWrtCurve object</returns>
    public DerivativesWrtCurve GetDerivatives(string curveName)
    {
      int pos = 0;
      try
      {
        pos = map_[curveName];
      }
      catch (Exception)
      {
        throw new ArgumentException("Curve {0} not found in the current collection", curveName);
      }
      return derivativesBin_[pos];
    }

    /// <summary>
    /// Add a set of curve derivatives to the collection
    /// </summary>
    /// <param name="derivativesWrtCurve">DerivativesWrtCuve object</param>
    public void Add(DerivativesWrtCurve derivativesWrtCurve)
    {
      if (derivativesWrtCurve.ReferenceCurve.Name == "")
        derivativesWrtCurve.ReferenceCurve.Name = this.Name + "." + index_.ToString();
      if(map_.ContainsKey(derivativesWrtCurve.ReferenceCurve.Name))
      {
        logger.DebugFormat("A curve with name {0} is already present in the basket. Possible naming conflict. Sensitivities of duplicate curve have not been stored",
                          derivativesWrtCurve.ReferenceCurve.Name);
        return;
      }
      derivativesBin_.Add(derivativesWrtCurve);
      map_.Add(derivativesWrtCurve.ReferenceCurve.Name, index_);
      index_++;
    }
    #endregion
  }
}
