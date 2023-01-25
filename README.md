# Grand Theft Bike V - Telemetry Service <!-- omit in toc -->

**Table of Contents**
- [Introduction](#introduction)
- [Backend](#backend)
  - [Data](#data)
  - [Queries](#queries)
- [Frontend](#frontend)
  - [API Key](#api-key)
  - [Azure Functions](#azure-functions)
- [Testing](#testing)
  - [Write data](#write-data)
  - [Read data](#read-data)


## Introduction
This repository holds the code for the telemetry service for collecting and analyzing telemetry data for GTBikeV mod. The collected information is anonymous and will be used to see what the real user base is and what kind of devices they use primarily. The user has the option `TelemetryData` to opt-in to send telemetry data. The option can be configured in the `GTBikeVConfig.ini` file. The default setting is **false** which does not send telemetry data. The implementation uses an Azure Cosmos DB for storing the data (backend) and Azure functions for providing REST API (frontend).


## Backend

### Data
Data is stored in Azure Cosmos DB database **gtbikev**. Data is collected in json document format in container **telemetry**. The container holds the following data definition:

| Attribute          | Data Type | Description                                                               |
| :----------------- | :-------- | :------------------------------------------------------------------------ |
| machineGuid        | string    | Machine guid                                                              |
| userId             | string    | MD5 hash of windows user name                                             |
| activityDistance   | float     | Activity distance in meters                                               |
| activityTime       | long      | Activity time in seconds                                                  |
| activityType       | string    | Activity type (SPORT_TYPE_CYCLING, SPORT_TYPE_RUNNING, SPORT_TYPE_ROWING) |
| deviceType         | string    | Device type                                                               |
| deviceId           | ushort    | Device id                                                                 |
| manufacturerId     | ushort    | Manufacturer id                                                           |
| modelNum           | ushort    | Model number                                                              |
| serialNum          | ushort    | Serial number                                                             |
| swVersion          | byte      | Software version                                                          |
| pcProcessorName    | string    | PC processor name                                                         |
| pcScreenResolution | string    | PC screen resolution (width x height)                                     |
| isoCountryCode     | string    | ISO 3-letter country code of windows regional settings                    |
| modVersion         | string    | Mod version                                                               |
| osVersion          | string    | OS version and build number                                               |
| utcTimestampClient | long      | UTC timestamp of record creation on client (format: yyyyMMddHHmmssfff)    |
| utcTimestampServer | long      | UTC timestamp of record creation on server (format: yyyyMMddHHmmssfff)    |


### Queries

The following queries can be used to perform telemetry reporting:

| Query Name        | SQL Query                                                                                                                                                                                                                         |
| :---------------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| User Base         | SELECT COUNT(UniqueUserBase) AS userBase FROM (SELECT t.userId FROM t GROUP BY t.userId) AS UniqueUserBase                                                                                                                        |
| Install Base      | SELECT COUNT(UniqueMachineBase) AS installBase FROM (SELECT t.machineGuid FROM t GROUP BY t.machineGuid) AS UniqueMachineBase                                                                                                     |
| Mod Version Count | SELECT ModVersionBase.modVersion, COUNT(ModVersionBase.modVersion) AS modVersionCount  FROM (SELECT t.machineGuid, t.modVersion FROM t GROUP BY t.machineGuid, t.modVersion) AS ModVersionBase GROUP BY ModVersionBase.modVersion |
| OS Version Count  | SELECT OsVersionBase.osVersion, COUNT(OsVersionBase.osVersion) AS osVersionCount  FROM (SELECT t.machineGuid, t.osVersion FROM t GROUP BY t.machineGuid, t.osVersion) AS OsVersionBase GROUP BY OsVersionBase.osVersion           |


## Frontend

### API Key
API key is used to prevent unauthorized access. API key (without brackets) is passed in URL `https://gtbikev.azurewebsites.net/api/{Azure Function}?code=<api-key>`.


### Azure Functions
The following Azure Functions are available:

| Azure Function                    | HTTP Action | Authorization | Description                                                                                                                                       |
| :-------------------------------- | :---------- | :------------ | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GetTelemetryData/<machine-guid>` | GET         | Anonymous     | Get data from <machine-guid> ordered by timestamp in descending order.                                                                            |
| `LogTelemetryData`                | POST        | API_KEY       | Post data. Attribute **machineGuid** must match regex pattern ` @"^[a-zA-z0-9]{8}-[a-zA-z0-9]{4}-[a-zA-z0-9]{4}-[a-zA-z0-9]{4}-[a-zA-z0-9]{12}$`. |

`<machine-guid>` is taken from Windows registry `Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography`.


## Testing

This section provides test cases for writing and reading data.

### Write data

```
// POST: 201 (Created)
curl -X POST https://gtbikev.azurewebsites.net/api/LogTelemetryData?code=<api-key>
     -H 'Content-Type: application/json'
     -d '{"machineGuid":"<machine-guid>","userId":"8bd50dc3451642df384febc3dbf54b53","activityDistance":30000,"activityTime":3600,"activityType":"SPORT_TYPE_CYCLING","devices":[{"deviceType": "FEC","deviceId": 28104,"manufacturerId": 32,"modelNum": 40,"serialNum": 21139,"swVersion": 3}],"pcProcessorName":"Intel(R) Core(TM) i7-10510U CPU @ 1.80GHz","pcScreenResolution":"1920x1080","isoCountryCode":"CHE","modVersion":"0.4.0.4","osVersion":"10.0.19041","utcTimestampClient":19700101000000000}'
```

```
// POST: 400 (Bad Request - Invalid data) -> <machine-guid> is null
curl -X POST https://gtbikev.azurewebsites.net/api/LogTelemetryData?code=<api-key>
     -H 'Content-Type: application/json'
     -d '{"userId":"8bd50dc3451642df384febc3dbf54b53","activityDistance":"30000","activityTime":"3600","activityType":"SPORT_TYPE_RUNNING","isoCountryCode":"CHE","pcProcessorName":"Intel(R) Core(TM) i7-10510U CPU @ 1.80GHz","pcScreenResolution":"1920x1080","isoCountryCode":"CHE","modVersion":"0.4.0.4","osVersion":"10.19041","utcTimestampClient":20201223140000500}'
```

```
// POST 400 (Bad Request - Invalid data) -> <machine-guid> is empty ("") 
curl -X POST https://gtbikev.azurewebsites.net/api/LogTelemetryData?code=<api-key>
     -H 'Content-Type: application/json'
     -d '{"machineGuid":"","userId":"8bd50dc3451642df384febc3dbf54b53","activityDistance":"30000","activityTime":"3600","activityType":"SPORT_TYPE_RUNNING","pcProcessorName":"Intel(R) Core(TM) i7-10510U CPU @ 1.80GHz","pcScreenResolution":"1920x1080","isoCountryCode":"CHE","modVersion":"0.4.0.4","osVersion":"10.19041""utcTimestampClient":20201223140000500}'
```

```
// POST 401 (Unautorized) -> <api-key> not specified
curl -X POST https://gtbikev.azurewebsites.net/api/LogTelemetryData
     -H 'Content-Type: application/json'
     -d '{"machineGuid":"<machine-guid>","userId":"8bd50dc3451642df384febc3dbf54b53","activityDistance":5000,"activityTime":2800,"activityType":"SPORT_TYPE_RUNNING","pcProcessorName":"Intel(R) Core(TM) i7-10510U CPU @ 1.80GHz","pcScreenResolution":"1920x1080","isoCountryCode":"CHE","modVersion":"0.4.0.4","osVersion":"10.19041""utcTimestampClient":20201223140000500}'
```

### Read data

```
// GET 200 (OK)
curl -X GET https://gtbikev.azurewebsites.net/api/GetTelemetryData/<machine-guid>
```