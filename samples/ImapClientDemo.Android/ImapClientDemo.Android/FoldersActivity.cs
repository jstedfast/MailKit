
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MailKit;
using System.Threading.Tasks;

namespace ImapClientDemo
{
    [Activity (Label = "Folders")]			
    public class FoldersActivity : Activity
    {
        ListView listView;
        FoldersAdapter adapter;

        protected async override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            Console.WriteLine ("FoldersCreated!");

            SetContentView (Resource.Layout.FoldersLayout);

            listView = FindViewById<ListView> (Resource.Id.listView);

            adapter = new FoldersAdapter (this); 

            listView.Adapter = adapter;
            listView.ItemClick += (sender, e) => {

                var folder = adapter [e.Position];

                Mail.CurrentFolder = folder;

                StartActivity (typeof (MessagesActivity));
            };

            await Reload ();
        }
            
        async Task Reload ()
        {
            var personal = Mail.Client.GetFolder (Mail.Client.PersonalNamespaces [0]); 

            var folders = new List<IMailFolder> ();

            // Recursively load all folders and subfolders
            await LoadChildFolders (folders, personal);

            adapter.Folders = folders;
            adapter.NotifyDataSetChanged ();
        }

        // Recursive function to load all folders and their subfolders
        async Task LoadChildFolders (List<IMailFolder> folders, IMailFolder imapFolder)
        {
            if (!string.IsNullOrWhiteSpace (imapFolder.FullName))
                folders.Add (imapFolder);

            var subfolders = await imapFolder.GetSubfoldersAsync ();

            foreach (var sf in subfolders)
                await LoadChildFolders (folders, sf);
        }


        public class FoldersAdapter : BaseAdapter<IMailFolder>
        {
            public FoldersAdapter (Activity parent)
            {
                Parent = parent;
                Folders = new List<IMailFolder> ();
            }

            public List<IMailFolder> Folders { get;set; }
            public Activity Parent { get;set; }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override int Count {
                get { return Folders.Count; }
            }

            public override IMailFolder this [int index] {
                get { return Folders [index]; }
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                var view = convertView 
                    ?? LayoutInflater.FromContext (Parent).Inflate (Android.Resource.Layout.SimpleListItem1, parent, false);

                var folder = Folders [position];

                view.FindViewById<TextView> (Android.Resource.Id.Text1).Text = folder.FullName;

                return view;
            }
        }
    }
}

