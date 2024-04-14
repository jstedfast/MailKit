//
// NtlmTargetInfo.cs
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
using System.Collections.Generic;

namespace MailKit.Security.Ntlm {
	/// <summary>
	/// An NTLM TargetInfo structure.
	/// </summary>
	/// <remarks>
	/// An NTLM TargetInfo structure.
	/// </remarks>
	class NtlmTargetInfo
	{
		readonly List<NtlmAttributeValuePair> attributes = new List<NtlmAttributeValuePair> ();

		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmTargetInfo"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NtlmTargetInfo"/>.
		/// </remarks>
		/// <param name="buffer">The raw target info buffer to decode.</param>
		/// <param name="startIndex">The starting index of the target info structure.</param>
		/// <param name="length">The length of the target info structure.</param>
		/// <param name="unicode"><c>true</c> if the target info strings are unicode; otherwise, <c>false</c>.</param>
		public NtlmTargetInfo (byte[] buffer, int startIndex, int length, bool unicode)
		{
			Decode (buffer, startIndex, length, unicode);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmTargetInfo"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NtlmTargetInfo"/>.
		/// </remarks>
		public NtlmTargetInfo ()
		{
		}

		/// <summary>
		/// Copy the attribute value pairs to another TargetInfo.
		/// </summary>
		/// <remarks>
		/// Copies the attribute value pairs to another TargetInfo.
		/// </remarks>
		public void CopyTo (NtlmTargetInfo targetInfo)
		{
			targetInfo.attributes.Clear ();

			foreach (var attribute in attributes) {
				if (attribute is NtlmAttributeTimestampValuePair timestamp)
					targetInfo.attributes.Add (new NtlmAttributeTimestampValuePair (timestamp.Attribute, timestamp.Value, timestamp.Size));
				else if (attribute is NtlmAttributeFlagsValuePair flags)
					targetInfo.attributes.Add (new NtlmAttributeFlagsValuePair (flags.Attribute, flags.Value, flags.Size));
				else if (attribute is NtlmAttributeByteArrayValuePair array)
					targetInfo.attributes.Add (new NtlmAttributeByteArrayValuePair (array.Attribute, array.Value));
				else if (attribute is NtlmAttributeStringValuePair str)
					targetInfo.attributes.Add (new NtlmAttributeStringValuePair (str.Attribute, str.Value));
			}
		}

		internal NtlmAttributeValuePair GetAvPair (NtlmAttribute attr)
		{
			for (int i = 0; i < attributes.Count; i++) {
				if (attributes[i].Attribute == attr)
					return attributes[i];
			}

			return null;
		}

		string GetAvPairString (NtlmAttribute attr)
		{
			return ((NtlmAttributeStringValuePair) GetAvPair (attr))?.Value;
		}

		void SetAvPairString (NtlmAttribute attr, string value)
		{
			var pair = (NtlmAttributeStringValuePair) GetAvPair (attr);

			if (pair == null) {
				if (value != null)
					attributes.Add (new NtlmAttributeStringValuePair (attr, value));
			} else if (value != null) {
				pair.Value = value;
			} else {
				attributes.Remove (pair);
			}
		}

		byte[] GetAvPairByteArray (NtlmAttribute attr)
		{
			return ((NtlmAttributeByteArrayValuePair) GetAvPair (attr))?.Value;
		}

		void SetAvPairByteArray (NtlmAttribute attr, byte[] value)
		{
			var pair = (NtlmAttributeByteArrayValuePair) GetAvPair (attr);

			if (pair == null) {
				if (value != null)
					attributes.Add (new NtlmAttributeByteArrayValuePair (attr, value));
			} else if (value != null) {
				pair.Value = value;
			} else {
				attributes.Remove (pair);
			}
		}

		/// <summary>
		/// Get or set the server's NetBIOS computer name.
		/// </summary>
		/// <remarks>
		/// Gets or sets the server's NetBIOS computer name.
		/// </remarks>
		/// <value>The server's NetBIOS computer name if available; otherwise, <c>null</c>.</value>
		public string ServerName {
			get { return GetAvPairString (NtlmAttribute.ServerName); }
			set { SetAvPairString (NtlmAttribute.ServerName, value); }
		}

		/// <summary>
		/// Get or set the server's NetBIOS domain name.
		/// </summary>
		/// <remarks>
		/// Gets or sets the server's NetBIOS domain name.
		/// </remarks>
		/// <value>The server's NetBIOS domain name if available; otherwise, <c>null</c>.</value>
		public string DomainName {
			get { return GetAvPairString (NtlmAttribute.DomainName); }
			set { SetAvPairString (NtlmAttribute.DomainName, value); }
		}

		/// <summary>
		/// Get or set the fully qualified domain name (FQDN) of the server.
		/// </summary>
		/// <remarks>
		/// Gets or sets the fully qualified domain name (FQDN) of the server.
		/// </remarks>
		/// <value>The fully qualified domain name (FQDN) of the server if available; otherwise, <c>null</c>.</value>
		public string DnsServerName {
			get { return GetAvPairString (NtlmAttribute.DnsServerName); }
			set { SetAvPairString (NtlmAttribute.DnsServerName, value); }
		}

		/// <summary>
		/// Get or set the fully qualified domain name (FQDN) of the domain.
		/// </summary>
		/// <remarks>
		/// Gets or sets the fully qualified domain name (FQDN) of the domain.
		/// </remarks>
		/// <value>The fully qualified domain name (FQDN) of the domain if available; otherwise, <c>null</c>.</value>
		public string DnsDomainName {
			get { return GetAvPairString (NtlmAttribute.DnsDomainName); }
			set { SetAvPairString (NtlmAttribute.DnsDomainName, value); }
		}

		/// <summary>
		/// Get or set the fully qualified domain name (FQDN) of the forest.
		/// </summary>
		/// <remarks>
		/// Gets or sets the fully qualified domain name (FQDN) of the forest.
		/// </remarks>
		/// <value>The fully qualified domain name (FQDN) of the forest if available; otherwise, <c>null</c>.</value>
		public string DnsTreeName {
			get { return GetAvPairString (NtlmAttribute.DnsTreeName); }
			set { SetAvPairString (NtlmAttribute.DnsTreeName, value); }
		}

		/// <summary>
		/// Get or set a 32-bit value indicating server or client configuration.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a 32-bit value indicating server or client configuration.</para>
		/// <para>0x00000001: Indicates to the client that the account authentication is constrained.</para>
		/// <para>0x00000002: Indicates that the client is providing message integrity in the MIC field (section 2.2.1.3) in the AUTHENTICATE_MESSAGE.</para>
		/// <para>0x00000004: Indicates that the client is providing a target SPN generated from an untrusted source.</para>
		/// </remarks>
		/// <value>The 32-bit flags value if available; otherwise, <c>null</c>.</value>
		public int? Flags {
			get { return ((NtlmAttributeFlagsValuePair) GetAvPair (NtlmAttribute.Flags))?.Value; }
			set {
				var pair = (NtlmAttributeFlagsValuePair) GetAvPair (NtlmAttribute.Flags);

				if (pair == null) {
					if (value != null)
						attributes.Add (new NtlmAttributeFlagsValuePair (NtlmAttribute.Flags, value.Value));
				} else if (value != null) {
					pair.Size = Math.Max (pair.Size, (short) (value.Value > short.MaxValue ? 4 : 2));
					pair.Value = value.Value;
				} else {
					attributes.Remove (pair);
				}
			}
		}

		/// <summary>
		/// Get or set a timestamp that contains the server local time.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a timestamp that contains the server local time.</para>
		/// <para>A FILETIME structure ([MS-DTYP] section 2.3.3) in little-endian byte order that contains
		/// the server local time. This structure is always sent in the CHALLENGE_MESSAGE.</para>
		/// </remarks>
		/// <value>The local time of the server, if available; otherwise <c>null</c>.</value>
		public long? Timestamp {
			get { return ((NtlmAttributeTimestampValuePair) GetAvPair (NtlmAttribute.Timestamp))?.Value; }
			set {
				var pair = (NtlmAttributeTimestampValuePair) GetAvPair (NtlmAttribute.Timestamp);

				if (pair == null) {
					if (value != null)
						attributes.Add (new NtlmAttributeTimestampValuePair (NtlmAttribute.Timestamp, value.Value));
				} else if (value != null) {
					pair.Size = Math.Max (pair.Size, (short) (value.Value > int.MaxValue ? 8 : 4));
					pair.Value = value.Value;
				} else {
					attributes.Remove (pair);
				}
			}
		}

		/// <summary>
		/// Get or set the single host data structure.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the single host data structure.</para>
		/// <para>The Value field contains a platform-specific blob, as well as a MachineID created at computer startup to identify the calling machine.</para>
		/// </remarks>
		/// <value>The single host data structure, if available; otherwise, <c>null</c>.</value>
		public byte[] SingleHost {
			get { return GetAvPairByteArray (NtlmAttribute.SingleHost); }
			set { SetAvPairByteArray (NtlmAttribute.SingleHost, value); }
		}

		/// <summary>
		/// Get or set the Service Principal Name (SPN) of the server.
		/// </summary>
		/// <remarks>
		/// Gets or sets the Service Principal Name (SPN) of the server.
		/// </remarks>
		/// <value>The Service Principal Name (SPN) of the server, if available; otherwise, <c>null</c>.</value>
		public string TargetName {
			get { return GetAvPairString (NtlmAttribute.TargetName); }
			set { SetAvPairString (NtlmAttribute.TargetName, value); }
		}

		/// <summary>
		/// Get or set the channel binding hash.
		/// </summary>
		/// <remarks>
		/// Gets or sets the channel binding hash.
		/// </remarks>
		/// <value>An MD5 hash of the channel binding data, if available; otherwise <c>null</c>.</value>
		public byte[] ChannelBinding {
			get { return GetAvPairByteArray (NtlmAttribute.ChannelBinding); }
			set { SetAvPairByteArray (NtlmAttribute.ChannelBinding, value); }
		}

		static byte[] DecodeByteArray (byte[] buffer, ref int index)
		{
			var length = BitConverterLE.ToInt16 (buffer, index);
			var value = new byte[length];

			Buffer.BlockCopy (buffer, index + 2, value, 0, length);

			index += 2 + length;

			return value;
		}

		static string DecodeString (byte[] buffer, ref int index, bool unicode)
		{
			var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
			var length = BitConverterLE.ToInt16 (buffer, index);
			var value = encoding.GetString (buffer, index + 2, length);

			index += 2 + length;

			return value;
		}

		static int DecodeFlags (byte[] buffer, ref int index, out short size)
		{
			size = BitConverterLE.ToInt16 (buffer, index);
			int flags;

			index += 2;

			switch (size) {
			case 4:  flags = BitConverterLE.ToInt32 (buffer, index); break;
			case 2:  flags = BitConverterLE.ToInt16 (buffer, index); break;
			default: flags = 0; break;
			}

			index += size;

			return flags;
		}

		static long DecodeTimestamp (byte[] buffer, ref int index, out short size)
		{
			size = BitConverterLE.ToInt16 (buffer, index);
			long value;

			index += 2;

			switch (size) {
			case 8:
				long lo = BitConverterLE.ToUInt32 (buffer, index);
				long hi = BitConverterLE.ToUInt32 (buffer, index + 4);
				value = (hi << 32) | lo;
				break;
			case 4: value = BitConverterLE.ToUInt32 (buffer, index); break;
			case 2: value = BitConverterLE.ToUInt16 (buffer, index); break;
			default: value = 0; break;
			}

			index += size;

			return value;
		}

		void Decode (byte[] buffer, int startIndex, int length, bool unicode)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (startIndex < 0 || startIndex > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (length < 12 || length > (buffer.Length - startIndex))
				throw new ArgumentOutOfRangeException (nameof (length));

			int index = startIndex;

			do {
				var attr = (NtlmAttribute) BitConverterLE.ToInt16 (buffer, index);
				short size;

				index += 2;

				switch (attr) {
				case NtlmAttribute.EOL:
					index = startIndex + length;
					break;
				case NtlmAttribute.ServerName:
				case NtlmAttribute.DomainName:
				case NtlmAttribute.DnsServerName:
				case NtlmAttribute.DnsDomainName:
				case NtlmAttribute.DnsTreeName:
				case NtlmAttribute.TargetName:
					attributes.Add (new NtlmAttributeStringValuePair (attr, DecodeString (buffer, ref index, unicode)));
					break;
				case NtlmAttribute.Flags:
					attributes.Add (new NtlmAttributeFlagsValuePair (attr, DecodeFlags (buffer, ref index, out size), size));
					break;
				case NtlmAttribute.Timestamp:
					attributes.Add (new NtlmAttributeTimestampValuePair (attr, DecodeTimestamp (buffer, ref index, out size), size));
					break;
				default:
					attributes.Add (new NtlmAttributeByteArrayValuePair (attr, DecodeByteArray (buffer, ref index)));
					break;
				}
			} while (index < startIndex + length);
		}

		int CalculateSize (Encoding encoding)
		{
			int length = 4;

			foreach (var attribute in attributes)
				length += attribute.GetEncodedLength (encoding);

			return length;
		}

		/// <summary>
		/// Encode the TargetInfo structure.
		/// </summary>
		/// <remarks>
		/// Encodes the TargetInfo structure.
		/// </remarks>
		/// <param name="unicode"><c>true</c> if the strings should be encoded in Unicode; otherwise, <c>false</c>.</param>
		/// <returns>The encoded TargetInfo.</returns>
		public byte[] Encode (bool unicode)
		{
			var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
			var buf = new byte[CalculateSize (encoding)];
			int index = 0;

			foreach (var attribute in attributes)
				attribute.EncodeTo (encoding, buf, ref index);

			return buf;
		}
	}
}
