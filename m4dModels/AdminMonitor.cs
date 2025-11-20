using System;
using System.Diagnostics;

namespace m4dModels
{
    public static class AdminMonitor
    {
        private static string _phase;
        private static int _iteration;

        private static string _lastTaskName;
        private static bool _lastTaskCompleted;
        private static string _lastTaskMessage;

        private static Stopwatch _stopwatch;

        private static readonly object Lock = new();

        public static bool IsRunning => Name != null;

        public static bool Succeeded => Name == null && _lastTaskCompleted;

        public static string Name { get; private set; }

        public static long Duration => _stopwatch.ElapsedMilliseconds;

        public static AdminStatus Status
        {
            get
            {
                lock (Lock)
                {
                    string message;

                    if (Name != null)
                    {
                        var e = _stopwatch.ElapsedMilliseconds;
                        message =
                            $"AdminMonitor: Task = {Name}; Phase = {_phase}, Iteration = {_iteration}, Duration = {e / 1000}.{e % 1000}";
                    }
                    else
                    {
                        message = _lastTaskName != null
                            ? string.Format(
                                _lastTaskCompleted
                                    ? "AdminMonitor: TaskCompleted - {0} \"{1}\""
                                    : "AdminMonitor: TaskFailed - {0} \"{1}\"", _lastTaskName,
                                _lastTaskMessage)
                            : "AdminMonitor: No task running or completed.";
                    }

                    return new AdminStatus
                    {
                        IsRunning = IsRunning,
                        Succeeded = Succeeded,
                        Status = message,
                        Exception = LastException
                    };
                }
            }
        }

        public static Exception LastException { get; private set; }

        public static bool StartTask(string name, string phase = null)
        {
            lock (Lock)
            {
                if (Name != null)
                {
                    return false;
                }

                _stopwatch = Stopwatch.StartNew();

                Name = name;
                _phase = phase;
                _iteration = 0;
                return true;
            }
        }

        public static void UpdateTask(string phase, int iteration = 0)
        {
            lock (Lock)
            {
                _phase = phase;
                _iteration = iteration;
            }
        }

        public static void CompleteTask(bool completed, string message, Exception exception = null)
        {
            lock (Lock)
            {
                _stopwatch?.Stop();

                _lastTaskCompleted = completed;
                _lastTaskName = Name;
                _lastTaskMessage = message;
                LastException = exception;

                Name = null;
                _phase = null;
                _iteration = 0;
            }
        }
    }

    public class AdminStatus
    {
        public bool IsRunning { get; set; }
        public bool Succeeded { get; set; }

        public string Status { get; set; }
        public Exception Exception { get; set; }

        public override string ToString()
        {
            return Status;
        }
    }
}
