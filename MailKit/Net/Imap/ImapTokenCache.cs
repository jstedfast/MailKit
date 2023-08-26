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

namespace MailKit.Net.Imap
{
	class ImapTokenCache
	{
		const int capacity = 128;

		readonly Dictionary<ImapTokenKey, LinkedListNode<ImapTokenItem>> cache;
		readonly LinkedList<ImapTokenItem> list;

		public ImapTokenCache ()
		{
			cache = new Dictionary<ImapTokenKey, LinkedListNode<ImapTokenItem>> ();
			list = new LinkedList<ImapTokenItem> ();
		}

		public ImapToken AddOrGet (ImapTokenType type, ByteArrayBuilder builder)
		{
			// Note: This ImapTokenKey .ctor does not duplicate the buffer and is meant as a temporary key
			// in order to avoid memory allocations for lookup purposes.
			var key = new ImapTokenKey (builder.GetBuffer (), builder.Length);

			lock (cache) {
				if (cache.TryGetValue (key, out var node)) {
					// move the node to the head of the list
					list.Remove (node);
					list.AddFirst (node);

					return node.Value.Token;
				}

				if (cache.Count >= capacity) {
					// remove the least recently used token
					node = list.Last;
					list.RemoveLast ();
					cache.Remove (node.Value.Key);
				}

				var token = new ImapToken (type, builder.ToString ());

				// Note: We recreate the key here so we have a permanent key. Also this allows for reuse of the token's Value string.
				key = new ImapTokenKey ((string) token.Value);

				var item = new ImapTokenItem (key, token);

				node = new LinkedListNode<ImapTokenItem> (item);
				cache.Add (key, node);
				list.AddFirst (node);

				return token;
			}
		}

		class ImapTokenKey
		{
			readonly byte[] byteArrayKey;
			readonly string stringKey;
			readonly int length;
			readonly int hashCode;

			public ImapTokenKey (byte[] key, int len)
			{
				byteArrayKey = key;
				length = len;

				var hash = new HashCode ();
				for (int i = 0; i < length; i++)
					hash.Add ((char) key[i]);

				hashCode = hash.ToHashCode ();
			}

			public ImapTokenKey (string key)
			{
				stringKey = key;
				length = key.Length;

				var hash = new HashCode ();
				for (int i = 0; i < length; i++)
					hash.Add (key[i]);

				hashCode = hash.ToHashCode ();
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
				if (self.length != other.length)
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
		}

		class ImapTokenItem
		{
			public readonly ImapTokenKey Key;
			public readonly ImapToken Token;

			public ImapTokenItem (ImapTokenKey key, ImapToken token)
			{
				Key = key;
				Token = token;
			}
		}
	}
}
