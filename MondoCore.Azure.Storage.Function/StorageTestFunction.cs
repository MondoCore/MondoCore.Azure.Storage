
using System.Collections.Generic;
using System.Text.Json;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Azure.Messaging.ServiceBus;

using MondoCore.Common;
using MondoCore.Log;

namespace MondoCore.Azure.Storage.Function;

public class StorageTestFunction(ILog log, IStorageTestService service)
{
    [Function("StorageTest")]
    [ServiceBusOutput("handleperson", Connection = "ServiceBusConnectionString")]
    public async Task<IEnumerable<Person>> Run
    (
        [ServiceBusTrigger("storagetest", Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  messageActions
    )
    {
        var requestLog = log.NewRequest("StorageTest", message.CorrelationId, new { FunctionName = "StorageTest" });
        IEnumerable<Person>? rtnValue;

        try
        { 
            await requestLog.WriteEvent("Begin Function");

            rtnValue = await service.Run();

            await requestLog.WriteEvent("End Function");
        }
        catch (Exception ex) 
        {
            await requestLog.WriteError(ex);

            rtnValue = Array.Empty<Person>();
        }

        // Complete the message
        await messageActions.CompleteMessageAsync(message);

        return rtnValue;
    }

    [Function("HandlePerson")]
    public async Task HandlePerson
    (
        [ServiceBusTrigger("handleperson", Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  messageActions
    )
    {
        var requestLog = log.NewRequest("HandlePerson", message.CorrelationId, new { FunctionName = "HandlePerson" });

        try
        { 
            await requestLog.WriteEvent("Begin Function");

            var person = JsonSerializer.Deserialize<Person>(message.Body.ToString())!;

            await service.HandlePerson(person);

            await requestLog.WriteEvent("End Function", person);
        }
        catch (Exception ex) 
        {
            await requestLog.WriteError(ex);
        }

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
    }
}