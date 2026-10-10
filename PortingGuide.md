# Porting from MailKit 4.x to MailKit 5.0

This guide explains how to update a project that uses MailKit 4.x so that it builds and behaves
correctly with MailKit 5.0. It is written so that it can be followed by a developer or by an AI
coding assistant.

For the complete list of changes (including new features), see
[ReleaseNotes.md](https://github.com/jstedfast/MailKit/blob/master/ReleaseNotes.md). MailKit 5.0
depends on MimeKit 5.0, which has its own
[release notes](https://github.com/jstedfast/MimeKit/blob/master/ReleaseNotes.md) and porting
notes. Port MimeKit usage at the same time.

## Instructions for AI assistants

When asked to port a project to MailKit 5.0:

1. Update the package references first (see [Packages](#1-packages)), then build the project to
   get the list of compile errors.
2. Use the **Detect** hints in each section below (compiler errors, type and member names) to find
   the affected code. Search the whole solution, including test projects, since mocks and fakes of
   MailKit interfaces are frequently affected.
3. Apply the **Fix** for each match. Prefer the smallest change that preserves the original
   behavior. Do not refactor unrelated code.
4. Review the [Behavior changes](#behavior-changes-that-still-compile) section. These do not cause
   compile errors, so search for the listed patterns explicitly and flag anything you can't safely
   change for the user.
5. Rebuild and run the project's tests. Repeat until the build is clean.
6. Summarize the changes for the user. List anything you couldn't port automatically, such as
   dashboards that match on telemetry values or logic that depended on removed hooks.

Unless stated otherwise, all types mentioned here are in the `MailKit` namespace. Many fixes rely on
extension methods in that namespace, so make sure that files calling `Send ()`, `CopyTo ()`,
`MoveTo ()`, `Expunge ()` or `Store ()` have a `using MailKit;` directive.

## Quick checklist

| Area | Typical symptom | Section |
|:-----|:----------------|:--------|
| `MailKitLite` package | Package not found | [1](#1-packages) |
| S/MIME, PGP, DKIM, ARC | Missing types or runtime errors | [1](#1-packages) |
| `SmtpClient.Send ()` return value | `Cannot implicitly convert type 'MailKit.SendResult' to 'string'` | [2](#2-sending-messages) |
| Subclassing `SmtpClient` (DSN, recipient hooks) | `no suitable method found to override` | [2](#2-sending-messages) |
| `SmtpClient.DeliveryStatusNotificationType` | `does not contain a definition for 'DeliveryStatusNotificationType'` | [2](#2-sending-messages) |
| `MessageSentEventArgs.Response`, `SmtpResponse.Response` | `does not contain a definition for 'Response'` | [2](#2-sending-messages) |
| `ImapCapabilities.Xxx`, `HasFlag`, `&`, `\|`, `~` | Operator or conversion errors on capabilities | [3](#3-capabilities) |
| `EnableQuickResync ()`, `EnableUTF8 ()` | Missing method | [4](#4-imap-enable) |
| Sizes, counts, offsets | `int`/`uint` vs `long` conversion errors | [5](#5-64-bit-sizes-and-offsets) |
| `IMessageSummary.Folder` | Missing property | [6](#6-imessagesummaryfolder-removed) |
| Custom `IMailFolder`/`MailFolder`/`IMailTransport` implementations, or mocks | Interface members not implemented, or mock setups fail | [7](#7-custom-implementations-and-mocks) |
| `SaslMechanism.Create ()` null checks | Exception instead of `null` | [8](#8-other-api-changes) |
| `Compress ()` on .NET Framework/.NET Standard | Missing method | [8](#8-other-api-changes) |
| Removed obsolete APIs | Missing constructors or properties | [9](#9-removed-obsolete-apis) |

---

## 1. Packages

**Detect:** `PackageReference` (or `packages.config`) entries for `MailKit`, `MailKitLite`,
`MimeKit` or `MimeKitLite`. Also look for code that uses S/MIME, OpenPGP, DKIM or ARC (namespace
`MimeKit.Cryptography`).

**Fix:**

* Replace `MailKitLite` with `MailKit` and update it to 5.0. MailKit no longer depends on
  BouncyCastle, so the two packages were identical.
* MailKit 5.0 depends on `MimeKit.Core` rather than the full MimeKit package. If the project uses
  S/MIME, PGP/MIME, DKIM or ARC, add a reference to `MimeKit.Cryptography` (or the `MimeKit`
  meta-package). Then call `CryptographyModule.Initialize ()` once during application startup:

  ```csharp
  using MimeKit.Cryptography;

  CryptographyModule.Initialize ();
  ```

---

## 2. Sending messages

MailKit 5.0 uses request objects to send messages: `ISendRequest`/`SendRequest` in general, and
`ISmtpSendRequest`/`SmtpSendRequest` for SMTP. The methods on `IMailTransport` take a request and
return a `SendResult` (`SmtpClient` returns the subclass `SmtpSendResult`).

### 2.1 The familiar `Send ()` overloads are now extension methods

`client.Send (message)`, `client.Send (message, sender, recipients)` and the overloads that take a
`FormatOptions` (plus their `SendAsync` variants) still exist. They are now extension methods in
`MailKit.IMailTransportExtensions`.

**Detect:** Calls like `client.Send (message)` fail with "no overload for method 'Send' takes N
arguments".

**Fix:** Add `using MailKit;` to the file.

### 2.2 `Send ()` returns `SendResult` instead of `string`

**Detect:** `string x = client.Send (...)`, `string x = await client.SendAsync (...)`, or a
`var` result that's used as a string.

**Fix:**

```csharp
// 4.x
string response = client.Send (message);

// 5.0
string response = client.Send (message).ResponseText;

// or, to keep the full result:
var result = client.Send (message);
Console.WriteLine (result.ResponseText);
```

`SendResult` also has `AcceptedRecipients` and `RejectedRecipients`. To get the SMTP status code,
cast the result to `SmtpSendResult`:

```csharp
var result = (SmtpSendResult) smtpClient.Send (message);
Console.WriteLine ($"{(int) result.StatusCode} {result.ResponseText}");
```

### 2.3 `SmtpClient` subclasses that override send hooks

These protected virtual methods were removed from `SmtpClient`:

| 4.x `SmtpClient` override | 5.0 `SmtpSendRequest` equivalent |
|:--------------------------|:---------------------------------|
| `string? GetEnvelopeId (MimeMessage message)` | `EnvelopeId` property |
| `DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)` | `DeliveryStatusNotifications` property, or override `GetDeliveryStatusNotifications (MailboxAddress recipient)` |
| `void OnSenderAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)` | override `OnSenderAccepted (MailboxAddress sender, SmtpResponse response)` |
| `void OnSenderNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)` | override `OnSenderNotAccepted (MailboxAddress sender, SmtpResponse response)` |
| `void OnRecipientAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)` | override `OnRecipientAccepted (MailboxAddress recipient, SmtpResponse response)` |
| `void OnRecipientNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)` | override `OnRecipientNotAccepted (MailboxAddress recipient, SmtpResponse response)` |
| `void OnNoRecipientsAccepted (MimeMessage message)` | override `OnNoRecipientsAccepted ()` |
| `DeliveryStatusNotificationType` property on `SmtpClient`/`ISmtpClient` | `DeliveryStatusNotificationType` property on `SmtpSendRequest` |

Inside a `SmtpSendRequest` subclass, the message is available from the `Message` property. The
default behavior is unchanged: `OnSenderNotAccepted ()` and `OnRecipientNotAccepted ()` throw
`SmtpCommandException`, and `OnNoRecipientsAccepted ()` does nothing.

**Detect:** Classes that derive from `SmtpClient` and override any of the methods above. Uses of
`client.DeliveryStatusNotificationType`.

**Fix:** Move the logic into an `SmtpSendRequest` (or a subclass of it) and pass the request to
`Send ()`. If the `SmtpClient` subclass only existed for these hooks, delete it and use `SmtpClient`
directly.

```csharp
// 4.x
class DsnSmtpClient : SmtpClient
{
    protected override string GetEnvelopeId (MimeMessage message) => message.MessageId;

    protected override DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
        => DeliveryStatusNotification.Failure;
}

using var client = new DsnSmtpClient ();
client.DeliveryStatusNotificationType = DeliveryStatusNotificationType.HeadersOnly;
client.Send (message);

// 5.0
using var client = new SmtpClient ();
var request = new SmtpSendRequest (message) {
    EnvelopeId = message.MessageId,
    DeliveryStatusNotificationType = DeliveryStatusNotificationType.HeadersOnly,
    DeliveryStatusNotifications = DeliveryStatusNotification.Failure
};
client.Send (request);
```

The same pattern applies to the hooks that report whether each recipient was accepted:

```csharp
// 4.x
class TolerantSmtpClient : SmtpClient
{
    protected override void OnRecipientNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
    {
        Log ($"{mailbox} rejected: {response.Response}");
        // not calling base: deliver to the remaining recipients
    }
}

// 5.0
class TolerantSendRequest : SmtpSendRequest
{
    public TolerantSendRequest (MimeMessage message) : base (message) { }

    public override void OnRecipientNotAccepted (MailboxAddress recipient, SmtpResponse response)
    {
        Log ($"{recipient} rejected: {response.ResponseText}");
    }
}

var result = client.Send (new TolerantSendRequest (message));
// result.RejectedRecipients now lists the rejected mailboxes.
```

To send with an explicit envelope sender and recipients, use
`new SmtpSendRequest (message, sender, recipients)`. To report progress, set the request's
`TransferProgress` property (or keep using the extension overloads that take an
`ITransferProgress`).

`SmtpClient`'s `Prepare ()`, `GetSize ()` and `PreferSendAsBinaryData` members are still
overridable.

### 2.4 Renamed `Response` properties

**Detect:** `.Response` on an `SmtpResponse` or `MessageSentEventArgs`.

**Fix:** Rename to `.ResponseText`.

* `SmtpResponse.Response` → `SmtpResponse.ResponseText`. The constructor parameter is now
  `responseText`, which matters for named arguments.
* `MessageSentEventArgs.Response` → `MessageSentEventArgs.ResponseText`.

### 2.5 `MessageSentEventArgs`

The constructor changed from `(MimeMessage message, string response)` to
`(ISendRequest request, SendResult result)`. The event args now expose `Request`, `Message`,
`Result` and `ResponseText`.

**Detect:** `new MessageSentEventArgs (` (usually in custom transports or tests).

**Fix:** `new MessageSentEventArgs (new SendRequest (message), new SendResult (response, accepted, rejected))`.

---

## 3. Capabilities

The `[Flags]` enums `ImapCapabilities`, `SmtpCapabilities` and `Pop3Capabilities` were renamed to
`ImapCapability`, `SmtpCapability` and `Pop3Capability`. They are no longer flags and have no
`None` value. The old names now refer to read-only collection classes (capability sets).

**Detect:** `ImapCapabilities.`, `SmtpCapabilities.` or `Pop3Capabilities.` followed by a member
name. Also `Capabilities.HasFlag (`, `Capabilities & `, `Capabilities |= `, `Capabilities &= ~` and
comparisons with `.None`.

**Fix:**

| 4.x | 5.0 |
|:----|:----|
| `client.Capabilities.HasFlag (ImapCapabilities.Idle)` | `client.Capabilities.Contains (ImapCapability.Idle)` |
| `(client.Capabilities & ImapCapabilities.Idle) != 0` | `client.Capabilities.Contains (ImapCapability.Idle)` |
| `(client.Capabilities & ImapCapabilities.Idle) == 0` | `!client.Capabilities.Contains (ImapCapability.Idle)` |
| `client.Capabilities &= ~ImapCapabilities.Compress;` | `client.Capabilities.Disable (ImapCapability.Compress);` |
| `client.Capabilities == ImapCapabilities.None` | `client.Capabilities.Count == 0` |
| `client.Capabilities.ToString ()` (for parsing) | `client.Capabilities.Names` (the raw capability names) |

The same mappings apply to `SmtpCapabilities` → `SmtpCapability` and `Pop3Capabilities` →
`Pop3Capability`.

You can no longer assign to the `Capabilities` property. Use `Disable ()` instead. If code combined
several flags with `|`, replace it with several `Contains ()` calls joined with `&&` (or `||`). Mocks
that set up `Capabilities` should construct a set with `new ImapCapabilities (new[] { ImapCapability.Idle, ... })`.

`Contains (string)` and `GetValues (string)` expose capabilities that MailKit doesn't know about,
e.g. `client.Capabilities.GetValues ("AUTH")`. Don't use the capability set's `ToString ()` output
for anything other than debugging.

---

## 4. IMAP `ENABLE`

**Detect:** `EnableQuickResync (`, `EnableQuickResyncAsync (`, `EnableUTF8 (` or
`EnableUTF8Async (`.

**Fix:**

```csharp
// 4.x
client.EnableQuickResync ();
client.EnableUTF8 ();

// 5.0 (one ENABLE command)
client.Enable (ImapFeatures.QuickResync | ImapFeatures.UTF8Accept);
```

`ImapFeatures` is in `MailKit.Net.Imap`. `EnableQuickResync ()` has also been removed from
`IMailStore`/`MailStore`, and `EnableUTF8 ()` from `IImapClient`. Code that only has an `IMailStore`
must now use `IImapClient` (or `ImapClient`).

---

## 5. 64-bit sizes and offsets

Message sizes, line counts, byte offsets and byte counts are now `long`:

| API | 4.x type | 5.0 type |
|:----|:---------|:---------|
| `IMessageSummary.Size` | `uint?` | `long?` |
| `IMailFolder.Size` | `ulong?` | `long?` |
| `BodyPartText.Lines`, `BodyPartMessage.Lines` | `uint` | `long` |
| `IMailFolder.GetStream (..., int offset, int count, ...)` | `int` | `long` |
| `ImapFolder.CreateStream ()`, `CommitStream ()` offset/length | `int` | `long` |
| `SearchQuery.LargerThan ()`, `SmallerThan ()` | `int` | `long` |
| `IMailSpool.GetMessageSize ()` | `int` | `long` |
| `IMailSpool.GetMessageSizes ()` | `IList<int>` | `IList<long>` |
| `ISmtpClient.MaxSize`, `SmtpClient.MaxSize` | `uint` | `long` |

**Detect:** Conversion errors involving `long`, `int`, `uint` or `ulong` around these members.

**Fix:** Change the receiving variables, fields or properties to `long` (or `long?`). If storage
must stay 32-bit, use an explicit checked conversion and handle values larger than `int.MaxValue`.
None of these values is ever negative.

---

## 6. `IMessageSummary.Folder` removed

The `IMessageSummary.Folder` property and the `MessageSummary (IMailFolder, int)` constructor were
removed.

**Detect:** `.Folder` on an `IMessageSummary`/`MessageSummary`, or `new MessageSummary (folder, index)`.

**Fix:** Keep track of the folder alongside the summaries. Usually this is just the folder on which
`Fetch ()` was called. If summaries from several folders are mixed together, store
`(IMailFolder folder, IMessageSummary summary)` pairs. Use `new MessageSummary (index)` for tests
and mocks.

```csharp
// 4.x
foreach (var summary in summaries)
    var message = summary.Folder.GetMessage (summary.UniqueId);

// 5.0
foreach (var summary in summaries)
    var message = folder.GetMessage (summary.UniqueId);
```

---

## 7. Custom implementations and mocks

Several interfaces now have a smaller set of request-based methods, and the convenience overloads
became extension methods. This doesn't affect callers, as long as they have `using MailKit;`. It
does affect classes that implement these interfaces or derive from the abstract base classes. It
also affects mocking frameworks, which can't mock extension methods.

| Interface / base class | 4.x members (now extension methods) | 5.0 members to implement |
|:-----------------------|:-----------------------------|:-------------------------|
| `IMailTransport`/`MailTransport` | `Send (FormatOptions, MimeMessage, ...)`, `Send (FormatOptions, MimeMessage, MailboxAddress, IEnumerable<MailboxAddress>, ...)` and `SendAsync` variants | `SendResult Send (FormatOptions, ISendRequest, CancellationToken)` and `Task<SendResult> SendAsync (FormatOptions, ISendRequest, CancellationToken)` |
| `IMailFolder`/`MailFolder` | `CopyTo (..., IMailFolder destination, ...)`, `MoveTo (..., IMailFolder destination, ...)` | `CopyTo (IList<UniqueId>/IList<int>, ICopyRequest, ...)`, `MoveTo (IList<UniqueId>/IList<int>, IMoveRequest, ...)` |
| `IMailFolder`/`MailFolder` | `Expunge (IList<UniqueId>, ...)` | `Expunge (IList<UniqueId>, IExpungeRequest, ...)` |
| `IMailFolder`/`MailFolder` | `Store (..., IList<Annotation>, ...)` and `Store (..., ulong modseq, IList<Annotation>, ...)` | `Store (..., IStoreAnnotationsRequest, ...)` |
| `IMailFolder` | none | `DeletedCount` property and `DeletedCountChanged` event (already provided by `MailFolder`) |
| `IStoreRequest`, `IAppendRequest` (and derived interfaces) | none | `OnStarted ()` and `OnCompleted ()` methods (no-ops in the built-in classes) |
| `IMailService` (and `IMailStore`, `IMailTransport`, `IMailSpool`) | none | `ValueTask DisposeAsync ()` (already provided by `MailService`) |

The request-based `Store ()` overloads for annotations return the messages that weren't modified
(`IList<UniqueId>`/`IList<int>`), or `bool` for a single message.

**Detect:** "does not implement interface member", "no suitable method found to override", or
mock setups (Moq `Setup`, NSubstitute `Returns`, FakeItEasy `A.CallTo`) for the 4.x overloads
listed above.

**Fix:**

* **Implementations:** Remove the old overrides and implement the request-based methods. Read the
  former arguments from the request, e.g. `request.Destination` (copy/move),
  `request.Annotations`/`request.UnchangedSince` (annotations), `request.Message`/`request.Sender`/
  `request.Recipients`/`request.TransferProgress` (send). Call `request.OnStarted (...)` and
  `request.OnCompleted (...)` where appropriate so that callers relying on the callbacks keep
  working.
* **Mocks:** Set up the request-based interface method instead, matching any request of the
  relevant type. For example, with Moq:

  ```csharp
  // 4.x
  transport.Setup (t => t.Send (It.IsAny<FormatOptions> (), It.IsAny<MimeMessage> (), It.IsAny<CancellationToken> (), It.IsAny<ITransferProgress> ()))
           .Returns ("OK");

  // 5.0 (client.Send (message) is an extension method that calls Send (ISendRequest, ...))
  transport.Setup (t => t.Send (It.IsAny<ISendRequest> (), It.IsAny<CancellationToken> ()))
           .Returns (new SendResult ("OK", Array.Empty<MailboxAddress> (), Array.Empty<MailboxAddress> ()));
  ```

  The extension methods without `FormatOptions` call `Send (ISendRequest, ...)`. The ones with
  `FormatOptions` call `Send (FormatOptions, ISendRequest, ...)`. Set up whichever overload the
  code under test uses. The same applies to `CopyTo`/`MoveTo`/`Expunge`/`Store` on `IMailFolder`.
* **`MessageSummary`:** Mocks and fakes of `IMessageSummary` should drop the `Folder` property
  (see [section 6](#6-imessagesummaryfolder-removed)).

---

## 8. Other API changes

* **`SaslMechanism.Create ()`** returns a non-null `SaslMechanism`. It throws
  `NotSupportedException` for an unsupported mechanism instead of returning `null`.
  * **Detect:** `SaslMechanism.Create (` followed by a `null` check.
  * **Fix:** Use `SaslMechanism.TryCreate (name, credentials, out var sasl)`.
* **IMAP `COMPRESS`** is only supported on .NET 8.0 and later. `IImapClient.Compress ()`/
  `CompressAsync ()` (and the `ImapClient` methods) don't exist in the .NET Framework and .NET
  Standard builds.
  * **Detect:** `Compress (`/`CompressAsync (` calls in projects targeting `net4x` or
    `netstandard2.x`.
  * **Fix:** Remove the call, or put it behind `#if NET8_0_OR_GREATER`.
* **`DeliveryStatusNotificationType`** moved from `MailKit.Net.Smtp` to `MailKit`.
  * **Fix:** Add `using MailKit;`.
* **`ImapCommandException`** has new constructors. Calls like
  `new ImapCommandException (response, responseText, message, null)` are now ambiguous.
  * **Fix:** Cast the `null`, e.g. `(Exception) null`.
* **`AnnotationAttribute (string)`** now validates the specifier (RFC 5257 §3.2). It throws
  `ArgumentException` for empty components (a leading or trailing `.`, or `..`), NUL or non-ASCII
  characters, or misplaced `priv`/`shared` components.
  * **Fix:** Correct the specifier string.

---

## 9. Removed obsolete APIs

| Removed | Replacement |
|:--------|:------------|
| Parameterless `BodyPart`, `BodyPartBasic`, `BodyPartMessage`, `BodyPartMultipart`, `BodyPartText` constructors | `(ContentType, string)` constructors |
| Parameterless `MailFolder` constructor | `MailFolder (string fullName, char directorySeparator, FolderAttributes attributes)` |
| `ImapFolderConstructorArgs.Name` | Derive the name from `FullName` and `DirectorySeparator` |
| `SslCipherAlgorithm`, `SslCipherStrength`, `SslHashAlgorithm`, `SslHashStrength`, `SslKeyExchangeAlgorithm`, `SslKeyExchangeStrength` (**.NET 10.0 build only**) | `SslCipherSuite` |

---

## Behavior changes that still compile

These changes don't cause build errors. Search for the patterns below and review each match.

1. **Multi-line SMTP responses are joined with `\r\n`** (previously `\n`). This affects
   `SmtpResponse.ResponseText`, `SmtpCommandException.ResponseText` and
   `SmtpProtocolException.LastResponse`.
   * **Detect:** `Split ('\n')`, `Split ("\n")` or `.Split (new[] { '\n' }...)` on these values
     (e.g. when splitting an `SmtpCommandException.ResponseText` into lines).
   * **Fix:** Split on `"\r\n"`, or trim a trailing `'\r'` from each line.
2. **`SmtpServiceNotAuthenticatedException`** (which derives from `ServiceNotAuthenticatedException`)
   is thrown for `530 Authentication required`. `catch (ServiceNotAuthenticatedException)` still
   works.
   * **Detect:** Exact type checks like `ex.GetType () == typeof (ServiceNotAuthenticatedException)`.
   * **Fix:** Use `is`.
3. **`SmtpProtocolException` messages** for unexpected disconnects no longer include the previous
   command's response text.
   * **Detect:** Code that parses `ex.Message`.
   * **Fix:** Use the new `SmtpProtocolException.LastResponse` and `Command` properties instead.
4. **Telemetry `error.type` values** reported by `mailkit.net.*.client.*` metrics are more specific.
   For example, `protocol_error` may now be `response_ended`, `invalid_response`,
   `server_disconnected` or `response_too_large`. `command_error` may now be `rejected`,
   `not_found`, `permission_denied`, etc.
   * **Detect:** Dashboards, alerts or code that match on `protocol_error` or `command_error`.
     These are usually outside the repository, so point them out to the user.
   * **Fix:** See the "Telemetry changes" table in ReleaseNotes.md.
5. **IMAP `COMPRESS`** now flushes with `Z_SYNC_FLUSH` instead of `Z_FULL_FLUSH`. No code changes
   are required.

---

## Optional: adopt new 5.0 features

These are not required to port, but they often simplify code that worked around 4.x limitations:

* `SendResult.AcceptedRecipients`/`RejectedRecipients` instead of tracking recipients through
  `SmtpClient` hooks.
* `CommandException.ErrorType`/`IsTransient` and `ProtocolException.ErrorType` instead of parsing
  exception messages or status codes to decide whether to retry.
* `SmtpCommandException.Command`/`ResponseText` and `SmtpProtocolException.LastResponse` for
  diagnostics.
* Request callbacks (`OnStarted ()`/`OnCompleted ()`) on `CopyRequest`, `MoveRequest`,
  `ExpungeRequest`, `StoreFlagsRequest`, `StoreLabelsRequest`, `StoreAnnotationsRequest`,
  `AppendRequest`, `ReplaceRequest` and `SendRequest` to observe exactly what the server applied.
* `ImapFeatures.IMAP4rev2` and `StatusItems.Deleted`/`IMailFolder.DeletedCount`.
* `await using var client = new ImapClient ();` now works because `MailService` implements
  `IAsyncDisposable`. It does not log out, so keep calling `DisconnectAsync (true)` first.
