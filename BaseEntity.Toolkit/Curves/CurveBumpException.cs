/*
 * CurveBumpException.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  ///
  /// <summary>
	///   Exception class for fitted curves.
	/// </summary>
	///
	/// <remarks>
	///   <para>Adds contextual information to failure-to-fit when bumping curves.</para>
	/// </remarks>
	///
	[Serializable]
  public class CurveBumpException : ToolkitException
  {
		#region Constructors
		/// <summary>
		///   Constructor
		/// </summary>
	  ///
	  /// <remarks>
		///   <para>Need to track which curve and tenor fail to fit for more robust code options during scaling.</para>
		/// </remarks>
		///
    /// <param name="message">Message.</param>
		/// <param name="curveIndex">Index of curve that failed to fit.</param>
    /// <param name="curveName">Curve name.</param>
		/// <param name="tenor">CurveTenor that failed to fit.</param>
    /// <param name="innerException">Inner exception.</param>
    ///
    public CurveBumpException(string message, int curveIndex,
      string curveName, CurveTenor tenor, Exception innerException)
      : base(message, innerException)
    {
      // Use properties for validation
      tenor_ = tenor;
      curveIndex_ = curveIndex;
      name_ = curveName;
      return;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurveBumpException"/> class.
    /// </summary>
    /// <param name="info">The information.</param>
    /// <param name="context">The context.</param>
    protected CurveBumpException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
      tenor_ = (CurveTenor)info.GetValue("tenor_", typeof(CurveTenor));
      curveIndex_ = info.GetInt32("curveIndex_");
      name_ = info.GetString("name_");
    }
    #endregion // Constructors

		#region Properties

    /// <summary>
    ///   Curve tenor
    /// </summary>
		public CurveTenor Tenor
		{
			get { return tenor_; }
		}

    /// <summary>
    ///   Curve index
    /// </summary>
		public int CurveIndex
		{
			get { return curveIndex_; }
		}

    /// <summary>
    ///   Curve Name
    /// </summary>
    public string CurveName
    {
      get { return name_; }
    }
    #endregion // Properties

    #region Methods
    /// <summary>
    /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with information about the exception.
    /// </summary>
    /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference (Nothing in Visual Basic). </exception>
    ///   
    /// <PermissionSet>
    ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/>
    ///   <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/>
    ///   </PermissionSet>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("tenor_", tenor_);
      info.AddValue("curveIndex_", curveIndex_);
      info.AddValue("name_", name_);
    }
    #endregion

    #region Data

    private CurveTenor tenor_;
		private int curveIndex_;
    private string name_;
    #endregion // Data


	} // class CurveBumpException

}
