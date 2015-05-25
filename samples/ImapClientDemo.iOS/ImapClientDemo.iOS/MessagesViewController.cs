using System;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

using MailKit;

namespace ImapClientDemo.iOS
{
    public partial class MessagesViewController : DialogViewController
    {
        ViewMessageViewController viewMessageViewController;

        public MessagesViewController (IMailFolder folder) : base (UITableViewStyle.Grouped, null, true)
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
                sectionMessages.Add (new MessageElement ( async (viewController, tableView, indexPath) => {

                    // When a message is selected, fetch the actual message by UID
                    var msg = await Folder.GetMessageAsync (summaries [indexPath.Row].UniqueId.Value);

                    // Show the message details view controller
                    viewMessageViewController = new ViewMessageViewController (msg);
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
