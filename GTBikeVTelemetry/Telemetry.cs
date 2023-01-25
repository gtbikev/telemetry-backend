using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Telemetry
{
    public class TelemetryData
    {
        public string machineGuid { get; set; }
        public string userId { get; set; }
        public float activityDistance { get; set; }
        public long activityTime { get; set; }
        public string activityType { get; set; }
        public List<TelemetryDeviceData> devices { get; set; }
        public string pcProcessorName { get; set; }
        public string pcScreenResolution { get; set; }
        public string isoCountryCode { get; set; }
        public string modVersion { get; set; }
        public string osVersion { get; set; }
        public long utcTimestampClient { get; set; }
        public long utcTimestampServer { get; set; }
    }

    public class TelemetryDeviceData
    {
        public string deviceType;
        public ushort deviceId;
        public ushort manufacturerId;
        public ushort modelNum;
        public ushort serialNum;
        public byte swVersion;
    }

    public static class LogTelemetryData
    {
        [FunctionName(nameof(LogTelemetryData))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "LogTelemetryData")] HttpRequest req,
            [CosmosDB(
                databaseName: "gtbikev",
                collectionName: "telemetry",
                ConnectionStringSetting = "gtbikev")] IAsyncCollector<object> telemetry,
            ILogger log)
        {
            try
            {
                // read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                // extract request body
                var input = JsonConvert.DeserializeObject<TelemetryData>(requestBody);

                // assign input to data class
                var telemetryData = new TelemetryData
                {
                    machineGuid = input.machineGuid,
                    userId = input.userId,
                    activityDistance = input.activityDistance,
                    activityTime = input.activityTime,
                    activityType = input.activityType,
                    devices = input.devices,
                    pcProcessorName = input.pcProcessorName,
                    pcScreenResolution = input.pcScreenResolution,
                    isoCountryCode = input.isoCountryCode,
                    modVersion = input.modVersion,
                    osVersion = input.osVersion,
                    utcTimestampClient = input.utcTimestampClient,
                    utcTimestampServer = System.Convert.ToInt64(DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"))
                };

                // verify machineGuid
                if (!VerifyRegexPattern(input.machineGuid, @"^[a-zA-z0-9]{8}-[a-zA-z0-9]{4}-[a-zA-z0-9]{4}-[a-zA-z0-9]{4}-[a-zA-z0-9]{12}$"))
                    {
                    // log and return error message
                    string errorMessageMachineGuid = "Invalid data";
                    log.LogError(errorMessageMachineGuid);
                    return new BadRequestObjectResult(errorMessageMachineGuid);
                }

                // add record to cosmos db
                await telemetry.AddAsync(telemetryData);

                // log message
                log.LogError("Item inserted.");

                // return status
                return new StatusCodeResult(StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                log.LogError($"Couldn't insert item. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public static bool VerifyRegexPattern(string input, string pattern)
        {
            string regexMatchMachineGuidPattern = pattern;
            if (input != null)
            {
                return Regex.IsMatch(input, regexMatchMachineGuidPattern);
            }
            else
            {
                return false;
            }
        }
    }

    public static class GetTelemetryData
    {
        [FunctionName(nameof(GetTelemetryData))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetTelemetryData/{machineGuid}")] HttpRequest req,
            [CosmosDB(
                databaseName: "gtbikev",
                collectionName: "telemetry",
                ConnectionStringSetting = "gtbikev",
                SqlQuery ="SELECT * FROM c WHERE c.machineGuid = {machineGuid} ORDER BY c.utcTimestampServer DESC")] IEnumerable<TelemetryData> telemetry,
            ILogger log,
            string machineGuid)
        {
            // verify output
            if (telemetry == null)
            {
                // return status not found 
                return (ActionResult)new NotFoundResult();
            }
            // return status ok
            return (ActionResult)new OkObjectResult(telemetry);
        }
    }

}