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
