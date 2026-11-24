using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace CursedEquipment
{
    [BepInPlugin("gkj.sfk.cursedequipment", "Cursed Equipment", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony patches;

        private void Awake()
        {
            patches = new Harmony("gkj.sfk.cursedequipment");
            try
            {
                patches.PatchAll(typeof(Plugin).Assembly);
                if (AddressablesManager.Instance != null)
                    Catalog.Register(AddressablesManager.Instance);
                Logger.LogInfo("Cursed Equipment 0.1.0 loaded.");
            }
            catch
            {
                patches.UnpatchSelf();
                throw;
            }
        }

        private void Update()
        {
            Chalice.Tick(Time.time);
        }

        private void OnDestroy()
        {
            Chalice.Clear();
            patches?.UnpatchSelf();
        }
    }
}
