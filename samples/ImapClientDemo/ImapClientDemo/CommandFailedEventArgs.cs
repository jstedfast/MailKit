using System;

namespace ImapClientDemo
{
	class CommandFailedEventArgs : EventArgs
	{
		public CommandFailedEventArgs (Exception ex)
		{
			Exception = ex;	
		}

		public Exception Exception {
			get; private set;
		}
	}
}
