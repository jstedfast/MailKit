//
// TargetInfo.cs
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
using System.Text;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Security.Ntlm {
	class TargetInfo
	{
		public TargetInfo (byte[] buffer, int startIndex, int length, bool unicode)
		{
			Decode (buffer, startIndex, length, unicode);
		}

		public TargetInfo ()
		{
		}

		public int? Flags {
			get; set;
		}

		public string DomainName {
			get; set;
		}

		public string ServerName {
			get; set;
		}

		public string DnsDomainName {
			get; set;
		}

		public string DnsServerName {
			get; set;
		}

		public string DnsTreeName {
			get; set;
		}

		public string TargetName {
			get; set;
		}

		public long Timestamp {
			get; set;
		}

		static string DecodeString (byte[] buffer, ref int index, bool unicode)
		{
			var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
			var length = BitConverterLE.ToInt16 (buffer, index);
			var value = encoding.GetString (buffer, index + 2, length);

			index += 2 + length;

			return value;
		}

		static int DecodeFlags (byte[] buffer, ref int index)
		{
			short nbytes = BitConverterLE.ToInt16 (buffer, index);
			int flags;

			index += 2;

			switch (nbytes) {
			case 4:  flags = BitConverterLE.ToInt32 (buffer, index); break;
			case 2:  flags = BitConverterLE.ToInt16 (buffer, index); break;
			default: flags = 0; break;
			}

			index += nbytes;

			return flags;
		}

		static long DecodeTimestamp (byte[] buffer, ref int index)
		{
			short nbytes = BitConverterLE.ToInt16 (buffer, index);
			long lo, hi;

			index += 2;

			switch (nbytes) {
			case 8:
				lo = BitConverterLE.ToUInt32 (buffer, index);
				index += 4;
				hi = BitConverterLE.ToUInt32 (buffer, index);
				index += 4;
				return (hi << 32) | lo;
			case 4:
				lo = BitConverterLE.ToUInt32 (buffer, index);
				index += 4;
				return lo;
			case 2:
				lo = BitConverterLE.ToUInt16 (buffer, index);
				index += 2;
				return lo;
			default:
				index += nbytes;
				return 0;
			}
		}

		void Decode (byte[] buffer, int startIndex, int length, bool unicode)
		{
			int index = startIndex;

			do {
				var type = BitConverterLE.ToInt16 (buffer, index);

				index += 2;

				switch (type) {
				case 0: index = startIndex + length; break; // a 'type' of 0 terminates the TargetInfo
				case 1: ServerName = DecodeString (buffer, ref index, unicode); break;
				case 2: DomainName = DecodeString (buffer, ref index, unicode); break;
				case 3: DnsServerName = DecodeString (buffer, ref index, unicode); break;
				case 4: DnsDomainName = DecodeString (buffer, ref index, unicode); break;
				case 5: DnsTreeName = DecodeString (buffer, ref index, unicode); break;
				case 6: Flags = DecodeFlags (buffer, ref index); break;
				case 7: Timestamp = DecodeTimestamp (buffer, ref index); break;
				case 9: TargetName = DecodeString (buffer, ref index, unicode); break;
				default: index += 2 + BitConverterLE.ToInt16 (buffer, index); break;
				}
			} while (index < startIndex + length);
		}

		int CalculateSize (bool unicode)
		{
			var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
			int length = 4;

			if (!string.IsNullOrEmpty (DomainName))
				length += 4 + encoding.GetByteCount (DomainName);

			if (!string.IsNullOrEmpty (ServerName))
				length += 4 + encoding.GetByteCount (ServerName);

			if (!string.IsNullOrEmpty (DnsDomainName))
				length += 4 + encoding.GetByteCount (DnsDomainName);

			if (!string.IsNullOrEmpty (DnsServerName))
				length += 4 + encoding.GetByteCount (DnsServerName);

			if (!string.IsNullOrEmpty (DnsTreeName))
				length += 4 + encoding.GetByteCount (DnsTreeName);

			if (Flags.HasValue)
				length += 8;

			if (Timestamp != 0)
				length += 12;

			if (!string.IsNullOrEmpty (TargetName))
				length += 4 + encoding.GetByteCount (TargetName);

			return length;
		}

		static void EncodeTypeAndLength (byte[] buf, ref int index, short type, short length)
		{
			buf[index++] = (byte) (type);
			buf[index++] = (byte) (type >> 8);
			buf[index++] = (byte) (length);
			buf[index++] = (byte) (length >> 8);
		}

		static void EncodeString (byte[] buf, ref int index, short type, string value, bool unicode)
		{
			var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
			int length = value.Length;

			if (unicode)
				length *= 2;

			EncodeTypeAndLength (buf, ref index, type, (short) length);
			encoding.GetBytes (value, 0, value.Length, buf, index);
			index += length;
		}

		static void EncodeInt32 (byte[] buf, ref int index, int value)
		{
			buf[index++] = (byte) (value);
			buf[index++] = (byte) (value >> 8);
			buf[index++] = (byte) (value >> 16);
			buf[index++] = (byte) (value >> 24);
		}

		static void EncodeTimestamp (byte[] buf, ref int index, short type, long value)
		{
			EncodeTypeAndLength (buf, ref index, type, 8);
			EncodeInt32 (buf, ref index, (int) (value & 0xffffffff));
			EncodeInt32 (buf, ref index, (int) (value >> 32));
		}

		static void EncodeFlags (byte[] buf, ref int index, short type, int value)
		{
			EncodeTypeAndLength (buf, ref index, type, 4);
			EncodeInt32 (buf, ref index, value);
		}

		public byte[] Encode (bool unicode)
		{
			var buf = new byte[CalculateSize (unicode)];
			int index = 0;

			if (!string.IsNullOrEmpty (DomainName))
				EncodeString (buf, ref index, 2, DomainName, unicode);

			if (!string.IsNullOrEmpty (ServerName))
				EncodeString (buf, ref index, 1, ServerName, unicode);

			if (!string.IsNullOrEmpty (DnsDomainName))
				EncodeString (buf, ref index, 4, DnsDomainName, unicode);

			if (!string.IsNullOrEmpty (DnsServerName))
				EncodeString (buf, ref index, 3, DnsServerName, unicode);

			if (!string.IsNullOrEmpty (DnsTreeName))
				EncodeString (buf, ref index, 5, DnsTreeName, unicode);

			if (Flags.HasValue)
				EncodeFlags (buf, ref index, 6, Flags.Value);

			if (Timestamp != 0)
				EncodeTimestamp (buf, ref index, 7, Timestamp);

			if (!string.IsNullOrEmpty (TargetName))
				EncodeString (buf, ref index, 9, TargetName, unicode);

			return buf;
		}
	}
}
