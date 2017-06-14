To help me debug your issue, please explain:
- What were you trying to do?
- What happened?
- What did you expect to happen?
- Step-by-step reproduction instructions and/or a simple test case.

If you got an exception, please include the exception Message *and* StackTrace.

Please also INCLUDE A PROTOCOL LOG (scrubbed of any authentication data), especially
if you got an exception such as `Syntax error in XYZ. Unexpected token: ...`.
If you do not include the protocol log, you will make me VERY UNHAPPY.

Without a protocol log, I CANNOT fix the issue. I will simply close the bug report.

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
