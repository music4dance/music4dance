using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace m4dModels
{
    public class IndexUpdater
    {
        public static void Enqueue(DanceMusicCoreService dms, SearchServiceInfo info)
        {
            // This turns "default" into a real id
            lock (s_updaters)
            {
                if (!s_updaters.TryGetValue(info.Id, out var updater))
                {
                    updater = new IndexUpdater(info.Id);
                    s_updaters[info.Id] = updater;
                }

                updater.Enqueue(dms);
            }
        }

        private IndexUpdater(string index)
        {
            _index = index;
        }

        private void Enqueue(DanceMusicCoreService dms)
        {
            Trace.WriteLine("Entering Enqueue");
            lock (_lock)
            {
                if (_task == null || _task.IsFaulted)
                {
                    Trace.WriteLine("Setting up task");
                    _task = Task.Delay(100 * 60).ContinueWith(_ => DoUpdate(dms));
                }
                else if (_task.Status == TaskStatus.Running)
                {
                    _continue = true;
                }
            }

            Trace.WriteLine("Exiting Enqueue");
        }

        private void DoUpdate(DanceMusicCoreService dms)
        {
            Trace.WriteLine("Entering DoUpdate");

            var count = dms.UpdateAzureIndex(_index);
            Trace.WriteLine($"Updated {count} songs.");

            // In the case where things have been enqueued 
            lock (_lock)
            {
                _task = null;
                if (_continue)
                {
                    _continue = false;
                    Enqueue(dms);
                }
                else
                {
                    dms.Dispose();
                }
            }

            Trace.WriteLine("Exiting DoUpdate");
        }

        private readonly string _index;
        private Task _task;
        private readonly object _lock = new object();
        private bool _continue;

        private static readonly Dictionary<string, IndexUpdater> s_updaters =
            new Dictionary<string, IndexUpdater>();
    }
}