# MailKit Telemetry Documentation

## Socket Metrics

### Metric: `mailkit.net.socket.connect.count`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect.count`         | Counter             | `{attempt}`     | The number of times a socket attempted to connect to a remote host.        |

| **Attribute**           | **Type** | **Description**                                 | **Examples**                            | **Presence**          |
|:------------------------|:---------|:------------------------------------------------|:----------------------------------------|:----------------------|
| `socket.connect.result` | string   | The connection result.                          | `succeeded`, `failed`, `cancelled`      | Always                |
| `network.peer.address`  | string   | Peer IP address of the socket connection.       | `142.251.167.109`                       | Always                |
| `server.address`        | string   | The host name that the socket is connecting to. | `smtp.gmail.com`                        | Always                |
| `server.port`           | int      | The port that the socket is connecting to.      | `465`                                   | Always                |
| `error.type`            | string   | The type of exception encountered.              | `System.Net.Sockets.SocketException`    | If an error occurred. |
| `socket.error`          | int      | The socket error code.                          | `10054`, `10060`, `10061`, ...          | If one was received.  |

This metric tracks the number of times a socket attempted to connect to a remote host.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

### Metric: `mailkit.net.socket.connect.duration`

| **Name**                     | **Instrument Type** | **Unit** | **Description**                                                            |
|:-----------------------------|:--------------------|:---------|:---------------------------------------------------------------------------|
| `mailkit.net.socket.connect` | Histogram           | `ms`     | The number of milliseconds taken for a socket to connect to a remote host. |

| **Attribute**           | **Type** | **Description**                                 | **Examples**                            | **Presence**          |
|:------------------------|:---------|:------------------------------------------------|:----------------------------------------|:----------------------|
| `socket.connect.result` | string   | The connection result.                          | `succeeded`, `failed`, `cancelled`      | Always                |
| `network.peer.address`  | string   | Peer IP address of the socket connection.       | `142.251.167.109`                       | Always                |
| `server.address`        | string   | The host name that the socket is connecting to. | `smtp.gmail.com`                        | Always                |
| `server.port`           | int      | The port that the socket is connecting to.      | `465`                                   | Always                |
| `error.type`            | string   | The type of exception encountered.              | `System.Net.Sockets.SocketException`    | If an error occurred. |
| `socket.error`          | int      | The socket error code.                          | `10054`, `10060`, `10061`, ...          | If one was received.  |

This metric measures the time it takes to connect a socket to a remote host.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

Available starting in: MailKit v4.4.0

## SmtpClient Metrics

### Metric: `mailkit.net.smtp.client.connect.count`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.connect.count`    | Counter             | `{attempt}`     | The number of times a client has attempted to connect to an SMTP server.   |

| **Attribute**           | **Type** | **Description**                                | **Examples**                            | **Presence**             |
|:------------------------|:---------|:-----------------------------------------------|:----------------------------------------|:-------------------------|
| `network.peer.address`  | string   | Peer IP address of the client connection.      | `142.251.167.109`                       | When available           |
| `server.address`        | string   | The host name that the client is connected to. | `smtp.gmail.com`                        | Always                   |
| `server.port`           | int      | The port that the client is connected to.      | `25`, `465`, `587`                      | Always                   |
| `url.scheme`            | string   | The URL scheme of the protocol used.           | `smtp` or `smtps`                       | Always                   |
| `error.type`            | string   | The type of exception encountered.             | `System.Net.Sockets.SocketException`    | If an error occurred.    |
| `smtp.status_code`      | int      | The SMTP status code returned by the server.   | `530`, `550`, `553`, ...                | If one was received.     |
| `socket.error`          | int      | The socket error code.                         | `10054`, `10060`, `10061`, ...          | If one was received.     |

This metric tracks the number of times an SmtpClient has attempted to connect to an SMTP server.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

For the list of potential `smtp.status_code` values, see the documentation for the
[SmtpStatusCode](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpStatusCode.htm) enum.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.connect.duration`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.connect.duration` | Histogram           | `ms`            | The amount of time it takes for the client to connect to an SMTP server.   |

| **Attribute**           | **Type** | **Description**                                | **Examples**                            | **Presence**             |
|:------------------------|:---------|:-----------------------------------------------|:----------------------------------------|:-------------------------|
| `network.peer.address`  | string   | Peer IP address of the client connection.      | `142.251.167.109`                       | When available           |
| `server.address`        | string   | The host name that the client is connected to. | `smtp.gmail.com`                        | Always                   |
| `server.port`           | int      | The port that the client is connected to.      | `25`, `465`, `587`                      | Always                   |
| `url.scheme`            | string   | The URL scheme of the protocol used.           | `smtp` or `smtps`                       | Always                   |
| `error.type`            | string   | The type of exception encountered.             | `System.Net.Sockets.SocketException`    | If an error occurred.    |
| `smtp.status_code`      | int      | The SMTP status code returned by the server.   | `530`, `550`, `553`, ...                | If one was received.     |
| `socket.error`          | int      | The socket error code.                         | `10054`, `10060`, `10061`, ...          | If one was received.     |

This metric tracks the amount of time it takes an SmtpClient to connect to an SMTP server.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

For the list of potential `smtp.status_code` values, see the documentation for the
[SmtpStatusCode](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpStatusCode.htm) enum.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.connection.duration`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.connection.duration` | Histogram           | `s`             | The duration of successfully established connections to an SMTP server.    |

| **Attribute**           | **Type** | **Description**                                | **Examples**                            | **Presence**             |
|:------------------------|:---------|:-----------------------------------------------|:----------------------------------------|:-------------------------|
| `network.peer.address`  | string   | Peer IP address of the client connection.      | `142.251.167.109`                       | When available           |
| `server.address`        | string   | The host name that the client is connected to. | `smtp.gmail.com`                        | Always                   |
| `server.port`           | int      | The port that the client is connected to.      | `25`, `465`, `587`                      | Always                   |
| `url.scheme`            | string   | The URL scheme of the protocol used.           | `smtp` or `smtps`                       | Always                   |
| `error.type`            | string   | The type of exception encountered.             | `MailKit.Net.Smtp.SmtpCommandException` | If an error occurred.    |
| `smtp.status_code`      | int      | The SMTP status code returned by the server.   | `530`, `550`, `553`, ...                | If one was received.     |
| `socket.error`          | int      | The socket error code.                         | `10054`, `10060`, `10061`, ...          | If one was received.     |

This metric tracks the connection duration of each SmtpClient connection and records any error details if the connection was terminated involuntarily.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

For the list of potential `smtp.status_code` values, see the documentation for the
[SmtpStatusCode](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpStatusCode.htm) enum.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.send.count`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.send.count`       | Counter             | `{message}`     | The number of messages sent to an SMTP server.                             |

| **Attribute**           | **Type** | **Description**                                | **Examples**                            | **Presence**             |
|:------------------------|:---------|:-----------------------------------------------|:----------------------------------------|:-------------------------|
| `network.peer.address`  | string   | Peer IP address of the client connection.      | `142.251.167.109`                       | When available           |
| `server.address`        | string   | The host name that the client is connected to. | `smtp.gmail.com`                        | Always                   |
| `server.port`           | int      | The port that the client is connected to.      | `25`, `465`, `587`                      | Always                   |
| `url.scheme`            | string   | The URL scheme of the protocol used.           | `smtp` or `smtps`                       | Always                   |
| `error.type`            | string   | The type of exception encountered.             | `MailKit.Net.Smtp.SmtpCommandException` | If an error occurred.    |
| `smtp.status_code`      | int      | The SMTP status code returned by the server.   | `530`, `550`, `553`, ...                | If one was received.     |
| `socket.error`          | int      | The socket error code.                         | `10054`, `10060`, `10061`, ...          | If one was received.     |

This metric tracks the number of messages sent by an SmtpClient and records any error details if the message was not sent successfully.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

For the list of potential `smtp.status_code` values, see the documentation for the
[SmtpStatusCode](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpStatusCode.htm) enum.

Available starting in: MailKit v4.4.0

### Metric: `mailkit.net.smtp.client.send.duration`

| **Name**                                   | **Instrument Type** | **Unit**        | **Description**                                                            |
|:-------------------------------------------|:--------------------|:----------------|:---------------------------------------------------------------------------|
| `mailkit.net.smtp.client.send.duration`    | Histogram           | `ms`            | The amount of time it takes to send a message to an SMTP server.           |

| **Attribute**           | **Type** | **Description**                                | **Examples**                            | **Presence**             |
|:------------------------|:---------|:-----------------------------------------------|:----------------------------------------|:-------------------------|
| `network.peer.address`  | string   | Peer IP address of the client connection.      | `142.251.167.109`                       | When available           |
| `server.address`        | string   | The host name that the client is connected to. | `smtp.gmail.com`                        | Always                   |
| `server.port`           | int      | The port that the client is connected to.      | `25`, `465`, `587`                      | Always                   |
| `url.scheme`            | string   | The URL scheme of the protocol used.           | `smtp` or `smtps`                       | Always                   |
| `error.type`            | string   | The type of exception encountered.             | `MailKit.Net.Smtp.SmtpCommandException` | If an error occurred.    |
| `smtp.status_code`      | int      | The SMTP status code returned by the server.   | `530`, `550`, `553`, ...                | If one was received.     |
| `socket.error`          | int      | The socket error code.                         | `10054`, `10060`, `10061`, ...          | If one was received.     |

This metric tracks the amount of time that it takes an SmtpClient to send a message and records any error details if the message was not sent successfully.

For the list of potential `socket.error` values, see the documentation for the
[SocketError](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror?view=net-8.0) enum.

For the list of potential `smtp.status_code` values, see the documentation for the
[SmtpStatusCode](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpStatusCode.htm) enum.

Available starting in: MailKit v4.4.0
