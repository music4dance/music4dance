namespace m4dModels
{
    public class ErrorModel
    {
        public int HttpStatusCode { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
