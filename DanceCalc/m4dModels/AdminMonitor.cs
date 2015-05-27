using System;

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

        public static void CompleteTask(bool completed, string message, Exception exception=null)
        {
            lock (Lock)
            {
                _lastTaskCompleted = completed;
                _lastTaskName = _name;
                _lastTaskMessage = message;
                _lastException = exception;

                _name = null;
                _phase = null;
                _iteration = 0;
            }
        }

        public static bool IsRunning { get { return _name != null; } }

        public static bool Succeeded { get { return _name == null && _lastTaskCompleted; } }

        public static AdminStatus Status
        {
            get
            {
                lock (Lock)
                {
                    string message = null;

                    if (_name != null)
                    {
                        message = string.Format("AdminMonitor: Task = {0}; Phase = {1}, Iteration = {2}", _name, _phase,
                            _iteration);
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
                        Exception = _lastException
                    };
                }
            }
        }

        private static string _name;
        private static string _phase;
        private static int _iteration;

        private static string _lastTaskName;
        private static bool _lastTaskCompleted;
        private static string _lastTaskMessage;
        private static Exception _lastException;

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
