namespace m4d.Utilities;

[Serializable]
public class AbortBatchException : Exception
{
    public AbortBatchException()
    {
    }

    public AbortBatchException(string message) : base(message)
    {
    }

    public AbortBatchException(string message, Exception inner) : base(message, inner)
    {
    }
}

// Apple Music kept answering 429 past the request's retry budget. Still stops a batch.
[Serializable]
public class AppleMusicThrottledException(string message) : AbortBatchException(message);
