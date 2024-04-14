//
// NtlmAttributeValuePair.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System;
using System.Text;
using System.Diagnostics;

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

		protected static void EncodeInt16 (byte[] buf, ref int index, short value)
		{
			buf[index++] = (byte) (value);
			buf[index++] = (byte) (value >> 8);
		}

		protected static void EncodeInt32 (byte[] buf, ref int index, int value)
		{
			buf[index++] = (byte) (value);
			buf[index++] = (byte) (value >> 8);
			buf[index++] = (byte) (value >> 16);
			buf[index++] = (byte) (value >> 24);
		}

		protected static void EncodeTypeAndLength (byte[] buf, ref int index, NtlmAttribute attr, short length)
		{
			EncodeInt16 (buf, ref index, (short) attr);
			EncodeInt16 (buf, ref index, length);
		}

		/// <summary>
		/// Get the number of bytes needed for encoding the attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the number of bytes needed for encoding the attribute value.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <returns>The number of bytes needed to encode the value.</returns>
		public abstract int GetEncodedLength (Encoding encoding);

		/// <summary>
		/// Encode the attribute value to the specified buffer.
		/// </summary>
		/// <remarks>
		/// Encodes the attribute value to the specified buffer.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="buffer">The output buffer.</param>
		/// <param name="index">The index into the buffer to start appending the encoded attribute.</param>
		public abstract void EncodeTo (Encoding encoding, byte[] buffer, ref int index);
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a string value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a string value.
	/// </remarks>
	[DebuggerDisplay ("{Attribute} = {Value}")]
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

		/// <summary>
		/// Get the number of bytes needed for encoding the attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the number of bytes needed for encoding the attribute value.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <returns>The number of bytes needed to encode the value.</returns>
		public override int GetEncodedLength (Encoding encoding)
		{
			return 4 + encoding.GetByteCount (Value);
		}

		/// <summary>
		/// Encode the attribute value to the specified buffer.
		/// </summary>
		/// <remarks>
		/// Encodes the attribute value to the specified buffer.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="buffer">The output buffer.</param>
		/// <param name="index">The index into the buffer to start appending the encoded attribute.</param>
		public override void EncodeTo (Encoding encoding, byte[] buffer, ref int index)
		{
			int length = encoding.GetByteCount (Value);

			EncodeTypeAndLength (buffer, ref index, Attribute, (short) length);
			encoding.GetBytes (Value, 0, Value.Length, buffer, index);
			index += length;
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a flags value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a flags value.
	/// </remarks>
	[DebuggerDisplay ("{Attribute} = {Value}")]
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

		/// <summary>
		/// Get the number of bytes needed for encoding the attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the number of bytes needed for encoding the attribute value.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <returns>The number of bytes needed to encode the value.</returns>
		public override int GetEncodedLength (Encoding encoding)
		{
			return 4 + Size;
		}

		/// <summary>
		/// Encode the attribute value to the specified buffer.
		/// </summary>
		/// <remarks>
		/// Encodes the attribute value to the specified buffer.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="buffer">The output buffer.</param>
		/// <param name="index">The index into the buffer to start appending the encoded attribute.</param>
		public override void EncodeTo (Encoding encoding, byte[] buffer, ref int index)
		{
			EncodeTypeAndLength (buffer, ref index, Attribute, Size);

			switch (Size) {
			case 2: EncodeInt16 (buffer, ref index, (short) Value); break;
			default: EncodeInt32 (buffer, ref index, Value); break;
			}
		}
	}

	/// <summary>
	/// An NTLM attribute and value pair consisting of a timestamp value.
	/// </summary>
	/// <remarks>
	/// An NTLM attribute and value pair consisting of a timestamp value.
	/// </remarks>
	[DebuggerDisplay ("{Attribute} = {Value}")]
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

		/// <summary>
		/// Get the number of bytes needed for encoding the attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the number of bytes needed for encoding the attribute value.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <returns>The number of bytes needed to encode the value.</returns>
		public override int GetEncodedLength (Encoding encoding)
		{
			return 4 + Size;
		}

		/// <summary>
		/// Encode the attribute value to the specified buffer.
		/// </summary>
		/// <remarks>
		/// Encodes the attribute value to the specified buffer.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="buffer">The output buffer.</param>
		/// <param name="index">The index into the buffer to start appending the encoded attribute.</param>
		public override void EncodeTo (Encoding encoding, byte[] buffer, ref int index)
		{
			EncodeTypeAndLength (buffer, ref index, Attribute, Size);

			switch (Size) {
			case 2: EncodeInt16 (buffer, ref index, (short) (Value & 0xffff)); break;
			case 4: EncodeInt32 (buffer, ref index, (int) (Value & 0xffffffff)); break;
			default:
				EncodeInt32 (buffer, ref index, (int) (Value & 0xffffffff));
				EncodeInt32 (buffer, ref index, (int) (Value >> 32));
				break;
			}
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

		/// <summary>
		/// Get the number of bytes needed for encoding the attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the number of bytes needed for encoding the attribute value.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <returns>The number of bytes needed to encode the value.</returns>
		public override int GetEncodedLength (Encoding encoding)
		{
			return 4 + Value.Length;
		}

		/// <summary>
		/// Encode the attribute value to the specified buffer.
		/// </summary>
		/// <remarks>
		/// Encodes the attribute value to the specified buffer.
		/// </remarks>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="buffer">The output buffer.</param>
		/// <param name="index">The index into the buffer to start appending the encoded attribute.</param>
		public override void EncodeTo (Encoding encoding, byte[] buffer, ref int index)
		{
			EncodeTypeAndLength (buffer, ref index, Attribute, (short) Value.Length);

			Buffer.BlockCopy (Value, 0, buffer, index, Value.Length);
			index += Value.Length;
		}
	}
}
