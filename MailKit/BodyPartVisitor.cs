//
// BodyPartVisitor.cs
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
	/// Represents a visitor for a tree of MIME body parts.
	/// </summary>
	/// <remarks>
	/// This class is designed to be inherited to create more specialized classes whose
	/// functionality requires traversing, examining or copying a tree of MIME body parts.
	/// </remarks>
	public abstract class BodyPartVisitor
	{
		/// <summary>
		/// Dispatches the entity to one of the more specialized visit methods in this class.
		/// </summary>
		/// <remarks>
		/// Dispatches the entity to one of the more specialized visit methods in this class.
		/// </remarks>
		/// <param name="body">The MIME body part.</param>
		public virtual void Visit (BodyPart body)
		{
			if (body != null)
				body.Accept (this);
		}

		/// <summary>
		/// Visit the abstract MIME body part.
		/// </summary>
		/// <remarks>
		/// Visits the abstract MIME body part.
		/// </remarks>
		/// <param name="entity">The MIME body part.</param>
		protected internal virtual void VisitBodyPart (BodyPart entity)
		{
		}

		/// <summary>
		/// Visit the basic MIME body part.
		/// </summary>
		/// <remarks>
		/// Visits the basic MIME body part.
		/// </remarks>
		/// <param name="entity">The basic MIME body part.</param>
		protected internal virtual void VisitBodyPartBasic (BodyPartBasic entity)
		{
			VisitBodyPart (entity);
		}

		/// <summary>
		/// Visit the message contained within a message/rfc822 or message/news MIME entity.
		/// </summary>
		/// <remarks>
		/// Visits the message contained within a message/rfc822 or message/news MIME entity.
		/// </remarks>
		/// <param name="message">The body part representing the message/rfc822 message.</param>
		protected virtual void VisitMessage (BodyPart message)
		{
			if (message != null)
				message.Accept (this);
		}

		/// <summary>
		/// Visit the message/rfc822 or message/news MIME entity.
		/// </summary>
		/// <remarks>
		/// Visits the message/rfc822 or message/news MIME entity.
		/// </remarks>
		/// <param name="entity">The message/rfc822 or message/news body part.</param>
		protected internal virtual void VisitBodyPartMessage (BodyPartMessage entity)
		{
			VisitBodyPartBasic (entity);
			VisitMessage (entity.Body);
		}

		/// <summary>
		/// Visit the children of a <see cref="MailKit.BodyPartMultipart"/>.
		/// </summary>
		/// <remarks>
		/// Visits the children of a <see cref="MailKit.BodyPartMultipart"/>.
		/// </remarks>
		/// <param name="multipart">The multipart.</param>
		protected virtual void VisitChildren (BodyPartMultipart multipart)
		{
			for (int i = 0; i < multipart.BodyParts.Count; i++)
				multipart.BodyParts[i].Accept (this);
		}

		/// <summary>
		/// Visit the abstract multipart MIME entity.
		/// </summary>
		/// <remarks>
		/// Visits the abstract multipart MIME entity.
		/// </remarks>
		/// <param name="multipart">The multipart body part.</param>
		protected internal virtual void VisitBodyPartMultipart (BodyPartMultipart multipart)
		{
			VisitBodyPart (multipart);
			VisitChildren (multipart);
		}

		/// <summary>
		/// Visit the text-based MIME part entity.
		/// </summary>
		/// <remarks>
		/// Visits the text-based MIME part entity.
		/// </remarks>
		/// <param name="entity">The text-based body part.</param>
		protected internal virtual void VisitBodyPartText (BodyPartText entity)
		{
			VisitBodyPartBasic (entity);
		}
	}
}
