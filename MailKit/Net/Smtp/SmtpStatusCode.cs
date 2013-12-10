//
// SmtpStatusCode.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
	enum SmtpStatusCode {
		SystemStatus = 211,
		HelpMessage = 214,
		ServiceReady = 220,
		ServiceClosingTransmissionChannel = 221,
		AuthenticationSuccessful = 235,
		Ok = 250,
		UserNotLocalWillForward = 251,
		CannotVerifyUserWillAttemptDelivery = 252,
		AuthenticationChallenge = 334,
		StartMailInput = 354,
		ServiceNotAvailable = 421,
		PasswordTransitionNeeded = 432,
		MailboxBusy = 450,
		ErrorInProcessing = 451,
		InsufficientStorage = 452,
		TemporaryAuthenticationFailure = 454,
		CommandUnrecognized = 500,
		SyntaxError = 501,
		CommandNotImplemented = 502,
		BadCommandSequence = 503,
		CommandParameterNotImplemented = 504,
		AuthenticationRequired = 530,
		AuthenticationMechanismTooWeak = 534,
		EncryptionRequiredForAuthenticationMechanism = 538,
		MailboxUnavailable = 550,
		UserNotLocalTryAlternatePath = 551,
		ExceededStorageAllocation = 552,
		MailboxNameNotAllowed = 553,
		TransactionFailed = 554,
	}
}
