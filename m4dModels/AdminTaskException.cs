using System;

namespace m4dModels
{
    public class AdminTaskException : Exception
    {
        public AdminTaskException()
        {
        }

        public AdminTaskException(string message) : base(message)
        {
        }

        public AdminTaskException(string message, Exception e)
            : base(message, e)
        {
        }
    }
}