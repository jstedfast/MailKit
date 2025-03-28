﻿//
// NtlmTargetInfoTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class NtlmTargetInfoTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var buffer = new byte[24];

			Assert.Throws<ArgumentNullException> (() => new NtlmTargetInfo (null, 0, 24, true));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmTargetInfo (buffer, -1, 24, true));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmTargetInfo (buffer, 0, 25, true));
		}

#if false
		static string ToCSharpByteArrayInitializer (string name, byte[] buffer)
		{
			var builder = new System.Text.StringBuilder ();
			int index = 0;

			builder.AppendLine ($"static readonly byte[] {name} = {{");
			while (index < buffer.Length) {
				builder.Append ('\t');
				for (int i = 0; i < 16 && index < buffer.Length; i++, index++)
					builder.AppendFormat ("0x{0}, ", buffer[index].ToString ("x2"));
				builder.Length--;
				if (index == buffer.Length)
					builder.Length--;
				builder.AppendLine ();
			}
			builder.AppendLine ($"}};");

			return builder.ToString ();
		}
#endif

		static void AssertDecode (byte[] buffer, bool unicode)
		{
			var channelBinding = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
			var timestamp = DateTime.FromFileTimeUtc (132737136905945346);
			var targetInfo = new NtlmTargetInfo (buffer, 0, buffer.Length, unicode);

			//var buffer1 = targetInfo.Encode (true);
			//var csharp = ToCSharpByteArrayInitializer ("NtlmTargetInfoUnorderedUnicode", buffer1);

			Assert.That (targetInfo.ServerName, Is.EqualTo ("ServerName"));
			Assert.That (targetInfo.DomainName, Is.EqualTo ("DomainName"));
			Assert.That (targetInfo.DnsServerName, Is.EqualTo ("DnsServerName"));
			Assert.That (targetInfo.DnsDomainName, Is.EqualTo ("DnsDomainName"));
			Assert.That (targetInfo.DnsTreeName, Is.EqualTo ("DnsTreeName"));
			Assert.That (targetInfo.Flags, Is.EqualTo (2), "Flags");
			Assert.That (targetInfo.Timestamp, Is.EqualTo (timestamp.ToFileTimeUtc ()), "Timestamp");
			//Assert.That (targetInfo.SingleHost, Is.EqualTo ("SingleHost"), "SingleHost");
			Assert.That (targetInfo.ChannelBinding, Has.Length.EqualTo (16), "ChannelBinding");

			for (int i = 0; i < channelBinding.Length; i++)
				Assert.That (targetInfo.ChannelBinding[i], Is.EqualTo (channelBinding[i]), $"ChannelBinding[{i}]");

			// Verify that re-encoding the target info results in an exact replica of the input.
			var encoded = targetInfo.Encode (unicode);

			Assert.That (encoded, Has.Length.EqualTo (buffer.Length), "Re-encoded lengths do not match");

			for (int i = 0; i < buffer.Length; i++)
				Assert.That (encoded[i], Is.EqualTo (buffer[i]), $"encoded[{i}]");
		}

		static readonly byte[] NtlmTargetInfoOrderedOem = {
			0x01, 0x00, 0x0a, 0x00, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x4e, 0x61, 0x6d, 0x65, 0x02, 0x00,
			0x0a, 0x00, 0x44, 0x6f, 0x6d, 0x61, 0x69, 0x6e, 0x4e, 0x61, 0x6d, 0x65, 0x03, 0x00, 0x0d, 0x00,
			0x44, 0x6e, 0x73, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x4e, 0x61, 0x6d, 0x65, 0x04, 0x00, 0x0d,
			0x00, 0x44, 0x6e, 0x73, 0x44, 0x6f, 0x6d, 0x61, 0x69, 0x6e, 0x4e, 0x61, 0x6d, 0x65, 0x05, 0x00,
			0x0b, 0x00, 0x44, 0x6e, 0x73, 0x54, 0x72, 0x65, 0x65, 0x4e, 0x61, 0x6d, 0x65, 0x06, 0x00, 0x04,
			0x00, 0x02, 0x00, 0x00, 0x00, 0x07, 0x00, 0x08, 0x00, 0x02, 0x01, 0xc8, 0x05, 0xb9, 0x93, 0xd7,
			0x01, 0x08, 0x00, 0x0a, 0x00, 0x53, 0x69, 0x6e, 0x67, 0x6c, 0x65, 0x48, 0x6f, 0x73, 0x74, 0x09,
			0x00, 0x0a, 0x00, 0x54, 0x61, 0x72, 0x67, 0x65, 0x74, 0x4e, 0x61, 0x6d, 0x65, 0x0a, 0x00, 0x10,
			0x00, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd,
			0xef, 0x00, 0x00, 0x00, 0x00
		};

		[Test]
		public void TestDecodeOrderedOem ()
		{
			AssertDecode (NtlmTargetInfoOrderedOem, false);
		}

		static readonly byte[] NtlmTargetInfoOrderedUnicode = {
			0x01, 0x00, 0x14, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x02, 0x00, 0x14, 0x00, 0x44, 0x00, 0x6f, 0x00,
			0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00,
			0x03, 0x00, 0x1a, 0x00, 0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x04, 0x00,
			0x1a, 0x00, 0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x05, 0x00, 0x16, 0x00,
			0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x54, 0x00, 0x72, 0x00, 0x65, 0x00, 0x65, 0x00, 0x4e, 0x00,
			0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x06, 0x00, 0x04, 0x00, 0x02, 0x00, 0x00, 0x00, 0x07, 0x00,
			0x08, 0x00, 0x02, 0x01, 0xc8, 0x05, 0xb9, 0x93, 0xd7, 0x01, 0x08, 0x00, 0x14, 0x00, 0x53, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x67, 0x00, 0x6c, 0x00, 0x65, 0x00, 0x48, 0x00, 0x6f, 0x00, 0x73, 0x00,
			0x74, 0x00, 0x09, 0x00, 0x14, 0x00, 0x54, 0x00, 0x61, 0x00, 0x72, 0x00, 0x67, 0x00, 0x65, 0x00,
			0x74, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x0a, 0x00, 0x10, 0x00, 0x01, 0x23,
			0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00,
			0x00, 0x00
		};

		[Test]
		public void TestDecodeOrderedUnicode ()
		{
			AssertDecode (NtlmTargetInfoOrderedUnicode, true);
		}

		static readonly byte[] NtlmTargetInfoUnorderedOem = {
			0x07, 0x00, 0x08, 0x00, 0x02, 0x01, 0xc8, 0x05, 0xb9, 0x93, 0xd7, 0x01, 0x09, 0x00, 0x0a, 0x00,
			0x54, 0x61, 0x72, 0x67, 0x65, 0x74, 0x4e, 0x61, 0x6d, 0x65, 0x08, 0x00, 0x0a, 0x00, 0x53, 0x69,
			0x6e, 0x67, 0x6c, 0x65, 0x48, 0x6f, 0x73, 0x74, 0x01, 0x00, 0x0a, 0x00, 0x53, 0x65, 0x72, 0x76,
			0x65, 0x72, 0x4e, 0x61, 0x6d, 0x65, 0x06, 0x00, 0x04, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00,
			0x0a, 0x00, 0x44, 0x6f, 0x6d, 0x61, 0x69, 0x6e, 0x4e, 0x61, 0x6d, 0x65, 0x05, 0x00, 0x0b, 0x00,
			0x44, 0x6e, 0x73, 0x54, 0x72, 0x65, 0x65, 0x4e, 0x61, 0x6d, 0x65, 0x03, 0x00, 0x0d, 0x00, 0x44,
			0x6e, 0x73, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x4e, 0x61, 0x6d, 0x65, 0x04, 0x00, 0x0d, 0x00,
			0x44, 0x6e, 0x73, 0x44, 0x6f, 0x6d, 0x61, 0x69, 0x6e, 0x4e, 0x61, 0x6d, 0x65, 0x0a, 0x00, 0x10,
			0x00, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd,
			0xef, 0x00, 0x00, 0x00, 0x00
		};

		[Test]
		public void TestDecodeUnorderedOem ()
		{
			AssertDecode (NtlmTargetInfoUnorderedOem, false);
		}

		static readonly byte[] NtlmTargetInfoUnorderedUnicode = {
			0x07, 0x00, 0x08, 0x00, 0x02, 0x01, 0xc8, 0x05, 0xb9, 0x93, 0xd7, 0x01, 0x09, 0x00, 0x14, 0x00,
			0x54, 0x00, 0x61, 0x00, 0x72, 0x00, 0x67, 0x00, 0x65, 0x00, 0x74, 0x00, 0x4e, 0x00, 0x61, 0x00,
			0x6d, 0x00, 0x65, 0x00, 0x08, 0x00, 0x14, 0x00, 0x53, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x67, 0x00,
			0x6c, 0x00, 0x65, 0x00, 0x48, 0x00, 0x6f, 0x00, 0x73, 0x00, 0x74, 0x00, 0x01, 0x00, 0x14, 0x00,
			0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x4e, 0x00, 0x61, 0x00,
			0x6d, 0x00, 0x65, 0x00, 0x06, 0x00, 0x04, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x14, 0x00,
			0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x4e, 0x00, 0x61, 0x00,
			0x6d, 0x00, 0x65, 0x00, 0x05, 0x00, 0x16, 0x00, 0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x54, 0x00,
			0x72, 0x00, 0x65, 0x00, 0x65, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x03, 0x00,
			0x1a, 0x00, 0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00,
			0x65, 0x00, 0x72, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x04, 0x00, 0x1a, 0x00,
			0x44, 0x00, 0x6e, 0x00, 0x73, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00,
			0x6e, 0x00, 0x4e, 0x00, 0x61, 0x00, 0x6d, 0x00, 0x65, 0x00, 0x0a, 0x00, 0x10, 0x00, 0x01, 0x23,
			0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00,
			0x00, 0x00
		};

		[Test]
		public void TestDecodeUnorderedUnicode ()
		{
			AssertDecode (NtlmTargetInfoUnorderedUnicode, true);
		}

		static NtlmSingleHostData GenerateSingleHostData ()
		{
			var customData = new byte[8];
			var machineId = new byte[32];
			var rng = new Random ();

			rng.NextBytes (customData);
			rng.NextBytes (machineId);

			return new NtlmSingleHostData (customData, machineId);
		}

		static void AssertSingleHost (NtlmSingleHostData expected, byte[] actual, string prefix)
		{
			var singleHost = new NtlmSingleHostData (actual, 0, actual.Length);

			Assert.That (singleHost.Size, Is.EqualTo (expected.Size), $"{prefix}.Size");
			for (int i = 0; i < 8; i++)
				Assert.That (singleHost.CustomData[i], Is.EqualTo (expected.CustomData[i]), $"{prefix}.CustomData[{i}]");
			for (int i = 0; i < 32; i++)
				Assert.That (singleHost.MachineId[i], Is.EqualTo (expected.MachineId[i]), $"{prefix}.MachineId[{i}]");
		}

		[Test]
		public void TestRemovingAttributes ()
		{
			var channelBinding = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
			var timestamp = DateTime.FromFileTimeUtc (132737136905945346);
			var singleHost = GenerateSingleHostData ();
			var targetInfo = new NtlmTargetInfo {
				ServerName = "ServerName",
				DomainName = "DomainName",
				SingleHost = singleHost.Encode (),
				Flags = 2,
				Timestamp = timestamp.ToFileTimeUtc (),
				ChannelBinding = channelBinding
			};

			Assert.That (targetInfo.ServerName, Is.EqualTo ("ServerName"));
			Assert.That (targetInfo.DomainName, Is.EqualTo ("DomainName"));
			AssertSingleHost (singleHost, targetInfo.SingleHost, "SingleHost");
			Assert.That (targetInfo.Flags, Is.EqualTo (2), "Flags");
			Assert.That (targetInfo.Timestamp, Is.EqualTo (timestamp.ToFileTimeUtc ()), "Timestamp");
			Assert.That (targetInfo.ChannelBinding, Has.Length.EqualTo (16), "ChannelBinding");

			for (int i = 0; i < channelBinding.Length; i++)
				Assert.That (targetInfo.ChannelBinding[i], Is.EqualTo (channelBinding[i]), $"ChannelBinding[{i}]");

			targetInfo.ServerName = null;
			Assert.That (targetInfo.ServerName, Is.Null, "ServerName remove attempt #1");
			targetInfo.ServerName = null;
			Assert.That (targetInfo.ServerName, Is.Null, "ServerName remove attempt #2");

			targetInfo.SingleHost = null;
			Assert.That (targetInfo.SingleHost, Is.Null, "SingleHost remove attempt #1");
			targetInfo.SingleHost = null;
			Assert.That (targetInfo.SingleHost, Is.Null, "SingleHost remove attempt #2");

			targetInfo.Flags = null;
			Assert.That (targetInfo.Flags, Is.Null, "Flags remove attempt #1");
			targetInfo.Flags = null;
			Assert.That (targetInfo.Flags, Is.Null, "Flags remove attempt #2");

			targetInfo.Timestamp = null;
			Assert.That (targetInfo.Timestamp, Is.Null, "Timestamp remove attempt #1");
			targetInfo.Timestamp = null;
			Assert.That (targetInfo.Timestamp, Is.Null, "Timestamp remove attempt #2");

			targetInfo.ChannelBinding = null;
			Assert.That (targetInfo.ChannelBinding, Is.Null, "ChannelBinding remove attempt #1");
			targetInfo.ChannelBinding = null;
			Assert.That (targetInfo.ChannelBinding, Is.Null, "ChannelBinding remove attempt #2");
		}

		[Test]
		public void TestUpdatingAttributes ()
		{
			var channelBinding = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
			var timestamp = DateTime.FromFileTimeUtc (132737136905945346);
			var updatedSingleHost = GenerateSingleHostData ();
			var singleHost = GenerateSingleHostData ();
			var targetInfo = new NtlmTargetInfo {
				ServerName = "ServerName",
				DomainName = "DomainName",
				SingleHost = singleHost.Encode (),
				Flags = 2,
				Timestamp = timestamp.ToFileTimeUtc (),
				ChannelBinding = channelBinding
			};

			Assert.That (targetInfo.ServerName, Is.EqualTo ("ServerName"));
			Assert.That (targetInfo.DomainName, Is.EqualTo ("DomainName"));
			AssertSingleHost (singleHost, targetInfo.SingleHost, "SingleHost");
			Assert.That (targetInfo.Flags, Is.EqualTo (2), "Flags");
			Assert.That (targetInfo.Timestamp, Is.EqualTo (timestamp.ToFileTimeUtc ()), "Timestamp");
			Assert.That (targetInfo.ChannelBinding, Has.Length.EqualTo (16), "ChannelBinding");

			for (int i = 0; i < channelBinding.Length; i++)
				Assert.That (targetInfo.ChannelBinding[i], Is.EqualTo (channelBinding[i]), $"ChannelBinding[{i}]");

			targetInfo.ServerName = "NewServerName";
			Assert.That (targetInfo.ServerName, Is.EqualTo ("NewServerName"), "Updated ServerName");

			targetInfo.SingleHost = updatedSingleHost.Encode ();
			AssertSingleHost (updatedSingleHost, targetInfo.SingleHost, "Updated SingleHost");

			targetInfo.Flags = 1;
			Assert.That (targetInfo.Flags, Is.EqualTo (1), "Updated Flags");

			targetInfo.Timestamp = 123456789;
			Assert.That (targetInfo.Timestamp, Is.EqualTo (123456789), "Updated Timestamp");

			targetInfo.ChannelBinding = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
			Assert.That (targetInfo.ChannelBinding, Has.Length.EqualTo (20), "Updated ChannelBinding");

			for (int i = 0; i < channelBinding.Length; i++)
				Assert.That (targetInfo.ChannelBinding[i], Is.EqualTo (i), $"Updated ChannelBinding[{i}]");
		}
	}
}
