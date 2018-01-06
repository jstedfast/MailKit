//
// AssemblyInfo.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle ("MailKit")]
[assembly: AssemblyDescription ("A cross-platform mail client library.")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Xamarin Inc.")]
[assembly: AssemblyProduct ("MailKit")]
[assembly: AssemblyCopyright ("Copyright © 2013-2018 Xamarin Inc. (www.xamarin.com)")]
[assembly: AssemblyTrademark ("Xamarin Inc.")]
[assembly: AssemblyCulture ("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible (true)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid ("2fe79b66-d107-45da-9493-175f59c4a53c")]
[assembly: InternalsVisibleTo ("UnitTests, PublicKey=002400000480000094000000060200" +
	"0000240000525341310004000011000000cde209732ce60a8fa70ee643cb32e9bf8149b61018c5" +
	"b166489b8a5cae44f1f88ca97ab9d9e035421933a6f0d556acc7c2219ae1464e35386ca1e239aa" +
	"42508b9edbb4164bfa82aa2a0f4cd983d9e5ba2acfe08a10a2093e2b2bf8408eef43114db89b39" +
	"99c59af1d3dc2c9f0cdbf51074e9a482cf09c9116ae1c5543ce8ff9b")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Micro Version
//      Build Number
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
//
// Note: AssemblyVersion is what the CLR matches against at runtime, so be careful
// about updating it. The AssemblyFileVersion is the official release version while
// the AssemblyInformationalVersion is just used as a display version.
//
// Based on my current understanding, AssemblyVersion is essentially the "API Version"
// and so should only be updated when the API changes. The other 2 Version attributes
// represent the "Release Version".
//
// Making releases:
//
// If any classes, methods, or enum values have been added, bump the Micro Version
//    in all version attributes and set the Build Number back to 0.
//
// If there have only been bug fixes, bump the Micro Version and/or the Build Number
//    in the AssemblyFileVersion attribute.
[assembly: AssemblyInformationalVersion ("2.0.1.0")]
[assembly: AssemblyFileVersion ("2.0.1.0")]
[assembly: AssemblyVersion ("2.0.0.0")]
