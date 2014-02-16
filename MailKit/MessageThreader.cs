//
// MessageThreader.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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

using MimeKit.Utils;
using MailKit.Search;

namespace MailKit {
	/// <summary>
	/// Threads messages according to the algorithms defined in rfc5256.
	/// </summary>
	public static class MessageThreader
	{
		class ThreadableNode : ISortable
		{
			public readonly List<ThreadableNode> Children = new List<ThreadableNode> ();
			public ThreadableNode Parent;
			public IThreadable Message;

			public bool HasParent {
				get { return Parent != null; }
			}

			public bool HasChildren {
				get { return Children.Count > 0; }
			}

			public string ThreadableSubject {
				get {
					if (Message != null)
						return Message.ThreadableSubject;

					return Children[0].ThreadableSubject;
				}
			}

			#region ISortable implementation

			bool ISortable.CanSort {
				get { return true; }
			}

			int ISortable.SortableIndex {
				get {
					if (Message != null)
						return Message.SortableIndex;

					return ((ISortable) Children[0]).SortableIndex;
				}
			}

			string ISortable.SortableCc {
				get {
					if (Message != null)
						return Message.SortableCc;

					return ((ISortable) Children[0]).SortableCc;
				}
			}

			DateTimeOffset ISortable.SortableDate {
				get {
					if (Message != null)
						return Message.SortableDate;

					return ((ISortable) Children[0]).SortableDate;
				}
			}

			string ISortable.SortableFrom {
				get {
					if (Message != null)
						return Message.SortableFrom;

					return ((ISortable) Children[0]).SortableFrom;
				}
			}

			uint ISortable.SortableSize {
				get {
					if (Message != null)
						return Message.SortableSize;

					return ((ISortable) Children[0]).SortableSize;
				}
			}

			string ISortable.SortableSubject {
				get {
					if (Message != null)
						return Message.SortableSubject;

					return ((ISortable) Children[0]).SortableSubject;
				}
			}

			string ISortable.SortableTo {
				get {
					if (Message != null)
						return Message.SortableTo;

					return ((ISortable) Children[0]).SortableTo;
				}
			}

			#endregion
		}

		static IDictionary<string, ThreadableNode> CreateIdTable (IEnumerable<IThreadable> messages)
		{
			var ids = new Dictionary<string, ThreadableNode> ();
			ThreadableNode node;

			foreach (var message in messages) {
				if (!message.CanThread)
					throw new ArgumentException ("One or more messages is missing information needed for threading.", "messages");

				var id = message.ThreadableMessageId;

				if (ids.TryGetValue (id, out node)) {
					if (node.Message == null) {
						// a previously processed message referenced this message
						node.Message = message;
					} else {
						// a duplicate message-id, just create a dummy id and use that
						id = MimeUtils.GenerateMessageId ();
						node = null;
					}
				}

				if (node == null) {
					// create a new ThreadContainer for this message and add it to ids
					node = new ThreadableNode ();
					node.Message = message;
					ids.Add (id, node);
				}

				ThreadableNode parent = null;
				foreach (var reference in message.ThreadableReferences) {
					ThreadableNode referenced;

					if (!ids.TryGetValue (reference, out referenced)) {
						// create a dummy container for the referenced message
						referenced = new ThreadableNode ();
						ids.Add (reference, referenced);
					}

					// chain up the references, disallowing loops
					if (parent != null && referenced.Parent == null && parent != referenced && !parent.Children.Contains (referenced)) {
						parent.Children.Add (referenced);
						referenced.Parent = parent;
					}

					parent = referenced;
				}

				// don't allow loops
				if (parent != null && (parent == node || node.Children.Contains (parent)))
					parent = null;

				if (node.HasParent) {
					// unlink from our old parent
					node.Parent.Children.Remove (node);
					node.Parent = null;
				}

				if (parent != null) {
					// add it as a child of our new parent
					parent.Children.Add (node);
					node.Parent = parent;
				}
			}

			return ids;
		}

		static ThreadableNode CreateRoot (IDictionary<string, ThreadableNode> ids)
		{
			var root = new ThreadableNode ();

			foreach (var message in ids.Values) {
				if (message.Parent == null)
					root.Children.Add (message);
			}

			return root;
		}

		static void PruneEmptyContainers (ThreadableNode root)
		{
			for (int i = 0; i < root.Children.Count; i++) {
				var node = root.Children[i];

				if (node.Message == null && node.Children.Count == 0) {
					// this is an empty container with no children, nuke it.
					root.Children.RemoveAt (i);
					i--;
				} else if (node.Message == null && node.HasChildren && (node.HasParent || node.Children.Count == 1)) {
					// If the Container has no Message, but does have children, remove this container but promote
					// its children to this level (that is, splice them in to the current child list.)
					//
					// Do not promote the children if doing so would promote them to the root set -- unless there
					// is only one child, in which case, do.
					root.Children.RemoveAt (i);

					for (int j = 0; j < node.Children.Count; j++) {
						node.Children[j].Parent = node.Parent;
						root.Children.Add (node.Children[j]);
					}

					node.Children.Clear ();
					i--;
				} else if (node.HasChildren) {
					PruneEmptyContainers (node);
				}
			}
		}

		static void GroupBySubject (ThreadableNode root)
		{
			var subjects = new Dictionary<string, ThreadableNode> (StringComparer.OrdinalIgnoreCase);
			ThreadableNode match;
			int count = 0;

			for (int i = 0; i < root.Children.Count; i++) {
				var current = root.Children[i];
				var subject = current.ThreadableSubject;

				// don't thread messages with empty subjects
				if (string.IsNullOrEmpty (subject))
					continue;

				if (!subjects.TryGetValue (subject, out match) ||
					(current.Message == null && match.Message != null) ||
					(match.Message != null && match.Message.IsThreadableReply &&
						current.Message != null && !current.Message.IsThreadableReply)) {
					subjects[subject] = current;
					count++;
				}
			}

			if (count == 0)
				return;

			for (int i = 0; i < root.Children.Count; i++) {
				var current = root.Children[i];
				var subject = current.ThreadableSubject;

				// don't thread messages with empty subjects
				if (string.IsNullOrEmpty (subject))
					continue;

				match = subjects[subject];

				if (match == current)
					continue;

				// remove the second message with the same subject
				root.Children.RemoveAt (i);

				// group these messages together...
				if (match.Message == null && current.Message == null) {
					// If both messages are dummies, append the current message's children
					// to the children of the message in the subject table (the children of
					// both messages become siblings), and then delete the current message.
					match.Children.AddRange (current.Children);
				} else if (match.Message == null && current.Message != null) {
					// If the message in the subject table is a dummy and the current message
					// is not, make the current message a child of the message in the subject
					// table (a sibling of its children).
					match.Children.Add (current);
				} else if (current.Message.IsThreadableReply && !match.Message.IsThreadableReply) {
					// If the current message is a reply or forward and the message in the
					// subject table is not, make the current message a child of the message
					// in the subject table (a sibling of its children).
					match.Children.Add (current);
				} else {
					// Otherwise, create a new dummy message and make both the current message
					// and the message in the subject table children of the dummy. Then replace
					// the message in the subject table with the dummy message.

					// Note: if we re-use the node already in the subject table and the root, then
					// we won't have to insert the new dummy node at the matched node's location
					var dummy = match;

					// clone the message already in the subject table
					match = new ThreadableNode ();
					match.Message = dummy.Message;
					match.Children.AddRange (dummy.Children);

					// empty out the old match node (aka the new dummy node)
					dummy.Children.Clear ();
					dummy.Message = null;

					// now add both messages to the dummy
					dummy.Children.Add (match);
					dummy.Children.Add (current);
				}
			}
		}

		static void GetThreads (ThreadableNode root, List<MessageThread> threads, OrderBy[] orderBy)
		{
			var sorted = MessageSorter.Sort (root.Children, orderBy);

			for (int i = 0; i < sorted.Count; i++) {
				var message = sorted[i].Message;
				UniqueId? uid = null;

				if (message != null)
					uid = message.ThreadableUid;

				var thread = new MessageThread (uid);
				GetThreads (sorted[i], thread.children, orderBy);
				threads.Add (thread);
			}
		}

		static MessageThread[] ThreadByReferences (IEnumerable<IThreadable> messages, OrderBy[] orderBy)
		{
			var threads = new List<MessageThread> ();
			var ids = CreateIdTable (messages);
			var root = CreateRoot (ids);

			PruneEmptyContainers (root);
			GroupBySubject (root);

			GetThreads (root, threads, orderBy);

			return threads.ToArray ();
		}

		static MessageThread[] ThreadBySubject (IEnumerable<IThreadable> messages, OrderBy[] orderBy)
		{
			var threads = new List<MessageThread> ();
			var root = new ThreadableNode ();

			foreach (var message in messages) {
				if (!message.CanThread)
					throw new ArgumentException ("One or more messages is missing information needed for threading.", "messages");

				var container = new ThreadableNode ();
				container.Message = message;

				root.Children.Add (container);
			}

			GroupBySubject (root);

			GetThreads (root, threads, orderBy);

			return threads.ToArray ();
		}

		/// <summary>
		/// Thread the messages according to the specified threading algorithm.
		/// </summary>
		/// <returns>The threaded messages.</returns>
		/// <param name="algorithm">The threading algorithm.</param>
		/// <param name="messages">The messages.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="messages"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not a valid threading algorithm.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="messages"/> contains one or more items that is missing information needed for threading.
		/// </exception>
		public static MessageThread[] Thread (ThreadingAlgorithm algorithm, IEnumerable<IThreadable> messages)
		{
			return Thread (algorithm, messages, new [] { OrderBy.Arrival });
		}

		/// <summary>
		/// Threads the messages according to the specified threading algorithm
		/// and sorts the resulting threads by the specified ordering.
		/// </summary>
		/// <returns>The threaded messages.</returns>
		/// <param name="algorithm">The threading algorithm.</param>
		/// <param name="messages">The messages.</param>
		/// <param name="orderBy">The requested sort ordering.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not a valid threading algorithm.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="messages"/> contains one or more items that is missing information needed for threading or sorting.
		/// </exception>
		public static MessageThread[] Thread (ThreadingAlgorithm algorithm, IEnumerable<IThreadable> messages, OrderBy[] orderBy)
		{
			if (messages == null)
				throw new ArgumentNullException ("messages");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			switch (algorithm) {
			case ThreadingAlgorithm.OrderedSubject: return ThreadBySubject (messages, orderBy);
			case ThreadingAlgorithm.References: return ThreadByReferences (messages, orderBy);
			default: throw new ArgumentOutOfRangeException ("algorithm");
			}
		}

		static bool IsForward (string subject, int index)
		{
			return (subject[index] == 'F' || subject[index] == 'f') &&
				(subject[index + 1] == 'W' || subject[index + 1] == 'w') &&
				(subject[index + 2] == 'D' || subject[index + 2] == 'd') &&
				subject[index + 3] == ':';
		}

		static bool IsReply (string subject, int index)
		{
			return (subject[index] == 'R' || subject[index] == 'r') &&
				(subject[index + 1] == 'E' || subject[index + 1] == 'e');
		}

		static void SkipWhiteSpace (string subject, ref int index)
		{
			while (index < subject.Length && char.IsWhiteSpace (subject[index]))
				index++;
		}

		static bool IsMailingListName (char c)
		{
			return c == '-' || c == '_' || char.IsLetterOrDigit (c);
		}

		static void SkipMailingListName (string subject, ref int index)
		{
			while (index < subject.Length && IsMailingListName (subject[index]))
				index++;
		}

		static bool SkipDigits (string subject, ref int index, out int value)
		{
			int startIndex = index;

			value = 0;

			while (index < subject.Length && char.IsDigit (subject[index])) {
				value = (value * 10) + (subject[index] - '0');
				index++;
			}

			return index > startIndex;
		}

		/// <summary>
		/// Gets the threadable subject.
		/// </summary>
		/// <returns>The threadable subject.</returns>
		/// <param name="subject">The Subject header value.</param>
		/// <param name="replyDepth">The reply depth.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="subject"/> is <c>null</c>.
		/// </exception>
		public static string GetThreadableSubject (string subject, out int replyDepth)
		{
			if (subject == null)
				throw new ArgumentNullException ("subject");

			replyDepth = 0;

			int endIndex = subject.Length;
			int startIndex = 0;
			int index, count;
			int left;

			do {
				SkipWhiteSpace (subject, ref startIndex);
				index = startIndex;

				if ((left = (endIndex - index)) < 3)
					break;

				if (left >= 4 && IsForward (subject, index)) {
					// skip over the "Fwd:" prefix
					startIndex = index + 4;
					replyDepth++;
					continue;
				}

				if (IsReply (subject, index)) {
					if (subject[index + 2] == ':') {
						// skip over the "Re:" prefix
						startIndex = index + 3;
						replyDepth++;
						continue;
					}

					if (subject[index + 2] == '[' || subject[index + 2] == '(') {
						char close = subject[index + 2] == '[' ? ']' : ')';

						// skip over "Re[" or "Re("
						index += 3;

						// if this is followed by "###]:" or "###):", then it's a condensed "Re:"
						if (SkipDigits (subject, ref index, out count) && (endIndex - index) >= 2 &&
							subject[index] == close && subject[index + 1] == ':') {
							startIndex = index + 2;
							replyDepth += count;
							continue;
						}
					}
				} else if (subject[index] == '[' && char.IsLetterOrDigit (subject[index + 1])) {
					// possibly a mailing-list prefix
					index += 2;

					SkipMailingListName (subject, ref index);

					if ((endIndex - index) >= 1 && subject[index] == ']') {
						startIndex = index + 1;
						continue;
					}
				}

				break;
			} while (true);

			// trim trailing whitespace
			while (endIndex > 0 && char.IsWhiteSpace (subject[endIndex - 1]))
				endIndex--;

			// canonicalize the remainder of the subject, condensing multiple spaces into 1
			var builder = new StringBuilder ();
			bool lwsp = false;

			for (int i = startIndex; i < endIndex; i++) {
				if (char.IsWhiteSpace (subject[i])) {
					if (!lwsp) {
						builder.Append (' ');
						lwsp = true;
					}
				} else {
					builder.Append (subject[i]);
					lwsp = false;
				}
			}

			var canonicalized = builder.ToString ();

			if (canonicalized.ToLowerInvariant () == "(no subject)")
				canonicalized = string.Empty;

			return canonicalized;
		}
	}
}
