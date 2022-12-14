using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ImapClientDemo
{
	public class CustomTaskScheduler : TaskScheduler
	{
		readonly ConcurrentQueue<Task> tasks = new ConcurrentQueue<Task> ();

		public CustomTaskScheduler () : this (SynchronizationContext.Current)
		{
		}

		public CustomTaskScheduler (SynchronizationContext context)
		{
			Context = context ?? throw new ArgumentNullException (nameof (context));
		}

		public SynchronizationContext Context { get; private set; }

		public override int MaximumConcurrencyLevel { get { return 1; } }

		void TryDequeueAndExecuteTask (object state)
		{
			if (tasks.TryDequeue (out var toExecute))
				TryExecuteTask (toExecute);
		}

		protected override void QueueTask (Task task)
		{
			// Add the task to the collection
			tasks.Enqueue (task);

			// Queue up a delegate that will dequeue and execute a task
			Context.Post (TryDequeueAndExecuteTask, null);
		}

		protected override bool TryExecuteTaskInline (Task task, bool taskWasPreviouslyQueued)
		{
			return SynchronizationContext.Current == Context && TryExecuteTask (task);
		}

		protected override IEnumerable<Task> GetScheduledTasks ()
		{
			return tasks.ToArray ();
		}
	}
}
