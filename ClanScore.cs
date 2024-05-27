using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;
using Oxide.Ext.CarbonAliases;
using Network;
using System.Text;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("ClanScore", "clancores", "1.4.0")]
    public class ClanScore : RustPlugin
    {
        [PluginReference] private readonly Plugin Clans;
        private static readonly HashSet<ulong> lootedContainer = new HashSet<ulong>();
        private static readonly HashSet<ulong> lockedUIs = new HashSet<ulong>();
        private static readonly Dictionary<ulong, Dictionary<ulong, float>> damageList = new Dictionary<ulong, Dictionary<ulong, float>>();
        private static readonly Dictionary<string, int> clanCooldowns = new Dictionary<string, int>();
        private static readonly Dictionary<ulong, ulong> lastBuildingDamage = new Dictionary<ulong, ulong>();
        private static bool redeemRanks = false;

        private static string staticJson = string.Empty;

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            LoadData();
            GenerateStaticUi();
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(DisplayScoreboardCommand));
        }

        private void OnServerInitialized()
        {
            lootedContainer.Clear();
            lockedUIs.Clear();
            damageList.Clear();
            clanCooldowns.Clear();
            lastBuildingDamage.Clear();
            if (Clans == null)
            {
                Puts("Clans plugin not found! Plugin won't work correctly! Unloading plugin...");
                Server.Command($"o.unload {Name}");
                return;
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                ShowScoreboard(player);
                string clanTag = Clans.Call<string>("GetClanOf", player);
                if (clanTag == null) continue;
                data.clanStats.TryAdd(clanTag, new ClanStats());
            }
            foreach (var tc in data.coreTcs.ToList())
            {
                BuildingPrivlidge entity = BaseNetworkable.serverEntities.Find(new NetworkableId(tc.Value)) as BuildingPrivlidge;
                if (entity == null)
                    data.coreTcs.Remove(tc.Key);
            }
            foreach (var clan in data.clanStats.ToList())
            {
                if (Clans.Call("GetClan", clan.Key) == null)
                    data.clanStats.Remove(clan.Key);
            }
            if (redeemRanks && config.winnerGroupName != string.Empty)
            {
                foreach (var player in data.oldWinners)
                    permission.RemoveUserGroup(player, config.winnerGroupName);
                Puts($"The {data.oldWinners.Count} teammates of old winner clan has lost his rank rewards!");
                data.oldWinners.Clear();
                string clanName = data.clanStats.OrderByDescending(x => x.Value.points)?.First().Key ?? string.Empty;
                if (clanName != string.Empty)
                {
                    JObject newClan = Clans.Call<JObject>("GetClan", clanName);
                    JArray newMembers = (JArray)newClan["members"];
                    foreach (var member in newMembers)
                    {
                        permission.AddUserGroup((string)member, config.winnerGroupName);
                        data.oldWinners.Add((string)member);
                    }
                    Puts($"The previous wipe winner clan is {clanName} and {newMembers.Count} members of this clan got his rank reward!");
                }
                if (config.clearData)
                {
                    data.clanStats.Clear();
                    data.coreTcs.Clear();
                }
                SaveData();
            }
            timer.Every(config.tcPentalityCooldown, () => {
                List<string> warnedClans = new List<string>();
                bool changed = false;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    string clanTag = Clans.Call<string>("GetClanOf", player);
                    if (clanTag == null) continue;
                    if (!data.clanStats.ContainsKey(clanTag) || data.clanStats[clanTag].points == 0) continue;
                    if (data.coreTcs.ContainsKey(clanTag)) continue;
                    if (warnedClans.Contains(clanTag)) continue;
                    data.clanStats[clanTag].points -= config.tcPentalityPoints;
                    if (data.clanStats[clanTag].points < 0) data.clanStats[clanTag].points = 0;
                    warnedClans.Add(clanTag);
                    changed = true;
                    foreach (var members in Clans.Call<List<string>>("GetClanMembers", player.userID))
                    {
                        BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                        if (member == null) continue;
                        SendReply(member, Lang("PointsLostNoCore", member.UserIDString, config.tcPentalityPoints, config.tcPentalityCooldown));
                    }
                }
                if (changed)
                    foreach (var onlinePlayer in BasePlayer.activePlayerList)
                        UpdateScoreboard(onlinePlayer);
            });
            timer.Every(1, () => {
                foreach (var cooldown in clanCooldowns.ToList())
                {
                    clanCooldowns[cooldown.Key]--;
                    if (clanCooldowns[cooldown.Key] <= 0)
                        clanCooldowns.Remove(cooldown.Key);
                }
            });
        }

        private void Unload()
        {
            SaveData();
            using CUI cui = new CUI(CuiHandler);
            foreach (var player in BasePlayer.activePlayerList)
            {
                cui.Destroy("ClanScore_Cupboard", player);
                cui.Destroy("ClanScore_Leaderboard", player);
            }
        }
        private void OnNewSave() => redeemRanks = true;

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsSleeping())
            {
                timer.Once(2, () => OnPlayerConnected(player));
                return;
            }
            string clanTag = Clans.Call<string>("GetClanOf", player);
            if (clanTag == null) return;
            data.clanStats.TryAdd(clanTag, new ClanStats());
            ShowScoreboard(player);
        }

        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (lootedContainer.Contains(entity.net.ID.Value)) return;
            if (!config.lootingPoints.ContainsKey(entity.ShortPrefabName)) return;
            string clanTag = Clans.Call<string>("GetClanOf", player);
            if (clanTag == null) return;
            if (!data.coreTcs.ContainsKey(clanTag))
            {
                SendReply(player, Lang("NoCoreTC"));
                return;
            }
            lootedContainer.Add(entity.net.ID.Value);
            data.clanStats.TryAdd(clanTag, new ClanStats());
            data.clanStats[clanTag].points += config.lootingPoints[entity.ShortPrefabName];
            data.clanStats[clanTag].pointsGained += config.lootingPoints[entity.ShortPrefabName];
            foreach (var onlinePlayer in BasePlayer.activePlayerList)
                UpdateScoreboard(onlinePlayer);
            if (!config.sendPointMessage) return;
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", player.userID))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsIncreasedLooting", member.UserIDString, config.lootingPoints[entity.ShortPrefabName]));
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.ShortPrefabName != "bradleyapc" && entity.ShortPrefabName != "patrolhelicopter") return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null) return;
            damageList.TryAdd(entity.net.ID.Value, new Dictionary<ulong, float>());
            damageList[entity.net.ID.Value].TryAdd(attacker.userID, 0);
            damageList[entity.net.ID.Value][attacker.userID] += info.damageTypes.Total();
        }

        private void OnEntityDeath(BaseCombatEntity entity)
        {
            if (entity.ShortPrefabName != "bradleyapc" && entity.ShortPrefabName != "patrolhelicopter") return;
            if (!damageList.ContainsKey(entity.net.ID.Value)) return;
            if (damageList[entity.net.ID.Value].Count == 0) return;
            ulong topDmg = damageList[entity.net.ID.Value].FirstOrDefault().Key;
            string clanTag = Clans.Call<string>("GetClanOf", topDmg);
            if (clanTag == null) return;
            if (!data.coreTcs.ContainsKey(clanTag)) return;
            int pointAmount = 0;
            string text = "PointsIncreasedKilling";
            string textBc = "PointsIncreasedKillingBroadcast";
            if (entity.ShortPrefabName == "bradleyapc")
            {
                text = "PointsIncreasedKillingBradley";
                textBc = "PointsIncreasedKillingBradleyBroadcast";
                pointAmount = config.bradleyPoints;
            }
            else if (entity.ShortPrefabName == "patrolhelicopter")
            {
                text = "PointsIncreasedKillingHelicopter";
                textBc = "PointsIncreasedKillingHelicopterBroadcast";
                pointAmount = config.heliPoints;
            }
            if (pointAmount == 0) return;
            data.clanStats.TryAdd(clanTag, new ClanStats());
            data.clanStats[clanTag].points += pointAmount;
            data.clanStats[clanTag].pointsGained += pointAmount;
            foreach (var onlinePlayer in BasePlayer.activePlayerList)
            {
                SendReply(onlinePlayer, Lang(textBc, onlinePlayer.UserIDString, clanTag, pointAmount));
                UpdateScoreboard(onlinePlayer);
            }
            damageList.Remove(entity.net.ID.Value);
            if (!config.sendPointMessage) return;
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", topDmg))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang(text, member.UserIDString, pointAmount));
            }
        }

        private void OnEntityTakeDamage(DecayEntity entity, HitInfo info)
        {
            BasePlayer lastAttacker = info.InitiatorPlayer;
            if (lastAttacker == null && info.WeaponPrefab != null)
                lastAttacker = BasePlayer.FindByID(info.WeaponPrefab.OwnerID);
            if (lastAttacker == null) return;
            string clanTag1 = Clans.Call<string>("GetClanOf", lastAttacker);
            string clanTag2 = Clans.Call<string>("GetClanOf", entity.OwnerID);
            if (clanTag1 == null || clanTag2 == null) return;
            if (clanTag1 == clanTag2) return;
            lastBuildingDamage.TryAdd(entity.buildingID, lastAttacker.userID);
            lastBuildingDamage[entity.buildingID] = lastAttacker.userID;
        }

        private void OnEntityKill(BuildingPrivlidge entity)
        {
            if (!data.coreTcs.ContainsValue(entity.net.ID.Value)) return;
            string tcOwner = data.coreTcs.Where(x => x.Value == entity.net.ID.Value).FirstOrDefault().Key;
            data.coreTcs.Remove(tcOwner);
        }

        private void OnEntityDeath(BuildingPrivlidge entity, HitInfo info)
        {
            if (!data.coreTcs.ContainsValue(entity.net.ID.Value)) return;
            string tcOwner = data.coreTcs.Where(x => x.Value == entity.net.ID.Value).FirstOrDefault().Key;
            BasePlayer lastAttacker = null;
            if (info != null)
                lastAttacker = info.InitiatorPlayer;
            if (lastAttacker == null && info != null && info.WeaponPrefab != null)
                lastAttacker = BasePlayer.FindByID(info.WeaponPrefab.OwnerID);
            if (lastAttacker == null)
            {
                TryFindLastDamage(entity);
                return;
            }
            string clanTag = Clans.Call<string>("GetClanOf", lastAttacker.userID);
            if (clanTag == null || clanTag == tcOwner) return;
            if (!data.coreTcs.ContainsKey(clanTag))
            {
                SendReply(lastAttacker, Lang("NoCoreTC"));
                return;
            }
            data.clanStats.TryAdd(tcOwner, new ClanStats());
            float addedPointsFloat = data.clanStats[tcOwner].points * (float)((float)config.raidPercentage / 100f);
            int addedPoints = Mathf.CeilToInt(addedPointsFloat);
            data.clanStats.TryAdd(clanTag, new ClanStats());
            data.clanStats[clanTag].points += addedPoints;
            data.clanStats[clanTag].pointsGained += addedPoints;
            data.clanStats[clanTag].raids++;
            data.clanStats[tcOwner].points -= addedPoints;
            data.clanStats[tcOwner].pointsLost += addedPoints;
            data.clanStats[clanTag].raided++;
            if (data.clanStats[tcOwner].points < 0) data.clanStats[tcOwner].points = 0;
            clanCooldowns.TryAdd(tcOwner, 0);
            clanCooldowns[tcOwner] = config.tcCooldown;
            lastBuildingDamage.Remove(entity.buildingID);
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", entity.OwnerID))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsLost", member.UserIDString));
            }
            foreach (var onlinePlayer in BasePlayer.activePlayerList)
            {
                SendReply(onlinePlayer, Lang("PointsIncreasedCupboardBroadcast", onlinePlayer.UserIDString, clanTag, tcOwner, addedPoints));
                UpdateScoreboard(onlinePlayer);
            }
            if (!config.sendPointMessage) return;
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", lastAttacker.userID))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsIncreasedCupboard", member.UserIDString, addedPoints));
            }
        }

        private void TryFindLastDamage(BuildingPrivlidge entity)
        {
            if (!lastBuildingDamage.ContainsKey(entity.buildingID)) return;
            ulong userId = lastBuildingDamage[entity.buildingID];
            string tcOwner = Clans.Call<string>("GetClanOf", entity.OwnerID);
            string clanTag = Clans.Call<string>("GetClanOf", userId);
            if (tcOwner == null) return;
            data.clanStats.TryAdd(tcOwner, new ClanStats());
            float addedPointsFloat = data.clanStats[tcOwner].points * (float)((float)config.raidPercentage / 100f);
            int addedPoints = Mathf.CeilToInt(addedPointsFloat);
            data.clanStats.TryAdd(clanTag, new ClanStats());
            data.clanStats[clanTag].points += addedPoints;
            data.clanStats[clanTag].pointsGained += addedPoints;
            data.clanStats[clanTag].raids++;
            data.clanStats[tcOwner].points -= addedPoints;
            data.clanStats[tcOwner].pointsLost += addedPoints;
            data.clanStats[clanTag].raided++;
            if (data.clanStats[tcOwner].points < 0) data.clanStats[tcOwner].points = 0;
            clanCooldowns.TryAdd(tcOwner, 0);
            clanCooldowns[tcOwner] = config.tcCooldown;
            lastBuildingDamage.Remove(entity.buildingID);
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", entity.OwnerID))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsLost", member.UserIDString));
            }
            foreach (var onlinePlayer in BasePlayer.activePlayerList)
            {
                SendReply(onlinePlayer, Lang("PointsIncreasedCupboardBroadcast", onlinePlayer.UserIDString, clanTag, tcOwner, addedPoints));
                UpdateScoreboard(onlinePlayer);
            }
            if (!config.sendPointMessage) return;
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", userId))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsIncreasedCupboard", member.UserIDString, addedPoints));
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null) return;
            string clanTag1 = Clans.Call<string>("GetClanOf", player);
            string clanTag2 = Clans.Call<string>("GetClanOf", attacker);
            if (string.IsNullOrEmpty(clanTag1) || string.IsNullOrEmpty(clanTag2)) return;
            if (player.currentTeam == attacker.currentTeam || clanTag1 == clanTag2) return;
            data.clanStats.TryAdd(clanTag2, new ClanStats());
            data.clanStats[clanTag2].points += config.playerPoints;
            data.clanStats[clanTag2].pointsGained += config.playerPoints;
            data.clanStats[clanTag2].clanKills++;
            float takenPointsFloat = config.playerPoints * (float)((float)config.playerPointsPercentage / 100f);
            int takenPoints = Mathf.CeilToInt(takenPointsFloat);
            data.clanStats.TryAdd(clanTag1, new ClanStats());
            data.clanStats[clanTag1].points -= takenPoints;
            data.clanStats[clanTag1].pointsLost += takenPoints;
            data.clanStats[clanTag1].clanDeaths++;
            if (data.clanStats[clanTag1].points < 0) data.clanStats[clanTag1].points = 0;
            foreach (var onlinePlayer in BasePlayer.activePlayerList)
                UpdateScoreboard(onlinePlayer);
            SendReply(attacker, Lang("PointsIncreasedKill", attacker.UserIDString, config.playerPoints));
        }

        private void OnLootEntity(BasePlayer player, BuildingPrivlidge entity) => ShowCupboardButton(player, entity);

        private void OnLootEntityEnd(BasePlayer player, BuildingPrivlidge entity)
        {
            using CUI cui = new CUI(CuiHandler);
            cui.Destroy("ClanScore_Cupboard", player);
        }

        private void API_AddPoints(string tag, int amount)
        {
            if (tag == null || tag == string.Empty) return;
            data.clanStats.TryAdd(tag, new ClanStats());
            data.clanStats[tag].points += amount;
            data.clanStats[tag].pointsGained += amount;
            JObject clan = Clans.Call<JObject>("GetClan", tag);
            foreach (var members in clan["members"])
            {
                BasePlayer member = BasePlayer.FindByID(Convert.ToUInt64(members));
                if (member == null) continue;
                SendReply(member, Lang("PointsIncreasedAPI", member.UserIDString, amount));
            }
        }

        private string API_GetClanData() => JsonConvert.SerializeObject(data.clanStats);

        private void API_ScoreboardToggle(BasePlayer player, bool enable)
        {
            if (enable)
            {
                lockedUIs.Remove(player.userID);
                ShowScoreboard(player);
            }
            else if (!enable && !lockedUIs.Contains(player.userID))
            {
                lockedUIs.Add(player.userID);
                using CUI cui = new CUI(CuiHandler);
                cui.Destroy("ClanScore_Leaderboard", player);
            }
        }

        private void DisplayScoreboardCommand(BasePlayer player)
        {
            if (data.enabledUIs.Contains(player.userID))
                data.enabledUIs.Remove(player.userID);
            else
                data.enabledUIs.Add(player.userID);
            ShowScoreboard(player);
        }

        [ChatCommand("myshopcommand")]
        private void myshopcommand(BasePlayer player)
        {
            player.SendConsoleCommand("welcomepanel_close");
            player.SendConsoleCommand("chat.say /s");
        }

        [ConsoleCommand("UI_SetCoreTC")]
        private void SetCoreCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            string clanTag = Clans.Call<string>("GetClanOf", player);
            if (clanTag == null) return;
            if (data.coreTcs.ContainsKey(clanTag)) return;
            ulong netId = ulong.Parse(arg.Args[0]);
            BuildingPrivlidge entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BuildingPrivlidge;
            if (entity == null) return;
            data.coreTcs.Add(clanTag, entity.net.ID.Value);
            foreach (var members in Clans.Call<List<string>>("GetClanMembers", player.userID))
            {
                BasePlayer member = BasePlayer.FindByID(ulong.Parse(members));
                if (member == null) continue;
                SendReply(member, Lang("CoreSet", member.UserIDString, player.displayName));
            }
            if (config.enabledMarker)
            {
                MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", entity.transform.position) as MapMarkerGenericRadius;
                marker.alpha = config.markerAlpha;
                Color color1;
                Color color2;
                ColorUtility.TryParseHtmlString(config.markerColor1, out color1);
                ColorUtility.TryParseHtmlString(config.markerColor2, out color2);
                marker.color1 = color1;
                marker.color2 = color2;
                marker.radius = config.markerRadius;
                marker.Spawn();
                marker.SetParent(entity);
                marker.transform.position = entity.transform.position;
                marker.SendNetworkUpdate();
                marker.SendUpdate();
            }
            using CUI cui = new CUI(CuiHandler);
            cui.Destroy("ClanScore_Cupboard", player);
            ShowCupboardButton(player, entity);
        }

        private void OnClanCreate(string tag)
        {
            data.clanStats.Remove(tag);
            data.clanStats.Add(tag, new ClanStats());
            foreach (var player in BasePlayer.activePlayerList)
                UpdateScoreboard(player);
        }

        private void OnClanDestroy(string tag)
        {
            data.clanStats.Remove(tag);
            data.coreTcs.Remove(tag);
            foreach (var player in BasePlayer.activePlayerList)
                UpdateScoreboard(player);
        }

        private class BetterColors
        {
            public static readonly string TextDarker = "0.729 0.694 0.659 1";
            public static readonly string Transparent = "1 1 1 0";
            public static readonly string Black = "0 0 0 1";
            public static readonly string LightGrayTransparent = "0.9686275 0.9215686 0.8823529 0.15";
            public static readonly string RedBackgroundTransparent = "0.6980392 0.2039216 0.003921569 0.5450981";
            public static readonly string RedText = "0.9411765 0.4862745 0.3058824 1";
            public static readonly string GreenBackgroundTransparent = "0.4509804 0.5529412 0.2705882 0.5450981";
            public static readonly string GreenText = "0.6078432 0.7058824 0.4313726 1";
        }

        private void GenerateStaticUi()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer leaderboard = cui.CreateContainer("ClanScore_Leaderboard", BetterColors.Transparent, 1, 1, 1, 1, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Hud, null);
            //Leaderboard Background
            var element = cui.CreateSimpleImage(leaderboard, "ClanScore_Leaderboard", "", "assets/icons/shadow.png", BetterColors.LightGrayTransparent, "assets/icons/iconmaterial.mat", 0f, 0f, 0f, 0f, -226, -14, -206, -14, 0f, 0f, false, false, null, null, false, "ClanScore_LeaderboardBackground", null, false);
            foreach (var comp in element.Element.Components)
                if (comp is CuiImageComponent)
                    (comp as CuiImageComponent).ImageType = UnityEngine.UI.Image.Type.Tiled;
            //Leaderboard Title
            cui.CreateText(leaderboard, "ClanScore_LeaderboardBackground", BetterColors.TextDarker, "", 14, 0f, 0f, 0f, 0f, 0, 212, 156, 180, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.PermanentMarker, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7", false, "ClanScore_LeaderboardTitle", null, false);
            //Leaderboard Teams
            cui.CreateText(leaderboard, "ClanScore_LeaderboardBackground", BetterColors.TextDarker, "", 14, 0f, 0f, 0f, 0f, 16, 200, 7, 157, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7", false, "ClanScore_LeaderboardTeams", null, false);
            //Leaderboard Scores
            cui.CreateText(leaderboard, "ClanScore_LeaderboardBackground", BetterColors.TextDarker, "", 12, 0f, 0f, 0f, 0f, 16, 196, 7, 159, TextAnchor.UpperRight, CUI.Handler.FontTypes.PermanentMarker, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7", false, "ClanScore_LeaderboardScores", null, false);
            staticJson = CuiHelper.ToJson(leaderboard);
        }

        private static void SendJson(BasePlayer player, string json) => CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.net.connection), null, "AddUI", json);

        private void ShowScoreboard(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            if (!data.enabledUIs.Contains(player.userID) || lockedUIs.Contains(player.userID))
            {
                cui.Destroy("ClanScore_Leaderboard", player);
                return;
            }
            SendJson(player, staticJson);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            elements.Add(cui.UpdateText("ClanScore_LeaderboardTitle", BetterColors.TextDarker, Lang("ScoreboardTitle", player.UserIDString), 14, font: CUI.Handler.FontTypes.PermanentMarker));
            elements.Send(player);
            elements.Dispose();
            UpdateScoreboard(player);
        }

        private void UpdateScoreboard(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            if (!data.enabledUIs.Contains(player.userID) || lockedUIs.Contains(player.userID))
            {
                cui.Destroy("ClanScore_Leaderboard", player);
                return;
            }
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            StringBuilder usernames = Pool.Get<StringBuilder>();
            StringBuilder scores = Pool.Get<StringBuilder>();
            usernames.Clear();
            scores.Clear();
            int position = 1;
            foreach (var clan in data.clanStats.OrderByDescending(x => x.Value.points).Take(9))
            {
                string color = string.Empty;
                if (position == 1) color = "#ffd700";
                else if (position == 2) color = "#c0c0c0";
                else if (position == 3) color = "#cd7f32";
                if (color != string.Empty)
                {
                    usernames.Append("<color=").Append(color).Append('>').Append(position).Append(". ").Append(clan.Key).Append("</color>\n");
                    scores.Append("<color=").Append(color).Append('>').Append(clan.Value.points).Append("</color>\n");
                }
                else
                {
                    usernames.Append(position).Append(". ").Append(clan.Key).AppendLine();
                    scores.AppendLine(clan.Key);
                }
                position++;
            }
            elements.Add(cui.UpdateText("ClanScore_LeaderboardTeams", BetterColors.TextDarker, usernames.ToString(), 14, align: TextAnchor.UpperLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("ClanScore_LeaderboardScores", BetterColors.TextDarker, scores.ToString(), 12, align: TextAnchor.UpperRight, font: CUI.Handler.FontTypes.PermanentMarker));
            elements.Send(player);
            elements.Dispose();
            Pool.Free(ref usernames);
            Pool.Free(ref scores);
        }

        private void ShowCupboardButton(BasePlayer player, BuildingPrivlidge entity)
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer tcButton = cui.CreateContainer("ClanScore_Cupboard", BetterColors.Transparent, 0.5f, 0.5f, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.HudMenu, null);
            string message = string.Empty;
            string bgColor = BetterColors.RedBackgroundTransparent;
            string textColor = BetterColors.RedText;
            string command = string.Empty;
            string clanTag = Clans.Call<string>("GetClanOf", player);
            if (clanTag == null)
                message = Lang("NoClanForTC", player.UserIDString);
            else if (entity.OwnerID != player.userID)
                message = Lang("NotTCOwner", player.UserIDString);
            else if (data.coreTcs.ContainsKey(clanTag))
                message = Lang("CoreAlreadySet", player.UserIDString);
            else if (data.coreTcs.ContainsValue(entity.net.ID.Value))
                message = Lang("OtherTeamCore", player.UserIDString);
            else if (clanCooldowns.ContainsKey(clanTag))
                message = Lang("CoreOnCooldown", player.UserIDString);
            else if (!data.coreTcs.ContainsKey(clanTag))
            {
                message = Lang("SetCoreTC", player.UserIDString);
                bgColor = BetterColors.GreenBackgroundTransparent;
                textColor = BetterColors.GreenText;
                command = $"UI_SetCoreTC {entity.net.ID}";
            }
            else if (data.coreTcs[clanTag] == entity.net.ID.Value)
                message = Lang("ThisIsCoreTC", player.UserIDString);
            //TC Button
            cui.CreateButton(tcButton, "ClanScore_Cupboard", bgColor, textColor, message, 14, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 350, 572, 596, 617, command, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "CupboardButton_TcButton", null, false);
            cui.Send(tcButton, player);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ScoreboardTitle"] = "SCOREBOARD",
                ["PointsIncreasedLooting"] = "Your clan points has been increased by <color=#5c81ed>{0}</color> for looting an container!",
                ["PointsIncreasedKilling"] = "Your clan points has been increased by <color=#5c81ed>{0}</color> for killing an entity!",
                ["PointsIncreasedKillingBroadcast"] = "<color=#5c81ed>{0}</color> clan points has been increased by <color=#5c81ed>{1}</color> for killing an entity!",
                ["PointsIncreasedKillingBradley"] = "Your clan points has been increased by <color=#5c81ed>{0}</color> for killing an Bradley!",
                ["PointsIncreasedKillingBradleyBroadcast"] = "<color=#5c81ed>{0}</color> clan points has been increased by <color=#5c81ed>{1}</color> for killing an Bradley!",
                ["PointsIncreasedKillingHelicopter"] = "Your clan points has been increased by <color=#5c81ed>{0}</color> for killing an Patrol Helicopter!",
                ["PointsIncreasedKillingHelicopterBroadcast"] = "<color=#5c81ed>{0}</color> clan points has been increased by <color=#5c81ed>{1}</color> for killing an Patrol Helicopter!",
                ["PointsIncreasedCupboard"] = "Congratulations! Your clan points has been increased by <color=#5c81ed>{0}</color> for destroying an enemy Core TC!",
                ["PointsIncreasedCupboardBroadcast"] = "<color=#5c81ed>{0}</color> clan raided <color=#5c81ed>{1}</color> clan and got <color=#5c81ed>{2}</color> points for cupboard!",
                ["PointsIncreasedKill"] = "Your clan points has been increased by <color=#5c81ed>{0}</color> for killing another clan member!",
                ["PointsIncreasedAPI"] = "Your clan points has been increased by <color=#5c81ed>{0}</color>!",
                ["PointsLost"] = "Your team Core TC has been destroyed and all poins has been lost!",
                ["PointsLostNoCore"] = "You've lost <color=#5c81ed>{0}</color> points for no core set!\nSet your core now, or in next <color=#5c81ed>{1}</color> seconds you will get another pentality!",
                ["SetCoreTC"] = "SET BASE CORE",
                ["NoClanForTC"] = "You don't have clan so you can't set base core!",
                ["CoreAlreadySet"] = "CORE ALREADY SET",
                ["ThisIsCoreTC"] = "THIS IS YOUR CORE TC",
                ["CoreOnCooldown"] = "You can set your new core in {0} seconds!",
                ["OtherTeamCore"] = "This is other team core!",
                ["NotTCOwner"] = "You need to be owner of cupboard!",
                ["NoCoreTC"] = "You cannot gain points without core cupboard set!",
                ["CoreSet"] = "<color=#5c81ed>{0}</color> has set your team core cupboard!",
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static PluginConfig config = new PluginConfig();

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                commands = new List<string>()
                {
                    "scoreboard",
                    "panel"
                },
                lootingPoints = new Dictionary<string, int>()
                {
                    { "codelockedhackablecrate_oilrig", 5 },
                    { "codelockedhackablecrate", 5 },
                }
            }, true);
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private class PluginConfig
        {
            [JsonProperty("Command List")]
            public List<string> commands = new List<string>();

            [JsonProperty("Marker Enabled")]
            public bool enabledMarker = true;

            [JsonProperty("Marker Radius")]
            public float markerRadius = 2;

            [JsonProperty("Marker Color #1")]
            public string markerColor1 = "#ff0000";

            [JsonProperty("Marker Color #2")]
            public string markerColor2 = "#ff0000";

            [JsonProperty("Marker Alpha")]
            public float markerAlpha = 0.6f;

            [JsonProperty("Looting Points")]
            public Dictionary<string, int> lootingPoints = new Dictionary<string, int>();

            [JsonProperty("Points for Bradley (0 to disable)")]
            public int bradleyPoints = 5;

            [JsonProperty("Points for Helicopter (0 to disable)")]
            public int heliPoints = 10;

            [JsonProperty("Points for Player (0 to disable)")]
            public int playerPoints = 2;

            [JsonProperty("Points for Player - Taken Percentage")]
            public int playerPointsPercentage = 100;

            [JsonProperty("Raid Points - Percentage (0-100)")]
            public int raidPercentage = 50;

            [JsonProperty("Cooldown for new Core TC (in seconds)")]
            public int tcCooldown = 120;

            [JsonProperty("Send Messages About Earned Points to Clan Members")]
            public bool sendPointMessage = true;

            [JsonProperty("No TC Pentality - How often (in seconds)")]
            public int tcPentalityCooldown = 30;

            [JsonProperty("No TC Pentality - How many points")]
            public int tcPentalityPoints = 5;

            [JsonProperty("Wipe - Clear Data From Previous Wipe")]
            public bool clearData = true;

            [JsonProperty("Winner Team - Group Name")]
            public string winnerGroupName = "winner";
        }

        private static PluginData data = new PluginData();

        public CUI.Handler CuiHandler { get; private set; }

        private class PluginData
        {
            public Dictionary<string, ClanStats> clanStats = new Dictionary<string, ClanStats>();

            public Dictionary<string, ulong> coreTcs = new Dictionary<string, ulong>();

            public HashSet<string> oldWinners = new HashSet<string>();

            public HashSet<ulong> enabledUIs = new HashSet<ulong>();
        }
        private class ClanStats
        {
            public int points = 0;
            public int raids = 0;
            public int raided = 0;
            public int pointsGained = 0;
            public int pointsLost = 0;
            public int clanKills = 0;
            public int clanDeaths = 0;

        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            timer.Every(Core.Random.Range(500, 700), SaveData);
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, data);
    }
}