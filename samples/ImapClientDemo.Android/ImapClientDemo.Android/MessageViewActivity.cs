//
// MessageViewActivity.cs
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

using Android.App;
using Android.OS;
using Android.Widget;

namespace ImapClientDemo
{
    [Activity (Label = "Details")]			
    public class MessageViewActivity : Activity
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
