using System;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Allocation-safe logging helper for optional WoW locomotion diagnostics.
    /// </summary>
    public static class WowLocomotionDebugLog
    {
        /// <summary>
        /// Logs a lazily-created message only when <paramref name="enabled"/> is true.
        /// </summary>
        /// <param name="enabled">Whether the diagnostic log should be emitted.</param>
        /// <param name="context">Optional Unity context object for inspector pinging.</param>
        /// <param name="messageFactory">Factory invoked only when logging is enabled.</param>
        public static void Log(bool enabled, UnityEngine.Object context, Func<string> messageFactory)
        {
            if (!enabled || messageFactory == null)
                return;

            Debug.Log(messageFactory(), context);
        }
    }
}
