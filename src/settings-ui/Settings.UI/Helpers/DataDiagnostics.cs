﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Settings.Helpers
{
    public static class DataDiagnostics
    {
        private static readonly string DataDiagnosticsRegistryKey = @"HKEY_CURRENT_USER\Software\Classes\PowerToys\";
        private static readonly string DataDiagnosticsRegistryValueName = @"AllowDataDiagnostics";

        public static bool GetValue()
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

        public static void SetValue(bool value)
        {
            try
            {
                Registry.SetValue(DataDiagnosticsRegistryKey, DataDiagnosticsRegistryValueName, value ? 1 : 0);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set the Data Diagnostics value in the registry: {ex.Message}");
            }
        }
    }
}
