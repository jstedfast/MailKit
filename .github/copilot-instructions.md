# MailKit — Copilot Instructions

MailKit is a cross-platform C# IMAP, POP3 and SMTP client library built on top of MimeKit. It is a
.NET Foundation project. Correctness against the [relevant IETF RFCs](../RFCs.md) and robust
handling of real-world (often non-compliant) servers are the dominant design constraints.

MimeKit is consumed as a git submodule (`submodules/MimeKit`) and built from source as part of
`MailKit.sln`. Run `git submodule update --init --recursive` before building. MimeKit has its own
`submodules/MimeKit/.github/copilot-instructions.md`, so read it before you change anything under
`submodules/MimeKit`.

## Build, Test, and Coverage

```powershell
dotnet restore MailKit.sln
dotnet tool restore                      # installs altcover, coveralls.net, nunit console runner
dotnet msbuild MailKit.sln -property:Platform="Any CPU" -property:Configuration=Debug
```

On non-Windows also pass `-property:MonoRuntime=true`. This defines `MONO` in `UnitTests` and
selects the non-Windows SQLite interop binaries.

Run the full test suite the way CI does:

```powershell
./scripts/test.ps1 -Configuration Debug -GenerateCodeCoverage false
```

`scripts/test.ps1` runs `dotnet nunit UnitTests/bin/<Config>/net8.0/UnitTests.dll`. With
`-GenerateCodeCoverage true`, it instruments the assemblies *in place* with AltCover and re-signs them
with `MailKit/mailkit.snk`. This matters because MimeKit's `InternalsVisibleTo ("MailKit, PublicKey=...")`
requires the signature. Rebuild before running without coverage if you previously ran with it.

Run a single test or fixture (fastest inner loop; builds as a side effect):

```powershell
dotnet test UnitTests\UnitTests.csproj --filter "FullyQualifiedName~ImapFolderFlagsTests.TestChangingFlagsOnEmptyListOfMessages"
dotnet test UnitTests\UnitTests.csproj --filter "FullyQualifiedName~UnitTests.Net.Smtp.SmtpClientTests"

# or, against an already-built test assembly (no rebuild):
dotnet nunit UnitTests\bin\Debug\net8.0\UnitTests.dll --where "class =~ /SmtpClientTests/" --noresult
```

Before finishing a library change, build every target framework in Release. This catches
API-availability problems on `net4x`/`netstandard2.0`:

```powershell
dotnet msbuild MailKit\MailKit.csproj -restore -property:Configuration=Release -v:m
```

AOT compatibility is enforced in CI:

```powershell
./scripts/test-aot-compatibility.ps1     # fails if `dotnet publish` of AotCompatibility/ emits any `warning IL*`
```

There is no separate lint step. `.editorconfig` and the analyzers enforce style. The `NoWarn` lists
in the `.csproj` files are deliberate, so do not "fix" a suppressed diagnostic by editing `NoWarn`.

## Project Layout

| Path | Notes |
|---|---|
| `MailKit/` | The library (`MailKit` assembly/package). Depends on `MimeKit.Core`, not on BouncyCastle. |
| `submodules/MimeKit/` | MimeKit source; `MailKit.sln` builds `MimeKit/MimeKit.csproj` (`MimeKit.Core`) from here. |
| `UnitTests/` | NUnit 4, `net8.0` only. |
| `AotCompatibility/` | Native-AOT publish probe used by `scripts/test-aot-compatibility.ps1`. |
| `Documentation/` | Sandcastle docs project. `Documentation/Examples/*.cs` are *not compiled* but are pulled into XML docs via `<code source="Examples\Foo.cs" region="..."/>`. |
| `nuget/MailKit.nuspec` | Package definition (the package is built with `nuget pack`, not `dotnet pack`). |
| `samples/` | Standalone sample apps, not part of `MailKit.sln`. |

`MailKit.csproj` uses the SDK's default globbing, so every `.cs` file under `MailKit/` is compiled.
A few legacy files are excluded with `<Compile Remove>` (e.g. `Net/SelectMode.cs`). Add a `Remove` if
you need to keep a file out of the build.

## Target Frameworks and Conditional Compilation

MailKit multi-targets `netstandard2.0;netstandard2.1;net462;net47;net48;net8.0;net10.0`. For every change:

- `LangVersion` is pinned to **12** (UnitTests: **10**). Do not use newer language features.
- `Nullable` is enabled in the library. It is **not** enabled in `UnitTests`, so don't add `?`/`!`
  annotations there (CS8632).
- Modern BCL APIs are not available on every target. Guard with `#if NET*` or use existing helpers.
- `IsAotCompatible` is set for net7.0+ targets. Do not use reflection-based dynamic code.
- Assembly version attributes are hand-maintained in `MailKit/Properties/AssemblyInfo.cs`, because
  assembly-info generation is disabled.

## Architecture

### Protocol layering

Each protocol under `MailKit/Net/{Imap,Pop3,Smtp}` follows the same layering:

- **`XxxClient`** is the public API. It derives from `MailStore` (IMAP), `MailSpool` (POP3) or
  `MailTransport` (SMTP), which in turn derive from `MailService`. It handles connection/TLS, auth
  (SASL from `MailKit/Security`), and state checks.
- **`XxxEngine`** (IMAP/POP3) owns the connection state, capabilities, command queue, and response
  parsing. IMAP commands are `ImapCommand` objects built from printf-like format strings
  (`%S` string/literal, `%F` folder, `%L` literal, `%d`, `%u`, `%s`). They are queued with
  `Engine.QueueCommand (...)` and driven with `Engine.Run (ic)` / `await Engine.RunAsync (ic)`.
  Untagged responses are dispatched through `ic.RegisterUntaggedHandler (atom, handler)`.
- **`XxxStream`** is the line/token-oriented network stream. SMTP has no engine;
  `SmtpStream.SendCommand[Async]` talks to the server directly.
- `ImapFolder` is split across many partial-class files by feature (`ImapFolderFetch.cs`,
  `ImapFolderFlags.cs`, `ImapFolderSearch.cs`, ...).
- `MailKit/Net/Proxy` holds HTTP/HTTPS/SOCKS4/4a/5 proxy clients. `MailKit/Security` holds SASL
  mechanisms (including NTLM) and certificate validation. `MailKit/Search` holds the `SearchQuery`
  tree and its optimizer. `MailKit/Telemetry` holds metrics (documented in `Telemetry.md`).
- Public interfaces (`IMailFolder`, `IImapClient`, `ISmtpClient`, ...) mirror the concrete types.
  Convenience overloads live in `IMailFolder*Extensions` classes. When you add a public member to a
  type that has an interface, add it to the interface too.

### Every async method is a hand-maintained mirror of its sync twin

There is no code generator, so sync and async logic are duplicated by hand. `ImapClient.cs` and
`AsyncImapClient.cs` (and likewise for Pop3/Smtp) are `partial class` pairs. Within other files,
`Foo`/`FooAsync` sit side by side with `await ... .ConfigureAwait (false)` substituted in. Some
low-level helpers take a `bool doAsync` parameter (e.g. `ImapContinuationHandler`).

- **A fix applied only to the sync path is a bug.**
- Watch for drift between the twins.
- Every `await` in the library must use `.ConfigureAwait (false)`.

### Hostile and quirky servers

Treat server responses as untrusted: bound anything that grows with input and fail with a protocol
exception, not an unbounded allocation. Many code paths deliberately tolerate specific broken
servers (Exchange, GMail, Domino, Yahoo, ...), so keep those workarounds intact when refactoring.

## Conventions

- **Formatting:** tabs, CRLF, and a space between a method name and its argument list
  (`client.GetFolder (name, cancellationToken)`) in both calls and declarations (Mono style).
  `else`/`catch`/`finally` stay on the same line as the closing brace. `.editorconfig` is authoritative.
- **Namespaces** use the brace-on-same-line form: `namespace MailKit.Net.Imap {`.
- **License header:** every source file starts with the standard MIT block, including
  `Author: Jeffrey Stedfast <jestedfa@microsoft.com>` and the `.NET Foundation and Contributors`
  copyright. Copy it verbatim into new files.
- **Argument validation:** use explicit checks, e.g.
  `if (x == null) throw new ArgumentNullException (nameof (x));`. Every public API is expected to
  validate its arguments, and `UnitTests/ArgumentExceptionTests.cs` covers them.
- **XML documentation** is required on public members, because the docs ship in the NuGet package.
  Document every exception a method can throw (follow the exhaustive `<exception>` lists used on
  existing methods). Prefer `<code language="c#" source="Examples\..." region="..."/>` examples
  backed by `Documentation/Examples`.
- **Strong naming:** both MailKit and UnitTests are signed with `MailKit/mailkit.snk`, and
  `InternalsVisibleTo` relies on that key.

## Testing Conventions

- Fixtures are named `<Type>Tests` and live in the directory that mirrors the source layout
  (`UnitTests/Net/Imap/`, `UnitTests/Net/Smtp/`, `UnitTests/Security/`, ...).
- Test methods are named `TestXxx`, and the async mirror is `TestXxxAsync`. When you add a test for an
  API with an async twin, add both. Sync and async tests usually share a `CreateXxxCommands ()` helper.
- Protocol tests don't use real servers. They script a conversation with
  **`ImapReplayStream`/`Pop3ReplayStream`/`SmtpReplayStream`** and a list of `XxxReplayCommand`s,
  each pairing an exact expected client command with a canned server response:

  ```csharp
  new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt")
  ...
  client.Connect (new ImapReplayStream (commands, isAsync), "localhost", 143, SecureSocketOptions.None);
  ```

  Set `TagPrefix = 'A'` on `ImapClient` so tags are predictable.
- Canned responses are embedded resources under `UnitTests/Net/{Imap,Pop3,Smtp}/Resources/`. They are
  referenced as `<server>.<name>.txt`, and IMAP responses are grouped into per-server folders
  (`common`, `dovecot`, `gmail`, `exchange`, ...). In resource files, `A########` is replaced with the
  command's tag, and LF line endings are converted to CRLF at load time.
- When a test is meant to pin a behaviour, sanity-check it by temporarily breaking the code under
  test and confirming the test fails.

## Releasing

Version numbers live in:

- `VersionPrefix` in `MailKit.csproj`;
- `MailKit/Properties/AssemblyInfo.cs`;
- `nuget/MailKit.nuspec`, both `<version>` and the `MimeKit.Core` dependency version.

`scripts/update-version.ps1 -Version x.y.z` updates all of them and the samples.

`.github/workflows/main.yml` (CI) builds pre-release packages and publishes them to MyGet on pushes to
`master`. Official releases are made by manually running `.github/workflows/release.yml` with the full
40-character commit SHA. The workflow:

1. verifies that the versions agree;
2. builds and packs;
3. optionally code-signs (`sign` input);
4. generates build-provenance attestations;
5. pushes to NuGet.org;
6. tags the commit.

Any step that modifies the package must run before attestation.

Update `ReleaseNotes.md` for user-visible changes, and `PortingGuide.md` for breaking changes.
