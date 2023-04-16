using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Wizard
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony Harmony { get; } = new ("VeryHarmonious");
        private static ConfigEntry<float> _dpsTimeSpan;

        private void Awake()
        {
            _dpsTimeSpan = Config.Bind("General", "DpsTimeSpan", 3f, "The time span for DPS calculation");
            Harmony.PatchAll();
        }

        // [HarmonyPatch(typeof(Player), "Update")]
        // private static class PlayerUpdatePatch
        // {
        //     private static int _frame = 1;
        //     private static void Postfix(Player __instance)
        //     {
        //         _frame++;
        //         if (_frame % 10 != 0) return;
        //         _frame = 0;
        //         __instance.health.RestoreHealth(10, false, false, false, false);
        //     }
        // }
        //
        private static int _timerID;

        [HarmonyPatch(typeof(GameUI), "SetNoticeText")]
        private static class DisplayPatch
        {
            private static void Prefix(ref string msg)
            {
                var lines = msg.Split(new[] {"\n"}, StringSplitOptions.None);
                // Format dps string to contain 1 dp
                var newText = lines.Length > 0 && !ChaosStopwatch.Check(_timerID)
                    ? $"{lines[0]}\nDPS: {GetDps():0}"
                    : $"DPS: {GetDps():0}";
                msg = newText;
            }
        }
        
        [HarmonyPatch(typeof(GameUI), "BroadcastNoticeMessage")]
        private static class TimerRecorder
        {
            private static void Prefix(string givenMsg, float givenTime = 3f)
            {
                _timerID = ChaosStopwatch.Begin(givenTime - 0.1f);
            }
        }
        
        private static List<AttackInfo> _attackInfos = new();
        
        private static float GetDps()
        {
            var newAttackInfos = new List<AttackInfo>();
            var timeNow = Time.time;
            var totalDamage = 0f;
            foreach (var attackInfo in _attackInfos)
            {
                if (timeNow - attackInfo.time < _dpsTimeSpan.Value)
                {
                    newAttackInfos.Add(attackInfo);
                    totalDamage += attackInfo.damage;
                }
            }
            _attackInfos = newAttackInfos;
            return totalDamage / _dpsTimeSpan.Value;
        }

        [HarmonyPatch(typeof(Health), "TakeDamage")]
        private static class DamageRecorder
        {
            private static void Prefix(
                AttackInfo givenAttackInfo,
                Entity attackEntity = null)
            {
                if (!attackEntity || !attackEntity.name.StartsWith("Player"))
                {
                    return;
                }
                
                _attackInfos.Add(givenAttackInfo);
            }
        }
    }
}
