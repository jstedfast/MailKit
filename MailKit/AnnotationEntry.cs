//
// Annotationentry.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
	/// An annotation entry.
	/// </summary>
	/// <remarks>
	/// <para>An annotation entry.</para>
	/// <para>For more information about annotations, see
	/// <a href="https://tools.ietf.org/html/rfc5257">rfc5257</a>.</para>
	/// </remarks>
	public class AnnotationEntry : IEquatable<AnnotationEntry>
	{
		/// <summary>
		/// An annotation entry for a comment on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set a comment on a message.
		/// </remarks>
		public static readonly AnnotationEntry Comment = new AnnotationEntry ("/comment", AnnotationScope.Both);

		/// <summary>
		/// An annotation entry for a private comment on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set a private comment on a message.
		/// </remarks>
		public static readonly AnnotationEntry PrivateComment = new AnnotationEntry ("/comment", AnnotationScope.Private);

		/// <summary>
		/// An annotation entry for a shared comment on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set a shared comment on a message.
		/// </remarks>
		public static readonly AnnotationEntry SharedComment = new AnnotationEntry ("/comment", AnnotationScope.Shared);

		/// <summary>
		/// An annotation entry for flags on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set flags on a message.
		/// </remarks>
		public static readonly AnnotationEntry Flags = new AnnotationEntry ("/flags", AnnotationScope.Both);

		/// <summary>
		/// An annotation entry for private flags on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set private flags on a message.
		/// </remarks>
		public static readonly AnnotationEntry PrivateFlags = new AnnotationEntry ("/flags", AnnotationScope.Private);

		/// <summary>
		/// Aa annotation entry for shared flags on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set shared flags on a message.
		/// </remarks>
		public static readonly AnnotationEntry SharedFlags = new AnnotationEntry ("/flags", AnnotationScope.Shared);

		/// <summary>
		/// An annotation entry for an alternate subject on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set an alternate subject on a message.
		/// </remarks>
		public static readonly AnnotationEntry AltSubject = new AnnotationEntry ("/altsubject", AnnotationScope.Both);

		/// <summary>
		/// An annotation entry for a private alternate subject on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set a private alternate subject on a message.
		/// </remarks>
		public static readonly AnnotationEntry PrivateAltSubject = new AnnotationEntry ("/altsubject", AnnotationScope.Private);

		/// <summary>
		/// An annotation entry for a shared alternate subject on a message.
		/// </summary>
		/// <remarks>
		/// Used to get or set a shared alternate subject on a message.
		/// </remarks>
		public static readonly AnnotationEntry SharedAltSubject = new AnnotationEntry ("/altsubject", AnnotationScope.Shared);

		static void ValidatePath (string path)
		{
			if (path == null)
				throw new ArgumentNullException (nameof (path));

			if (path.Length == 0)
				throw new ArgumentException ("Annotation entry paths cannot be empty.", nameof (path));

			if (path[0] != '/' && path[0] != '*' && path[0] != '%')
				throw new ArgumentException ("Annotation entry paths must begin with '/'.", nameof (path));

			if (path.Length > 1 && path[1] >= '0' && path[1] <= '9')
				throw new ArgumentException ("Annotation entry paths must not include a part-specifier.", nameof (path));

			if (path == "*" || path == "%")
				return;

			char pc = path[0];

			for (int i = 1; i < path.Length; i++) {
				char c = path[i];

				if (c > 127)
					throw new ArgumentException ($"Invalid character in annotation entry path: '{c}'.", nameof (path));

				if (c >= '0' && c <= '9' && pc == '/')
					throw new ArgumentException ("Invalid annotation entry path.", nameof (path));

				if ((pc == '/' || pc == '.') && (c == '/' || c == '.'))
					throw new ArgumentException ("Invalid annotation entry path.", nameof (path));

				pc = c;
			}

			int endIndex = path.Length - 1;

			if (path[endIndex] == '/')
				throw new ArgumentException ("Annotation entry paths must not end with '/'.", nameof (path));

			if (path[endIndex] == '.')
				throw new ArgumentException ("Annotation entry paths must not end with '.'.", nameof (path));
		}

		static void ValidatePartSpecifier (string partSpecifier)
		{
			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			char pc = '\0';

			for (int i = 0; i < partSpecifier.Length; i++) {
				char c = partSpecifier[i];

				if (!((c >= '0' && c <= '9') || c == '.') || (c == '.' && (pc == '.' || pc == '\0')))
					throw new ArgumentException ("Invalid part-specifier.", nameof (partSpecifier));

				pc = c;
			}

			if (pc == '.')
				throw new ArgumentException ("Invalid part-specifier.", nameof (partSpecifier));
		}

		AnnotationEntry ()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AnnotationEntry"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationEntry"/>.
		/// </remarks>
		/// <param name="path">The annotation entry path.</param>
		/// <param name="scope">The scope of the annotation.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="path"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="path"/> is invalid.
		/// </exception>
		public AnnotationEntry (string path, AnnotationScope scope = AnnotationScope.Both)
		{
			ValidatePath (path);

			switch (scope) {
			case AnnotationScope.Private: Entry = path + ".priv"; break;
			case AnnotationScope.Shared: Entry = path + ".shared"; break;
			default: Entry = path; break;
			}
			PartSpecifier = null;
			Path = path;
			Scope = scope;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AnnotationEntry"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationEntry"/> for an individual body part of a message.
		/// </remarks>
		/// <param name="partSpecifier">The part-specifier of the body part of the message.</param>
		/// <param name="path">The annotation entry path.</param>
		/// <param name="scope">The scope of the annotation.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="partSpecifier"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="path"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="partSpecifier"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="path"/> is invalid.</para>
		/// </exception>
		public AnnotationEntry (string partSpecifier, string path, AnnotationScope scope = AnnotationScope.Both)
		{
			ValidatePartSpecifier (partSpecifier);
			ValidatePath (path);

			switch (scope) {
			case AnnotationScope.Private: Entry = string.Format ("/{0}{1}.priv", partSpecifier, path); break;
			case AnnotationScope.Shared: Entry = string.Format ("/{0}{1}.shared", partSpecifier, path); break;
			default: Entry = string.Format ("/{0}{1}", partSpecifier, path); break;
			}
			PartSpecifier = partSpecifier;
			Path = path;
			Scope = scope;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AnnotationEntry"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationEntry"/> for an individual body part of a message.
		/// </remarks>
		/// <param name="part">The body part of the message.</param>
		/// <param name="path">The annotation entry path.</param>
		/// <param name="scope">The scope of the annotation.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="part"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="path"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="path"/> is invalid.
		/// </exception>
		public AnnotationEntry (BodyPart part, string path, AnnotationScope scope = AnnotationScope.Both)
		{
			if (part == null)
				throw new ArgumentNullException (nameof (part));

			ValidatePath (path);

			switch (scope) {
			case AnnotationScope.Private: Entry = string.Format ("/{0}{1}.priv", part.PartSpecifier, path); break;
			case AnnotationScope.Shared: Entry = string.Format ("/{0}{1}.shared", part.PartSpecifier, path); break;
			default: Entry = string.Format ("/{0}{1}", part.PartSpecifier, path); break;
			}
			PartSpecifier = part.PartSpecifier;
			Path = path;
			Scope = scope;
		}

		/// <summary>
		/// Get the annotation entry specifier.
		/// </summary>
		/// <remarks>
		/// Gets the annotation entry specifier.
		/// </remarks>
		/// <value>The annotation entry specifier.</value>
		public string Entry {
			get; private set;
		}

		/// <summary>
		/// Get the part-specifier component of the annotation entry.
		/// </summary>
		/// <remarks>
		/// Gets the part-specifier component of the annotation entry.
		/// </remarks>
		public string PartSpecifier {
			get; private set;
		}

		/// <summary>
		/// Get the path component of the annotation entry.
		/// </summary>
		/// <remarks>
		/// Gets the path component of the annotation entry.
		/// </remarks>
		public string Path {
			get; private set;
		}

		/// <summary>
		/// Get the scope of the annotation.
		/// </summary>
		/// <remarks>
		/// Gets the scope of the annotation.
		/// </remarks>
		public AnnotationScope Scope {
			get; private set;
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="MailKit.AnnotationEntry"/> is equal to the current <see cref="MailKit.AnnotationEntry"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="MailKit.AnnotationEntry"/> is equal to the current <see cref="MailKit.AnnotationEntry"/>.
		/// </remarks>
		/// <param name="other">The <see cref="MailKit.AnnotationEntry"/> to compare with the current <see cref="MailKit.AnnotationEntry"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="MailKit.AnnotationEntry"/> is equal to the current
		/// <see cref="MailKit.AnnotationEntry"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AnnotationEntry other)
		{
			return other?.Entry == Entry;
		}

		#endregion

		/// <summary>
		/// Determines whether two annotation entries are equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two annotation entries are equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="entry1"/> and <paramref name="entry2"/> are equal; otherwise, <c>false</c>.</returns>
		/// <param name="entry1">The first annotation entry to compare.</param>
		/// <param name="entry2">The second annotation entry to compare.</param>
		public static bool operator == (AnnotationEntry entry1, AnnotationEntry entry2)
		{
			return entry1?.Entry == entry2?.Entry;
		}

		/// <summary>
		/// Determines whether two annotation entries are not equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two annotation entries are not equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="entry1"/> and <paramref name="entry2"/> are not equal; otherwise, <c>false</c>.</returns>
		/// <param name="entry1">The first annotation entry to compare.</param>
		/// <param name="entry2">The second annotation entry to compare.</param>
		public static bool operator != (AnnotationEntry entry1, AnnotationEntry entry2)
		{
			return entry1?.Entry != entry2?.Entry;
		}

		/// <summary>
		/// Determine whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AnnotationEntry"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AnnotationEntry"/>.
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.AnnotationEntry"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
		/// <see cref="MailKit.AnnotationEntry"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return obj is AnnotationEntry && ((AnnotationEntry) obj).Entry == Entry;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.AnnotationEntry"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.AnnotationEntry"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Entry.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationEntry"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationEntry"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationEntry"/>.</returns>
		public override string ToString ()
		{
			return Entry;
		}

		/// <summary>
		/// Parse an annotation entry.
		/// </summary>
		/// <remarks>
		/// Parses an annotation entry.
		/// </remarks>
		/// <param name="entry">The annotation entry.</param>
		/// <returns>The parsed annotation entry.</returns>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="entry"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.FormatException">
		/// <paramref name="entry"/> does not conform to the annotation entry syntax.
		/// </exception>
		public static AnnotationEntry Parse (string entry)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			if (entry.Length == 0)
				throw new FormatException ("An annotation entry cannot be empty.");

			if (entry[0] != '/' && entry[0] != '*' && entry[0] != '%')
				throw new FormatException ("An annotation entry must begin with a '/' character.");

			var scope = AnnotationScope.Both;
			int startIndex = 0, endIndex;
			string partSpecifier = null;
			var component = 0;
			var pc = entry[0];
			string path;

			for (int i = 1; i < entry.Length; i++) {
				char c = entry[i];

				if (c >= '0' && c <= '9' && pc == '/') {
					if (component > 0)
						throw new FormatException ("Invalid annotation entry.");

					startIndex = i;
					endIndex = i + 1;
					pc = c;

					while (endIndex < entry.Length) {
						c = entry[endIndex];

						if (c == '/') {
							if (pc == '.')
								throw new FormatException ("Invalid part-specifier in annotation entry.");

							break;
						}

						if (!(c >= '0' && c <= '9') && c != '.')
							throw new FormatException ($"Invalid character in part-specifier: '{c}'.");

						if (c == '.' && pc == '.')
							throw new FormatException ("Invalid part-specifier in annotation entry.");

						endIndex++;
						pc = c;
					}

					if (endIndex >= entry.Length)
						throw new FormatException ("Incomplete part-specifier in annotation entry.");

					partSpecifier = entry.Substring (startIndex, endIndex - startIndex);
					i = startIndex = endIndex;
					component++;
				} else if (c == '/' || c == '.') {
					if (pc == '/' || pc == '.')
						throw new FormatException ("Invalid annotation entry path.");

					if (c == '/')
						component++;
				} else if (c > 127) {
					throw new FormatException ($"Invalid character in annotation entry path: '{c}'.");
				}

				pc = c;
			}

			if (pc == '/' || pc == '.')
				throw new FormatException ("Invalid annotation entry path.");

			if (entry.EndsWith (".shared", StringComparison.Ordinal)) {
				endIndex = entry.Length - ".shared".Length;
				scope = AnnotationScope.Shared;
			} else if (entry.EndsWith (".priv", StringComparison.Ordinal)) {
				endIndex = entry.Length - ".priv".Length;
				scope = AnnotationScope.Private;
			} else {
				endIndex = entry.Length;
			}

			path = entry.Substring (startIndex, endIndex - startIndex);

			return new AnnotationEntry {
				PartSpecifier = partSpecifier,
				Entry = entry,
				Path = path,
				Scope = scope
			};
		}

		internal static AnnotationEntry Create (string entry)
		{
			switch (entry) {
			case "/comment": return Comment;
			case "/comment.priv": return PrivateComment;
			case "/comment.shared": return SharedComment;
			case "/flags": return Flags;
			case "/flags.priv": return PrivateFlags;
			case "/flags.shared": return SharedFlags;
			case "/altsubject": return AltSubject;
			case "/altsubject.priv": return PrivateAltSubject;
			case "/altsubject.shared": return SharedAltSubject;
			default: return Parse (entry);
			}
		}
	}
}
