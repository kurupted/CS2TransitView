using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace BetterTransitView.ModSettings
{
    [FileLocation(nameof(BetterTransitView))]
    [SettingsUIGroupOrder(kVisualsGroup, kKeybindingGroup)]
    [SettingsUIShowGroupName(kVisualsGroup, kKeybindingGroup)]
    [SettingsUIKeyboardAction(Mod.kToggleActionName, ActionType.Button, usages: new string[] { "BetterTransitView_Usage" }, interactions: new string[] { "UIButton" }, modifierOptions: ModifierOptions.Allow)]
    public class ModSettings : ModSetting
    {
        public const string kSection = "Main";
        public const string kKeybindingGroup = "KeyBinding";
        public const string kVisualsGroup = "Visuals";

        public static ModSettings Instance { get; set; }

        public ModSettings(IMod mod) : base(mod)
        {
            //Instance = this;
        }

        [SettingsUISlider(min = 10, max = 100, step = 5, unit = Unit.kPercentage)]
        [SettingsUISection(kSection, kVisualsGroup)]
        public int RouteOpacity { get; set; } = 70;
        
        [SettingsUISlider(min = 10, max = 200, step = 5, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kVisualsGroup)]
        public int MaxVehicleTraffic { get; set; } = 50;

        [SettingsUISlider(min = 10, max = 500, step = 10, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kVisualsGroup)]
        public int MaxPedestrianTraffic { get; set; } = 100;

        // Define the default binding (/ key)
        [SettingsUIKeyboardBinding(BindingKeyboard.Slash, Mod.kToggleActionName, ctrl: false)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding ToggleToolBinding { get; set; }

        public override void SetDefaults()
        {
            MaxVehicleTraffic = 50;
            MaxPedestrianTraffic = 100;
        }
    }
}