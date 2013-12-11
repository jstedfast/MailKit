# MailKit

## What is MailKit?

MailKit is a cross-platform mail client library built on top of [MimeKit](https://github.com/jstedfast/MimeKit).


## License Information

MailKit is Copyright (C) 2013 Jeffrey Stedfast and is licensed under the MIT license:

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
* MailKit.Net45.sln just includes the .NET Framework 4.5 C# project (MailKit/MailKit.csproj)
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

				// Note: if the server requires SSL-on-connect, use the smtps:// protocol instead
				var uri = new Uri ("smtp://smtp.gmail.com:587");

				using (var cancel = new CancellationTokenSource ()) {
					client.Connect (uri, credentials, cancel.Token);
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

				// Note: if the server requires SSL-on-connect, use the pop3s:// protocol instead
				var uri = new Uri ("pop3://mail.friends.com");

				using (var cancel = new CancellationTokenSource ()) {
					client.Connect (uri, credentials, cancel.Token);

					int count = client.Count (cancel.Token);
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

XML-formatted API documentation can be found in the [docs](https://github.com/jstedfast/MailKit/tree/master/docs)
directory which is largely autogenerated based on the inline XML documentation in the source code.
