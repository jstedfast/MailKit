//
// SmtpStatusCode.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An enumeration of possible SMTP status codes.
	/// </summary>
	/// <remarks>
	/// An enumeration of possible SMTP status codes.
	/// </remarks>
	public enum SmtpStatusCode {
		/// <summary>
		/// The "system status" status code.
		/// </summary>
		SystemStatus = 211,

		/// <summary>
		/// The "help" status code.
		/// </summary>
		HelpMessage = 214,

		/// <summary>
		/// The "service ready" status code.
		/// </summary>
		ServiceReady = 220,

		/// <summary>
		/// The "service closing transmission channel" status code.
		/// </summary>
		ServiceClosingTransmissionChannel = 221,

		/// <summary>
		/// The "authentication successful" status code.
		/// </summary>
		AuthenticationSuccessful = 235,

		/// <summary>
		/// The general purpose "OK" status code.
		/// </summary>
		Ok = 250,

		/// <summary>
		/// The "User not local; will forward" status code.
		/// </summary>
		UserNotLocalWillForward = 251,

		/// <summary>
		/// The "cannot verify user; will attempt delivery" status code.
		/// </summary>
		CannotVerifyUserWillAttemptDelivery = 252,

		/// <summary>
		/// The "authentication challenge" status code.
		/// </summary>
		AuthenticationChallenge = 334,

		/// <summary>
		/// The "start mail input" status code.
		/// </summary>
		StartMailInput = 354,

		/// <summary>
		/// The "service not available" status code.
		/// </summary>
		ServiceNotAvailable = 421,

		/// <summary>
		/// The "password transition needed" status code.
		/// </summary>
		PasswordTransitionNeeded = 432,

		/// <summary>
		/// The "mailbox busy" status code.
		/// </summary>
		MailboxBusy = 450,

		/// <summary>
		/// The "error in processing" status code.
		/// </summary>
		ErrorInProcessing = 451,

		/// <summary>
		/// The "insufficient storage" status code.
		/// </summary>
		InsufficientStorage = 452,

		/// <summary>
		/// The "temporary authentication failure" status code.
		/// </summary>
		TemporaryAuthenticationFailure = 454,

		/// <summary>
		/// The "command unrecognized" status code.
		/// </summary>
		CommandUnrecognized = 500,

		/// <summary>
		/// The "syntax error" status code.
		/// </summary>
		SyntaxError = 501,

		/// <summary>
		/// The "command not implemented" status code.
		/// </summary>
		CommandNotImplemented = 502,

		/// <summary>
		/// The "bad command sequence" status code.
		/// </summary>
		BadCommandSequence = 503,

		/// <summary>
		/// The "command parameter not implemented" status code.
		/// </summary>
		CommandParameterNotImplemented = 504,

		/// <summary>
		/// The "authentication required" status code.
		/// </summary>
		AuthenticationRequired = 530,

		/// <summary>
		/// The "authentication mechanism too weak" status code.
		/// </summary>
		AuthenticationMechanismTooWeak = 534,

		/// <summary>
		/// The "authentication invalid credentials" status code.
		/// </summary>
		AuthenticationInvalidCredentials = 535,

		/// <summary>
		/// The "encryption required for authentication mechanism" status code.
		/// </summary>
		EncryptionRequiredForAuthenticationMechanism = 538,

		/// <summary>
		/// The "mailbox unavailable" status code.
		/// </summary>
		MailboxUnavailable = 550,

		/// <summary>
		/// The "user not local try alternate path" status code.
		/// </summary>
		UserNotLocalTryAlternatePath = 551,

		/// <summary>
		/// The "exceeded storage allocation" status code.
		/// </summary>
		ExceededStorageAllocation = 552,

		/// <summary>
		/// The "mailbox name not allowed" status code.
		/// </summary>
		MailboxNameNotAllowed = 553,

		/// <summary>
		/// The "transaction failed" status code.
		/// </summary>
		TransactionFailed = 554,

		/// <summary>
		/// The "mail from/rcpt to parameters not recognized or not implemented" status code.
		/// </summary>
		MailFromOrRcptToParametersNotRecognizedOrNotImplemented = 555,
	}
}
