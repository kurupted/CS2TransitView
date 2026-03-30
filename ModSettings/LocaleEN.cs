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

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kKeybindingGroup), "Controls" },

                // Matches the property name "ToggleToolBinding" in ModSettings.cs
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ToggleToolBinding)), "Activate Better Transit View" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ToggleToolBinding)), $"Press this key to activate Better Transit View." },

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