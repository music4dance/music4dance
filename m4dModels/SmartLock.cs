using System;
using System.Diagnostics;
using System.Threading;

namespace m4dModels
{
    public class SmartLock
    {
        private readonly object _lockObject = new object();
        private string _holdingTrace = "";

        private static readonly int WARN_TIMEOUT_MS = 5000; //5 secs

        public void Lock(Action action)
        {
            try
            {
                Enter();
                action.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"SmartLock Lock action: {ex.Message}");
            }
            finally
            {
                Exit();
            }
        }

        public TResult Lock<TResult>(Func<TResult> action)
        {

            try
            {
                Enter();
                return action.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"SmartLock Lock action: {ex.Message}");
                return default(TResult);
            }
            finally
            {
                Exit();
            }
        }

        private void Enter()
        {
            try
            {
                var locked = false;
                var timeoutMs = 0;
                while (!locked)
                {
                    //keep trying to get the lock, and warn if not accessible after timeout
                    locked = Monitor.TryEnter(_lockObject, WARN_TIMEOUT_MS);
                    if (!locked)
                    {
                        timeoutMs += WARN_TIMEOUT_MS;
                        Trace.WriteLineIf(TraceLevels.General.TraceError,"Lock held: " + (timeoutMs / 1000) + " secs by " + _holdingTrace + " requested by " + GetStackTrace());
                    }
                }

                //save a stack trace for the code that is holding the lock
                _holdingTrace = GetStackTrace();
            }
            catch (Exception ex)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"SmartLock Enter: {ex.Message}");
            }
        }

        private string GetStackTrace()
        {
            var trace = new StackTrace();
            var threadId = Thread.CurrentThread.Name ?? Thread.CurrentThread.ManagedThreadId.ToString();
            return "[" + threadId + "]" + trace.ToString().Replace('\n', '|').Replace("\r", "");
        }

        private void Exit()
        {
            try
            {
                Monitor.Exit(_lockObject);
                _holdingTrace = "";
            }
            catch (Exception ex)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"SmartLock Exit: {ex.Message}");
            }
        }
    }
}
