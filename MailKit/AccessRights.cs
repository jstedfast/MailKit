//
// AccessRights.cs
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
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A set of access rights.
	/// </summary>
	/// <remarks>
	/// The set of access rights for a particular identity.
	/// </remarks>
	public class AccessRights : HashSet<AccessRight>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of access rights.
		/// </remarks>
		/// <param name="rights">The access rights.</param>
		public AccessRights (IEnumerable<AccessRight> rights) : base (rights)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of access rights.
		/// </remarks>
		/// <param name="rights">The access rights.</param>
		public AccessRights (string rights)
		{
			AddRange (rights);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates an empty set of access rights.
		/// </remarks>
		public AccessRights ()
		{
		}

		/// <summary>
		/// Add the specified right.
		/// </summary>
		/// <remarks>
		/// Adds the right specified by the given character.
		/// </remarks>
		/// <param name="right">The right.</param>
		public void Add (char right)
		{
			Add (new AccessRight (right));
		}

		/// <summary>
		/// Add the rights specified by the characters in the given string.
		/// </summary>
		/// <remarks>
		/// Adds the rights specified by the characters in the given string.
		/// </remarks>
		/// <param name="rights">The rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="rights"/> is <c>null</c>.
		/// </exception>
		public void AddRange (string rights)
		{
			if (rights == null)
				throw new ArgumentNullException ("rights");

			for (int i = 0; i < rights.Length; i++)
				Add (new AccessRight (rights[i]));
		}

		/// <summary>
		/// Return a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.</returns>
		public override string ToString ()
		{
			var rights = new char[Count];
			int i = 0;

			foreach (var right in this)
				rights[i++] = right.Right;

			return new string (rights);
		}
	}
}
