using System;
using System.Net;

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
		MimeEntity ParseMultipartFormData (HttpWebResponse response)
		{
			// create a temporary file to store our large HTTP data stream
			var tmp = Path.GetTempFileName ();

			using (var stream = File.Open (tmp, FileMode.Open, FileAccess.ReadWrite)) {
				// create a header for the multipart/form-data MIME entity based on the Content-Type value of the HTTP
				// response
				var header = Encoding.UTF8.GetBytes (string.Format ("Content-Type: {0}\r\n\r\n", response.ContentType));

				// write the header to the stream
				stream.Write (header, 0, header.Length);

				// copy the content of the HTTP response to our temporary stream
				response.GetResponseStream ().CopyTo (stream);

				// reset the stream back to the beginning
				stream.Position = 0;

				// parse the MIME entity with persistent = true, telling the parser not to load the content into memory
				return MimeEntity.Load (stream, persistent: true);
			}
		}
		#endregion
	}
}
