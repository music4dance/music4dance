using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace m4dModels
{
    public class DbObject
    {
        public DbObject()
        {
#if _NEVER
            InstanceId = Interlocked.Increment(ref _instanceCount);
#endif
        }

#if _NEVER
        [NotMapped]
        public int InstanceId { get; private set; }
#endif
        public virtual void Dump()
        {
#if _NEVER
            Trace.Write(string.Format("0X{0:X4}: ", InstanceId));
#endif
        }

#if _NEVER
        static public int InstanceCount
        {
            get { return _instanceCount; }
        }

        static int _instanceCount = 0;
#endif
    }
}
