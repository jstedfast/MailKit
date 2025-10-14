﻿//
// IMessageSummaryIndex.cs
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

using System;
using System.Threading;
using System.Collections.Generic;

namespace MailKit.Caching {
	public interface IMessageSummaryIndex : IDisposable
	{
		// FIXME: Does it make sense to have UIDVALIDITY, UIDNEXT, and HIGHESTMODSEQ values on this interface? Or some other interface?
		uint? UidValidity { get; }
		uint? UidNext { get; }
		ulong? HighestModSeq { get; }

		int Count { get; }

		bool IsDirty { get; }

		void Load (CancellationToken cancellationToken = default);
		void Save (CancellationToken cancellationToken = default);

		void OnUidValidityChanged (uint uidValidity);
		void OnUidNextChanged (uint nextUid);
		void OnHighestModSeqChanged (ulong highestModSeq);

		void Rename (string newFullName);
		void Delete ();

		void AddOrUpdate (IMessageSummary message);

		void Expunge (int index);
		void Expunge (IEnumerable<UniqueId> uids);

		IList<IMessageSummary> Fetch (int min, int max, IFetchRequest request);
		IList<IMessageSummary> Fetch (IList<int> indexes, IFetchRequest request);
		IList<IMessageSummary> Fetch (IList<UniqueId> uids, IFetchRequest request);
	}
}
