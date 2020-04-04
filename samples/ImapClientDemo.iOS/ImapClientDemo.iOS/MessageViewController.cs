//
// MessageViewController.cs
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

using MimeKit;

namespace ImapClientDemo.iOS
{
    public class MessageViewController : DialogViewController
    {
        public MessageViewController (MimeMessage msg) : base (UITableViewStyle.Grouped, null, true)
        {
            Root = new RootElement ("Details") {
                new Section {
                    new StyledMultilineElement (msg.Subject),
                    new StyledMultilineElement ("From:", msg.From.ToString ()),
                    new StyledStringElement (msg.Date.LocalDateTime.ToString ()) {
                        Font = UIFont.SystemFontOfSize (12f)
                    },
                },
                new Section {
                    new StyledMultilineElement (msg.TextBody) {
                        Font = UIFont.SystemFontOfSize (12f)
                    }
                },
            };
        }
    }
}
