//
// NtlmSingleHostData.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-nlmp/b38c36ed-2804-4868-a9ff-8dd3182128e4

using System;

namespace MailKit.Security.Ntlm {
	/// <summary>
	/// An NTLM SingleHostData structure.
	/// </summary>
	/// <remarks>
	/// An NTLM SingleHostData structure.
	/// </remarks>
	class NtlmSingleHostData
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmSingleHostData"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NtlmSingleHostData"/>.
		/// </remarks>
		/// <param name="buffer">The raw target info buffer to decode.</param>
		/// <param name="startIndex">The starting index of the single host data structure.</param>
		/// <param name="length">The length of the single host data structure.</param>
		public NtlmSingleHostData (byte[] buffer, int startIndex, int length)
		{
			Decode (buffer, startIndex, length);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NtlmSingleHostData"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NtlmSingleHostData"/>.
		/// </remarks>
		/// <param name="customData">The 8-byte platform-specific blob.</param>
		/// <param name="machineId">The 256-bit randomly generated machine id.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="customData"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="machineId"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="customData"/> is not 8 bytes.</para>
		/// <para>-or-</para>
		/// <para><paramref name="machineId"/> is not 32 bytes.</para>
		/// </exception>
		public NtlmSingleHostData (byte[] customData, byte[] machineId)
		{
			if (customData == null)
				throw new ArgumentNullException (nameof (customData));

			if (customData.Length != 8)
				throw new ArgumentException ("The custom data must be 8 bytes.", nameof (customData));

			if (machineId == null)
				throw new ArgumentNullException (nameof (machineId));

			if (machineId.Length != 32)
				throw new ArgumentException ("The machine id must be 32 bytes.", nameof (machineId));

			CustomData = customData;
			MachineId = machineId;
			Size = 48;
		}

		/// <summary>
		/// Get or set an 8-byte platform-specific blob.
		/// </summary>
		/// <remarks>
		/// Gets or sets an 8-byte platform-specific blob.
		/// </remarks>
		public byte[] CustomData {
			get; private set;
		}

		/// <summary>
		/// Get the 256-bit randomly generated machine ID.
		/// </summary>
		/// <remarks>
		/// Gets the 256-bit randomly generated machine ID.
		/// </remarks>
		/// <value>The 256-bit randomly generated machine ID.</value>
		public byte[] MachineId {
			get; private set;
		}

		/// <summary>
		/// Get the size of the SingleHostData structure.
		/// </summary>
		/// <remarks>
		/// Gets the size of the SingleHostData structure.
		/// </remarks>
		/// <value>The size of the SingleHostData structure.</value>
		public int Size {
			get; private set;
		}

		void Decode (byte[] buffer, int startIndex, int length)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (startIndex < 0 || startIndex > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (length < 48 || length > (buffer.Length - startIndex))
				throw new ArgumentOutOfRangeException (nameof (length));

			int index = startIndex;

			// Size (4 bytes): A 32-bit unsigned integer that defines the length, in bytes, of the Value field in the AV_PAIR (section 2.2.2.1) structure.
			Size = BitConverterLE.ToInt32 (buffer, index);
			index += 4;

			// Z4 (4 bytes): A 32-bit integer value containing 0x00000000.
			index += 4;

			// CustomData (8 bytes): An 8-byte platform-specific blob containing info only relevant when the client and the server are on the same host.
			CustomData = new byte[8];
			Buffer.BlockCopy (buffer, index, CustomData, 0, 8);
			index += 8;

			// MachineID (32 bytes): A 256-bit random number created at computer startup to identify the calling machine.
			MachineId = new byte[32];
			Buffer.BlockCopy (buffer, index, MachineId, 0, 32);
		}

		/// <summary>
		/// Encode the SingleHostData structure.
		/// </summary>
		/// <remarks>
		/// Encodes the SingleHostData structure.
		/// </remarks>
		/// <returns>The encoded SingleHostData structure.</returns>
		public byte[] Encode ()
		{
			var buffer = new byte[Size];
			int index = 0;

			// Size (4 bytes): A 32-bit unsigned integer that defines the length, in bytes, of the Value field in the AV_PAIR (section 2.2.2.1) structure.
			buffer[index++] = (byte) (Size);
			buffer[index++] = (byte) (Size >> 8);
			buffer[index++] = (byte) (Size >> 16);
			buffer[index++] = (byte) (Size >> 24);

			// Z4 (4 bytes): A 32-bit integer value containing 0x00000000.
			index += 4;

			// CustomData (8 bytes): An 8-byte platform-specific blob containing info only relevant when the client and the server are on the same host.
			Buffer.BlockCopy (CustomData, 0, buffer, index, 8);
			index += 8;

			// MachineID (32 bytes): A 256-bit random number created at computer startup to identify the calling machine.
			Buffer.BlockCopy (MachineId, 0, buffer, index, 32);

			return buffer;
		}
	}
}
