# MailKit Telemetry Documentation

## Socket Metrics

### Metric: `mailkit.net.socket.connect.count`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect.count`            | Counter             | `{attempt}`     | The number of times a socket attempted to connect to a remote host.        |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `network.peer.address`     | string   | Peer IP address of the socket connection.        | `142.251.167.109`                               | Always                |
| `server.address`           | string   | The host name that the socket is connecting to.  | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the socket is connecting to.       | `465`                                           | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |

This metric tracks the number of times a socket attempted to connect to a remote host.

`error.type` has the following values:

| **Value**               | **Description**                                                                |
|:------------------------|:-------------------------------------------------------------------------------|
| `cancelled`             | The operation was cancelled.                                                   |
| `host_not_found`        | No such host is known. The name is not an official host name or alias.         |
| `host_unreachable`      | There is no network route to the specified host.                               |
| `network_unreachable`   | No route to the remote host exists.                                            |
| `connection_aborted`    | The connection was aborted by .NET or the underlying socket provider.          |
| `connection_refused`    | The remote host is actively refusing a connection.                             |
| `connection_reset`      | The connection was reset by the remote peer.                                   |
| `timed_out`             | The connection attempt timed out, or the connected host has failed to respond. |
| `too_many_open_sockets` | There are too many open sockets in the underlying socket provider.             |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.socket.connect.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect.duration`         | Histogram           | `ms`            | The number of milliseconds taken for a socket to connect to a remote host. |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `network.peer.address`     | string   | Peer IP address of the socket connection.        | `142.251.167.109`                               | Always                |
| `server.address`           | string   | The host name that the socket is connecting to.  | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the socket is connecting to.       | `465`                                           | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |

This metric measures the time it takes to connect a socket to a remote host.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |

Available starting in: MailKit v4.4.0

## SmtpClient Metrics

### Metric: `mailkit.net.smtp.client.connection.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.connection.duration` | Histogram           | `s`             | The duration of successfully established connections to an SMTP server.    |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |

This metric tracks the connection duration of each SmtpClient connection and records any error details if the connection was terminated involuntarily.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.operation.count`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on an SMTP server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |

This metric tracks the number of times an SmtpClient has performed an operation on an SMTP server.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.operation.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the SMTP server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `smtp.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `25`, `465`, `587`                              | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `smtp` or `smtps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, `send`, ...          | Always                |

This metric tracks the amount of time it takes an SMTP server to perform an operation.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

## Pop3Client Metrics

### Metric: `mailkit.net.pop3.client.connection.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.connection.duration` | Histogram           | `s`             | The duration of successfully established connections to a POP3 server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the connection duration of each Pop3Client connection and records any error details if the connection was terminated involuntarily.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.pop3.client.operation.count`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on a POP3 server.      |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the number of times an Pop3Client has performed an operation on a POP3 server.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.pop3.client.operation.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.pop3.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the POP3 server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `pop.gmail.com`                                 | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `110`, `995`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `pop3` or `pop3s`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the amount of time it takes a POP3 server to perform an operation.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

## ImapClient Metrics

### Metric: `mailkit.net.imap.client.connection.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.connection.duration` | Histogram           | `s`             | The duration of successfully established connections to an IMAP server.    |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the connection duration of each ImapClient connection and records any error details if the connection was terminated involuntarily.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.imap.client.operation.count`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.operation.count`     | Counter             | `{operation}`   | The number of times a client performed an operation on an IMAP server.     |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the number of times an ImapClient has performed an operation on an IMAP server.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.imap.client.operation.duration`

**Status:** [Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.30.0/specification/document-status.md)

| **Name**                                      | **Instrument Type** | **Unit**        | **Description**                                                            |
|:----------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.imap.client.operation.duration`  | Histogram           | `ms`            | The amount of time it takes for the IMAP server to perform an operation.   |

| **Attribute**              | **Type** | **Description**                                  | **Examples**                                    | **Presence**          |
|:---------------------------|:---------|:-------------------------------------------------|:------------------------------------------------|:----------------------|
| `server.address`           | string   | The host name that the client is connected to.   | `imap.gmail.com`                                | Always                |
| `server.port`              | int      | The port that the client is connected to.        | `143`, `993`                                    | Always                |
| `url.scheme`               | string   | The URL scheme of the protocol used.             | `imap` or `imaps`                               | Always                |
| `error.type`               | string   | The type of error encountered.                   | `host_not_found`, `host_unreachable`, ...       | If an error occurred. |
| `network.operation`        | string   | The name of the operation.                       | `connect`, `authenticate`, ...                  | Always                |

This metric tracks the amount of time it takes an IMAP server to perform an operation.

`error.type` has the following values:

| **Value**                 | **Description**                                                                         |
|:--------------------------|:----------------------------------------------------------------------------------------|
| `cancelled`               | An operation was cancelled.                                                             |
| `host_not_found`          | No such host is known. The name is not an official host name or alias.                  |
| `host_unreachable`        | There is no network route to the specified host.                                        |
| `network_unreachable`     | No route to the remote host exists.                                                     |
| `connection_aborted`      | The connection was aborted by .NET or the underlying socket provider.                   |
| `connection_refused`      | The remote host is actively refusing a connection.                                      |
| `connection_reset`        | The connection was reset by the remote peer.                                            |
| `timed_out`               | The connection attempt timed out, or the connected host has failed to respond.          |
| `too_many_open_sockets`   | There are too many open sockets in the underlying socket provider.                      |
| `secure_connection_error` | An SSL or TLS connection could not be negotiated.                                       |
| `protocol_error`          | The connection was terminated due to an incomplete or invalid response from the server. |

Available starting in: MailKit v4.4.0
