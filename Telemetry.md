# MailKit Telemetry Documentation

## Socket Metrics

### Metric: `mailkit.net.socket.connect.count`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect.count`            | Counter             | `{attempt}`     | The number of times a socket attempted to connect to a remote host.        |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `network.peer.address`     | string   | Peer IP address of the socket connection.        | `142.251.167.109`                               | Always                |
| `server.address`           | string   | The host name that the socket is connecting to.  | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the socket is connecting to.       | `465`                                           | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `SocketException`, `OperationCanceledException` | If an error occurred. |
| `socket.error`             | int      | The socket error code.                           | `10054`, `10060`, `10061`, ...                  | If SocketException.   |

This metric tracks the number of times a socket attempted to connect to a remote host.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

### Metric: `mailkit.net.socket.connect.duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect`                  | Histogram           | `ms`            | The number of milliseconds taken for a socket to connect to a remote host. |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `network.peer.address`     | string   | Peer IP address of the socket connection.        | `142.251.167.109`                               | Always                |
| `server.address`           | string   | The host name that the socket is connecting to.  | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the socket is connecting to.       | `465`                                           | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `SocketException`, `OperationCanceledException` | If an error occurred. |
| `socket.error`             | int      | The socket error code.                           | `10054`, `10060`, `10061`, ...                  | If SocketException.   |

This metric measures the time it takes to connect a socket to a remote host.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

Available starting in: MailKit v4.4.0

## SmtpClient Metrics

### Metric: `mailkit.net.smtp.client.connection_duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.connection_duration` | Histogram           | `s`             | The duration of successfully established connections to an SMTP server.    |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `SmtpCommandException`, `SmtpProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the connection duration of each SmtpClient connection and records any error details if the connection was terminated involuntarily.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.operation.count`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on an SMTP server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `SmtpCommandException`, `SmtpProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the number of times an SmtpClient has performed an operation on an SMTP server.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.operation.duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the SMTP server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `SmtpCommandException`, `SmtpProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the amount of time it takes an SMTP server to perform an operation.

Available starting in: MailKit v4.4.0

## Pop3Client Metrics

### Metric: `mailkit.net.pop3.client.connection_duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.connection_duration` | Histogram           | `s`             | The duration of successfully established connections to a POP3 server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `Pop3CommandException`, `Pop3ProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the connection duration of each Pop3Client connection and records any error details if the connection was terminated involuntarily.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.pop3.client.operation.count`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on a POP3 server.      |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `Pop3CommandException`, `Pop3ProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the number of times an Pop3Client has performed an operation on a POP3 server.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.pop3.client.operation.duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the POP3 server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `Pop3CommandException`, `Pop3ProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the amount of time it takes a POP3 server to perform an operation.

Available starting in: MailKit v4.4.0

## ImapClient Metrics

### Metric: `mailkit.net.imap.client.connection_duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.connection_duration` | Histogram           | `s`             | The duration of successfully established connections to an IMAP server.    |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `ImapCommandException`, `ImapProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the connection duration of each ImapClient connection and records any error details if the connection was terminated involuntarily.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.imap.client.operation.count`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on an IMAP server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `ImapCommandException`, `ImapProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the number of times an ImapClient has performed an operation on an IMAP server.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.imap.client.operation.duration`

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the IMAP server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `exception.type`           | string   | The type of exception encountered.               | `ImapCommandException`, `ImapProtocolException` | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |
| `network.operation.status` | string   | The status of the operation.                     | `ok`, `cancelled`, or `error`                   | Always                |

This metric tracks the amount of time it takes an IMAP server to perform an operation.

Available starting in: MailKit v4.4.0
