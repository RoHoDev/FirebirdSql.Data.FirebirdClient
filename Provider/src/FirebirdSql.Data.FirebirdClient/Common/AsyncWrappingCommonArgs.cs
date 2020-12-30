/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/blob/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

//$Authors = Jiri Cincura (jiri@cincura.net)

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FirebirdSql.Data.Common
{
	internal readonly struct AsyncWrappingCommonArgs
	{
		public bool IsAsync { get; }
		public CancellationToken CancellationToken { get; }

		public AsyncWrappingCommonArgs(bool isAsync, CancellationToken cancellationToken)
		{
			IsAsync = isAsync;
			CancellationToken = cancellationToken;
		}

		public Task<TResult> AsyncSyncCall<TResult>(Func<CancellationToken, Task<TResult>> asyncCall, Func<TResult> syncCall)
		{
			return IsAsync ? asyncCall(CancellationToken) : Task.FromResult(syncCall());
		}
		public Task<TResult> AsyncSyncCall<TResult>(Func<Task<TResult>> asyncCall, Func<TResult> syncCall)
		{
			return IsAsync ? asyncCall() : Task.FromResult(syncCall());
		}
		public Task<TResult> AsyncSyncCall<T1, TResult>(Func<T1, CancellationToken, Task<TResult>> asyncCall, Func<T1, TResult> syncCall, T1 arg1)
		{
			return IsAsync ? asyncCall(arg1, CancellationToken) : Task.FromResult(syncCall(arg1));
		}
		public Task<TResult> AsyncSyncCall<T1, TResult>(Func<T1, Task<TResult>> asyncCall, Func<T1, TResult> syncCall, T1 arg1)
		{
			return IsAsync ? asyncCall(arg1) : Task.FromResult(syncCall(arg1));
		}

		public Task AsyncSyncCall(Func<CancellationToken, Task> asyncCall, Action syncCall)
		{
			return IsAsync ? asyncCall(CancellationToken) : SyncTaskCompleted(syncCall);
		}
		public Task AsyncSyncCall(Func<Task> asyncCall, Action syncCall)
		{
			return IsAsync ? asyncCall() : SyncTaskCompleted(syncCall);
		}
		public Task AsyncSyncCall<T1>(Func<T1, CancellationToken, Task> asyncCall, Action<T1> syncCall, T1 arg1)
		{
			return IsAsync ? asyncCall(arg1, CancellationToken) : SyncTaskCompleted(syncCall, arg1);
		}
		public Task AsyncSyncCall<T1>(Func<T1, Task> asyncCall, Action<T1> syncCall, T1 arg1)
		{
			return IsAsync ? asyncCall(arg1) : SyncTaskCompleted(syncCall, arg1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static Task SyncTaskCompleted(Action sync)
		{
			sync();
			return Task.CompletedTask;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static Task SyncTaskCompleted<T1>(Action<T1> sync, T1 arg1)
		{
			sync(arg1);
			return Task.CompletedTask;
		}
	}
}
