using System;

namespace m4d.Utilities
{
    [Serializable]
    public class RedirectException : Exception
    {
        public RedirectException()
        {
        }

        public RedirectException(string view, object model)
            : base($"Redirecting to view {view}")
        {
            View = view;
            Model = model;
        }

        public string View { get; }
        public object Model { get; }
    }
}
