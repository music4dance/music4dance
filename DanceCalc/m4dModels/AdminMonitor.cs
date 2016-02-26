using System;
using System.Runtime.Serialization;

namespace m4dModels
{
    public static class AdminMonitor
    {
        public static bool StartTask(string name, string phase = null)
        {
            lock (Lock)
            {
                if (_name != null) return false;

                _name = name;
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

        public static void TryUpdateTask(string phase, int iteration = 0)
        {
            lock (Lock)
            {
                if (_name == null) return;

                _phase = phase;
                _iteration = iteration;
            }
        }
        public static void CompleteTask(bool completed, string message, Exception exception=null)
        {
            lock (Lock)
            {
                _lastTaskCompleted = completed;
                _lastTaskName = _name;
                _lastTaskMessage = message;
                LastException = exception;

                _name = null;
                _phase = null;
                _iteration = 0;
            }
        }

        public static bool IsRunning => _name != null;

        public static bool Succeeded => _name == null && _lastTaskCompleted;

        public static AdminStatus Status
        {
            get
            {
                lock (Lock)
                {
                    string message;

                    if (_name != null)
                    {
                        message = $"AdminMonitor: Task = {_name}; Phase = {_phase}, Iteration = {_iteration}";
                    }
                    else
                    {
                        message = _lastTaskName != null ?
                            string.Format(_lastTaskCompleted ? "AdminMonitor: TaskCompleted - {0} \"{1}\"" :
                                "AdminMonitor: TaskFailed - {0} \"{1}\"", _lastTaskName, _lastTaskMessage) :
                                "AdminMonitor: No task running or completed.";                        
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

        private static string _name;
        private static string _phase;
        private static int _iteration;

        private static string _lastTaskName;
        private static bool _lastTaskCompleted;
        private static string _lastTaskMessage;

        private static readonly object Lock = new object();
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
