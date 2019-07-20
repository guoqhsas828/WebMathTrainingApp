using System.Reflection;
using System.Runtime.CompilerServices;

//
// In order to sign your assembly you must specify a key to use. Refer to the 
// Microsoft .NET Framework documentation for more information on assembly signing.
//
// Use the attributes below to control which key is used for signing. 
//
// Notes: 
//   (*) If no key is specified, the assembly is not signed.
//   (*) KeyName refers to a key that has been installed in the Crypto Service
//       Provider (CSP) on your machine. KeyFile refers to a file which contains
//       a key.
//   (*) If the KeyFile and the KeyName values are both specified, the 
//       following processing occurs:
//       (1) If the KeyName can be found in the CSP, that key is used.
//       (2) If the KeyName does not exist and the KeyFile does exist, the key 
//           in the KeyFile is installed into the CSP and used.
//   (*) In order to create a KeyFile, you can use the sn.exe (Strong Name) utility.
//       When specifying the KeyFile, the location of the KeyFile should be
//       relative to the project output directory which is
//       %Project Directory%\obj\<configuration>. For example, if your KeyFile is
//       located in the project directory, you would specify the AssemblyKeyFile 
//       attribute as [assembly: AssemblyKeyFile("..\\..\\mykey.snk")]
//   (*) Delay Signing is an advanced option - see the Microsoft .NET Framework
//       documentation for more information on this.
//
[assembly: AssemblyDelaySign(false)]

[assembly: AssemblyKeyFile("")]

[assembly: InternalsVisibleTo("WebMathTraining.Database.Test, PublicKey=0024000004800000940000000602000000240000525341310004000001000100573a023c5e8ea09bd08d366d1728ae3f9e68ee30bfb5ea986a5fa4494e23cd4461de251f5d49ef0bc8182182059076e5702097bf87212058093bd1acf3e2318f68be204ab1727614688bfec16e1f1f5777bc450bcfe305842c64d4ed42b9c3434c2d81911c1d41486b1fed8c3472270e38d5de251cdf5b5992960457f4b58bb6")]

