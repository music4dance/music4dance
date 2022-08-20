namespace m4d.Utilities
{
    // TODO: This only works for single application instances, eventually
    //  move this to some kin of data store/azure supported settings manager
    public static class GlobalState
    {
        public static string UpdateMessage { get; set; }
    }
}
