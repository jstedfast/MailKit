//
// ITransferProgress.cs
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

namespace MailKit {
	/// <summary>
	/// An interface for reporting progress of uploading or downloading messages.
	/// </summary>
	/// <remarks>
	/// An interface for reporting progress of uploading or downloading messages.
	/// </remarks>
	public interface ITransferProgress
	{
		/// <summary>
		/// Report the progress of the transfer operation.
		/// </summary>
		/// <remarks>
		/// <para>Reports the progress of the transfer operation.</para>
		/// <para>This method is only used if the operation knows the size
		/// of the message, part, or stream being transferred without doing
		/// extra work to calculate it.</para>
		/// </remarks>
		/// <param name="bytesTransferred">The number of bytes transferred.</param>
		/// <param name="totalSize">The total size, in bytes, of the message, part, or stream being transferred.</param>
		void Report (long bytesTransferred, long totalSize);

		/// <summary>
		/// Report the progress of the transfer operation.
		/// </summary>
		/// <remarks>
		/// <para>Reports the progress of the transfer operation.</para>
		/// </remarks>
		/// <param name="bytesTransferred">The number of bytes transferred.</param>
		void Report (long bytesTransferred);
	}
}
