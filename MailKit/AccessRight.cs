//
// AccessRight.cs
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

namespace MailKit {
	/// <summary>
	/// An individual Access Right to be used with ACLs.
	/// </summary>
	/// <remarks>
	/// <para>An individual Access Right meant to be used with
	/// <see cref="AccessControlList"/>.</para>
	/// <para>For more information on what rights are available,
	/// see https://tools.ietf.org/html/rfc4314#section-2.1
	/// </para>
	/// </remarks>
	public struct AccessRight : IEquatable<AccessRight>
	{
		/// <summary>
		/// The access right for folder lookups.
		/// </summary>
		/// <remarks>
		/// Allows the <see cref="MailKit.IMailFolder"/> to be visible when listing folders.
		/// </remarks>
		public static readonly AccessRight LookupFolder = new AccessRight ('l');

		/// <summary>
		/// The access right for opening a folder and getting the status.
		/// </summary>
		/// <remarks>
		/// Provides access for opening and getting the status of the folder.
		/// </remarks>
		public static readonly AccessRight OpenFolder = new AccessRight ('r');

		/// <summary>
		/// The access right for adding or removing the Seen flag on messages in the folder.
		/// </summary>
		/// <remarks>
		/// Provides access to add or remove the <see cref="MessageFlags.Seen"/> flag on messages within the
		/// <see cref="MailKit.IMailFolder"/>.
		/// </remarks>
		public static readonly AccessRight SetMessageSeen = new AccessRight ('s');

		/// <summary>
		/// The access right for adding or removing flags (other than Seen and Deleted)
		/// on messages in a folder.
		/// </summary>
		/// <remarks>
		/// Provides access to add or remove the <see cref="MessageFlags"/> on messages
		/// (other than <see cref="MessageFlags.Seen"/> and
		/// <see cref="MessageFlags.Deleted"/>) within the folder.
		/// </remarks>
		public static readonly AccessRight SetMessageFlags = new AccessRight ('w');

		/// <summary>
		/// The access right allowing messages to be appended or copied into the folder.
		/// </summary>
		/// <remarks>
		/// Provides access to append or copy messages into the folder.
		/// </remarks>
		public static readonly AccessRight AppendMessages = new AccessRight ('i');

		/// <summary>
		/// The access right allowing subfolders to be created.
		/// </summary>
		/// <remarks>
		/// Provides access to create subfolders.
		/// </remarks>
		public static readonly AccessRight CreateFolder = new AccessRight ('k');

		/// <summary>
		/// The access right for deleting a folder and/or its subfolders.
		/// </summary>
		/// <remarks>
		/// Provides access to delete the folder and/or any subfolders.
		/// </remarks>
		public static readonly AccessRight DeleteFolder = new AccessRight ('x');

		/// <summary>
		/// The access right for adding or removing the Deleted flag to messages within a folder.
		/// </summary>
		/// <remarks>
		/// Provides access to add or remove the <see cref="MessageFlags.Deleted"/> flag from
		/// messages within the folder. It also provides access for setting the
		/// <see cref="MessageFlags.Deleted"/> flag when appending a message to a folder.
		/// </remarks>
		public static readonly AccessRight SetMessageDeleted = new AccessRight ('t');

		/// <summary>
		/// The access right for expunging deleted messages in a folder.
		/// </summary>
		/// <remarks>
		/// Provides access to expunge deleted messages in a folder.
		/// </remarks>
		public static readonly AccessRight ExpungeFolder = new AccessRight ('e');

		/// <summary>
		/// The access right for administering the ACLs of a folder.
		/// </summary>
		/// <remarks>
		/// Provides administrative access to change the ACLs for the folder.
		/// </remarks>
		public static readonly AccessRight Administer = new AccessRight ('a');

		/// <summary>
		/// The character representing the particular access right.
		/// </summary>
		/// <remarks>
		/// Represents the character value of the access right.
		/// </remarks>
		public readonly char Right;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRight"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessRight"/> struct.
		/// </remarks>
		/// <param name="right">The access right.</param>
		public AccessRight (char right)
		{
			Right = right;
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="MailKit.AccessRight"/> is equal to the current <see cref="MailKit.AccessRight"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="MailKit.AccessRight"/> is equal to the current <see cref="MailKit.AccessRight"/>.
		/// </remarks>
		/// <param name="other">The <see cref="MailKit.AccessRight"/> to compare with the current <see cref="MailKit.AccessRight"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="MailKit.AccessRight"/> is equal to the current
		/// <see cref="MailKit.AccessRight"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AccessRight other)
		{
			return other.Right == Right;
		}

		#endregion

		/// <summary>
		/// Determines whether two access rights are equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two access rights are equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="right1"/> and <paramref name="right2"/> are equal; otherwise, <c>false</c>.</returns>
		/// <param name="right1">The first access right to compare.</param>
		/// <param name="right2">The second access right to compare.</param>
		public static bool operator == (AccessRight right1, AccessRight right2)
		{
			return right1.Right == right2.Right;
		}

		/// <summary>
		/// Determines whether two access rights are not equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two access rights are not equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="right1"/> and <paramref name="right2"/> are not equal; otherwise, <c>false</c>.</returns>
		/// <param name="right1">The first access right to compare.</param>
		/// <param name="right2">The second access right to compare.</param>
		public static bool operator != (AccessRight right1, AccessRight right2)
		{
			return right1.Right != right2.Right;
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AccessRight"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AccessRight"/>.
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.AccessRight"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AccessRight"/>;
		/// otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return obj is AccessRight && ((AccessRight) obj).Right == Right;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.AccessRight"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.AccessRight"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Right.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRight"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRight"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRight"/>.</returns>
		public override string ToString ()
		{
			return Right.ToString ();
		}
	}
}
