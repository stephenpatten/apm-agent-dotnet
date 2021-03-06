﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Elastic.Apm.DiagnosticSource;
using Elastic.Apm.Model.Payload;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elastic.Apm.EntityFrameworkCore
{
    internal class EfCoreDiagnosticListener : IDiagnosticListener
    {
        public string Name => "Microsoft.EntityFrameworkCore";

        private readonly ConcurrentDictionary<Guid, Span> _spans = new ConcurrentDictionary<Guid, Span>();

        public void OnCompleted() {}

        public void OnError(Exception error) {}

        public void OnNext(KeyValuePair<string, object> kv)
        {
            switch (kv.Key)
            {
                case string k when k == RelationalEventId.CommandExecuting.Name:
                    if (kv.Value is CommandEventData commandEventData)
                    {
                        var newSpan = new Span();

                        var transactionStartTime = TransactionContainer.Transactions.Value[0].TimestampInDateTime;  
                        var utcNow = DateTime.UtcNow;
                        newSpan.Start = (decimal)(utcNow - transactionStartTime).TotalMilliseconds;
                        _spans.TryAdd(commandEventData.CommandId, newSpan);
                    }
                    break;
                case string k when k == RelationalEventId.CommandExecuted.Name:
                    if (kv.Value is CommandExecutedEventData commandExecutedEventData)
                    {
                        if (_spans.TryRemove(commandExecutedEventData.CommandId, out Span span))
                        {
                            span.Context = new Span.ContextC
                            {
                                Db = new Db
                                {
                                    Statement = commandExecutedEventData.Command.CommandText,
                                    Instance = commandExecutedEventData.Command.Connection.Database,
                                    Type = "sql"
                                }
                            };
                            span.Duration = commandExecutedEventData.Duration.TotalMilliseconds;
                            span.Name = commandExecutedEventData.Command.CommandText;
                            span.Type = "Db";

                            TransactionContainer.Transactions.Value[0].Spans.Add(span);
                        }
                    }
                    break;
            }
        }
    }
}
