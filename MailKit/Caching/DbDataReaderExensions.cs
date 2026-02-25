//
// DbDataReaderExtensions.cs
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

using System.Data.Common;

using MimeKit;
using MimeKit.Utils;

namespace MailKit.Caching {
	static class DbDataReaderExensions
	{
		public static BodyPart GetBodyStructure (this DbDataReader reader, int ordinal)
		{
			var text = reader.GetString (ordinal);

			if (string.IsNullOrEmpty (text))
				return null;

			BodyPart.TryParse (text, out var body);

			return body;
		}

		public static InternetAddressList GetInternetAddressList (this DbDataReader reader, int ordinal)
		{
			var text = reader.GetString (ordinal);

			return InternetAddressList.Parse (text ?? string.Empty);
		}

		public static MessageFlags GetMessageFlags (this DbDataReader reader, int ordinal)
		{
			return (MessageFlags) reader.GetInt32 (ordinal);
		}

		public static MessageIdList GetReferences (this DbDataReader reader, int ordinal)
		{
			var text = reader.GetString (ordinal);
			var references = new MessageIdList ();

			if (!string.IsNullOrEmpty (text)) {
				foreach (var msgid in MimeUtils.EnumerateReferences (text))
					references.Add (msgid);
			}

			return references;
		}

		public static ulong GetUInt64 (this DbDataReader reader, int ordinal)
		{
			return (ulong) reader.GetInt64 (ordinal);
		}

		public static UniqueId GetUniqueId (this DbDataReader reader, int ordinal, uint uidValidity)
		{
			return new UniqueId (uidValidity, (uint) reader.GetInt64 (ordinal));
		}
	}
}
