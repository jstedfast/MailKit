using System;
using System.IO;
using System.Net;
using System.Text;

using MimeKit;

namespace Examples {
	class MultipartFormDataExample
	{
		#region ParseMultipartFormDataSimple
		MimeEntity ParseMultipartFormData (HttpWebResponse response)
		{
			var contentType = ContentType.Parse (response.ContentType);

			return MimeEntity.Load (contentType, response.GetResponseStream ());
		}
		#endregion

		#region ParseMultipartFormDataComplex
		MimeEntity ParseMultipartFormData (HttpWebResponse response, Stream stream)
		{
			// The caller supplies an empty, readable, writable, seekable temporary-file stream
			// and keeps it open and unchanged until the returned entity is no longer needed.

			// create a header for the multipart/form-data MIME entity based on the Content-Type value of the HTTP
			// response
			var header = Encoding.UTF8.GetBytes (string.Format ("Content-Type: {0}\r\n\r\n", response.ContentType));

			// write the header to the stream
			stream.Write (header, 0, header.Length);

			// copy the content of the HTTP response to our temporary stream
			response.GetResponseStream ().CopyTo (stream);

			// reset the stream back to the beginning
			stream.Position = 0;

			// reference the source stream instead of loading the content into memory
			return MimeEntity.Load (stream, storage: MimeContentStorage.Source);
		}
		#endregion
	}
}
