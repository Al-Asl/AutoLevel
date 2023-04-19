using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace AutoLevel
{

    public static class Profiling
    {
        private const string DEBUG = "AUTOLEVEL_DEBUG";

        private static readonly object timers_lock = new object();

        private static Dictionary<int, Stopwatch> timers
            = new Dictionary<int, Stopwatch>();

        [Conditional(DEBUG)]
        public static void StartTimer(string name, bool paused = false) => StartTimer(name.GetHashCode(), paused);
        [Conditional(DEBUG)]
        public static void StartTimer(int hash, bool paused = false)
        {
            var watch = Stopwatch.StartNew();
            if (paused) watch.Stop();
            lock(timers_lock)
            {
                timers[hash] = watch;
            }
        }

        [Conditional(DEBUG)]
        public static void PauseTimer(string name) => PauseTimer(name.GetHashCode());
        [Conditional(DEBUG)]
        public static void PauseTimer(int hash)
        {
            timers[hash].Stop();
        }

        [Conditional(DEBUG)]
        public static void ResumeTimer(string name) => ResumeTimer(name.GetHashCode());
        [Conditional(DEBUG)]
        public static void ResumeTimer(int hash)
        {
            timers[hash].Start();
        }

        [Conditional(DEBUG)]
        public static void LogTimer(string message, string name) => LogTimer(message, name.GetHashCode());
        [Conditional(DEBUG)]
        public static void LogTimer(string message, int hash)
        {
            Debug.Log($"{message} {timers[hash].ElapsedTicks / 10000f} ms");
        }

        [Conditional(DEBUG)]
        public static void RemoveTimer(string name) => RemoveTimer(name.GetHashCode());
        [Conditional(DEBUG)]
        public static void RemoveTimer(int hash)
        {
            lock (timers_lock)
            {
                timers.Remove(hash);
            }
        }

        [Conditional(DEBUG)]
        public static void LogAndRemoveTimer(string message, string name) => LogAndRemoveTimer(message, name.GetHashCode());
        [Conditional(DEBUG)]
        public static void LogAndRemoveTimer(string message, int hash)
        {
            LogTimer(message, hash);
            RemoveTimer(hash);
        }
    }

}