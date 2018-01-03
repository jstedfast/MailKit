//
// AccessControl.cs
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

using System;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// An Access Control.
	/// </summary>
	/// <remarks>
	/// An Access Control is a set of permissions available for a particular identity,
	/// controlling whether or not that identity has the ability to perform various tasks.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	public class AccessControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControl"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControl"/> with the given name and
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rights"/> is <c>null</c>.</para>
		/// </exception>
		public AccessControl (string name, IEnumerable<AccessRight> rights)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			Rights = new AccessRights (rights);
			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControl"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControl"/> with the given name and
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rights"/> is <c>null</c>.</para>
		/// </exception>
		public AccessControl (string name, string rights)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			Rights = new AccessRights (rights);
			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControl"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControl"/> with the given name and no
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		public AccessControl (string name)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			Rights = new AccessRights ();
			Name = name;
		}

		/// <summary>
		/// The identifier name for the access control.
		/// </summary>
		/// <remarks>
		/// The identifier name for the access control.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The identifier name.</value>
		public string Name {
			get; private set;
		}

		/// <summary>
		/// Get the access rights.
		/// </summary>
		/// <remarks>
		/// Gets the access rights.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The access rights.</value>
		public AccessRights Rights {
			get; private set;
		}
	}
}
