//
// FoldersActivity.cs
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

using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

using MailKit;

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

                StartActivity (typeof (MessageListActivity));
            };

            await Reload ();
        }
            
        async Task Reload ()
        {
            var personal = Mail.Client.GetFolder (Mail.Client.PersonalNamespaces[0]); 

            var folders = new List<IMailFolder> ();

            // Recursively load all folders and subfolders
            await LoadChildFolders (folders, personal);

            adapter.Folders = folders;
            adapter.NotifyDataSetChanged ();
        }

        // Recursive function to load all folders and their subfolders
		async Task LoadChildFolders (ICollection<IMailFolder> folders, IMailFolder folder)
        {
			if (!folder.IsNamespace)
                folders.Add (folder);

			if (folder.Attributes.HasFlag (FolderAttributes.HasNoChildren))
				return;

            var subfolders = await folder.GetSubfoldersAsync ();

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
