﻿using System;
using System.Threading.Tasks;
using AsyncFriendlyStackTrace;
using Common.Log;
using JetBrains.Annotations;
using Lykke.MonitoringServiceApiCaller.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;

namespace Lykke.MonitoringServiceApiCaller
{
    /// <summary>
    /// Class for auto-registration in monitoring service
    /// </summary>
    [PublicAPI]
    public static class AutoRegistrationInMonitoring
    {
        private const string _myMonitoringUrlEnvVarName = "MyMonitoringUrl";
        private const string _missingEnvVarUrl = "0.0.0.0";
        private const string _myMonitoringNameEnvVarName = "MyMonitoringName";
        private const string _disableAutoRegistrationEnvVarName = "DisableAutoRegistrationInMonitoring";
        private const string _podNameEnvVarName = "ENV_INFO";

        /// <summary>
        /// Registers calling application in monitoring service based on application url from environemnt variable.
        /// </summary>
        /// <param name="configuration">Application configuration that is used for environemnt variable search.</param>
        /// <param name="monitoringServiceUrl">Monitoring service url.</param>
        /// <param name="log">ILog implementation. LogToConsole is used on case this parmeter is null.</param>
        public static async Task RegisterAsync(
            IConfigurationRoot configuration,
            string monitoringServiceUrl,
            ILog log)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (log == null)
                throw new ArgumentNullException(nameof(log));

            string disableAutoRegistrationStr = configuration[_disableAutoRegistrationEnvVarName];
            if (bool.TryParse(disableAutoRegistrationStr, out bool disableAutoRegistration) && disableAutoRegistration)
            {
                log.WriteMonitor("Auto-registration in monitoring", "", $"Auto-registration is disabled");
                return;
            }

            if (string.IsNullOrWhiteSpace(monitoringServiceUrl))
                throw new ArgumentNullException(nameof(monitoringServiceUrl));

            string podTag = configuration[_podNameEnvVarName] ?? "";

            try
            {
                string myMonitoringUrl = configuration[_myMonitoringUrlEnvVarName];
                if (string.IsNullOrWhiteSpace(myMonitoringUrl))
                {
                    myMonitoringUrl = _missingEnvVarUrl;
                    log.WriteMonitor(
                        "Auto-registration in monitoring",
                        podTag,
                        $"{_myMonitoringUrlEnvVarName} environment variable is not found. Using {myMonitoringUrl} for monitoring registration");
                }
                string myMonitoringName = configuration[_myMonitoringNameEnvVarName];
                if (string.IsNullOrWhiteSpace(myMonitoringName))
                    myMonitoringName = PlatformServices.Default.Application.ApplicationName;
                var monitoringService = new MonitoringServiceFacade(monitoringServiceUrl);

                try
                {
                    var monitoringRegistration = await monitoringService.GetService(myMonitoringName);
                    if (monitoringRegistration?.Url?.ToLower() == myMonitoringUrl.ToLower())
                    {
                        log.WriteMonitor("Auto-registration in monitoring", podTag, $"Service is already registered in monitoring with such url. Skipping.");
                        return;
                    }

                    if (monitoringRegistration.Url != _missingEnvVarUrl)
                    {
                        log.WriteMonitor("Auto-registration in monitoring", podTag, $"There is a registration for {myMonitoringName} in monitoring service!");

                        myMonitoringUrl = _missingEnvVarUrl;
                        string instanceTag = string.IsNullOrEmpty(podTag) ? Guid.NewGuid().ToString() : podTag;
                        myMonitoringName = $"{myMonitoringName}-{instanceTag}";
                    }
                }
                catch
                {
                    //Duplicated registration is not found - proceed with usual registration
                }

                await monitoringService.MonitorUrl(
                    new UrlMonitoringObjectModel
                    {
                        Url = myMonitoringUrl,
                        ServiceName = myMonitoringName,
                    });
                log.WriteMonitor("Auto-registration in monitoring", podTag, $"Auto-registered in Monitoring with name {myMonitoringName} on {myMonitoringUrl}");
            }
            catch (Exception ex)
            {
                log.WriteMonitor("Auto-registration in monitoring", podTag, ex.ToAsyncString());
            }
        }
    }
}
