﻿// ------------------------------------------------------------[-----------
//  <copyright file="ChangesHandler.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Raven.Abstractions.Logging;
using Raven.Server.Json;
using Raven.Server.Json.Parsing;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.Handlers
{
    public class ChangesHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/changes", "GET", "/databases/{databaseName:string}/changes")]
        public async Task GetChanges()
        {
            using (var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync())
            {
                //TODO: select small context size (maybe pool just for them?)
                MemoryOperationContext context;
                using (ContextPool.AllocateOperationContext(out context))
                {
                    try
                    {
                        await HandleConnection(webSocket, context);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException("Error encountered in changes handler", ex);

                        using (var ms = new MemoryStream())
                        {
                            using (var writer = new BlittableJsonTextWriter(context, ms))
                            {
                                context.Write(writer, new DynamicJsonValue
                                {
                                    ["Exception"] = ex,
                                });
                            }

                            ArraySegment<byte> bytes;
                            ms.TryGetBuffer(out bytes);
                            await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, Database.DatabaseShutdown);
                        }
                    }
                }
            }
        }

        private async Task HandleConnection(WebSocket webSocket, MemoryOperationContext context)
        {
            var debugTag = "changes/" + Database.Name;

            var connection = new NotificationsClientConnection(webSocket, Database);
            Database.Notifications.Connect(connection);
            var sendTask = connection.StartSendingNotifications();
            try
            {
                var buffer = context.GetManagedBuffer();
                var segments = new[]
                {
                    new ArraySegment<byte>(buffer, 0, buffer.Length/2),
                    new ArraySegment<byte>(buffer, buffer.Length/2, buffer.Length/2)
                };
                int index = 0;
                var receiveAsync = webSocket.ReceiveAsync(segments[index], Database.DatabaseShutdown);
                var jsonParserState = new JsonParserState();
                using (var parser = new UnmanagedJsonParser(context, jsonParserState, debugTag))
                {
                    var result = await receiveAsync;
                    parser.SetBuffer(new ArraySegment<byte>(segments[index].Array, segments[index].Offset, result.Count));
                    index++;
                    receiveAsync = webSocket.ReceiveAsync(segments[index], Database.DatabaseShutdown);

                    while (true)
                    {
                        using (var builder = new BlittableJsonDocumentBuilder(context, BlittableJsonDocumentBuilder.UsageMode.None, debugTag, parser, jsonParserState))
                        {
                            builder.ReadObject();

                            while (builder.Read() == false)
                            {
                                result = await receiveAsync;

                                parser.SetBuffer(new ArraySegment<byte>(segments[index].Array, segments[index].Offset, result.Count));
                                if (++index >= segments.Length)
                                    index = 0;
                                receiveAsync = webSocket.ReceiveAsync(segments[index], Database.DatabaseShutdown);
                            }

                            builder.FinalizeDocument();

                            using (var reader = builder.CreateReader())
                            {
                                string command, commandParameter;
                                if (reader.TryGet("Command", out command) == false)
                                    throw new ArgumentNullException(nameof(command), "Command argument is mandatory");

                                reader.TryGet("Param", out commandParameter);
                                connection.HandleCommand(command, commandParameter);

                                int commandId;
                                if (reader.TryGet("CommandId", out commandId))
                                {
                                    connection.Confirm(commandId);
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                /* Client was disconnected, write to log */
                Log.DebugException("Client was disconnected", ex);
            }
            finally
            {
                Database.Notifications.Disconnect(connection);
            }
            await sendTask;
        }
    }
}