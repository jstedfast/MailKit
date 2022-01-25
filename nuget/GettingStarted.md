## Getting Started

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

Using the [Fetch](http://www.mimekit.net/docs/html/Overload_MailKit_Net_Imap_ImapFolder_Fetch.htm) method overloads,
it's possible to obtain any subset of summary information for any range of messages in a given folder.

```csharp
foreach (var summary in inbox.Fetch (0, -1, MessageSummaryItems.Full)) {
	Console.WriteLine ("[summary] {0:D2}: {1}", summary.Index, summary.Envelope.Subject);
}
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

## Donate

MailKit is a personal open source project that I have put thousands of hours into perfecting with the
goal of making it the very best email framework for .NET. I need your help to achieve this.

Donating helps pay for things such as web hosting, domain registration and licenses for developer tools
such as a performance profiler, memory profiler, a static code analysis tool, and more. It also helps
motivate me to continue working on the project.

<a href="https://github.com/sponsors/jstedfast" _target="blank"><img alt="Click here to lend your support to MailKit by making a donation!" src="https://www.paypal.com/en_US/i/btn/x-click-but21.gif"></a>

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

API documentation can be found at [http://mimekit.net/docs](http://mimekit.net/docs).

A copy of the xml formatted API documentation is also included in the NuGet and/or
Xamarin Component package.
