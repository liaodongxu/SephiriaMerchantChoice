using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace SephiriaMerchantChoice
{
    [BepInPlugin(PluginGuid, "商人自选商品 (Sephiria Merchant Choice)", "1.0.0")]
    [BepInIncompatibility("com.codex.sephiria.runqol")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.codex.sephiria.merchantchoice";
        internal static ManualLogSource LogSource;
        internal static Plugin Instance;
        private void Awake()
        {
            Instance = this;
            LogSource = Logger;
            ShopCatalog.Enabled = Config.Bind("Shop", "EnableChoiceCatalog", true,
                "Open the merchant choice catalog instead of rerolling; native prices and slot limits.");
            new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("MerchantChoice loaded (split from Run QoL 1.3.3).");
        }
    }
}

