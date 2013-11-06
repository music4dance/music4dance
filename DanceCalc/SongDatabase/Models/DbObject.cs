using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
{
    public class DbObject
    {
        public DbObject()
        {
            InstanceId = Interlocked.Increment(ref _instanceCount);
        }

        [NotMapped]
        public int InstanceId { get; private set; }

        public virtual void Dump()
        {
            Debug.Write(string.Format("0X{0:X4}: ", InstanceId));
        }

        static public int InstanceCount
        {
            get { return _instanceCount; }
        }

        static int _instanceCount = 0;
    }
}
