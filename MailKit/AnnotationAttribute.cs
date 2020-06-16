//
// AnnotationAttribute.cs
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
	/// An annotation attribute.
	/// </summary>
	/// <remarks>
	/// <para>An annotation attribute.</para>
	/// <para>For more information about annotations, see
	/// <a href="https://tools.ietf.org/html/rfc5257">rfc5257</a>.</para>
	/// </remarks>
	public class AnnotationAttribute : IEquatable<AnnotationAttribute>
	{
		static readonly char[] Wildcards = { '*', '%' };

		/// <summary>
		/// The annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get or set both the private and shared values of an annotation.
		/// </remarks>
		public static readonly AnnotationAttribute Value = new AnnotationAttribute ("value", AnnotationScope.Both);

		/// <summary>
		/// The shared annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get or set the shared value of an annotation.
		/// </remarks>
		public static readonly AnnotationAttribute SharedValue = new AnnotationAttribute ("value", AnnotationScope.Shared);

		/// <summary>
		/// The private annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get or set the private value of an annotation.
		/// </remarks>
		public static readonly AnnotationAttribute PrivateValue = new AnnotationAttribute ("value", AnnotationScope.Private);

		/// <summary>
		/// The size of an annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get the size of the both the private and shared annotation values.
		/// </remarks>
		public static readonly AnnotationAttribute Size = new AnnotationAttribute ("size", AnnotationScope.Both);

		/// <summary>
		/// The size of a shared annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get the size of a shared annotation value.
		/// </remarks>
		public static readonly AnnotationAttribute SharedSize = new AnnotationAttribute ("size", AnnotationScope.Shared);

		/// <summary>
		/// The size of a private annotation value.
		/// </summary>
		/// <remarks>
		/// Used to get the size of a private annotation value.
		/// </remarks>
		public static readonly AnnotationAttribute PrivateSize = new AnnotationAttribute ("size", AnnotationScope.Private);

		AnnotationAttribute (string name, AnnotationScope scope)
		{
			switch (scope) {
			case AnnotationScope.Shared: Specifier = string.Format ("{0}.shared", name); break;
			case AnnotationScope.Private: Specifier = string.Format ("{0}.priv", name); break;
			default: Specifier = name; break;
			}
			Scope = scope;
			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AnnotationAttribute"/> class.
		/// </summary>
		/// <param name="specifier">The annotation attribute specifier.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="specifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="specifier"/> contains illegal characters.
		/// </exception>
		public AnnotationAttribute (string specifier)
		{
			if (specifier == null)
				throw new ArgumentNullException (nameof (specifier));

			if (specifier.Length == 0)
				throw new ArgumentException ("Annotation attribute specifiers cannot be empty.", nameof (specifier));

			// TODO: improve validation
			if (specifier.IndexOfAny (Wildcards) != -1)
				throw new ArgumentException ("Annotation attribute specifiers cannot contain '*' or '%'.", nameof (specifier));

			Specifier = specifier;

			if (specifier.EndsWith (".shared", StringComparison.Ordinal)) {
				Name = specifier.Substring (0, specifier.Length - ".shared".Length);
				Scope = AnnotationScope.Shared;
			} else if (specifier.EndsWith (".priv", StringComparison.Ordinal)) {
				Name = specifier.Substring (0, specifier.Length - ".priv".Length);
				Scope = AnnotationScope.Private;
			} else {
				Scope = AnnotationScope.Both;
				Name = specifier;
			}
		}

		/// <summary>
		/// Get the name of the annotation attribute.
		/// </summary>
		/// <remarks>
		/// Gets the name of the annotation attribute.
		/// </remarks>
		public string Name {
			get; private set;
		}

		/// <summary>
		/// Get the scope of the annotation attribute.
		/// </summary>
		/// <remarks>
		/// Gets the scope of the annotation attribute.
		/// </remarks>
		public AnnotationScope Scope {
			get; private set;
		}

		/// <summary>
		/// Get the annotation attribute specifier.
		/// </summary>
		/// <remarks>
		/// Gets the annotation attribute specifier.
		/// </remarks>
		public string Specifier {
			get; private set;
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="MailKit.AnnotationAttribute"/> is equal to the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="MailKit.AnnotationAttribute"/> is equal to the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </remarks>
		/// <param name="other">The <see cref="MailKit.AnnotationAttribute"/> to compare with the current <see cref="MailKit.AnnotationAttribute"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="MailKit.AnnotationAttribute"/> is equal to the current
		/// <see cref="MailKit.AnnotationAttribute"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (AnnotationAttribute other)
		{
			return other?.Specifier == Specifier;
		}

		#endregion

		/// <summary>
		/// Determines whether two annotation attributes are equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two annotation attributes are equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="attr1"/> and <paramref name="attr2"/> are equal; otherwise, <c>false</c>.</returns>
		/// <param name="attr1">The first annotation attribute to compare.</param>
		/// <param name="attr2">The second annotation attribute to compare.</param>
		public static bool operator == (AnnotationAttribute attr1, AnnotationAttribute attr2)
		{
			return attr1?.Specifier == attr2?.Specifier;
		}

		/// <summary>
		/// Determines whether two annotation attributes are not equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two annotation attributes are not equal.
		/// </remarks>
		/// <returns><c>true</c> if <paramref name="attr1"/> and <paramref name="attr2"/> are not equal; otherwise, <c>false</c>.</returns>
		/// <param name="attr1">The first annotation attribute to compare.</param>
		/// <param name="attr2">The second annotation attribute to compare.</param>
		public static bool operator != (AnnotationAttribute attr1, AnnotationAttribute attr2)
		{
			return attr1?.Specifier != attr2?.Specifier;
		}

		/// <summary>
		/// Determine whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.AnnotationAttribute"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
		/// <see cref="MailKit.AnnotationAttribute"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return obj is AnnotationAttribute && ((AnnotationAttribute) obj).Specifier == Specifier;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.AnnotationAttribute"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.AnnotationAttribute"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Specifier.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationAttribute"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.AnnotationAttribute"/>.</returns>
		public override string ToString ()
		{
			return Specifier;
		}
	}
}
