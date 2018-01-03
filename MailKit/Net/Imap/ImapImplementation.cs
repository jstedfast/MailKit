//
// ImapImplementation.cs
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

using System.Collections.Generic;

namespace MailKit.Net.Imap {
	/// <summary>
	/// The details of an IMAP client or server implementation.
	/// </summary>
	/// <remarks>
	/// Allows an IMAP client and server to share their implementation details
	/// with each other for the purposes of debugging.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	public class ImapImplementation
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapImplementation"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapImplementation"/>.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		public ImapImplementation ()
		{
			Properties = new Dictionary<string, string> ();
		}

		string GetProperty (string property)
		{
			string value;

			Properties.TryGetValue (property, out value);

			return value;
		}

		/// <summary>
		/// Get the identification properties.
		/// </summary>
		/// <remarks>
		/// Gets the dictionary of raw identification properties.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The properties.</value>
		public Dictionary<string, string> Properties {
			get; private set;
		}

		/// <summary>
		/// Get or set the name of the program.
		/// </summary>
		/// <remarks>
		/// Gets or sets the name of the program.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The program name.</value>
		public string Name {
			get { return GetProperty ("name"); }
			set { Properties["name"] = value; }
		}

		/// <summary>
		/// Get or set the version of the program.
		/// </summary>
		/// <remarks>
		/// Gets or sets the version of the program.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The program version.</value>
		public string Version {
			get { return GetProperty ("version"); }
			set { Properties["version"] = value; }
		}

		/// <summary>
		/// Get or set the name of the operating system.
		/// </summary>
		/// <remarks>
		/// Gets or sets the name of the operating system.
		/// </remarks>
		/// <value>The name of the operation system.</value>
		public string OS {
			get { return GetProperty ("os"); }
			set { Properties["os"] = value; }
		}

		/// <summary>
		/// Get or set the version of the operating system.
		/// </summary>
		/// <remarks>
		/// Gets or sets the version of the operating system.
		/// </remarks>
		/// <value>The version of the operation system.</value>
		public string OSVersion {
			get { return GetProperty ("os-version"); }
			set { Properties["os-version"] = value; }
		}

		/// <summary>
		/// Get or set the name of the vendor.
		/// </summary>
		/// <remarks>
		/// Gets or sets the name of the vendor.
		/// </remarks>
		/// <value>The name of the vendor.</value>
		public string Vendor {
			get { return GetProperty ("vendor"); }
			set { Properties["vendor"] = value; }
		}

		/// <summary>
		/// Get or set the support URL.
		/// </summary>
		/// <remarks>
		/// Gets or sets the support URL.
		/// </remarks>
		/// <value>The support URL.</value>
		public string SupportUrl {
			get { return GetProperty ("support-url"); }
			set { Properties["support-url"] = value; }
		}

		/// <summary>
		/// Get or set the postal address of the vendor.
		/// </summary>
		/// <remarks>
		/// Gets or sets the postal address of the vendor.
		/// </remarks>
		/// <value>The postal address.</value>
		public string Address {
			get { return GetProperty ("address"); }
			set { Properties["address"] = value; }
		}

		/// <summary>
		/// Get or set the release date of the program.
		/// </summary>
		/// <remarks>
		/// Gets or sets the release date of the program.
		/// </remarks>
		/// <value>The release date.</value>
		public string ReleaseDate {
			get { return GetProperty ("date"); }
			set { Properties["date"] = value; }
		}

		/// <summary>
		/// Get or set the command used to start the program.
		/// </summary>
		/// <remarks>
		/// Gets or sets the command used to start the program.
		/// </remarks>
		/// <value>The command used to start the program.</value>
		public string Command {
			get { return GetProperty ("command"); }
			set { Properties["command"] = value; }
		}

		/// <summary>
		/// Get or set the command-line arguments used to start the program.
		/// </summary>
		/// <remarks>
		/// Gets or sets the command-line arguments used to start the program.
		/// </remarks>
		/// <value>The command-line arguments used to start the program.</value>
		public string Arguments {
			get { return GetProperty ("arguments"); }
			set { Properties["arguments"] = value; }
		}

		/// <summary>
		/// Get or set the environment variables available to the program.
		/// </summary>
		/// <remarks>
		/// Get or set the environment variables available to the program.
		/// </remarks>
		/// <value>The environment variables.</value>
		public string Environment {
			get { return GetProperty ("environment"); }
			set { Properties["environment"] = value; }
		}
	}
}
