using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

using MailKit;

namespace ImapClientDemo.iOS
{
    public partial class FoldersViewController : DialogViewController
    {
        MessagesViewController messagesViewController;

        public FoldersViewController () : base (UITableViewStyle.Grouped, null, true)
        {
            Root = new RootElement ("Folders");
        }

        public async override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            var foldersSection = new Section ();

            var personal = Mail.Client.GetFolder (Mail.Client.PersonalNamespaces [0]); 

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

            var subfolders = await folder.GetSubfoldersAsync ();

            foreach (var sf in subfolders)
                await LoadChildFolders (foldersSection, sf);
        }

        void OpenFolder (IMailFolder folder)
        {
            messagesViewController = new MessagesViewController (folder);
            NavigationController.PushViewController (messagesViewController, true);
        }
    }
}
