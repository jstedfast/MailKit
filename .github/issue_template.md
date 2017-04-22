To help me debug your issue, please explain:
- What were you trying to do?
- What happened? If you got an exception, please include the exception Message *and* StackTrace. If you hit a `CommandException` or `ProtocolException` such as `Syntax error in XYZ. Unexpected token: [atom: 0]`, INCLUDE THE PROTOCOL LOG (scrubbed of any authentication data). If you do not include the protocol log, you will make me VERY UNHAPPY.
- What did you expect to happen?
- Step-by-step reproduction instructions and/or a simple test case.

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
