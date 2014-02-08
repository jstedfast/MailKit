# MailKit

## What is MailKit?

MailKit is a cross-platform mail client library built on top of [MimeKit](https://github.com/jstedfast/MimeKit).

## Features

* SASL Authentication
  * DIGEST-MD5
  * CRAM-MD5
  * LOGIN
  * PLAIN
  * XOAUTH2 (partial support - you need to fetch the auth tokens yourself)
* SMTP Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "smtps" protocol.
  * Supports the STARTTLS extension.
  * Supports client SSL/TLS certificates.
  * Supports the 8BITMIME and BINARYMIME extensions.
  * Supports the PIPELINING extension.
  * Parses the SIZE extension value.
  * All APIs are cancellable.
* POP3 Client
  * Supports all of the SASL mechanisms listed above.
  * Also supports authentication via APOP and USER/PASS.
  * Supports SSL-wrapped connections via the "pop3s" protocol.
  * Supports the STLS extension.
  * Supports client SSL/TLS certificates.
  * Supports the UIDL command.
  * All APIs are cancellable.
* IMAP4 Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "imaps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * LITERAL+
    * NAMESPACE
    * CHILDREN
    * LOGINDISABLED
    * STARTTLS
    * MULTIAPPEND
    * UNSELECT
    * UIDPLUS
    * CONDSTORE
    * ESEARCH
    * SASL-IR
    * SORT
    * THREAD
    * SPECIAL-USE
    * MOVE
    * XLIST
    * X-GM-EXT1 (X-GM-MSGID and X-GM-THRID)
  * All APIs are cancellable.

## TODO

* SASL Authentication
  * Improve XOAUTH2
  * ANONYMOUS
  * GSSAPI
  * KERBEROS_v4
  * NTLM
  * SCRAM-*
* SMTP Client
  * CHUNKING (hmmm, doesn't really seem all that useful...)
  * Throw an exception if the MimeMessage is larger than the SIZE value?
  * Async APIs
* POP3 Client
  * PIPELINING
  * Async APIs
* IMAP4 Client
  * Extensions:
    * QUOTA
    * IDLE
    * BINARY
    * CATENATE
    * COMPRESS
    * ENABLE
    * QRESYNC
    * LIST-EXTENDED
    * CONVERT
    * ESORT
    * METADATA
    * NOTIFY
    * FILTERS
    * LIST-STATUS
    * MULTISEARCH
    * GMail Extensions
      * X-GM-RAW
      * X-GM-LABELS
  * Async APIs
* Maildir
* Thunderbird mbox folder trees

## Goals

The main goal of this project is to optimize for mobile platforms such as Android, iOS, and eventually Windows Phone 8.
This means that IMAP extensions such as CONDSTORE, QRESYNC, and any other extensions that allow clients to reduce
network traffic (IDLE? ESEARCH? COMPRESS?) will take priority over any of the other extensions. After that, the most
common extensions will take precedence over the lesser-used extensions until finally all of the extensions anyone
actually cares about are implemented.

At some point in the (near?) future, I'd also like to start using async/await. For those who are stuck in .NET 4.0,
Microsoft has released an
[Async Targetting Pack](http://blogs.msdn.com/b/bclteam/archive/2013/04/17/microsoft-bcl-async-is-now-stable.aspx)
on [NuGet](https://nuget.org/packages/Microsoft.Bcl.Async) which allows the use of the async/await keywords on
pre-.NET 4.5 platforms. If you haven't already, I'd highly recommend installing it.

## License Information

MailKit is Copyright (C) 2013-2014 Xamarin Inc. and is licensed under the MIT license:

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.

## Installing via NuGet

The easiest way to install MimeKit is via [NuGet](https://www.nuget.org/packages/MailKit/).

In Visual Studio's [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console),
simply enter the following command:

    Install-Package MailKit

## Building

First, you'll need to clone MailKit, MimeKit and Bouncy Castle from my GitHub repository:

    git clone https://github.com/jstedfast/MailKit.git
    git clone https://github.com/jstedfast/MimeKit.git
    git clone https://github.com/jstedfast/bc-csharp.git

Currently, MailKit (through its use of MimeKit) depends on the visual-studio-2010 branch of bc-csharp for
the Visual Studio 2010 project files that I've added (to replace the Visual Studio 2003 project files).
To switch to that branch,

    cd bc-csharp
    git checkout -b visual-studio-2010 origin/visual-studio-2010

In the top-level MailKit source directory, there are three solution files: MailKit.sln, MailKit.Net45.sln and MailKit.Mobile.sln.

* MailKit.Mobile.sln just includes the Xamarin.iOS and Xamarin.Android projects.
* MailKit.Net40.sln just includes the .NET Framework 4.0 C# project (MailKit/MailKit.csproj)
* MailKit.sln includes everything that is in the MailKit.Net40.sln solution as well as the projects for Xamarin.Android,
Xamarin.iOS, and Xamarin.Mac.

If you don't have the Xamarin products, you'll probably want to open the MailKit.Net45.sln instead of MailKit.sln.

Once you've opened the appropriate MailKit solution file in either Xamarin Studio or Visual Studio 2010+ (either will work),
you can simply choose the Debug or Release build configuration and then build.

Note: The Release build will generate the xml API documentation, but the Debug build will not.

## Using MailKit

### Sending Messages

One of the more common operations that MailKit is meant for is sending email messages.

```csharp
using System;
using System.Net;
using System.Threading;

using MailKit.Net.Smtp;
using MailKit;
using MimeKit;

namespace TestClient {
	class Program
	{
		public static void Main (string[] args)
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Joey Tribbiani", "joey@friends.com"));
			message.To.Add (new MailboxAddress ("Mrs. Chanandler Bong", "chandler@friends.com"));
			message.Subject = "How you doin'?";

			message.Body = new TextPart ("plain") {
				Text = @"Hey Chandler,

I just wanted to let you know that Monica and I were going to go play some paintball, you in?

-- Joey"
			};

			using (var client = new SmtpClient ()) {
				var credentials = new NetworkCredential ("joey", "password");

				// Note: if the server requires SSL-on-connect, use the "smtps" protocol instead
				var uri = new Uri ("smtp://smtp.gmail.com:587");

				using (var cancel = new CancellationTokenSource ()) {
					client.Connect (uri, cancel.Token);

					// only needed if the SMTP server requires authentication
					client.Authenticate (credentials, cancel.Token);

					client.Send (message, cancel.Token);
					client.Disconnect (true, cancel.Token);
				}
			}
		}
	}
}
```

## Retrieving Messages (via Pop3)

One of the other main uses of MailKit is retrieving messages from pop3 servers.

```csharp
using System;
using System.Net;
using System.Threading;

using MailKit.Net.Pop3;
using MailKit;
using MimeKit;

namespace TestClient {
	class Program
	{
		public static void Main (string[] args)
		{
			using (var client = new Pop3Client ()) {
				var credentials = new NetworkCredential ("joey", "password");

				// Note: if the server requires SSL-on-connect, use the "pop3s" protocol instead
				var uri = new Uri ("pop3://mail.friends.com");

				using (var cancel = new CancellationTokenSource ()) {
					client.Connect (uri, cancel.Token);
					client.Authenticate (credentials, cancel.Token);

					int count = client.GetMessageCount (cancel.Token);
					for (int i = 0; i < count; i++) {
						var message = client.GetMessage (i, cancel.Token);
						Console.WriteLine ("Subject: {0}", message.Subject);
					}

					client.Disconnect (true, cancel.Token);
				}
			}
		}
	}
}
```

## Using IMAP

More important than POP3 support is the IMAP support. Here's a simple use-case of retreiving messages from an IMAP server:

```csharp
using System;
using System.Net;
using System.Threading;

using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;

namespace TestClient {
	class Program
	{
		public static void Main (string[] args)
		{
			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("joey", "password");
				var uri = new Uri ("imaps://imap.gmail.com");

				using (var cancel = new CancellationTokenSource ()) {
					client.Connect (uri, cancel.Token);
					client.Authenticate (credentials, cancel.Token);

					// The Inbox folder is always available on all IMAP servers...
					var inbox = client.Inbox;
					inbox.Open (FolderAccess.ReadOnly, cancel.Token);

					Console.WriteLine ("Total messages: {0}", inbox.Count);
					Console.WriteLine ("Recent messages: {0}", inbox.Recent);

					for (int i = 0; i < inbox.Count; i++) {
						var message = inbox.GetMessage (i, cancel.Token);
						Console.WriteLine ("Subject: {0}", i, message.Subject);
					}

					client.Disconnect (true, cancel.Token);
				}
			}
		}
	}
}
```

However, you probably want to do more complicated things with IMAP such as fetching summary information
so that you can display a list of messages in a mail client without having to first download all of the
messages from the server:

```csharp
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.Full, cancel.Token)) {
	Console.WriteLine ("[summary] {0:D2}: {1}", summary.Index, summary.Envelope.Subject);
}
```

You may also be interested in searching...

```csharp
var query = SearchQuery.DeliveredAfter (DateTime.Parse ("2013-01-12"))
    .And (SearchQuery.SubjectContains ("MailKit")).And (SearchQuery.Seen);

foreach (var uid in inbox.Search (query, cancel.Token)) {
	var message = inbox.GetMessage (uid, cancel.Token);
	Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}
```

Of course, instead of downloading the message, you could also fetch the summary information for the matching messages
or do any of a number of other things with the UIDs that are returned.

How about navigating folders? MailKit can do that, too:

```csharp
// Get the first personal namespace and list the toplevel folders under it.
var personal = client.GetFolder (client.PersonalNamespaces[0]);
foreach (var folder in personal.GetSubfolders (false, cancel.Token))
	Console.WriteLine ("[folder] {0}", folder.Name);
```

If the IMAP server supports the SPECIAL-USE or the XLIST (GMail) extension, you can get ahold of
the pre-defined All, Drafts, Flagged (aka Important), Junk, Sent, Trash, etc folders like this:

```csharp
if ((client.Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) != 0) {
	var drafts = client.GetFolder (SpecialFolder.Drafts);
} else {
	// maybe check the user's preferences for the Drafts folder?
}
```

## Contributing

The first thing you'll need to do is fork MailKit to your own GitHub repository. Once you do that,

    git clone git@github.com/<your-account>/MailKit.git

If you use [Xamarin Studio](http://xamarin.com/studio) or [MonoDevelop](http://monodevelop.org), all of the
solution files are configured with the coding style used by MailKit. If you use Visual Studio or some
other editor, please try to maintain the existing coding style as best as you can.

Once you've got some changes that you'd like to submit upstream to the official MailKit repository,
simply send me a Pull Request and I will try to review your changes in a timely manner.

## Reporting Bugs

Have a bug or a feature request? [Please open a new issue](https://github.com/jstedfast/MailKit/issues).

Before opening a new issue, please search for existing issues to avoid submitting duplicates.

## Documentation

API documentation can be found at [http://jstedfast.github.io/MailKit/docs](http://jstedfast.github.io/MailKit/docs).
