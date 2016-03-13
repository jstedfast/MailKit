//
// MessageListActivity.cs
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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

using MailKit;

namespace ImapClientDemo
{
    [Activity (Label = "Messages")]
    public class MessageListActivity : Activity
    {
        ListView listView;
        MessageListAdapter adapter;

        protected async override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            Title = Mail.CurrentFolder.FullName;

            SetContentView (Resource.Layout.MessagesLayout);

            listView = FindViewById<ListView> (Resource.Id.listView);

            adapter = new MessageListAdapter (this);
            listView.Adapter = adapter;

            listView.ItemClick += async (sender, e) => {
                var summary = adapter[e.Position];

                var msg = await Mail.CurrentFolder.GetMessageAsync (summary.UniqueId);

                Mail.CurrentMessage = msg;

                StartActivity (typeof (MessageViewActivity));
            };

            await Reload ();
        }

        async Task Reload ()
        {
            // Open the folder for reading
            await Mail.CurrentFolder.OpenAsync (FolderAccess.ReadOnly);

            // Get the message summaries
            var summaries = await Mail.CurrentFolder.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate);

            // Assign to adapter and notify changed
            adapter.Messages = summaries.ToList ();
            adapter.NotifyDataSetChanged ();
        }

        public class MessageListAdapter : BaseAdapter<IMessageSummary>
        {
            public MessageListAdapter (Activity parent)
            {
                Parent = parent;
                Messages = new List<IMessageSummary> ();
            }

            public Activity Parent { get;set; }
            public List<IMessageSummary> Messages { get;set; }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override int Count {
                get { return Messages.Count; }
            }

            public override IMessageSummary this [int index] {
                get { return Messages [index]; }
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                var view = convertView
                           ?? LayoutInflater.FromContext (Parent).Inflate (Resource.Layout.MessageListItemLayout, parent, false);

                var msg = Messages [position];

                view.FindViewById<TextView> (Resource.Id.textSubject).Text = msg.Envelope.Subject;
                view.FindViewById<TextView> (Resource.Id.textFrom).Text = msg.Envelope.From.ToString ();
                view.FindViewById<TextView> (Resource.Id.textDate).Text = msg.Envelope.Date.Value.LocalDateTime.ToString ();

                return view;
            }
        }
    }
}
