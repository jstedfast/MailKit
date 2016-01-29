//
// FoldersViewController.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
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
using System.Threading.Tasks;
using System.Collections.Generic;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

using MailKit;

namespace ImapClientDemo.iOS
{
    public class FoldersViewController : DialogViewController
    {
		MessageListViewController messageListViewController;

        public FoldersViewController () : base (UITableViewStyle.Grouped, null, true)
        {
            Root = new RootElement ("Folders");
        }

        public async override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            var foldersSection = new Section ();

            var personal = Mail.Client.GetFolder (Mail.Client.PersonalNamespaces[0]); 

            // Recursively load all folders and subfolders
            await LoadChildFolders (foldersSection, personal);

            Root.Clear ();
            Root.Add (foldersSection);
        }

        // Recursive function to load all folders and their subfolders
        async Task LoadChildFolders (Section foldersSection, IMailFolder folder)
        {
            if (!folder.IsNamespace) {
                foldersSection.Add (new StyledStringElement (folder.FullName, () =>
                    OpenFolder (folder)));
            }

			if (folder.Attributes.HasFlag (FolderAttributes.HasNoChildren))
				return;

            var subfolders = await folder.GetSubfoldersAsync ();

            foreach (var sf in subfolders)
                await LoadChildFolders (foldersSection, sf);
        }

        void OpenFolder (IMailFolder folder)
        {
            messageListViewController = new MessageListViewController (folder);
            NavigationController.PushViewController (messageListViewController, true);
        }
    }
}
