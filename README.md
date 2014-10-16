# MailKit

## What is MailKit?

MailKit is a cross-platform mail client library built on top of [MimeKit](https://github.com/jstedfast/MimeKit).

## Features

* SASL Authentication
  * CRAM-MD5
  * DIGEST-MD5
  * LOGIN
  * NTLM
  * PLAIN
  * SCRAM-SHA-1
  * XOAUTH2 (partial support - you need to fetch the auth tokens yourself)
* SMTP Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "smtps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions: STARTTLS, SIZE, 8BITMIME, PIPELINING, BINARYMIME, SMTPUTF8
  * All APIs are cancellable.
  * Async APIs are available.
* POP3 Client
  * Supports all of the SASL mechanisms listed above.
  * Also supports authentication via APOP and USER/PASS.
  * Supports SSL-wrapped connections via the "pops" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions: STLS, UIDL, PIPELINING, UTF8, LANG
  * All APIs are cancellable.
  * Async APIs are available.
* IMAP4 Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "imaps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * QUOTA
    * LITERAL+
    * IDLE
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
    * COMPRESS
    * WITHIN
    * ENABLE
    * QRESYNC
    * SORT
    * THREAD
    * ESORT (partial)
    * SPECIAL-USE
    * SEARCH=FUZZY (partial)
    * MOVE
    * UTF8=ACCEPT
    * UTF8=ONLY
    * XLIST
    * X-GM-EXT1 (X-GM-MSGID, X-GM-THRID, X-GM-RAW and X-GM-LABELS)
  * All APIs are cancellable.
  * Async APIs are available.
* Client-side sorting and threading of messages.

## Goals

The main goal of this project is to provide the .NET world with robust, fully featured and RFC-compliant
SMTP, POP3, and IMAP client implementations.

All of the other .NET IMAP client implementations that I could find suffer from major architectural
problems such as ignoring unexpected untagged responses, assuming that literal string tokens will
never be used for anything other than message bodies (when in fact they could be used for pretty
much any string token in a response), assuming that the way to find the end of a message body in a
FETCH response is by scanning for ") UID", and not properly handling mailbox names with international
characters to simply name a few.

IMAP requires a LOT of time spent laboriously reading and re-reading the IMAP specifications (as well
as the MIME specifications) to understand all of the subtleties of the protocol and most (all?) of the
other Open Source .NET IMAP libraries, at least, were written by developers that only cared enough that
it worked for their simple needs. There's nothing necessarily wrong with doing that, but the web is full
of half-working, non-RFC-compliant IMAP implementations out there that it was finally time for a carefully
designed and implemented IMAP client library to be written.

For POP3, libraries such as OpenPOP.NET are actually fairly decent, although the MIME parser is far
too strict - throwing exceptions any time it encounteres a Content-Type or Content-Disposition
parameter that it doesn't already know about, which, if you read over the mailing-list, is a problem
that OpenPOP.NET users are constantly running into. MailKit's Pop3Client, of course, doesn't have this
problem. It also parses messages directly from the socket instead of downloading the message into a
large string buffer before parsing it, so you'll probably find that not only is MailKit faster (MailKit's
MIME parser, [MimeKit](https://github.com/jstedfast/MimeKit), parses messages from disk 25x faster than
OpenPOP.NET's parser), but also uses far less memory.

For SMTP, most developers use System.Net.Mail.SmtpClient which suits their needs more-or-less satisfactorily
and so is probably not high on their list of needs. However, the SmtpClient implementation included with
MailKit is a much better option if cross-platform support is needed or if the developer wants to be able to
save and re-load MIME messages before sending them via SMTP. MailKit's SmtpClient also supports PIPELINING
which should improve performance of sending messages (although might not be very noticeable).

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

The easiest way to install MailKit is via [NuGet](https://www.nuget.org/packages/MailKit/).

In Visual Studio's [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console),
simply enter the following command:

    Install-Package MailKit

## Building

First, you'll need to clone MailKit, MimeKit, Bouncy Castle, and Portable.Text.Encoding from my GitHub repository:

    git clone https://github.com/jstedfast/MailKit.git
    git clone https://github.com/jstedfast/MimeKit.git
    git clone https://github.com/jstedfast/bc-csharp.git
    git clone https://github.com/jstedfast/Portable.Text.Encoding.git

Currently, MailKit (through its use of MimeKit) depends on the visual-studio-2010 branch of bc-csharp for
the Visual Studio 2010 project files that I've added (to replace the Visual Studio 2003 project files).
To switch to that branch,

    cd bc-csharp
    git checkout -b visual-studio-2010 origin/visual-studio-2010

In the top-level MailKit source directory, there are three solution files: MailKit.sln, MailKit.Net40.sln and MailKit.Mobile.sln.

* MailKit.sln includes the projects for .NET 4.5, .NET 4.0, Xamarin.Android, and Xamarin.iOS.
* MailKit.Net40.sln just includes the .NET Framework 4.0 C# project (MailKit/MailKit.Net40.csproj)
* MailKit.Mobile.sln just includes the Xamarin.iOS and Xamarin.Android projects.
* MailKit.Win.sln just includes the Windows 8.1 Universal project (aka wpa81).

If you don't have the Xamarin products, you'll probably want to open the MailKit.Net45.sln instead of MailKit.sln.

Once you've opened the appropriate MailKit solution file in either Xamarin Studio or Visual Studio 2010+ (either will work),
you can simply choose the Debug or Release build configuration and then build.

Note: The Release build will generate the xml API documentation, but the Debug build will not.

## Using MailKit

### Sending Messages

One of the more common operations that MailKit is meant for is sending email messages.

```csharp
using System;

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
				client.Connect ("smtp.friends.com", 587, false);

				// Note: since we don't have an OAuth2 token, disable
				// the XOAUTH2 authentication mechanism.
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				// Note: only needed if the SMTP server requires authentication
				client.Authenticate ("joey", "password");

				client.Send (message);
				client.Disconnect (true);
			}
		}
	}
}
```

## Retrieving Messages (via Pop3)

One of the other main uses of MailKit is retrieving messages from pop3 servers.

```csharp
using System;

using MailKit.Net.Pop3;
using MailKit;
using MimeKit;

namespace TestClient {
	class Program
	{
		public static void Main (string[] args)
		{
			using (var client = new Pop3Client ()) {
				client.Connect ("pop.friends.com", 110, false);

				// Note: since we don't have an OAuth2 token, disable
				// the XOAUTH2 authentication mechanism.
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				client.Authenticate ("joey", "password");

				int count = client.GetMessageCount ();
				for (int i = 0; i < count; i++) {
					var message = client.GetMessage (i);
					Console.WriteLine ("Subject: {0}", message.Subject);
				}

				client.Disconnect (true);
			}
		}
	}
}
```

## Using IMAP

More important than POP3 support is the IMAP support. Here's a simple use-case of retreiving messages from an IMAP server:

```csharp
using System;

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
				client.Connect ("imap.friends.com", 995, true);

				// Note: since we don't have an OAuth2 token, disable
				// the XOAUTH2 authentication mechanism.
				client.AuthenticationMechanisms.Remove ("XOAUTH");

				client.Authenticate ("joey", "password");

				// The Inbox folder is always available on all IMAP servers...
				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadOnly);

				Console.WriteLine ("Total messages: {0}", inbox.Count);
				Console.WriteLine ("Recent messages: {0}", inbox.Recent);

				for (int i = 0; i < inbox.Count; i++) {
					var message = inbox.GetMessage (i);
					Console.WriteLine ("Subject: {0}", message.Subject);
				}

				client.Disconnect (true);
			}
		}
	}
}
```

However, you probably want to do more complicated things with IMAP such as fetching summary information
so that you can display a list of messages in a mail client without having to first download all of the
messages from the server:

```csharp
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId)) {
	Console.WriteLine ("[summary] {0:D2}: {1}", summary.Index, summary.Envelope.Subject);
}
```

The results of a Fetch command can also be used to download individual MIME parts rather
than downloading the entire message. For example:

```csharp
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId)) {
	var text = summary.Body as BodyPartText;

	if (text == null) {
		var multipart = summary.Body as BodyPartMultipart;

		if (multipart != null)
			text = multipart.BodyParts.OfType<BodyPartText> ().FirstOrDefault ();
	}

	if (text == null)
		continue;

	// this will download *just* the text part
	var part = inbox.GetBodyPart (summary.UniqueId.Value, text);
}
```

You may also be interested in sorting and searching...

```csharp
// let's search for all messages received after Jan 12, 2013 with "MailKit" in the subject...
var query = SearchQuery.DeliveredAfter (DateTime.Parse ("2013-01-12"))
    .And (SearchQuery.SubjectContains ("MailKit")).And (SearchQuery.Seen);

foreach (var uid in inbox.Search (query)) {
	var message = inbox.GetMessage (uid);
	Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}

// let's do the same search, but this time sort them in reverse arrival order
var orderBy = new [] { OrderBy.ReverseArrival };
foreach (var uid in inbox.Search (query, orderBy)) {
	var message = inbox.GetMessage (uid);
	Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}

// you'll notice that the orderBy argument is an array... this is because you
// can actually sort the search results based on multiple columns:
orderBy = new [] { OrderBy.ReverseArrival, OrderBy.Subject };
foreach (var uid in inbox.Search (query, orderBy)) {
	var message = inbox.GetMessage (uid);
	Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}
```

Of course, instead of downloading the message, you could also fetch the summary information for the matching messages
or do any of a number of other things with the UIDs that are returned.

How about navigating folders? MailKit can do that, too:

```csharp
// Get the first personal namespace and list the toplevel folders under it.
var personal = client.GetFolder (client.PersonalNamespaces[0]);
foreach (var folder in personal.GetSubfolders (false))
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

In cases where the IMAP server does *not* support the SPECIAL-USE or XLIST extensions, you'll have to
come up with your own heuristics for getting the Sent, Drafts, Trash, etc folders. For example, you
might use logic similar to this:

```csharp
static string[] CommonSentFolderNames = { "Sent Items", "Sent Mail", /* maybe add some translated names */ };

static IFolder GetSentFolder (ImapClient client, CancellationToken cancellationToken)
{
    var personal = client.GetFolder (client.PersonalNamespaces[0]);

    foreach (var folder in personal.GetSubfolders (false, cancellationToken)) {
        foreach (var name in CommonSentFolderNames) {
            if (folder.Name == commonName)
                return folder;
        }
    }

    return null;
}
```

Using LINQ, you could simplify this down to something more like this:

```csharp
static string[] CommonSentFolderNames = { "Sent Items", "Sent Mail", /* maybe add some translated names */ };

static IFolder GetSentFolder (ImapClient client, CancellationToken cancellationToken)
{
    var personal = client.GetFolder (client.PersonalNamespaces[0]);
    
    return personal.GetSubfolders (false, cancellationToken).FirstOrDefault (x => CommonSentFolderNames.Contains (x.Name));
}
```

Another option might be to allow the user of your application to configure which folder he or she wants to use as their Sent folder, Drafts folder, Trash folder, etc.

How you handle this is up to you.

## Contributing

The first thing you'll need to do is fork MailKit to your own GitHub repository. Once you do that,

    git clone git@github.com/<your-account>/MailKit.git

If you use [Xamarin Studio](http://xamarin.com/studio) or [MonoDevelop](http://monodevelop.com), all of the
solution files are configured with the coding style used by MailKit. If you use Visual Studio or some
other editor, please try to maintain the existing coding style as best as you can.

Once you've got some changes that you'd like to submit upstream to the official MailKit repository,
simply send me a Pull Request and I will try to review your changes in a timely manner.

If you'd like to contribute but don't have any particular features in mind to work on, check out the issue
tracker and look for something that might pique your interest!

## Reporting Bugs

Have a bug or a feature request? [Please open a new issue](https://github.com/jstedfast/MailKit/issues).

Before opening a new issue, please search for existing issues to avoid submitting duplicates.

## Documentation

API documentation can be found at [http://jstedfast.github.io/MailKit/docs](http://jstedfast.github.io/MailKit/docs).
