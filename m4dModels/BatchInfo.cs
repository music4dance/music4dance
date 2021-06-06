using System;

namespace m4dModels
{
    public class BatchInfo
    {
        public DateTime LastTime { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public bool Complete { get; set; }
        public string Message { get; set; }
    }
}