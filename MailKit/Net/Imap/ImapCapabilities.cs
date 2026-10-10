//
// ImapCapabilities.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Collections;
using System.Collections.Generic;

namespace MailKit.Net.Imap {
	/// <summary>
	/// The set of capabilities supported by an IMAP server.
	/// </summary>
	/// <remarks>
	/// <para>Capabilities are read as part of the response to the <c>CAPABILITY</c> command that is
	/// issued during the connection and authentication phases of the
	/// <see cref="ImapClient"/>.</para>
	/// <para>The capabilities can be accessed in two ways:</para>
	/// <list type="bullet">
	/// <item><description>As a set of well-known <see cref="ImapCapability"/> values that MailKit
	/// understands and makes use of. This is the recommended way of checking whether the server
	/// supports a particular feature (see
	/// <see cref="Contains(ImapCapability)"/>).</description></item>
	/// <item><description>As the raw list of capability atoms that the server advertised, including
	/// any that MailKit does not know about (see <see cref="Names"/>,
	/// <see cref="Contains(string)"/> and <see cref="GetValues(string)"/>). This is useful for
	/// checking support for extensions that do not (yet) have a corresponding
	/// <see cref="ImapCapability"/> value.</description></item>
	/// </list>
	/// <note type="note">The set of <see cref="ImapCapability"/> values is not necessarily
	/// identical to what the server advertised. For example, servers that advertise
	/// <c>IMAP4rev2</c> are assumed to support a number of extensions that are part of the base
	/// IMAP4rev2 specification even if they are not advertised individually. Capabilities may also
	/// be disabled (see <see cref="Disable(ImapCapability)"/>) in order to prevent MailKit from
	/// using them, but disabling a capability does not affect the raw list of capability atoms
	/// advertised by the server.</note>
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	public sealed class ImapCapabilities : IReadOnlyCollection<ImapCapability>
	{
		// Note: ImapCapability values must be sequential, starting at 0.
		static readonly int CapabilityCount = Enum.GetNames (typeof (ImapCapability)).Length;

		readonly CapabilityNameTable table = new CapabilityNameTable ();
		readonly ulong[] bits = new ulong[(CapabilityCount + 63) / 64];
		int count;

		internal ImapCapabilities ()
		{
		}

		/// <summary>
		/// Initialize a new instance of the <see cref="ImapCapabilities"/> class.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new set of capabilities.</para>
		/// <para>This constructor is mostly useful for mocking an <see cref="IImapClient"/>.</para>
		/// </remarks>
		/// <param name="capabilities">The well-known capabilities.</param>
		/// <param name="names">The raw capability atoms advertised by the server (e.g.
		/// <c>"IMAP4rev1"</c> or <c>"AUTH=PLAIN"</c>).</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="capabilities"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="capabilities"/> contains an invalid <see cref="ImapCapability"/> value.
		/// </exception>
		public ImapCapabilities (IEnumerable<ImapCapability> capabilities, IEnumerable<string>? names = null)
		{
			if (capabilities is null)
				throw new ArgumentNullException (nameof (capabilities));

			foreach (var capability in capabilities) {
				if ((uint) capability >= CapabilityCount)
					throw new ArgumentOutOfRangeException (nameof (capabilities));

				Add (capability);
			}

			if (names != null) {
				foreach (var name in names) {
					if (name != null)
						AddName (name);
				}
			}
		}

		/// <summary>
		/// Get the number of well-known capabilities in the set.
		/// </summary>
		/// <remarks>
		/// Gets the number of well-known <see cref="ImapCapability"/> values in the set.
		/// </remarks>
		/// <value>The number of well-known capabilities.</value>
		public int Count {
			get { return count; }
		}

		/// <summary>
		/// Get the raw capability atoms advertised by the server.
		/// </summary>
		/// <remarks>
		/// <para>Gets the raw capability atoms advertised by the server, in the order that they
		/// were advertised (e.g. <c>"IMAP4rev1"</c>, <c>"SORT=DISPLAY"</c> or
		/// <c>"AUTH=PLAIN"</c>).</para>
		/// <para>This list includes capability atoms that MailKit does not know about and is not
		/// affected by <see cref="Disable(ImapCapability)"/>.</para>
		/// </remarks>
		/// <value>The raw capability atoms.</value>
		public IReadOnlyList<string> Names {
			get { return table.Names; }
		}

		/// <summary>
		/// Check whether the set contains the specified well-known capability.
		/// </summary>
		/// <remarks>
		/// Checks whether the set contains the specified well-known capability.
		/// </remarks>
		/// <returns><see langword="true" /> if the set contains the capability; otherwise,
		/// <see langword="false" />.</returns>
		/// <param name="capability">The capability.</param>
		public bool Contains (ImapCapability capability)
		{
			var index = (uint) capability;

			if (index >= CapabilityCount)
				return false;

			return (bits[index >> 6] & (1UL << (int) (index & 63))) != 0;
		}

		/// <summary>
		/// Check whether the server advertised the specified capability atom.
		/// </summary>
		/// <remarks>
		/// <para>Checks whether the server advertised the specified capability atom, using a
		/// case-insensitive comparison.</para>
		/// <para>The <paramref name="name"/> must match the entire capability atom, so atoms of the
		/// form <c>NAME=VALUE</c> such as <c>"SORT=DISPLAY"</c> must be specified in full. To get
		/// all of the values advertised for a particular key, use <see cref="GetValues(string)"/>
		/// instead.</para>
		/// <para>This method is not affected by <see cref="Disable(ImapCapability)"/>.</para>
		/// </remarks>
		/// <returns><see langword="true" /> if the server advertised the capability atom;
		/// otherwise, <see langword="false" />.</returns>
		/// <param name="name">The capability atom (e.g. <c>"OBJECTID"</c> or
		/// <c>"SORT=DISPLAY"</c>).</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/>.
		/// </exception>
		public bool Contains (string name)
		{
			if (name is null)
				throw new ArgumentNullException (nameof (name));

			return table.Contains (name);
		}

		/// <summary>
		/// Get the values advertised for the specified capability atom.
		/// </summary>
		/// <remarks>
		/// <para>Gets the values of all of the capability atoms of the form <c>KEY=VALUE</c>
		/// advertised by the server for the specified <paramref name="key"/>. For example, if the
		/// server advertised <c>"AUTH=PLAIN AUTH=XOAUTH2"</c>, then <c>GetValues ("AUTH")</c> would
		/// return <c>"PLAIN"</c> and <c>"XOAUTH2"</c>.</para>
		/// <para>This method is not affected by <see cref="Disable(ImapCapability)"/>.</para>
		/// </remarks>
		/// <returns>The values advertised for the capability atom or an empty list if there were
		/// none.</returns>
		/// <param name="key">The capability atom key (e.g. <c>"AUTH"</c>, <c>"THREAD"</c> or
		/// <c>"RIGHTS"</c>).</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="key"/> is <see langword="null"/>.
		/// </exception>
		public IReadOnlyList<string> GetValues (string key)
		{
			if (key is null)
				throw new ArgumentNullException (nameof (key));

			return table.GetValues (key);
		}

		/// <summary>
		/// Disable the specified well-known capability.
		/// </summary>
		/// <remarks>
		/// <para>Disables the specified well-known capability by removing it from the set,
		/// preventing MailKit from making use of the corresponding feature.</para>
		/// <para>Capabilities can only be disabled, they cannot be enabled. Disabling a capability
		/// does not affect the raw list of capability atoms advertised by the server (see
		/// <see cref="Names"/>).</para>
		/// <note type="note">The capabilities are reset each time they are re-read from the
		/// server.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if the capability was disabled; otherwise,
		/// <see langword="false" /> if it was not supported or had already been disabled.</returns>
		/// <param name="capability">The capability to disable.</param>
		public bool Disable (ImapCapability capability)
		{
			var index = (uint) capability;

			if (index >= CapabilityCount)
				return false;

			ulong mask = 1UL << (int) (index & 63);

			if ((bits[index >> 6] & mask) == 0)
				return false;

			bits[index >> 6] &= ~mask;
			count--;

			return true;
		}

		internal bool Add (ImapCapability capability)
		{
			var index = (uint) capability;
			ulong mask = 1UL << (int) (index & 63);

			if ((bits[index >> 6] & mask) != 0)
				return false;

			bits[index >> 6] |= mask;
			count++;

			return true;
		}

		internal void AddName (string name)
		{
			table.AddAtom (name);
		}

		internal void Clear ()
		{
			Array.Clear (bits, 0, bits.Length);
			table.Clear ();
			count = 0;
		}

		/// <summary>
		/// Get an enumerator for the well-known capabilities in the set.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the well-known capabilities in the set.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<ImapCapability> GetEnumerator ()
		{
			for (int i = 0; i < CapabilityCount; i++) {
				if ((bits[i >> 6] & (1UL << (i & 63))) != 0)
					yield return (ImapCapability) i;
			}
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		/// <summary>
		/// Get a string representation of the well-known capabilities in the set.
		/// </summary>
		/// <remarks>
		/// <para>Gets a comma-separated list of the well-known capabilities in the set (e.g.
		/// <c>"IMAP4rev1, Idle"</c>).</para>
		/// <para>This method is only meant to be a useful aid in debugging. The format of the
		/// string is not guaranteed to remain the same between releases and is not meant to be
		/// parsed, nor does it include the raw capability atoms advertised by the server (see
		/// <see cref="Names"/>).</para>
		/// </remarks>
		/// <returns>A string representation of the well-known capabilities.</returns>
		public override string ToString ()
		{
			return string.Join (", ", this);
		}
	}
}
