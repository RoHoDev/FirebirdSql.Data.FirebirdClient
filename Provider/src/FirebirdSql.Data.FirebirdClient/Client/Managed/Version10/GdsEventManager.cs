﻿/*
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

//$Authors = Carlos Guzman Alvarez, Jiri Cincura (jiri@cincura.net)

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using FirebirdSql.Data.Common;

namespace FirebirdSql.Data.Client.Managed.Version10
{
	internal class GdsEventManager : IDisposable
	{
		bool _disposing;
		int _handle;
		string _ipAddress;
		int _portNumber;
		GdsDatabase _database;

		public GdsEventManager(int handle, string ipAddress, int portNumber)
		{
			_disposing = false;
			_handle = handle;
			_ipAddress = ipAddress;
			_portNumber = portNumber;
		}

		public async Task Open(AsyncWrappingCommonArgs async)
		{
			var connection = new GdsConnection(_ipAddress, _portNumber);
			await connection.Connect(async).ConfigureAwait(false);
			_database = new GdsDatabase(connection);
		}

		public async Task WaitForEventsAsync(RemoteEvent remoteEvent)
		{
#warning ASYNC
			while (true)
			{
				try
				{
					var operation = await _database.ReadOperationAsync().ConfigureAwait(false);

					switch (operation)
					{
						case IscCodes.op_event:
							var dbHandle = _database.Xdr.ReadInt32();
							var buffer = _database.Xdr.ReadBuffer();
							var ast = new byte[8];
							_database.Xdr.ReadBytes(ast, 8);
							var eventId = _database.Xdr.ReadInt32();

							await remoteEvent.EventCountsAsync(buffer).ConfigureAwait(false);

							break;

						default:
							Debug.Assert(false);
							break;
					}
				}
				catch (Exception) when (_disposing)
				{
					return;
				}
				catch (Exception ex)
				{
					remoteEvent.EventError(ex);
					break;
				}
			}
		}

		public void Dispose()
		{
			_disposing = true;
			_database.CloseConnection();
		}
	}
}
