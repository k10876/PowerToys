﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry.Events;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Telemetry
{
    /// <summary>
    /// Telemetry helper class for PowerToys.
    /// </summary>
    public class PowerToysTelemetry : TelemetryBase
    {
        private static readonly string DataDiagnosticsRegistryKey = @"HKEY_CURRENT_USER\Software\Classes\PowerToys\";
        private static readonly string DataDiagnosticsRegistryValueName = @"AllowDataDiagnostics";

        /// <summary>
        /// Name for ETW event.
        /// </summary>
        private const string EventSourceName = "Microsoft.PowerToys";

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerToysTelemetry"/> class.
        /// </summary>
        public PowerToysTelemetry()
            : base(EventSourceName)
        {
        }

        /// <summary>
        /// Gets an instance of the <see cref="PowerLauncherTelemetry"/> class.
        /// </summary>
        public static PowerToysTelemetry Log { get; } = new PowerToysTelemetry();

        private static bool IsDataDiagnosticsEnabled()
        {
            object registryValue = null;
            try
            {
                registryValue = Registry.GetValue(DataDiagnosticsRegistryKey, DataDiagnosticsRegistryValueName, false);
            }
            catch
            {
            }

            if (registryValue is not null)
            {
                return (int)registryValue == 1 ? true : false;
            }

            return false;
        }

        /// <summary>
        /// Publishes ETW event when an action is triggered on
        /// </summary>
        public void WriteEvent<T>(T telemetryEvent)
            where T : EventBase, IEvent
        {
            if (IsDataDiagnosticsEnabled())
            {
                this.Write<T>(
                    telemetryEvent.EventName,
                    new EventSourceOptions()
                    {
                        Keywords = ProjectKeywordMeasure,
                        Tags = ProjectTelemetryTagProductAndServicePerformance,
                    },
                    telemetryEvent);
            }
        }
    }
}
