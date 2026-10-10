//
// CapabilityNameTable.cs
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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MailKit.Net {
	/// <summary>
	/// A table of the raw capability names (and their values) advertised by a server.
	/// </summary>
	/// <remarks>
	/// Names are compared case-insensitively and are kept in the order that the server advertised
	/// them.
	/// </remarks>
	sealed class CapabilityNameTable
	{
		static readonly ReadOnlyCollection<string> Empty = new ReadOnlyCollection<string> (Array.Empty<string> ());

		readonly Dictionary<string, List<string>> values = new Dictionary<string, List<string>> (StringComparer.OrdinalIgnoreCase);
		readonly HashSet<string> set = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		readonly List<string> names = new List<string> ();

		public CapabilityNameTable ()
		{
			Names = names.AsReadOnly ();
		}

		public ReadOnlyCollection<string> Names {
			get;
		}

		public void Add (string name)
		{
			if (set.Add (name))
				names.Add (name);
		}

		public void AddValue (string key, string value)
		{
			if (!values.TryGetValue (key, out var list)) {
				list = new List<string> ();
				values.Add (key, list);
			}

			foreach (var item in list) {
				if (item.Equals (value, StringComparison.OrdinalIgnoreCase))
					return;
			}

			list.Add (value);
		}

		/// <summary>
		/// Add a capability atom of the form <c>NAME</c> or <c>NAME=VALUE</c> (e.g. IMAP).
		/// </summary>
		public void AddAtom (string atom)
		{
			if (atom.Length == 0)
				return;

			Add (atom);

			int index = atom.IndexOf ('=');

			if (index > 0 && index + 1 < atom.Length)
				AddValue (atom.Substring (0, index), atom.Substring (index + 1));
		}

		/// <summary>
		/// Add a capability line of the form <c>KEYWORD [PARAM ...]</c> (e.g. SMTP and POP3).
		/// </summary>
		public void AddKeyword (string line)
		{
			int index = 0;

			while (index < line.Length && char.IsWhiteSpace (line[index]))
				index++;

			int startIndex = index;

			while (index < line.Length && !char.IsWhiteSpace (line[index]) && line[index] != '=')
				index++;

			if (index == startIndex)
				return;

			var keyword = line.Substring (startIndex, index - startIndex);

			Add (keyword);

			if (index < line.Length && line[index] == '=')
				index++;

			while (index < line.Length) {
				while (index < line.Length && char.IsWhiteSpace (line[index]))
					index++;

				startIndex = index;

				while (index < line.Length && !char.IsWhiteSpace (line[index]))
					index++;

				if (index > startIndex)
					AddValue (keyword, line.Substring (startIndex, index - startIndex));
			}
		}

		public bool Contains (string name)
		{
			return set.Contains (name);
		}

		public IReadOnlyList<string> GetValues (string key)
		{
			if (values.TryGetValue (key, out var list))
				return list.AsReadOnly ();

			return Empty;
		}

		public void Clear ()
		{
			values.Clear ();
			names.Clear ();
			set.Clear ();
		}
	}
}
