using System.Web.Optimization;
using BundleTransformer.Core.Orderers;
using BundleTransformer.Core.Transformers;

namespace m4d
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.UseCdn = true;   //enable CDN support

            bundles.Add(AjaxBundle("jquery",
                "jQuery/jquery-2.1.4.min.js", "window.jQuery",
                "jquery-{version}.js"));

            bundles.Add(AjaxBundle("jqueryval",
                "jquery.validate/1.14.0/jquery.validate.min.js", "window.jQuery().validate",
                "jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(AjaxBundle("modernizr",
                "modernizr/modernizr-2.8.3.js", "window.Modernizr",
                "modernizr-*"));

            bundles.Add(AjaxBundle("bootstrap",
                "bootstrap/3.3.5/bootstrap.min.js", "$.fn.modal",
                "bootstrap.js","respond.js"));

            bundles.Add(AjaxBundle("knockout",
                "knockout/knockout-3.3.0.js", "window.ko",
                "knockout-3.3.0.js"));

            bundles.Add(new ScriptBundle("~/bundles/knockout-mapping").Include(
                "~/Scripts/knockout.mapping-latest.js"));

            bundles.Add(new ScriptBundle("~/bundles/tools-script").Include(
                "~/Scripts/chosen.jquery.js",
                "~/Scripts/jquery.nouislider.full.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/markdowndeep").Include(
                "~/Scripts/MarkdownDeepLib.min.js"));

            bundles.Add(PageBundle("catalog"));
            bundles.Add(PageBundle("tempi"));
            bundles.Add(PageBundle("musicservice"));
            bundles.Add(PageBundle("showdance"));
            bundles.Add(PageBundle("editdance"));
            bundles.Add(PageBundle("counter"));
            bundles.Add(PageBundle("undo"));
            bundles.Add(PageBundle("tagchooser"));
            bundles.Add(PageBundle("edit"));
            bundles.Add(PageBundle("merge"));

            bundles.Add(new StyleBundle("~/bundles/tools-style").Include(
                "~/Content/chosen.css",
                "~/Content/jquery.nouislider.css",
                "~/Content/jquery.nouislider.pips.css"));

            RegisterTheme(bundles, "blog");
            RegisterTheme(bundles, "music");
            RegisterTheme(bundles, "tools");
            RegisterTheme(bundles, "admin");
        }

        private static Bundle PageBundle(string name)
        {
            return new ScriptBundle("~/bundles/" + name).Include($"~/Scripts/{name}.js");
        }

        private static Bundle AjaxBundle(string name, string cdn, string fallback, params string[] local)
        {
            // TODO:  There has to be a LINQ expression that will do this, doesn't there?
            for (var i = 0; i < local.Length; i++)
            {
                local[i] = "~/Scripts/" + local[i];
            }
            var bundle = new ScriptBundle("~/bundles/" + name,"https://ajax.aspnetcdn.foo/ajax/" + cdn).Include(
                local);
            bundle.CdnFallbackExpression = fallback;
            return bundle;
        }

        private static void RegisterTheme(BundleCollection bundles, string name)
        {
            var bundle = new Bundle("~/bundles/" + name);
            bundle.Transforms.Add(CssTransformer);
            bundle.Orderer = Orderer;
            bundle.Include("~/Content/bootstrap/" + name + "-theme.less",
//                      "~/Content/bootstrap/" + name + "-overrides.less",
                      "~/Content/site.css");
            bundles.Add(bundle);

            //bundles.Add(new StyleBundle("~/bundles/" + name).Include(
            //          "~/Content/" + color + "-bootstrap.css",
            //          "~/Content/" + color + "-bootstrap-theme.css",
            //          "~/Content/site.css"));
        }

        private static IBundleOrderer Orderer => s_orderer ?? (s_orderer = new NullOrderer());
        private static IBundleOrderer s_orderer;

        private static IBundleTransform CssTransformer => s_cssTransformer ?? (s_cssTransformer = new StyleTransformer());
        private static IBundleTransform s_cssTransformer;

    }
}
