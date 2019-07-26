/*
 * Delegates.cs
 *
 * Copyright (c)   2002-2008. All rights reserved.
 *
 */

// Defines for useful delegate types used by Swig interface

/// <exclude />
public delegate double Func_Double_Double(double x);

/// <exclude />
public delegate double Func_Double_int(int x);

/// <exclude />
public delegate double Double_Double_Fn( double x, out string exceptMessage );

/// <exclude />
public delegate void Void_Double_Fn( double x );

/// <exclude />
public delegate int Int_Void_Fn();

/// <exclude />
public delegate double Double_Int_Fn(int i);

/// <exclude />
unsafe public delegate double Double_Vector_Fn( double* x );

/// <exclude />
unsafe public delegate void Void_Vector_Vector_Fn( double* x, double* g );

/// <exclude />
unsafe public delegate void Void_Vector_Vector_Vector_Fn(double* x, double* f, double* g);

#if TBD
typedef void (CALLBACK *CRKEvaluate)( double, std::vector< std::complex<double> > &, std::vector< std::complex<double> > & );
typedef void (CALLBACK *RKEvaluate)( double, std::vector<double> &, std::vector<double> & );
typedef double (CALLBACK *OptimizerEvaluate)( std::vector<double> & );
typedef void (CALLBACK *OptimizerDerivative)( std::vector<double> &, std::vector<double> & );
#endif
