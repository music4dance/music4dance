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
            // TOOD: Use the full transformer for jquery as well: http://benjii.me/2012/10/using-less-css-with-mvc4-web-optimization/
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new ScriptBundle("~/bundles/knockout").Include(
                      "~/Scripts/knockout-3.2.0.js",
                      "~/Scripts/knockout.mapping-latest.js"));

            bundles.Add(new ScriptBundle("~/bundles/knockout").Include(
                      "~/Scripts/chosen.jquery.js"));

            RegisterTheme(bundles, "blog");
            RegisterTheme(bundles, "music");
            RegisterTheme(bundles, "tools");
            RegisterTheme(bundles, "admin");
        }

        public static void RegisterTheme(BundleCollection bundles, string name)
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

        private static IBundleOrderer Orderer
        {
            get
            {
                if (_orderer == null)
               {
                   _orderer = new NullOrderer();
               }
               return _orderer;
            }
        }
        private static IBundleOrderer _orderer;

        private static IBundleTransform CssTransformer
        {
            get
            {
                if (_cssTransformer == null)
                {
                    _cssTransformer = new StyleTransformer();
                }
                return _cssTransformer;
            }
        }
        private static IBundleTransform _cssTransformer;

    }
}
