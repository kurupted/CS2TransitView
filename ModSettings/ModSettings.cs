using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace BetterTransitView.ModSettings
{
    [FileLocation(nameof(BetterTransitView))]
    [SettingsUIGroupOrder(kKeybindingGroup)]
    [SettingsUIShowGroupName(kKeybindingGroup)]
    [SettingsUIKeyboardAction(Mod.kToggleActionName, ActionType.Button, usages: new string[] { "BetterTransitView_Usage" }, interactions: new string[] { "UIButton" }, modifierOptions: ModifierOptions.Allow)]
    public class ModSettings : ModSetting
    {
        public const string kSection = "Main";
        public const string kKeybindingGroup = "KeyBinding";

        public static ModSettings Instance { get; set; }

        public ModSettings(IMod mod) : base(mod)
        {
            //Instance = this;
        }

        // Define the default binding (' key)
        [SettingsUIKeyboardBinding(BindingKeyboard.Quote, Mod.kToggleActionName, ctrl: false)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding ToggleToolBinding { get; set; }

        public override void SetDefaults()
        {
            //
        }
    }
}