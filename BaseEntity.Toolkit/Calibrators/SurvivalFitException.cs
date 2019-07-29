/*
 * SurvivalFitException.cs
 *
 *
 */

using System;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Calibrators
{
  ///<summary>
  ///  Exception class for SurvivalFitCalibrator.
  ///</summary>
  ///<remarks>
  ///  <para>Adds contextual information to failure-to-fit when bumping curves.</para>
  ///</remarks>
  [Serializable]
  public class SurvivalFitException : ToolkitException
  {
    #region Constructors

    ///<summary>
    ///  Constructor
    ///</summary>
    ///<remarks>
    ///  <para>Need to track which curve and tenor fail to fit for more robust code options during scaling.</para>
    ///</remarks>
    ///<param name = "message">Message</param>
    ///<param name = "curveName">Name of curve that failed to fit</param>
    ///<param name = "tenor">CurveTenor on curve that failed to fit</param>
    public SurvivalFitException(string message, string curveName, CurveTenor tenor) : base(message)
    {
      // Use properties for validation
      CurveName = curveName;
      Tenor = tenor;

      return;
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref = "SurvivalFitException" /> class.
    /// </summary>
    /// <param name = "info">The information.</param>
    /// <param name = "context">The context.</param>
    protected SurvivalFitException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
      tenor_ = (CurveTenor) info.GetValue("tenor_", typeof (CurveTenor));
      curveName_ = info.GetString("curveName_");
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    ///   Tenor of SurvivalCurve generating exception
    /// </summary>
    public CurveTenor Tenor
    {
      get { return tenor_; }
      set { tenor_ = value; }
    }

    /// <summary>
    ///   Name of SurvivalCurve generating exception
    /// </summary>
    public string CurveName
    {
      get { return curveName_; }
      set { curveName_ = value; }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    ///   When overridden in a derived class, sets the <see cref = "T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.
    /// </summary>
    /// <param name = "info">The <see cref = "T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
    /// <param name = "context">The <see cref = "T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
    /// <exception cref = "T:System.ArgumentNullException">The <paramref name = "info" /> parameter is a null reference (Nothing in Visual Basic). </exception>
    /// <PermissionSet>
    ///   <IPermission class = "System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version = "1" Read = "*AllFiles*" PathDiscovery = "*AllFiles*" />
    ///   <IPermission class = "System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version = "1" Flags = "SerializationFormatter" />
    /// </PermissionSet>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("tenor_", tenor_);
      info.AddValue("curveName_", curveName_);
    }

    #endregion

    #region Data

    private string curveName_;
    private CurveTenor tenor_;

    #endregion // Data
  }
}