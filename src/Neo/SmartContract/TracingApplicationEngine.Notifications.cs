// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngine.Notifications.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.SmartContract
{
    public partial class TracingApplicationEngine
    {
        private void RecordNotifications(int baseline)
        {
            var notificationSnapshot = Notifications;
            if (notificationSnapshot.Count <= baseline)
                return;

            for (int i = baseline; i < notificationSnapshot.Count; i++)
            {
                var notification = notificationSnapshot[i];
                string? stateJson = null;
                try
                {
                    stateJson = JsonSerializer.Serialize(notification.State)?.ToString();
                }
                catch
                {
                    stateJson = null;
                }

                _traceRecorder.RecordNotification(
                    notification.ScriptHash,
                    notification.EventName,
                    stateJson);
            }
        }
    }
}

