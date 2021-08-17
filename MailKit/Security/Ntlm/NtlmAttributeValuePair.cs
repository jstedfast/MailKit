//
// NtlmAttributeValuePair.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-nlmp/b38c36ed-2804-4868-a9ff-8dd3182128e4

namespace MailKit.Security.Ntlm {
	/// <summary>
	/// An abstract NTLM attribute and value pair.
	/// </summary>
	/// <remarks>
	/// An abstract NTLM attribute and value pair.
	/// </remarks>
	abstract class NtlmAttributeValuePair
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		protected NtlmAttributeValuePair (NtlmAttribute attr)
		{
			Attribute = attr;
		}

		/// <summary>
		/// Get the NTLM attribute that this pair represents.
		/// </summary>
		/// <remarks>
		/// Gets the NTLM attribute that this pair represents.
		/// </remarks>
		/// <value>The NTLM attribute.</value>
		public NtlmAttribute Attribute {
			get; private set;
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a string value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a string value.
	/// </remarks>
	sealed class NtlmAttributeStringValuePair : NtlmAttributeValuePair
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeStringValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a string value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		public NtlmAttributeStringValuePair (NtlmAttribute attr, string value) : base (attr)
		{
			Value = value;
		}

		/// <summary>
		/// Get or set the value of the attribute.
		/// </summary>
		/// <remarks>
		/// Gets or sets the value of the attribute.
		/// </remarks>
		/// <value>The attribute value.</value>
		public string Value {
			get; set;
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a flags value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a flags value.
	/// </remarks>
	sealed class NtlmAttributeFlagsValuePair : NtlmAttributeValuePair
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeFlagsValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a flags value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		/// <param name="size">The size of the encoded flags value.</param>
		internal NtlmAttributeFlagsValuePair (NtlmAttribute attr, int value, short size) : base (attr)
		{
			Value = value;
			Size = size;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeFlagsValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a flags value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		public NtlmAttributeFlagsValuePair (NtlmAttribute attr, int value) : this (attr, value, 4)
		{
		}

		/// <summary>
		/// Get or set the size of the encoded flags value.
		/// </summary>
		/// <remarks>
		/// Gets or sets the size of the encoded flags value.
		/// </remarks>
		public short Size {
			get; internal set;
		}

		/// <summary>
		/// Get or set the value of the attribute.
		/// </summary>
		/// <remarks>
		/// Gets or sets the value of the attribute.
		/// </remarks>
		/// <value>The attribute value.</value>
		public int Value {
			get; set;
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a timestamp value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a timestamp value.
	/// </remarks>
	sealed class NtlmAttributeTimestampValuePair : NtlmAttributeValuePair
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeTimestampValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a timestamp value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		/// <param name="size">The size of the encoded flags value.</param>
		internal NtlmAttributeTimestampValuePair (NtlmAttribute attr, long value, short size) : base (attr)
		{
			Value = value;
			Size = size;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeTimestampValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a timestamp value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		public NtlmAttributeTimestampValuePair (NtlmAttribute attr, long value) : this (attr, value, 8)
		{
		}

		/// <summary>
		/// Get or set the size of the encoded timestamp value.
		/// </summary>
		/// <remarks>
		/// Gets or sets the size of the encoded timestamp value.
		/// </remarks>
		public short Size {
			get; internal set;
		}

		/// <summary>
		/// Get or set the value of the attribute.
		/// </summary>
		/// <remarks>
		/// Gets or sets the value of the attribute.
		/// </remarks>
		/// <value>The attribute value.</value>
		public long Value {
			get; set;
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a byte array value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a byte array value.
	/// </remarks>
	sealed class NtlmAttributeByteArrayValuePair : NtlmAttributeValuePair
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmAttributeByteArrayValuePair"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM attribute and value pair consisting of a byte array value.
		/// </remarks>
		/// <param name="attr">The NTLM attribute.</param>
		/// <param name="value">The NTLM attribute value.</param>
		public NtlmAttributeByteArrayValuePair (NtlmAttribute attr, byte[] value) : base (attr)
		{
			Value = value;
		}

		/// <summary>
		/// Get or set the value of the attribute.
		/// </summary>
		/// <remarks>
		/// Gets or sets the value of the attribute.
		/// </remarks>
		/// <value>The attribute value.</value>
		public byte[] Value {
			get; set;
		}
	}
}
