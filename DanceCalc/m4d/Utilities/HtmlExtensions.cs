using System.Web.Mvc;

namespace m4d.Utilities
{
    static public class HtmlExtensions
    {
        public static bool IsReleaseBuild(this HtmlHelper helper)
        {
#if DEBUG
            return false;
#else
            return true;
#endif
        }
    }
}