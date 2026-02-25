using HarmonyLib;
using MageQuitModFramework.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AxeElement
{
    /// <summary>
    /// Axe Element Patches for MageQuit.
    /// Replaces kill-based scoring with placement-based scoring:
    ///  - 1st place (last alive): 3 points
    ///  - 2nd place: 2 points
    ///  - 3rd place: 1 point
    ///  - 4th place and lower: 0 points
    /// All original kill/death/damage/healing tracking remains untouched.
    /// </summary>
    [HarmonyPatch]
    public static class AxeElementPatches
    {
        private static Dictionary<int, int> totalPoints = [];
        private static Dictionary<int, int> roundPoints = [];
        private static List<int> deathOrder   = [];
        private static Dictionary<Score, int> aggregateCache = [];

        public static void Initialize()
        {
            GameEventsObserver.SubscribeToRoundStart(ResetRound);
        }

        public static int GetTotalPoints(int playerId) =>
            totalPoints.TryGetValue(playerId, out int pts) ? pts : 0;

        public static int GetRoundPoints(int playerId) =>
            roundPoints.TryGetValue(playerId, out int pts) ? pts : 0;

        private static int OriginalSumKills(Score score)
        {
            if (score.kills == null || score.kills.Count == 0)
                return 0;
            return score.kills.Count(x => x != -1) - score.kills.Count(x => x == -1);
        }

        private static void AwardRoundPoints()
        {
            roundPoints.Clear();

            if (PlayerManager.round <= 0)
                return;

            foreach (var kvp in PlayerManager.players)
            {
                int idx = deathOrder.IndexOf(kvp.Key);
                int place = idx == -1 ? 0 : (PlayerManager.players.Count - 1 - idx);

                int pts = Mathf.Max(0, Mathf.Min(PlayerManager.players.Count - place - 1, 3 - place));

                Plugin.Log.LogDebug($"AwardRoundPoints: PlayerId={kvp.Key}, Index={idx}, Place={place}, Points={pts}");
                roundPoints[kvp.Key] = pts;
            }
        }

        private static void ResetRound()
        {
            deathOrder.Clear();
            roundPoints.Clear();
            aggregateCache.Clear();

            if (PlayerManager.round <= 1)
                totalPoints.Clear();
        }

        /// <summary>
        /// Record death order when a wizard dies.
        /// </summary>
        [HarmonyPatch(typeof(WizardStatus), nameof(WizardStatus.DieRightNow))]
        [HarmonyPrefix]
        private static void TrackDeath(WizardStatus __instance)
        {
            if (Globals.round_recap_manager != null && Globals.round_recap_manager.scoresSent)
                return;

            var wc = __instance.GetComponent<WizardController>();
            if (wc != null && !wc.isClone)
            {
                int owner = __instance.GetComponent<Identity>().owner;
                if (!deathOrder.Contains(owner))
                {
                    deathOrder.Add(owner);
                    Plugin.Log.LogDebug($"TrackDeath: Added owner {owner} to deathOrder. Current order: [{string.Join(",", deathOrder)}]");
                }
                else
                {
                    Plugin.Log.LogDebug($"TrackDeath: Owner {owner} already in deathOrder. Current order: [{string.Join(",", deathOrder)}]");
                }
            }
        }

        /// <summary>
        /// Award placement points before the recap screen initializes.
        /// </summary>
        [HarmonyPatch(typeof(RoundRecapManager), nameof(RoundRecapManager.Initialize))]
        [HarmonyPrefix]
        private static void AwardPoints()
        {
            AwardRoundPoints();
            aggregateCache.Clear();
        }

        /// <summary>
        /// Intercept SumKills() -- return placement points instead of kill count.
        /// </summary>
        [HarmonyPatch(typeof(Score), nameof(Score.SumKills))]
        [HarmonyPrefix]
        private static bool PatchSumKills(Score __instance, ref int __result)
        {
            if (PlayerManager.players == null)
                return true;

            foreach (var kvp in PlayerManager.players)
            {
                if (kvp.Value.totalScore == __instance)
                {
                    __result = GetTotalPoints(kvp.Key);
                    return false;
                }
                if (kvp.Value.roundScore == __instance)
                {
                    __result = GetRoundPoints(kvp.Key);
                    return false;
                }
            }

            if (aggregateCache.TryGetValue(__instance, out int cached))
            {
                __result = cached;
                return false;
            }

            __result = OriginalSumKills(__instance);
            return false;
        }

        /// <summary>
        /// Track aggregate Score objects (team totals) built via AddScore.
        /// </summary>
        [HarmonyPatch(typeof(Score), nameof(Score.AddScore), new[] { typeof(Score) })]
        [HarmonyPostfix]
        private static void TrackAggregates(Score __instance, Score score)
        {
            if (PlayerManager.players == null)
                return;

            foreach (var kvp in PlayerManager.players)
            {
                if (kvp.Value.totalScore == score)
                {
                    if (!aggregateCache.ContainsKey(__instance))
                        aggregateCache[__instance] = 0;
                    aggregateCache[__instance] += GetTotalPoints(kvp.Key);
                    return;
                }
            }
        }

        /// <summary>
        /// Rename "Kills:" label to "Points" on the final stats popup.
        /// </summary>
        [HarmonyPatch(typeof(RecapCard), nameof(RecapCard.ShowStats))]
        [HarmonyPostfix]
        private static void RenameKillsLabel(RecapCard __instance)
        {
            foreach (var text in __instance.stats.GetComponentsInChildren<Text>())
            {
                if (text.text == "Kills:")
                {
                    text.text = "Points:";
                    break;
                }
            }
        }

        /// <summary>
        /// Override the round recap gem display to show placement points.
        /// Runs AFTER Initialize to fix mostKills, suppress suicides, and
        /// replace roundScore.kills with fake entries matching each player
        /// point count, so the NewKills animation shows the right gem count.
        /// </summary>
        [HarmonyPatch(typeof(RoundRecapManager), nameof(RoundRecapManager.Initialize))]
        [HarmonyPostfix]
        private static void OverrideRecapDisplay(RoundRecapManager __instance)
        {
            int maxPts = 0;

            foreach (var kvp in PlayerManager.players)
            {
                int pts = GetRoundPoints(kvp.Key);
                if (!totalPoints.ContainsKey(kvp.Key))
                    totalPoints[kvp.Key] = 0;
                totalPoints[kvp.Key] += pts;
                Plugin.Log.LogDebug($"CommitRoundPoints: PlayerId={kvp.Key}, Points={pts}, Total={totalPoints[kvp.Key]}");
            }

            foreach (var kvp in PlayerManager.players)
            {
                int pts = GetRoundPoints(kvp.Key);
                maxPts = Mathf.Max(pts, maxPts);

                kvp.Value.roundScore.kills = [.. Enumerable.Repeat(kvp.Key, pts)];
            }

            var t = Traverse.Create(__instance);
            t.Field("mostKills").SetValue(maxPts);
            t.Field("showSuicides").SetValue(false);
            t.Field("suicides").GetValue<Dictionary<int, bool>>().Clear();
        }
    }
}
