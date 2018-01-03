//
// FolderAttributes.cs
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

namespace MailKit {
	/// <summary>
	/// Folder attributes as used by <see cref="IMailFolder.Attributes"/>.
	/// </summary>
	/// <remarks>
	/// Folder attributes as used by <see cref="IMailFolder.Attributes"/>.
	/// </remarks>
	[Flags]
	public enum FolderAttributes {
		/// <summary>
		/// The folder does not have any attributes.
		/// </summary>
		None          = 0,

		/// <summary>
		/// It is not possible for any subfolders to exist under the folder.
		/// </summary>
		NoInferiors   = (1 << 0),

		/// <summary>
		/// It is not possible to select the folder.
		/// </summary>
		NoSelect      = (1 << 1),

		/// <summary>
		/// The folder has been marked as possibly containing new messages
		/// since the folder was last selected.
		/// </summary>
		Marked        = (1 << 2),

		/// <summary>
		/// The folder does not contain any new messages since the folder
		/// was last selected.
		/// </summary>
		Unmarked      = (1 << 3),

		/// <summary>
		/// The folder does not exist, but is simply a place-holder.
		/// </summary>
		NonExistent   = (1 << 4),

		/// <summary>
		/// The folder is subscribed.
		/// </summary>
		Subscribed    = (1 << 5),

		/// <summary>
		/// The folder is remote.
		/// </summary>
		Remote        = (1 << 6),

		/// <summary>
		/// The folder has subfolders.
		/// </summary>
		HasChildren   = (1 << 7),

		/// <summary>
		/// The folder does not have any subfolders.
		/// </summary>
		HasNoChildren = (1 << 8),

		/// <summary>
		/// The folder is a special "All" folder containing an aggregate of all messages.
		/// </summary>
		All           = (1 << 9),

		/// <summary>
		/// The folder is a special "Archive" folder.
		/// </summary>
		Archive       = (1 << 10),

		/// <summary>
		/// The folder is the special "Drafts" folder.
		/// </summary>
		Drafts        = (1 << 11),

		/// <summary>
		/// The folder is the special "Flagged" folder.
		/// </summary>
		Flagged       = (1 << 12),

		/// <summary>
		/// The folder is the special "Inbox" folder.
		/// </summary>
		Inbox         = (1 << 13),

		/// <summary>
		/// The folder is the special "Junk" folder.
		/// </summary>
		Junk          = (1 << 14),

		/// <summary>
		/// The folder is the special "Sent" folder.
		/// </summary>
		Sent          = (1 << 15),

		/// <summary>
		/// The folder is the special "Trash" folder.
		/// </summary>
		Trash         = (1 << 16),
	}
}
