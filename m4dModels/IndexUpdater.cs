using System.Diagnostics;
using System.Threading.Tasks;

namespace m4dModels
{
    public class IndexUpdater
    {
        public static void Enqueue()
        {
            Trace.WriteLine("Entering Enque");
            lock (s_lock)
            {
                if (s_task == null)
                {
                    Trace.WriteLine("Setting up task");
                    s_task = Task.Delay(1000*60).ContinueWith(_ => DoUpdate());
                }
                else if (s_task.Status == TaskStatus.Running)
                {
                    s_continue = true;
                }

            }
            Trace.WriteLine("Exiting Enque");
        }

        private static void DoUpdate()
        {
            Trace.WriteLine("Entering DoUpdate");
            using (var dms = DanceMusicService.GetService()) 
            {
                var count = dms.UpdateAzureIndex();
                Trace.WriteLine($"Updated {count} songs.");
            }

            // In the case where things have been enqueud 
            lock (s_lock)
            {
                s_task = null;
                if (s_continue)
                {
                    s_continue = false;
                    Enqueue();
                }
            }
            Trace.WriteLine("Exiting DoUpdate");
        }

        private static Task s_task;
        private static readonly object s_lock = new object();
        private static bool s_continue;
    }
}
