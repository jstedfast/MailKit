//
// MessageListViewController.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2020 Xamarin Inc. (www.xamarin.com)
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
using System.Linq;
using System.Collections.Generic;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

using MailKit;

namespace ImapClientDemo.iOS
{
    public partial class MessageListViewController : DialogViewController
    {
        MessageViewController viewMessageViewController;

        public MessageListViewController (IMailFolder folder) : base (UITableViewStyle.Grouped, null, true)
        {
            Folder = folder;

            Root = new RootElement (folder.FullName) {
                new Section ()
            };
        }

        public IMailFolder Folder { get; private set; }

        public async override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Open the folder for reading
            await Folder.OpenAsync (FolderAccess.ReadOnly);

            // Get the message summaries
            var summaries = await Folder.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate);

            var sectionMessages = new Section ();

            // Loop through all the message summaries
            foreach (var s in summaries) {
                // Add an item to the UI section/list
                sectionMessages.Add (new MessageElement (async (viewController, tableView, indexPath) => {
                    // When a message is selected, fetch the actual message by UID
                    var msg = await Folder.GetMessageAsync (summaries [indexPath.Row].UniqueId);

                    // Show the message details view controller
                    viewMessageViewController = new MessageViewController (msg);
                    NavigationController.PushViewController (viewMessageViewController, true);
                }) {
                    Sender = s.Envelope.Sender.ToString (),
                    Subject = s.Envelope.Subject,
                    Body = "",
                    Date = s.Envelope.Date.Value.LocalDateTime,
                    NewFlag = false,
                    MessageCount = 0,                   
                });
            }

            Root.Clear ();
            Root.Add (sectionMessages);
        }
    }
}
