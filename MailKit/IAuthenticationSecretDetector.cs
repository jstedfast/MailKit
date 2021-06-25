//
// IAuthenticationSecretDetector.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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

using System;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// An authentication secret.
	/// </summary>
	/// <remarks>
	/// An authentication secret.
	/// </remarks>
	public struct AuthenticationSecret
	{
		/// <summary>
		/// Get the starting offset of the secret within a buffer.
		/// </summary>
		/// <remarks>
		/// Gets the starting offset of the secret within a buffer.
		/// </remarks>
		/// <value>The start offset of the secret.</value>
		public int StartIndex { get; private set; }

		/// <summary>
		/// Get the length of the secret within a buffer.
		/// </summary>
		/// <remarks>
		/// Gets the length of the secret within a buffer.
		/// </remarks>
		/// <value>The length of the secret.</value>
		public int Length { get; private set; }

		/// <summary>
		/// Create a new <see cref="AuthenticationSecret"/>.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AuthenticationSecret"/>.
		/// </remarks>
		/// <param name="startIndex">The start index of the secret.</param>
		/// <param name="length">The length of the secret.</param>
		public AuthenticationSecret (int startIndex, int length)
		{
			StartIndex = startIndex;
			Length = length;
		}
	}

	/// <summary>
	/// An interface for detecting authentication secrets.
	/// </summary>
	/// <remarks>
	/// An interface for detecting authentication secrets.
	/// </remarks>
	public interface IAuthenticationSecretDetector

	{
		/// <summary>
		/// Detect a list of secrets within a buffer.
		/// </summary>
		/// <remarks>
		/// Detects a list of secrets within a buffer.
		/// </remarks>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The buffer offset.</param>
		/// <param name="count">The length of the buffer.</param>
		/// <returns>A list of secrets.</returns>
		IList<AuthenticationSecret> DetectSecrets (byte[] buffer, int offset, int count);
	}
}
