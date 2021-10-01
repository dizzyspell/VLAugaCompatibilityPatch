using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

// TODO: fix status effect descriptions
// TODO: tweak VL_Abilities positioning
// TODO: fix truncating ability names
// TODO: fix abilities text styling

namespace VLAugaCompatibilityPatch
{
    [BepInPlugin(PluginGuid, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ValheimLegends")]
    [BepInDependency("randyknapp.mods.auga")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dizzyspell.VLAugaCompatibilityPatch";

        public static Color MsHotkeyColor = new(255, 191, 27);
        public static GameObject MsAbilitiesPrefab;

        private void Awake()
        {
            // Load the AssetBundle and hope it works!! :)
            AssetBundle fLoadedAssetBundle = LoadEmbeddedAssetBundle("assets");
            if (fLoadedAssetBundle == null)
            {
                Debug.Log($"{PluginInfo.PLUGIN_NAME} Failed to load AssetBundle(s)!");
            }

            // Load the new abilities prefab from the bundle
            MsAbilitiesPrefab = fLoadedAssetBundle.LoadAsset<GameObject>("VL_Abilities");

            // Harmony stuff ensues
            new Harmony(PluginGuid).PatchAll();

            Logger.LogInfo("Loaded successfully!");
        }

        /// <summary>
        /// Loads an AssetBundle from the embedded resources stream by filename.
        /// </summary>
        /// <param name="aFileName">The full filename of the embedded resource; or the identifier at the end.</param>
        /// <returns>The AssetBundle stored in the given embedded resource file.</returns>
        /// <remarks>
        /// Embedded resource files follow the naming convention
        /// `{AssemblyName}.{FileName}`, so just specifying `{FileName}` is
        /// enough to identify a unique resource - this function checks the end
        /// of the string to support such shorthand.
        /// </remarks>
        private static AssetBundle LoadEmbeddedAssetBundle(string aFileName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            // This complicated line just searches all resources names in the 
            // assembly manifest for one ending with the given string
            string fName = executingAssembly.GetManifestResourceNames().Single(str => str.EndsWith(aFileName));

            // Get a stream for the resource
            Stream fResourceStream = executingAssembly.GetManifestResourceStream(fName);

            // The using statement here automatically calls fResourceStream.Dispose()
            // when exiting the code block - that means it closes the filestream
            // for you :)
            using (fResourceStream)
                return AssetBundle.LoadFromStream(fResourceStream);
        }

        /// <summary>
        /// Fixes the missing ability status icons, normally in the bottom left
        /// of the HUD and as a result, also fixes the weird "Equipping Bronze
        /// Armor" message in the center of the screen.
        /// </summary>
        /// <remarks>
        /// The issue stems from the fact that Auga aggressively removes the
        /// original UI, completely removing the status effects root GameObject
        /// and replacing the status effect template with an empty dummy template.
        /// Both of those are used by ValheimLegends to create the ability status
        /// icons, causing Null Reference Exceptions every single frame. This
        /// patch replaces the ValheimLegends icons with a custom prefab and
        /// attaches them to the health panel - the closest intact hudroot object.
        /// </remarks>
        [HarmonyPatch(typeof(ValheimLegends.VL_Utility), nameof(ValheimLegends.VL_Utility.InitiateAbilityStatus))]
        public class VlMissingAbilityIcons_Patch
        {
            public static bool Prefix()
            {
                if (!ValheimLegends.ValheimLegends.ClassIsValid)
                    return false;

                // If we get here, we *should* be ready to clear away the existing statuses
                ValheimLegends.ValheimLegends.abilitiesStatus = new List<RectTransform>();
                ValheimLegends.ValheimLegends.abilitiesStatus.Clear();

                // Instantiate the abilities game object and attach to the health panel root
                GameObject fAbilitiesRootGo = Instantiate(MsAbilitiesPrefab, Hud.m_instance.transform.Find("hudroot/healthpanel"));

                // Set up the child objects and add them to the statuses list!
                List<string> fAbilityNames = new() { 
                    Localization.instance.Localize(ValheimLegends.ValheimLegends.Ability1_Name.ToString()), 
                    Localization.instance.Localize(ValheimLegends.ValheimLegends.Ability2_Name.ToString()), 
                    Localization.instance.Localize(ValheimLegends.ValheimLegends.Ability3_Name.ToString()) 
                };
                for (int fIndex = 0; fIndex < fAbilitiesRootGo.transform.childCount; fIndex++)
                {
                    RectTransform fAbilityTransform = (RectTransform) fAbilitiesRootGo.transform.GetChild(fIndex);
                    fAbilityTransform.gameObject.SetActive(true);
                    fAbilityTransform.Find("Name").GetComponent<Text>().text = fAbilityNames[fIndex];
                    ValheimLegends.ValheimLegends.abilitiesStatus.Add(fAbilityTransform);
                }

                // Skip the original implementation
                return false;
            }
        }

        /// <summary>
        /// A companion to <see cref="VlMissingAbilityIcons_Patch"/> which ensures
        /// the TimeText colors get updated appropriately, since this behavior
        /// was gutted by Auga.
        /// </summary>
        [HarmonyPatch(typeof(ValheimLegends.ValheimLegends.SkillIcon_Patch), nameof(ValheimLegends.ValheimLegends.SkillIcon_Patch.Postfix))]
        public class VlUpdateAbilityIcons_Patch
        {
            public static void Postfix()
            {
                // Prevent additional NREs. If any of these are true, then the
                // logs will already by flooded with NREs and we don't want more :/
                if (!ValheimLegends.ValheimLegends.ClassIsValid 
                    || !ValheimLegends.ValheimLegends.showAbilityIcons.Value 
                    || ValheimLegends.ValheimLegends.abilitiesStatus == null)
                    return;

                for (int fIndex = 0; fIndex < ValheimLegends.ValheimLegends.abilitiesStatus.Count; ++fIndex)
                {
                    Text fTimeText = ValheimLegends.ValheimLegends.abilitiesStatus[fIndex].transform.Find("TimeText").GetComponent<Text>();

                    // "If ability on cooldown and the text isn't already red, make it red"
                    if (Player.m_localPlayer.GetSEMan().HaveStatusEffect($"SE_VL_Ability{fIndex + 1}_CD")
                        && fTimeText.color != ValheimLegends.ValheimLegends.abilityCooldownColor)
                        fTimeText.color = ValheimLegends.ValheimLegends.abilityCooldownColor;
                    // "Otherwise, make it gold if it isn't already gold"
                    else if (!Player.m_localPlayer.GetSEMan().HaveStatusEffect($"SE_VL_Ability{fIndex + 1}_CD") 
                             && fTimeText.color != MsHotkeyColor)
                        fTimeText.color = MsHotkeyColor;
                }
            }
        }

        /// <summary>
        /// Fixes Compendium entries being excluded from Auga's new Compendium
        /// page.
        /// </summary>
        /// <remarks>
        /// The issue stems from the fact that Auga tries to separate tutorial 
        /// and lore entries into separate tabs using some pretty specific
        /// string compares - it checks for the $tutorial_ and $lore_ strings
        /// in the text keys. Most mods, including VL, do not use these 
        /// identifier conventions and would thus be excluded from either tab.
        /// This patch adds such entries back into the TextsDialog after Auga
        /// removes them.
        /// </remarks>
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
        public class VlMissingCompendiumEntries_Patch
        {
            public static void Postfix(TextsDialog __instance)
            {
                foreach (var (fFey, fValue) in Player.m_localPlayer.GetKnownTexts())
                {
                    if (!fFey.StartsWith("$"))
                    {
                        __instance.m_texts.Add(new TextsDialog.TextInfo(fFey, Localization.instance.Localize(fValue)));
                    }
                }

                __instance.m_texts.Sort((a, b) => string.Compare(a.m_topic, b.m_topic, StringComparison.CurrentCulture));
            }
        }
    }
}
