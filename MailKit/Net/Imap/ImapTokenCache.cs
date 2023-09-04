//
// ImapTokenCache.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2023 .NET Foundation and Contributors
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
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;

namespace MailKit.Net.Imap
{
	class ImapTokenCache
	{
		const int capacity = 128;

		readonly Dictionary<ImapTokenKey, LinkedListNode<ImapTokenItem>> cache;
		readonly LinkedList<ImapTokenItem> list;
		readonly ImapTokenKey lookupKey;
		readonly Decoder[] decoders;
		readonly char[] chars;

		public ImapTokenCache ()
		{
			cache = new Dictionary<ImapTokenKey, LinkedListNode<ImapTokenItem>> ();
			list = new LinkedList<ImapTokenItem> ();
			lookupKey = new ImapTokenKey ();

			// Start with the assumption that token values will be valid UTF-8 and then fall back to iso-8859-1.
			decoders = new Decoder[2] {
				TextEncodings.UTF8.GetDecoder (),
				TextEncodings.Latin1.GetDecoder ()
			};

			chars = new char[128];
		}

		public ImapToken AddOrGet (ImapTokenType type, ByteArrayBuilder builder)
		{
			// lookupKey is a pre-allocated key used for lookups
			lookupKey.Init (decoders, chars, type, builder.GetBuffer (), builder.Length, out var decoder, out int charsNeeded);

			if (cache.TryGetValue (lookupKey, out var node)) {
				// move the node to the head of the list
				list.Remove (node);
				list.AddFirst (node);
				node.Value.Count++;

				return node.Value.Token;
			}

			string value;

			if (charsNeeded <= chars.Length) {
				// If the number of needed chars is <= the length of our temp buffer, then it should all be contained.
				value = new string (chars, 0, charsNeeded);
			} else {
				var buffer = ArrayPool<char>.Shared.Rent (charsNeeded);
				try {
					// Note: This conversion should go flawlessly, so we'll just Debug.Assert() our expectations.
					decoder.Convert (builder.GetBuffer (), 0, builder.Length, buffer, 0, buffer.Length, true, out var bytesUsed, out var charsUsed, out var completed);
					Debug.Assert (bytesUsed == builder.Length);
					Debug.Assert (charsUsed == charsNeeded);
					Debug.Assert (completed);
					value = new string (buffer, 0, charsUsed);
				} finally {
					ArrayPool<char>.Shared.Return (buffer);
					decoder.Reset ();
				}
			}

			var token = new ImapToken (type, value);

			if (cache.Count >= capacity) {
				// remove the least recently used token
				node = list.Last;
				list.RemoveLast ();
				cache.Remove (node.Value.Key);

				// re-use the node, item and key to avoid allocations
				node.Value.Key.Init (type, (string) token.Value);
				node.Value.Token = token;
			} else {
				var key = new ImapTokenKey (type, (string) token.Value);
				var item = new ImapTokenItem (key, token);

				node = new LinkedListNode<ImapTokenItem> (item);
			}

			cache.Add (node.Value.Key, node);
			list.AddFirst (node);

			return token;
		}

		class ImapTokenKey
		{
			ImapTokenType type;
			byte[] byteArrayKey;
			string stringKey;
			int length;
			int hashCode;

			public ImapTokenKey ()
			{
			}

			public ImapTokenKey (ImapTokenType type, string key)
			{
				Init (type, key);
			}

			public void Init (Decoder[] decoders, char[] chars, ImapTokenType type, byte[] key, int length, out Decoder correctDecoder, out int charsNeeded)
			{
				this.type = type;
				this.byteArrayKey = key;
				this.stringKey = null;
				this.length = length;

				var hash = new HashCode ();
				hash.Add ((int) type);

				correctDecoder = null;
				charsNeeded = 0;

				foreach (var decoder in decoders) {
					bool completed;
					int index = 0;

					correctDecoder = decoder;

					do {
						try {
							decoder.Convert (key, index, length - index, chars, 0, chars.Length, true, out var bytesUsed, out var charsUsed, out completed);
							charsNeeded += charsUsed;
							index += bytesUsed;

							for (int i = 0; i < charsUsed; i++)
								hash.Add (chars[i]);
						} catch (DecoderFallbackException) {
							// Restart the hash...
							hash = new HashCode ();
							hash.Add ((int) type);
							completed = false;
							charsNeeded = 0;
							break;
						}
					} while (!completed);

					decoder.Reset ();

					if (completed)
						break;
				}

				this.hashCode = hash.ToHashCode ();
			}

			public void Init (ImapTokenType type, string key)
			{
				this.type = type;
				this.byteArrayKey = null;
				this.stringKey = key;
				this.length = key.Length;

				var hash = new HashCode ();
				hash.Add ((int) type);
				for (int i = 0; i < length; i++)
					hash.Add (key[i]);

				this.hashCode = hash.ToHashCode ();
			}

			static bool Equals (string str, byte[] bytes)
			{
				for (int i = 0; i < str.Length; i++) {
					if (str[i] != (char) bytes[i])
						return false;
				}

				return true;
			}

			static bool Equals (ImapTokenKey self, ImapTokenKey other)
			{
				if (self.type != other.type || self.length != other.length)
					return false;

				if (self.stringKey != null) {
					if (other.stringKey != null)
						return self.stringKey.Equals (other.stringKey, StringComparison.Ordinal);

					return Equals (self.stringKey, other.byteArrayKey);
				}

				if (other.stringKey != null)
					return Equals (other.stringKey, self.byteArrayKey);

				for (int i = 0; i < self.length; i++) {
					if (self.byteArrayKey[i] != other.byteArrayKey[i])
						return false;
				}

				return true;
			}

			public override bool Equals (object obj)
			{
				return obj is ImapTokenKey other && Equals (this, other);
			}

			public override int GetHashCode ()
			{
				return hashCode;
			}

			public override string ToString ()
			{
				return string.Format ("{0}: {1}", type, stringKey ?? Encoding.UTF8.GetString (byteArrayKey, 0, length));
			}
		}

		class ImapTokenItem
		{
			public ImapTokenKey Key;
			public ImapToken Token;
			public int Count;

			public ImapTokenItem (ImapTokenKey key, ImapToken token)
			{
				Key = key;
				Token = token;
				Count = 1;
			}

			public override string ToString ()
			{
				return $"{Count}";
			}
		}
	}
}
