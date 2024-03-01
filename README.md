# MailKit

|  Package  |Latest Release|Latest Build|
|:----------|:------------:|:----------:|
|**MimeKit**|[![NuGet Badge MimeKit](https://buildstats.info/nuget/MimeKit)](https://www.nuget.org/packages/MimeKit)|[![MyGet Badge MimeKit](https://buildstats.info/myget/mimekit/MimeKit)](https://www.myget.org/feed/mimekit/package/nuget/MimeKit)|
|**MailKit**|[![NuGet Badge MailKit](https://buildstats.info/nuget/MailKit)](https://www.nuget.org/packages/MailKit)|[![MyGet Badge MailKit](https://buildstats.info/myget/mimekit/MailKit)](https://www.myget.org/feed/mimekit/package/nuget/MailKit)|

|  Platform   |Build Status|Code Coverage|Static Analysis|
|:------------|:----------:|:-----------:|:-------------:|
|**Linux/Mac**|[![Build Status](https://github.com/jstedfast/MailKit/actions/workflows/main.yml/badge.svg?event=push)](https://github.com/jstedfast/MailKit/actions/workflows/main.yml)|[![Code Coverage](https://coveralls.io/repos/jstedfast/MailKit/badge.svg?branch=master)](https://coveralls.io/r/jstedfast/MailKit?branch=master)|[![Static Analysis](https://scan.coverity.com/projects/3202/badge.svg)](https://scan.coverity.com/projects/3202)|
|**Windows**  |[![Build Status](https://github.com/jstedfast/MailKit/actions/workflows/main.yml/badge.svg?event=push)](https://github.com/jstedfast/MailKit/actions/workflows/main.yml)|[![Code Coverage](https://coveralls.io/repos/jstedfast/MailKit/badge.svg?branch=master)](https://coveralls.io/r/jstedfast/MailKit?branch=master)|[![Static Analysis](https://scan.coverity.com/projects/3202/badge.svg)](https://scan.coverity.com/projects/3202)|

## What is MailKit?

MailKit is a cross-platform mail client library built on top of [MimeKit](https://github.com/jstedfast/MimeKit).

## Donate

MailKit is a personal open source project that I have put thousands of hours into perfecting with the
goal of making it the very best email framework for .NET. I need your help to achieve this.

Donating helps pay for things such as web hosting, domain registration and licenses for developer tools
such as a performance profiler, memory profiler, a static code analysis tool, and more. It also helps
motivate me to continue working on the project.

<a href="https://github.com/sponsors/jstedfast" _target="blank"><img alt="Click here to lend your support to MailKit by making a donation!" src="https://www.paypal.com/en_US/i/btn/x-click-but21.gif"></a>

## Features

* SASL Authentication
  * [CRAM-MD5](https://tools.ietf.org/html/rfc2195)
  * [DIGEST-MD5](https://tools.ietf.org/html/rfc2831)
  * [LOGIN](https://tools.ietf.org/html/draft-murchison-sasl-login-00)
  * [NTLM](https://davenport.sourceforge.net/ntlm.html)
  * [PLAIN](https://tools.ietf.org/html/rfc2595)
  * [SCRAM-SHA-1[-PLUS]](https://tools.ietf.org/html/rfc5802)
  * [SCRAM-SHA-256[-PLUS]](https://tools.ietf.org/html/rfc5802)
  * [SCRAM-SHA-512[-PLUS]](https://tools.ietf.org/html/draft-melnikov-scram-sha-512-01)
  * [OAUTHBEARER](https://tools.ietf.org/html/rfc7628) (partial support - you need to fetch the auth tokens yourself)
  * XOAUTH2 (partial support - you need to fetch the auth tokens yourself)
* Proxy Support
  * [SOCKS4/4a](https://www.openssh.com/txt/socks4.protocol)
  * [SOCKS5](https://tools.ietf.org/html/rfc1928)
  * [HTTP/S](https://tools.ietf.org/html/rfc2616)
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
    * [ANNOTATE](https://tools.ietf.org/html/rfc5257)
    * [LIST-EXTENDED](https://tools.ietf.org/html/rfc5258)
    * [ESORT](https://tools.ietf.org/html/rfc5267)
    * [METADATA / METADATA-SERVER](https://tools.ietf.org/html/rfc5464)
    * [NOTIFY](https://tools.ietf.org/html/rfc5465)
    * [FILTERS](https://tools.ietf.org/html/rfc5466)
    * [LIST-STATUS](https://tools.ietf.org/html/rfc5819)
    * [SORT=DISPLAY](https://tools.ietf.org/html/rfc5957)
    * [SPECIAL-USE / CREATE-SPECIAL-USE](https://tools.ietf.org/html/rfc6154)
    * [SEARCH=FUZZY](https://tools.ietf.org/html/rfc6203)
    * [MOVE](https://tools.ietf.org/html/rfc6851)
    * [UTF8=ACCEPT / UTF8=ONLY](https://tools.ietf.org/html/rfc6855)
    * [LITERAL-](https://tools.ietf.org/html/rfc7888)
    * [APPENDLIMIT](https://tools.ietf.org/html/rfc7889)
    * [STATUS=SIZE](https://tools.ietf.org/html/rfc8438)
    * [OBJECTID](https://tools.ietf.org/html/rfc8474)
    * [REPLACE](https://tools.ietf.org/html/rfc8508)
    * [SAVEDATE](https://tools.ietf.org/html/rfc8514)
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

```text
MIT License

Copyright (C) 2013-2024 .NET Foundation and Contributors

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
```

## Installing via NuGet

The easiest way to install MailKit is via [NuGet](https://www.nuget.org/packages/MailKit/).

In Visual Studio's [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console),
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

* **MailKit.sln** - includes the projects for .NET Framework 4.6.2/4.7/4.8, .NETStandard 2.0/2.1, .NET6.0 as well as the unit tests.
* **MailKit.Coverity.sln** - this is used to generate Coverity static analysis builds and is not generally useful.
* **MailKit.Documentation.sln** - this is used to generate the documentation found at https://mimekit.net/docs

Once you've opened the appropriate MailKit solution file in [Visual Studio](https://www.visualstudio.com/downloads/),
you can choose the **Debug** or **Release** build configuration and then build.

Both Visual Studio 2017 and Visual Studio 2019 should be able to build MailKit without any issues, but older versions such as
Visual Studio 2015 will require modifications to the projects in order to build correctly. It has been reported that adding
NuGet package references to [Microsoft.Net.Compilers](https://www.nuget.org/packages/Microsoft.Net.Compilers/) >= 3.6.0
and [System.ValueTuple](https://www.nuget.org/packages/System.ValueTuple/) >= 4.5.0 to the MimeKit and MailKit projects will
allow them to build successfully.

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

More important than POP3 support is the IMAP support. Here's a simple use-case of retrieving messages from an IMAP server:

```csharp
using System;

using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;

namespace TestClient {
    class Program
    {
        public static void Main (string[] args)
        {
            using (var client = new ImapClient ()) {
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

### Fetching Information About the Messages in an IMAP Folder

One of the advantages of IMAP over POP3 is that the IMAP protocol allows clients to retrieve information about
the messages in a folder without having to first download all of them.

Using the [Fetch](https://www.mimekit.net/docs/html/Overload_MailKit_Net_Imap_ImapFolder_Fetch.htm) and
[FetchAsync](https://www.mimekit.net/docs/html/Overload_MailKit_Net_Imap_ImapFolder_FetchAsync.htm) method overloads
(or the convenient [extension methods](https://www.mimekit.net/docs/html/Overload_MailKit_IMailFolderExtensions_Fetch.htm)),
it's possible to obtain any subset of summary information for any range of messages in a given folder.

```csharp
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.Envelope)) {
    Console.WriteLine ("[summary] {0:D2}: {1}", summary.Index, summary.Envelope.Subject);
```

It's also possible to use Fetch/FetchAsync APIs that take an [IFetchRequest](https://www.mimekit.net/docs/html/T_MailKit_IFetchRequest.htm)
argument to get even more control over what to fetch:

```csharp
// Let's Fetch non-Received headers:
var request = new FetchRequest {
    Headers = new HeaderSet (new HeaderId[] { HeaderId.Received }) {
        Exclude = true
    }
};

foreach (var summary in inbox.Fetch (0, -1, request)) {
    Console.WriteLine ("[summary] {0:D2}: {1}", summary.Index, summary.Headers[HeaderId.Subject]);
```

The results of a Fetch method can also be used to download individual MIME parts rather
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

### Setting Message Flags in IMAP

In order to set or update the flags on a particular message, what is actually needed is the UID or index of the message and
the folder that it belongs to.

An obvious reason to want to update message flags is to mark a message as "read" (aka "seen") after a user has opened a
message and read it.

```csharp
folder.Store (uid, new StoreFlagsRequest (StoreAction.Add, MessageFlags.Seen) { Silent = true });
```

### Deleting Messages in IMAP

Deleting messages in IMAP involves setting a `\Deleted` flag on a message and, optionally, expunging it from the folder.

The way to mark a message as `\Deleted` works the same way as marking a message as `\Seen`.

```csharp
folder.Store (uid, new StoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true });
folder.Expunge ();
```

### Searching an IMAP Folder

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
foreach (var uid in inbox.Sort (query, orderBy)) {
    var message = inbox.GetMessage (uid);
    Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}

// you'll notice that the orderBy argument is an array... this is because you
// can actually sort the search results based on multiple columns:
orderBy = new [] { OrderBy.ReverseArrival, OrderBy.Subject };
foreach (var uid in inbox.Sort (query, orderBy)) {
    var message = inbox.GetMessage (uid);
    Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
}
```

Of course, instead of downloading the message, you could also fetch the summary information for the matching messages
or do any of a number of other things with the UIDs that are returned.

### Navigating Folders in IMAP

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
static string[] CommonSentFolderNames = { "Sent Items", "Sent Mail", "Sent Messages", /* maybe add some translated names */ };

static IFolder GetSentFolder (ImapClient client, CancellationToken cancellationToken)
{
    var personal = client.GetFolder (client.PersonalNamespaces[0]);

    foreach (var folder in personal.GetSubfolders (false, cancellationToken)) {
        foreach (var name in CommonSentFolderNames) {
            if (folder.Name == name)
                return folder;
        }
    }

    return null;
}
```

Using LINQ, you could simplify this down to something more like this:

```csharp
static string[] CommonSentFolderNames = { "Sent Items", "Sent Mail", "Sent Messages", /* maybe add some translated names */ };

static IFolder GetSentFolder (ImapClient client, CancellationToken cancellationToken)
{
    var personal = client.GetFolder (client.PersonalNamespaces[0]);

    return personal.GetSubfolders (false, cancellationToken).FirstOrDefault (x => CommonSentFolderNames.Contains (x.Name));
}
```

Another option might be to allow the user of your application to configure which folder he or she wants to use as their
Sent folder, Drafts folder, Trash folder, etc.

How you handle this is up to you.

## Contributing

The first thing you'll need to do is fork MailKit to your own GitHub repository. For instructions on how to
do that, see the section titled **Getting the Source Code**.

If you use [Visual Studio for Mac](https://visualstudio.microsoft.com/vs/mac/) or [MonoDevelop](https://monodevelop.com),
all of the solution files are configured with the coding style used by MailKit. If you use Visual Studio on Windows
or some other editor, please try to maintain the existing coding style as best as you can.

Once you've got some changes that you'd like to submit upstream to the official MailKit repository,
send me a **Pull Request** and I will try to review your changes in a timely manner.

If you'd like to contribute but don't have any particular features in mind to work on, check out the issue
tracker and look for something that might pique your interest!

## Reporting Bugs

Have a bug or a feature request? Please open a new
[bug report](https://github.com/jstedfast/MailKit/issues/new?template=bug_report.md)
or
[feature request](https://github.com/jstedfast/MailKit/issues/new?template=feature_request.md).

Before opening a new issue, please search through any [existing issues](https://github.com/jstedfast/MailKit/issues)
to avoid submitting duplicates. It may also be worth checking the
[FAQ](https://github.com/jstedfast/MailKit/blob/master/FAQ.md) for common questions that other developers
have had.

If MailKit does not work with your mail server, please include a [protocol
log](https://github.com/jstedfast/MailKit/blob/master/FAQ.md#ProtocolLog) in your bug report, otherwise
there is nothing I can do to fix the problem.

If you are getting an exception from somewhere within MailKit, don't just provide the `Exception.Message`
string. Please include the `Exception.StackTrace` as well. The `Message`, by itself, is often useless.

## Documentation

API documentation can be found at [https://www.mimekit.net/docs](https://www.mimekit.net/docs).

Some example snippets can be found in the [`Documentation/Examples`](https://github.com/jstedfast/MailKit/tree/master/Documentation/Examples) directory.

Sample applications can be found in the [`samples`](https://github.com/jstedfast/MailKit/tree/master/samples) directory.

A copy of the XML-formatted API reference documentation is also included in the NuGet package.

## .NET Foundation

MailKit is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](https://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](https://www.dotnetfoundation.org/code-of-conduct).

General .NET OSS discussions: [.NET Foundation forums](https://forums.dotnetfoundation.org)
