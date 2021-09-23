---
name: Bug report
about: Create a report to help us improve

---

**Describe the bug**
A clear and concise description of what the bug is.

**Platform (please complete the following information):**
 - OS: [e.g. Windows, Linux, MacOS, iOS, Android, Windows Phone, etc.]
 - .NET Runtime: [e.g. CoreCLR, Mono]
 - .NET Framework: [e.g. .Net Core, .NET 4.5, UWP, etc.]
 - MailKit Version: 

**Exception**
If you got an exception, please include the exception Message *and* StackTrace.

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected behavior**
A clear and concise description of what you expected to happen.

**Code Snippets**
If applicable, add code snippets to help explain your problem.

```csharp
// Add your code snippet here.
```

**Protocol Logs**
Please include a protocol log (scrubbed of any authentication data), especially
if you got an exception such as `Syntax error in XYZ. Unexpected token: ...`.

To get a protocol log, follow one of the following code snippets:

```csharp
// log to a file called 'imap.log'
var client = new ImapClient (new ProtocolLogger ("imap.log"));
```

```csharp
// log to a file called 'pop3.log'
var client = new Pop3Client (new ProtocolLogger ("pop3.log"));
```

```csharp
// log to a file called 'smtp.log'
var client = new SmtpClient (new ProtocolLogger ("smtp.log"));
```

Note: if the protocol log contains sensitive information, feel free to email it to me at
jestedfa@microsoft.com instead of including it in the GitHub issue.

**Additional context**
Add any other context about the problem here.
