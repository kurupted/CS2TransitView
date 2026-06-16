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
        public const string kGeneralGroup = "General";
        public const string kDefaultsGroup = "Defaults";

        public static ModSettings Instance { get; set; }

        public ModSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // Define the default binding (' key)
        [SettingsUIKeyboardBinding(BindingKeyboard.Quote, Mod.kToggleActionName, ctrl: false)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding ToggleToolBinding { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool ShowAverageWaitTime { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool MapModeActivatedByDefault { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultBusVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultTrainVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultTramVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultSubwayVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultShipVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultAirplaneVisible { get; set; }

        [SettingsUISection(kSection, kDefaultsGroup)]
        public bool DefaultCargoVisible { get; set; }

        public override void SetDefaults()
        {
            ShowAverageWaitTime = true;
            MapModeActivatedByDefault = false;
            DefaultBusVisible = true;
            DefaultTrainVisible = true;
            DefaultTramVisible = true;
            DefaultSubwayVisible = true;
            DefaultShipVisible = true;
            DefaultAirplaneVisible = false;
            DefaultCargoVisible = false;
        }
    }
}