using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace m4dModels
{
    public class IndexUpdater
    {
        public static void Enqueue(string index)
        {
            // This turns "default" into a real id
            index = SearchServiceInfo.GetInfo(index).Id;
            lock (s_updaters)
            {
                IndexUpdater updater;
                if (!s_updaters.TryGetValue(index, out updater))
                {
                    updater = new IndexUpdater(index);
                    s_updaters[index] = updater;
                }

                updater.Enqueue();
            }
        }

        private IndexUpdater(string index)
        {
            _index = index;
        }

        private void Enqueue()
        {
            Trace.WriteLine("Entering Enque");
            lock (_lock)
            {
                if (_task == null || _task.IsFaulted)
                {
                    Trace.WriteLine("Setting up task");
                    _task = Task.Delay(100*60).ContinueWith(_ => DoUpdate());
                }
                else if (_task.Status == TaskStatus.Running)
                {
                    _continue = true;
                }

            }
            Trace.WriteLine("Exiting Enque");
        }

        private void DoUpdate()
        {
            Trace.WriteLine("Entering DoUpdate");
            using (var dms = DanceMusicService.GetService())
            {
                var count = dms.UpdateAzureIndex(_index);
                Trace.WriteLine($"Updated {count} songs.");
            }

            // In the case where things have been enqueud 
            lock (_lock)
            {
                _task = null;
                if (_continue)
                {
                    _continue = false;
                    Enqueue();
                }
            }
            Trace.WriteLine("Exiting DoUpdate");
        }

        private readonly string _index;
        private Task _task;
        private readonly object _lock = new object();
        private bool _continue;

        private static readonly Dictionary<string,IndexUpdater> s_updaters = new Dictionary<string,IndexUpdater>();
    }
}
