# MailKit

[![Join the chat at https://gitter.im/jstedfast/MailKit](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/jstedfast/MailKit?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![Issue Stats](http://www.issuestats.com/github/jstedfast/MailKit/badge/pr)](http://www.issuestats.com/github/jstedfast/MailKit)
[![Issue Stats](http://www.issuestats.com/github/jstedfast/MailKit/badge/issue)](http://www.issuestats.com/github/jstedfast/MailKit)

|             |Build Status|Code Coverage|Static Analysis|
|-------------|:----------:|:-----------:|:-------------:|
|**Linux/Mac**|[![Build Status](https://travis-ci.org/jstedfast/MailKit.svg)](https://travis-ci.org/jstedfast/MailKit)|[![Code Coverage](https://coveralls.io/repos/jstedfast/MailKit/badge.svg?branch=HEAD)](https://coveralls.io/r/jstedfast/MailKit?branch=HEAD)|[![Static Analysis](https://scan.coverity.com/projects/3202/badge.svg)](https://scan.coverity.com/projects/3202)|
|**Windows**  |[![Build Status](https://ci.appveyor.com/api/projects/status/fd38t1ri3cmujnpq/branch/master?svg=true)](https://ci.appveyor.com/project/jstedfast/mailkit/branch/master)|[![Code Coverage](https://coveralls.io/repos/jstedfast/MailKit/badge.svg?branch=HEAD)](https://coveralls.io/r/jstedfast/MailKit?branch=HEAD)|[![Static Analysis](https://scan.coverity.com/projects/3202/badge.svg)](https://scan.coverity.com/projects/3202)|

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
  * SCRAM-SHA-256
  * XOAUTH2 (partial support - you need to fetch the auth tokens yourself)
* SMTP Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "smtps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * [SIZE](https://tools.ietf.org/html/rfc1870)
    * [DSN](https://tools.ietf.org/html/rfc1891)
    * [AUTH](https://tools.ietf.org/html/rfc2554)
    * [8BITMIME](https://tools.ietf.org/html/rfc2821)
    * [PIPELINING](https://tools.ietf.org/html/rfc2920)
    * [BINARYMIME](https://tools.ietf.org/html/rfc3030)
    * [CHUNKING](https://tools.ietf.org/html/rfc3030)
    * [STARTTLS](https://tools.ietf.org/html/rfc3207)
    * [SMTPUTF8](https://tools.ietf.org/html/rfc6531)
  * All APIs are cancellable.
  * Async APIs are available.
* POP3 Client
  * Supports all of the SASL mechanisms listed above.
  * Also supports authentication via [APOP](https://tools.ietf.org/html/rfc1939#page-15) and `USER`/`PASS`.
  * Supports SSL-wrapped connections via the "pops" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * [TOP](https://tools.ietf.org/html/rfc1939#page-11)
    * [UIDL](https://tools.ietf.org/html/rfc1939#page-12)
    * [EXPIRE](https://tools.ietf.org/html/rfc2449)
    * [LOGIN-DELAY](https://tools.ietf.org/html/rfc2449)
    * [PIPELINING](https://tools.ietf.org/html/rfc2449)
    * [SASL](https://tools.ietf.org/html/rfc2449)
    * [STLS](https://tools.ietf.org/html/rfc2595)
    * [UTF8](https://tools.ietf.org/html/rfc6856)
    * [UTF8=USER](https://tools.ietf.org/html/rfc6856)
    * [LANG](https://tools.ietf.org/html/rfc6856)
  * All APIs are cancellable.
  * Async APIs are available.
* IMAP4 Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "imaps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * [ACL](https://tools.ietf.org/html/rfc4314)
    * [QUOTA](https://tools.ietf.org/html/rfc2087)
    * [LITERAL+](https://tools.ietf.org/html/rfc2088)
    * [IDLE](https://tools.ietf.org/html/rfc2177)
    * [NAMESPACE](https://tools.ietf.org/html/rfc2342)
    * [ID](https://tools.ietf.org/html/rfc2971)
    * [CHILDREN](https://tools.ietf.org/html/rfc3348)
    * [LOGINDISABLED](https://tools.ietf.org/html/rfc3501)
    * [STARTTLS](https://tools.ietf.org/html/rfc3501)
    * [MULTIAPPEND](https://tools.ietf.org/html/rfc3502)
    * [UNSELECT](https://tools.ietf.org/html/rfc3691)
    * [UIDPLUS](https://tools.ietf.org/html/rfc4315)
    * [CONDSTORE](https://tools.ietf.org/html/rfc4551)
    * [ESEARCH](https://tools.ietf.org/html/rfc4731)
    * [SASL-IR](https://tools.ietf.org/html/rfc4959)
    * [COMPRESS](https://tools.ietf.org/html/rfc4978)
    * [WITHIN](https://tools.ietf.org/html/rfc5032)
    * [ENABLE](https://tools.ietf.org/html/rfc5161)
    * [QRESYNC](https://tools.ietf.org/html/rfc5162)
    * [SORT](https://tools.ietf.org/html/rfc5256)
    * [THREAD](https://tools.ietf.org/html/rfc5256)
    * [LIST-EXTENDED](https://tools.ietf.org/html/rfc5258)
    * [ESORT](https://tools.ietf.org/html/rfc5267)
    * [METADATA](https://tools.ietf.org/html/rfc5464)
    * [FILTERS](https://tools.ietf.org/html/rfc5466)
    * [LIST-STATUS](https://tools.ietf.org/html/rfc5819)
    * [SORT=DISPLAY](https://tools.ietf.org/html/rfc5957)
    * [SPECIAL-USE](https://tools.ietf.org/html/rfc6154)
    * [CREATE-SPECIAL-USE](https://tools.ietf.org/html/rfc6154)
    * [SEARCH=FUZZY](https://tools.ietf.org/html/rfc6203)
    * [MOVE](https://tools.ietf.org/html/rfc6851)
    * [UTF8=ACCEPT](https://tools.ietf.org/html/rfc6855)
    * [UTF8=ONLY](https://tools.ietf.org/html/rfc6855)
    * [LITERAL-](https://tools.ietf.org/html/rfc7888)
    * [APPENDLIMIT](https://tools.ietf.org/html/rfc7889)
    * [XLIST](https://developers.google.com/gmail/imap_extensions)
    * [X-GM-EXT1](https://developers.google.com/gmail/imap_extensions) (X-GM-MSGID, X-GM-THRID, X-GM-RAW and X-GM-LABELS)
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
FETCH response is by scanning for `") UID"`, and not properly handling mailbox names with international
characters to simply name a few.

IMAP requires a LOT of time spent laboriously reading and re-reading the IMAP specifications (as well
as the MIME specifications) to understand all of the subtleties of the protocol and most (all?) of the
other Open Source .NET IMAP libraries, at least, were written by developers that only cared enough that
it worked for their simple needs. There's nothing necessarily wrong with doing that, but the web is full
of half-working, non-RFC-compliant IMAP implementations out there that it was finally time for a carefully
designed and implemented IMAP client library to be written.

For POP3, libraries such as OpenPOP.NET are actually fairly decent, although the MIME parser is far
too strict - throwing exceptions any time it encounters a Content-Type or Content-Disposition
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

MailKit is Copyright (C) 2013-2018 Xamarin Inc. and is licensed under the MIT license:

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
enter the following command:

    Install-Package MailKit

## Getting the Source Code

First, you'll need to clone MailKit from my GitHub repository. To do this using the command-line version of Git,
you'll need to issue the following command in your terminal:

    git clone --recursive https://github.com/jstedfast/MailKit.git

If you are using [TortoiseGit](https://tortoisegit.org) on Windows, you'll need to right-click in the directory
where you'd like to clone MailKit and select **Git Clone...** in the menu. Once you do that, you'll get the
following dialog:

![Download the source code using TortoiseGit](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/clone.png)

Fill in the areas outlined in red and then click **OK**. This will recursively clone MailKit onto your local machine.

## Updating the Source Code

Occasionally you might want to update your local copy of the source code if I have made changes to MailKit since you
downloaded the source code in the step above. To do this using the command-line version of Git, you'll need to issue
the following commands in your terminal within the MailKit directory:

    git pull
    git submodule update

If you are using [TortoiseGit](https://tortoisegit.org) on Windows, you'll need to right-click on the MailKit
directory and select **Git Sync...** in the menu. Once you do that, you'll need to click the **Pull** and
**Submodule Update** buttons in the following dialog:

![Update the source code using TortoiseGit](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/update.png)

## Building

In the top-level MailKit directory, there are a number of solution files; they are:

* **MailKit.sln** - includes the projects for .NET 4.5, .NETStandard 1.3/1.6/2.0, Windows Universal 8.1,
  Xamarin.Android, and Xamarin.iOS.
* **MailKit.Mobile.sln** - includes only the Xamarin.iOS and Xamarin.Android projects.
* **MailKit.Net45.sln** - includes only the .NET 4.5 project and the unit tests.

If you don't have the Xamarin products, you'll probably want to open the MailKit.Net45.sln instead of MailKit.sln.

Once you've opened the appropriate MailKit solution file in either [Xamarin Studio](https://www.xamarin.com/download)
or [Visual Studio 2017](https://www.visualstudio.com/downloads/), you can choose the **Debug** or **Release**
build configuration and then build.

Note: The **Release** build will generate the xml API documentation, but the **Debug** build will not.

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
				// For demo-purposes, accept all SSL certificates (in case the server supports STARTTLS)
				client.ServerCertificateValidationCallback = (s,c,h,e) => true;

				client.Connect ("smtp.friends.com", 587, false);

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
				// For demo-purposes, accept all SSL certificates (in case the server supports STARTTLS)
				client.ServerCertificateValidationCallback = (s,c,h,e) => true;

				client.Connect ("pop.friends.com", 110, false);

				client.Authenticate ("joey", "password");

				for (int i = 0; i < client.Count; i++) {
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
				// For demo-purposes, accept all SSL certificates
				client.ServerCertificateValidationCallback = (s,c,h,e) => true;

				client.Connect ("imap.friends.com", 993, true);

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
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure)) {
    if (summary.TextBody != null) {
	// this will download *just* the text/plain part
	var text = inbox.GetBodyPart (summary.UniqueId, summary.TextBody);
    }
    
    if (summary.HtmlBody != null) {
        // this will download *just* the text/html part
	var html = inbox.GetBodyPart (summary.UniqueId, summary.HtmlBody);
    }
    
    // if you'd rather grab, say, an image attachment... it might look something like this:
    if (summary.Body is BodyPartMultipart) {
        var multipart = (BodyPartMultipart) summary.Body;
        
        var attachment = multipart.BodyParts.OfType<BodyPartBasic> ().FirstOrDefault (x => x.FileName == "logo.jpg");
        if (attachment != null) {
            // this will download *just* the attachment
            var part = inbox.GetBodyPart (summary.UniqueId, attachment);
        }
    }
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

The first thing you'll need to do is fork MailKit to your own GitHub repository. For instructions on how to
do that, see the section titled **Getting the Source Code**.

If you use [Xamarin Studio](http://xamarin.com/studio) or [MonoDevelop](http://monodevelop.com), all of the
solution files are configured with the coding style used by MailKit. If you use Visual Studio or some
other editor, please try to maintain the existing coding style as best as you can.

Once you've got some changes that you'd like to submit upstream to the official MailKit repository,
send me a **Pull Request** and I will try to review your changes in a timely manner.

If you'd like to contribute but don't have any particular features in mind to work on, check out the issue
tracker and look for something that might pique your interest!

## Donate

MailKit is a personal open source project that I have put thousands of hours into perfecting with the
goal of making it the very best email framework for .NET. I need your help to achieve this.

Donating helps pay for things such as web hosting, domain registration and licenses for developer tools
such as a performance profiler, memory profiler, a static code analysis tool, and more.

<a href="https://liberapay.com/MimeKit/donate" _target="blank"><img alt="Click here to lend your support to MimeKit and MailKit by making a donation!" src="https://liberapay.com/assets/widgets/donate.svg"></a>

## Reporting Bugs

Have a bug or a feature request? [Please open a new issue](https://github.com/jstedfast/MailKit/issues).

Before opening a new issue, please search for existing issues to avoid submitting duplicates.

If MailKit does not work with your mail server, please include a [protocol
log](https://github.com/jstedfast/MailKit/blob/master/FAQ.md#ProtocolLog) in your bug report, otherwise
there is nothing I can do to fix the problem.

If you are getting an exception from somewhere within MailKit, don't just provide the `Exception.Message`
string. Please include the `Exception.StackTrace` as well. The `Message`, by itself, is useless.

## Documentation

API documentation can be found at [http://mimekit.net/docs](http://mimekit.net/docs).

A copy of the xml formatted API documentation is also included in the NuGet and/or
Xamarin Component package.
