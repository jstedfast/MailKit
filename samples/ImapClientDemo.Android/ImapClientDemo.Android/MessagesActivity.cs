
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MailKit;
using Android.Webkit;

namespace ImapClientDemo
{
    [Activity (Label = "Messages")]			
    public class MessagesActivity : Activity
    {
        ListView listView;
        MessagesAdapter adapter;

        protected async override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            Title = Mail.CurrentFolder.FullName;

            SetContentView (Resource.Layout.MessagesLayout);

            listView = FindViewById<ListView> (Resource.Id.listView);
           
            adapter = new MessagesAdapter (this);
            listView.Adapter = adapter;

            listView.ItemClick += async (sender, e) => {

                var summary = adapter [e.Position];

                var msg = await Mail.CurrentFolder.GetMessageAsync (summary.UniqueId.Value);

                Mail.CurrentMessage = msg;

                StartActivity (typeof (ViewMessageActivity));
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


        public class MessagesAdapter : BaseAdapter<IMessageSummary>
        {
            public MessagesAdapter (Activity parent)
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

