//
// AccessControlList.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (www.xamarin.com)
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
using System.Linq;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// An Access Control List (ACL)
	/// </summary>
	/// <remarks>
	/// An Access Control List (ACL) is a set of permissions available for a particular user,
	/// controlling whether or not that person has the ability to perform various tasks.
	/// </remarks>
	public class AccessControlList
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControlList"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControlList"/> with the given name and
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		public AccessControlList (string name, IEnumerable<AccessRight> rights)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			Rights = new List<AccessRight> (rights);
			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControlList"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControlList"/> with the given name and
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		public AccessControlList (string name, string rights)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			Rights = new List<AccessRight> (rights.Select (c => new AccessRight (c)));
			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControlList"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControlList"/> with the given name and no
		/// access rights.
		/// </remarks>
		/// <param name="name">The identifier name.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		public AccessControlList (string name)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			Rights = new List<AccessRight> ();
			Name = name;
		}

		/// <summary>
		/// The identifier name for the ACL entry.
		/// </summary>
		/// <remarks>
		/// The identifier name for the ACL entry.
		/// </remarks>
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
		/// <value>The access rights.</value>
		public IList<AccessRight> Rights {
			get; private set;
		}
	}
}
