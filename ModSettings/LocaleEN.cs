using Colossal;
using Game.Input;
using Game.Settings;
using System.Collections.Generic;

namespace BetterTransitView.ModSettings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;
        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), ModAssemblyInfo.Title },
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGeneralGroup), "General" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDefaultsGroup), "Defaults" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kKeybindingGroup), "Controls" },

                // Matches the property name "ToggleToolBinding" in ModSettings.cs
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ToggleToolBinding)), "Activate Better Transit View" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ToggleToolBinding)), $"Press this key to activate Better Transit View." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShowAverageWaitTime)), "Show Average Wait Time" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShowAverageWaitTime)), "Include average wait time in the waiting passengers labels." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.MapModeActivatedByDefault)), "Map Mode by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.MapModeActivatedByDefault)), "Activate the gray map infoview mode automatically when opening the panel." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultBusVisible)), "Bus Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultBusVisible)), "Show Bus lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultTrainVisible)), "Train Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultTrainVisible)), "Show Train lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultTramVisible)), "Tram Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultTramVisible)), "Show Tram lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultSubwayVisible)), "Subway Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultSubwayVisible)), "Show Subway lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultShipVisible)), "Ship/Ferry Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultShipVisible)), "Show Ship and Ferry lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultAirplaneVisible)), "Airplane Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultAirplaneVisible)), "Show Airplane lines when the panel first opens." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DefaultCargoVisible)), "Cargo Lines Enabled by Default" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DefaultCargoVisible)), "Show Cargo lines when the panel first opens." },

                // Matches the Action Name in Mod.cs
                { m_Setting.GetBindingKeyLocaleID(Mod.kToggleActionName), "Activation Key" },

                { m_Setting.GetBindingMapLocaleID(), ModAssemblyInfo.Title },
                
                { "Infoviews.NAME[BetterTransitViewCustomView]", "Better Transit View" },
                { "Infoviews.DESC[BetterTransitViewCustomView]", "Custom transit overview." },
                { "Infoviews.INFOMODE[BetterTransitViewStations]", "Show Stations" }
            };
        }

        public void Unload()
        {

        }
    }
}