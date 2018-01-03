//
// ImapResponseCode.cs
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

namespace MailKit.Net.Imap {
	enum ImapResponseCodeType : byte {
		Alert,
		BadCharset,
		Capability,
		NewName,
		Parse,
		PermanentFlags,
		ReadOnly,
		ReadWrite,
		TryCreate,
		UidNext,
		UidValidity,
		Unseen,

		// RESP-CODES introduced in rfc2086:
		MyRights,

		// RESP-CODES introduced in rfc2221:
		Referral,

		// RESP-CODES introduced in rfc3516,
		UnknownCte,

		// RESP-CODES introduced in rfc4315:
		AppendUid,
		CopyUid,
		UidNotSticky,

		// RESP-CODES introduced in rfc4467:
		UrlMech,

		// RESP-CODES introduced in rfc4469:
		BadUrl,
		TooBig,

		// RESP-CODES introduced in rfc4551:
		HighestModSeq,
		Modified,
		NoModSeq,

		// RESP-CODES introduced in rfc4978:
		CompressionActive,

		// RESP-CODES introduced in rfc5162:
		Closed,

		// RESP-CODES introduced in rfc5182:
		NotSaved,

		// RESP-CODES introduced in rfc5255:
		BadComparator,

		// RESP-CODES introduced in rfc5257:
		Annotate,
		Annotations,

		// RESP-CODES introduced in rfc5259:
		MaxConvertMessages,
		MaxConvertParts,
		TempFail,

		// RESP-CODES introduced in rfc5267:
		NoUpdate,

		// RESP-CODES introduced in rfc5464:
		Metadata,

		// RESP-CODES introduced in rfc5465:
		NotificationOverflow,
		BadEvent,

		// RESP-CODES introduced in rfc5466:
		UndefinedFilter,

		// RESP-CODES introduced in rfc5530:
		Unavailable,
		AuthenticationFailed,
		AuthorizationFailed,
		Expired,
		PrivacyRequired,
		ContactAdmin,
		NoPerm,
		InUse,
		ExpungeIssued,
		Corruption,
		ServerBug,
		ClientBug,
		CanNot,
		Limit,
		OverQuota,
		AlreadyExists,
		NonExistent,

		// RESP-CODES introduced in rfc6154:
		UseAttr,

		Unknown       = 255
	}

	class ImapResponseCode
	{
		public readonly ImapResponseCodeType Type;
		public readonly bool IsError;
		public string Message;

		internal ImapResponseCode (ImapResponseCodeType type, bool isError)
		{
			IsError = isError;
			Type = type;
		}

		public static ImapResponseCode Create (ImapResponseCodeType type)
		{
			switch (type) {
			case ImapResponseCodeType.Alert:                return new ImapResponseCode (type, false);
			case ImapResponseCodeType.BadCharset:           return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Capability:           return new ImapResponseCode (type, false);
			case ImapResponseCodeType.NewName:              return new NewNameResponseCode (type);
			case ImapResponseCodeType.Parse:                return new ImapResponseCode (type, true);
			case ImapResponseCodeType.PermanentFlags:       return new PermanentFlagsResponseCode (type);
			case ImapResponseCodeType.ReadOnly:             return new ImapResponseCode (type, false);
			case ImapResponseCodeType.ReadWrite:            return new ImapResponseCode (type, false);
			case ImapResponseCodeType.TryCreate:            return new ImapResponseCode (type, true);
			case ImapResponseCodeType.UidNext:              return new UidNextResponseCode (type);
			case ImapResponseCodeType.UidValidity:          return new UidValidityResponseCode (type);
			case ImapResponseCodeType.Unseen:               return new UnseenResponseCode (type);
			case ImapResponseCodeType.Referral:             return new ImapResponseCode (type, false);
			case ImapResponseCodeType.UnknownCte:           return new ImapResponseCode (type, true);
			case ImapResponseCodeType.AppendUid:            return new AppendUidResponseCode (type);
			case ImapResponseCodeType.CopyUid:              return new CopyUidResponseCode (type);
			case ImapResponseCodeType.UidNotSticky:         return new ImapResponseCode (type, false);
			case ImapResponseCodeType.UrlMech:              return new ImapResponseCode (type, false);
			case ImapResponseCodeType.BadUrl:               return new BadUrlResponseCode (type);
			case ImapResponseCodeType.TooBig:               return new ImapResponseCode (type, true);
			case ImapResponseCodeType.HighestModSeq:        return new HighestModSeqResponseCode (type);
			case ImapResponseCodeType.Modified:             return new ModifiedResponseCode (type);
			case ImapResponseCodeType.NoModSeq:             return new ImapResponseCode (type, false);
			case ImapResponseCodeType.CompressionActive:    return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Closed:               return new ImapResponseCode (type, false);
			case ImapResponseCodeType.NotSaved:             return new ImapResponseCode (type, true);
			case ImapResponseCodeType.BadComparator:        return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Annotate:             return new ImapResponseCode (type, false);
			case ImapResponseCodeType.Annotations:          return new ImapResponseCode (type, false);
			case ImapResponseCodeType.MaxConvertMessages:   return new MaxConvertResponseCode (type);
			case ImapResponseCodeType.MaxConvertParts:      return new MaxConvertResponseCode (type);
			case ImapResponseCodeType.TempFail:             return new ImapResponseCode (type, true);
			case ImapResponseCodeType.NoUpdate:             return new NoUpdateResponseCode (type);
			case ImapResponseCodeType.Metadata:             return new MetadataResponseCode (type);
			case ImapResponseCodeType.NotificationOverflow: return new ImapResponseCode (type, true);
			case ImapResponseCodeType.BadEvent:             return new ImapResponseCode (type, true);
			case ImapResponseCodeType.UndefinedFilter:      return new UndefinedFilterResponseCode (type);
			case ImapResponseCodeType.Unavailable:          return new ImapResponseCode (type, true);
			case ImapResponseCodeType.AuthenticationFailed: return new ImapResponseCode (type, true);
			case ImapResponseCodeType.AuthorizationFailed:  return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Expired:              return new ImapResponseCode (type, true);
			case ImapResponseCodeType.PrivacyRequired:      return new ImapResponseCode (type, true);
			case ImapResponseCodeType.ContactAdmin:         return new ImapResponseCode (type, true);
			case ImapResponseCodeType.NoPerm:               return new ImapResponseCode (type, true);
			case ImapResponseCodeType.InUse:                return new ImapResponseCode (type, true);
			case ImapResponseCodeType.ExpungeIssued:        return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Corruption:           return new ImapResponseCode (type, true);
			case ImapResponseCodeType.ServerBug:            return new ImapResponseCode (type, true);
			case ImapResponseCodeType.ClientBug:            return new ImapResponseCode (type, true);
			case ImapResponseCodeType.CanNot:               return new ImapResponseCode (type, true);
			case ImapResponseCodeType.Limit:                return new ImapResponseCode (type, true);
			case ImapResponseCodeType.OverQuota:            return new ImapResponseCode (type, true);
			case ImapResponseCodeType.AlreadyExists:        return new ImapResponseCode (type, true);
			case ImapResponseCodeType.NonExistent:          return new ImapResponseCode (type, true);
			case ImapResponseCodeType.UseAttr:              return new ImapResponseCode (type, true);
			default:                                        return new ImapResponseCode (type, true);
			}
		}
	}

	class NewNameResponseCode : ImapResponseCode
	{
		public string OldName;
		public string NewName;

		internal NewNameResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class PermanentFlagsResponseCode : ImapResponseCode
	{
		public MessageFlags Flags;

		internal PermanentFlagsResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class UidNextResponseCode : ImapResponseCode
	{
		public UniqueId Uid;

		internal UidNextResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class UidValidityResponseCode : ImapResponseCode
	{
		public uint UidValidity;

		internal UidValidityResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class UnseenResponseCode : ImapResponseCode
	{
		public int Index;

		internal UnseenResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class AppendUidResponseCode : UidValidityResponseCode
	{
		public UniqueIdSet UidSet;

		internal AppendUidResponseCode (ImapResponseCodeType type) : base (type)
		{
		}
	}

	class CopyUidResponseCode : UidValidityResponseCode
	{
		public UniqueIdSet SrcUidSet, DestUidSet;

		internal CopyUidResponseCode (ImapResponseCodeType type) : base (type)
		{
		}
	}

	class BadUrlResponseCode : ImapResponseCode
	{
		public string BadUrl;

		internal BadUrlResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}
	}

	class HighestModSeqResponseCode : ImapResponseCode
	{
		public ulong HighestModSeq;

		internal HighestModSeqResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class ModifiedResponseCode : ImapResponseCode
	{
		public UniqueIdSet UidSet;

		internal ModifiedResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}
	}

	class MaxConvertResponseCode : ImapResponseCode
	{
		public int MaxConvert;

		internal MaxConvertResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}
	}

	class NoUpdateResponseCode : ImapResponseCode
	{
		public string Tag;

		internal NoUpdateResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}
	}

	enum MetadataResponseCodeSubType
	{
		LongEntries,
		MaxSize,
		TooMany,
		NoPrivate
	}

	class MetadataResponseCode : ImapResponseCode
	{
		public MetadataResponseCodeSubType SubType;
		public uint Value;

		// FIXME: the LONGENTRIES code is not an error
		internal MetadataResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}
	}

	class UndefinedFilterResponseCode : ImapResponseCode
	{
		public string Name;

		internal UndefinedFilterResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}
	}
}
