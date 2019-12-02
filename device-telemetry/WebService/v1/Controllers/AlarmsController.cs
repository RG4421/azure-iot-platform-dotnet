﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Mmm.Platform.IoT.Common.Services.Diagnostics;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Common.Services.Filters;
using Mmm.Platform.IoT.DeviceTelemetry.Services;
using Mmm.Platform.IoT.DeviceTelemetry.Services.Models;
using Mmm.Platform.IoT.DeviceTelemetry.WebService.v1.Controllers.Helpers;
using Mmm.Platform.IoT.DeviceTelemetry.WebService.v1.Models;

namespace Mmm.Platform.IoT.DeviceTelemetry.WebService.v1.Controllers
{
    [TypeFilter(typeof(ExceptionsFilterAttribute))]
    public class AlarmsController : Controller
    {
        private const int DEVICE_LIMIT = 1000;
        private const int DELETE_LIMIT = 1000;

        private readonly IAlarms alarmService;
        private readonly ILogger log;

        public AlarmsController(
            IAlarms alarmService,
            ILogger logger)
        {
            this.alarmService = alarmService;
            this.log = logger;
        }

        [HttpGet(Version.PATH + "/[controller]")]
        [Authorize("ReadAll")]
        public async Task<AlarmListApiModel> ListAsync(
            [FromQuery] string from,
            [FromQuery] string to,
            [FromQuery] string order,
            [FromQuery] int? skip,
            [FromQuery] int? limit,
            [FromQuery] string devices)
        {
            string[] deviceIds = new string[0];
            if (!string.IsNullOrEmpty(devices))
            {
                deviceIds = devices.Split(',');
            }

            return await this.ListHelperAsync(from, to, order, skip, limit, deviceIds);
        }

        [HttpPost(Version.PATH + "/[controller]")]
        [Authorize("ReadAll")]
        public async Task<AlarmListApiModel> PostAsync([FromBody] QueryApiModel body)
        {
            string[] deviceIds = body.Devices == null
                ? new string[0]
                : body.Devices.ToArray();

            return await this.ListHelperAsync(
                body.From,
                body.To,
                body.Order,
                body.Skip,
                body.Limit,
                deviceIds);
        }

        [HttpGet(Version.PATH + "/[controller]/{id}")]
        [Authorize("ReadAll")]
        public async Task<AlarmApiModel> GetAsync([FromRoute] string id)
        {
            Alarm alarm = await this.alarmService.GetAsync(id);
            return new AlarmApiModel(alarm);
        }

        [HttpPatch(Version.PATH + "/[controller]/{id}")]
        [Authorize("UpdateAlarms")]
        public async Task<AlarmApiModel> PatchAsync(
            [FromRoute] string id,
            [FromBody] AlarmStatusApiModel body)
        {
            // validate input
            if (!(body.Status.Equals("open", StringComparison.OrdinalIgnoreCase) ||
                  body.Status.Equals("closed", StringComparison.OrdinalIgnoreCase) ||
                  body.Status.Equals("acknowledged", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidInputException(
                    "Status must be `closed`, `open`, or `acknowledged`." +
                    " Value provided:" + body.Status);
            }

            Alarm alarm = await this.alarmService.UpdateAsync(id, body.Status.ToLowerInvariant());
            return new AlarmApiModel(alarm);
        }

        [HttpDelete(Version.PATH + "/[controller]/{id}")]
        [Authorize("DeleteAlarms")]
        public async Task DeleteAsync([FromRoute] string id)
        {
            if (id == null)
            {
                throw new InvalidInputException("no id given to delete");
            }

            await this.alarmService.DeleteAsync(id);
        }

        [HttpPost(Version.PATH + "/[controller]!delete")]
        [Authorize("DeleteAlarms")]
        public void Delete([FromBody] AlarmIdListApiModel alarmList)
        {
            if (alarmList.Items == null || !alarmList.Items.Any())
            {
                throw new InvalidInputException("Must give list of at least 1 id to delete");
            }

            if (alarmList.Items.Count > DELETE_LIMIT)
            {
                throw new InvalidInputException("Cannot delete more than 1000 alarms");
            }

            this.alarmService.Delete(alarmList.Items);
        }

        private async Task<AlarmListApiModel> ListHelperAsync(
            string from,
            string to,
            string order,
            int? skip,
            int? limit,
            string[] deviceIds)
        {
            DateTimeOffset? fromDate = DateHelper.ParseDate(from);
            DateTimeOffset? toDate = DateHelper.ParseDate(to);

            if (order == null) order = "asc";
            if (skip == null) skip = 0;
            if (limit == null) limit = 1000;

            if (deviceIds.Length > DEVICE_LIMIT)
            {
                this.log.Warn("The client requested too many devices", () => new { deviceIds.Length });
                throw new BadRequestException("The number of devices cannot exceed " + DEVICE_LIMIT);
            }

            List<Alarm> alarmsList = await this.alarmService.ListAsync(
                fromDate,
                toDate,
                order,
                skip.Value,
                limit.Value,
                deviceIds);

            return new AlarmListApiModel(alarmsList);
        }
    }
}
