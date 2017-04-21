When submitting a bug report, please also include a protocol log (scrubbed of any authentication parameters).

If your bug report is about hitting a [CommandException](http://www.mimekit.net/docs/html/T_MailKit_CommandException.htm) or
[ProtocolException](http://www.mimekit.net/docs/html/T_MailKit_ProtocolException.htm) such as
`Syntax error in XYZ. Unexpected token: [atom: 0]`, not including a protocol log will make me very unhappy.

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
[jestedfa@microsoft.com](mailto:jestedfa@microsoft.com?Subject=MailKit%20protocol%20log)
instead of including it in the GitHub issue.
