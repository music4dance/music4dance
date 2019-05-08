using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.Msie;

namespace m4d
{
    public class JsEngineSwitcherConfig
    {
        public static void Configure(IJsEngineSwitcher engineSwitcher)
        {
            engineSwitcher.EngineFactories
                .AddMsie(new MsieSettings
                {
                    UseEcmaScript5Polyfill = true,
                    UseJson2Library = true
                })
                ;

            engineSwitcher.DefaultEngineName = MsieJsEngine.EngineName;
        }
    }
}