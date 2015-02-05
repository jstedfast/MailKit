
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

namespace ImapClientDemo
{
    [Activity (Label = "Details")]			
    public class ViewMessageActivity : Activity
    {
        TextView textSubject;
        TextView textFrom;
        TextView textDate;
        TextView textBody;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.ViewMessageLayout);
         
            textSubject = FindViewById<TextView> (Resource.Id.textSubject);
            textFrom = FindViewById<TextView> (Resource.Id.textFrom);
            textDate = FindViewById<TextView> (Resource.Id.textDate);
            textBody = FindViewById<TextView> (Resource.Id.textBody);

            var msg = Mail.CurrentMessage;

            textSubject.Text = msg.Subject;
            textFrom.Text = msg.From.ToString ();
            textDate.Text = msg.Date.LocalDateTime.ToString ();
            textBody.Text = msg.TextBody;
        }
    }
}

