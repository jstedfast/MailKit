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
using System.Buffers;
using System.Diagnostics;
using System.Collections.Generic;

namespace MailKit.Net.Imap
{
	class ImapTokenCache
	{
		const int capacity = 128;

		readonly Dictionary<ImapTokenKey, LinkedListNode<ImapTokenItem>> cache;
		readonly LinkedList<ImapTokenItem> list;
		readonly ImapTokenKey lookupKey;
		readonly Decoder[] decoders;
		char[] charBuffer;

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

			charBuffer = ArrayPool<char>.Shared.Rent (256);
		}

		public ImapToken AddOrGet (ImapTokenType type, ByteArrayBuilder builder)
		{
			// lookupKey is a pre-allocated key used for lookups
			lookupKey.Init (decoders, ref charBuffer, type, builder.GetBuffer (), builder.Length, out int charsNeeded);

			if (cache.TryGetValue (lookupKey, out var node)) {
				// move the node to the head of the list
				list.Remove (node);
				list.AddFirst (node);
				node.Value.Count++;

				return node.Value.Token;
			}

			var value = new string (charBuffer, 0, charsNeeded);
			var token = new ImapToken (type, value);

			if (cache.Count >= capacity) {
				// remove the least recently used token
				node = list.Last;
				list.RemoveLast ();
				cache.Remove (node.Value.Key);

				// re-use the node, item and key to avoid allocations
				node.Value.Key.Init (type, value, lookupKey);
				node.Value.Token = token;
			} else {
				var key = new ImapTokenKey (type, value, lookupKey);
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
			char[] charBuffer;
			string stringKey;
			int hashCode;
			int length;

			public ImapTokenKey ()
			{
			}

			public ImapTokenKey (ImapTokenType type, string value, ImapTokenKey key)
			{
				Init (type, value, key);
			}

			public void Init (Decoder[] decoders, ref char[] charBuffer, ImapTokenType type, byte[] key, int length, out int charsNeeded)
			{
				this.type = type;

				var hash = new HashCode ();
				hash.Add ((int) type);

				charsNeeded = 0;

				// Make sure the char buffer is at least as large as the key.
				if (charBuffer.Length < length) {
					ArrayPool<char>.Shared.Return (charBuffer);
					charBuffer = ArrayPool<char>.Shared.Rent (length);
				}

				foreach (var decoder in decoders) {
					bool completed;
					int index = 0;

					do {
						try {
							decoder.Convert (key, index, length - index, charBuffer, charsNeeded, charBuffer.Length - charsNeeded, true, out var bytesUsed, out var charsUsed, out completed);
							charsNeeded += charsUsed;
							index += bytesUsed;

							for (int i = 0; i < charsUsed; i++)
								hash.Add (charBuffer[i]);

							if (completed)
								break;
						} catch (DecoderFallbackException) {
							// Restart the hash...
							hash = new HashCode ();
							hash.Add ((int) type);
							completed = false;
							charsNeeded = 0;
							break;
						}

						// The char buffer was not large enough to contain the full token. Resize it and try again.
						var newBuffer = ArrayPool<char>.Shared.Rent (charBuffer.Length + (length - index));
						charBuffer.AsSpan (0, charsNeeded).CopyTo (newBuffer);
						ArrayPool<char>.Shared.Return (charBuffer);
						charBuffer = newBuffer;
					} while (true);

					decoder.Reset ();

					if (completed)
						break;
				}

				this.charBuffer = charBuffer;
				this.length = charsNeeded;

				this.hashCode = hash.ToHashCode ();
			}

			public void Init (ImapTokenType type, string value, ImapTokenKey key)
			{
				this.type = type;
				this.charBuffer = null;
				this.stringKey = value;
				this.length = value.Length;
				this.hashCode = key.hashCode;
			}

			static bool Equals (string str, char[] chars)
			{
				for (int i = 0; i < str.Length; i++) {
					if (str[i] != chars[i])
						return false;
				}

				return true;
			}

			static bool Equals (ImapTokenKey self, ImapTokenKey other)
			{
				if (self.type != other.type || self.length != other.length)
					return false;

				// Note: At most, only one of the ImapTokenKeys will use a charBuffer and that ImapTokenKey will be the lookup key.
				if (self.stringKey != null) {
					if (other.stringKey != null)
						return self.stringKey.Equals (other.stringKey, StringComparison.Ordinal);

					return Equals (self.stringKey, other.charBuffer);
				} else {
					// Note: 'self' MUST be the lookup key.
					Debug.Assert (self.charBuffer != null);

					return Equals (other.stringKey, self.charBuffer);
				}
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
				return string.Format ("{0}: {1}", type, stringKey ?? new string (charBuffer, 0, length));
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
