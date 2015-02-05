
using System;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

namespace ImapClientDemo.iOS
{
    public partial class ViewMessageViewController : DialogViewController
    {
        public ViewMessageViewController (MimeKit.MimeMessage msg) : base (UITableViewStyle.Grouped, null, true)
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
