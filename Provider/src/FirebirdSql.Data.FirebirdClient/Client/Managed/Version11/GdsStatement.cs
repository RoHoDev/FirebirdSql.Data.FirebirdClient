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
using System.Collections.Generic;
using System.Text;
using System.IO;

using FirebirdSql.Data.Common;

namespace FirebirdSql.Data.Client.Managed.Version11
{
	internal class GdsStatement : Version10.GdsStatement
	{
		#region Constructors

		public GdsStatement(IDatabase db)
			: base(db)
		{ }

		public GdsStatement(IDatabase db, TransactionBase transaction)
			: base(db, transaction)
		{ }

		#endregion

		#region Overriden Methods

		public override void Prepare(string commandText)
		{
			ClearAll();

			try
			{
				var numberOfResponses = 0;
				if (State == StatementState.Deallocated)
				{
					SendAllocateToBuffer();
					numberOfResponses++;
				}

				SendPrepareToBuffer(commandText);
				numberOfResponses++;

				SendInfoSqlToBuffer(StatementTypeInfoItems, IscCodes.STATEMENT_TYPE_BUFFER_SIZE);
				numberOfResponses++;

				_database.Xdr.Flush();

				try
				{
					GenericResponse allocateResponse = null;
					if (State == StatementState.Deallocated)
					{
						numberOfResponses--;
						allocateResponse = _database.ReadResponse<GenericResponse>();
					}

					numberOfResponses--;
					var prepareResponse = _database.ReadResponse<GenericResponse>();
					var deferredExecute = ((prepareResponse.ObjectHandle & IscCodes.STMT_DEFER_EXECUTE) == IscCodes.STMT_DEFER_EXECUTE);

					numberOfResponses--;
					var statementTypeResponse = _database.ReadResponse<GenericResponse>();

					if (allocateResponse != null)
					{
						ProcessAllocateResponse(allocateResponse);
					}
					ProcessPrepareResponse(prepareResponse);
					StatementType = ProcessStatementTypeInfoBuffer(ProcessInfoSqlResponse(statementTypeResponse));
				}
				finally
				{
					SafeFinishFetching(ref numberOfResponses);
				}

				State = StatementState.Prepared;
			}
			catch (IOException ex)
			{
				State = State == StatementState.Allocated ? StatementState.Error : State;
				throw IscException.ForErrorCode(IscCodes.isc_network_error, ex);
			}
		}

		public override void Execute()
		{
			if (State == StatementState.Deallocated)
			{
				throw new InvalidOperationException("Statement is not correctly created.");
			}

			Clear();

			try
			{
				RecordsAffected = -1;

				SendExecuteToBuffer();

				var readRowsAffectedResponse = false;
				if (DoRecordsAffected)
				{
					SendInfoSqlToBuffer(RowsAffectedInfoItems, IscCodes.ROWS_AFFECTED_BUFFER_SIZE);

					readRowsAffectedResponse = true;
				}

				_database.Xdr.Flush();

				var numberOfResponses = (StatementType == DbStatementType.StoredProcedure ? 1 : 0) + 1 + (readRowsAffectedResponse ? 1 : 0);
				try
				{
					SqlResponse sqlStoredProcedureResponse = null;
					if (StatementType == DbStatementType.StoredProcedure)
					{
						numberOfResponses--;
						sqlStoredProcedureResponse = _database.ReadResponse<SqlResponse>();
						ProcessStoredProcedureExecuteResponse(sqlStoredProcedureResponse);
					}

					numberOfResponses--;
					var executeResponse = _database.ReadResponse<GenericResponse>();

					GenericResponse rowsAffectedResponse = null;
					if (readRowsAffectedResponse)
					{
						numberOfResponses--;
						rowsAffectedResponse = _database.ReadResponse<GenericResponse>();
					}

					ProcessExecuteResponse(executeResponse);
					if (readRowsAffectedResponse)
						RecordsAffected = ProcessRecordsAffectedBuffer(ProcessInfoSqlResponse(rowsAffectedResponse));
				}
				finally
				{
					SafeFinishFetching(ref numberOfResponses);
				}

				State = StatementState.Executed;
			}
			catch (IOException ex)
			{
				State = StatementState.Error;
				throw IscException.ForErrorCode(IscCodes.isc_network_error, ex);
			}
		}

		#endregion

		#region Protected methods
		protected void SafeFinishFetching(ref int numberOfResponses)
		{
			while (numberOfResponses > 0)
			{
				numberOfResponses--;
				try
				{
					_database.ReadResponse();
				}
				catch (IscException)
				{ }
			}
		}

		protected override void Free(int option)
		{
			if (FreeNotNeeded(option))
				return;

			DoFreePacket(option);
			(Database as GdsDatabase).DeferredPackets.Enqueue(ProcessFreeResponse);
		}
		#endregion
	}
}
