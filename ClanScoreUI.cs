using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Ext.CarbonAliases;
using Network;
using System.Text;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("ClanScoreUI", "ClanCores", "1.0.0")]
    public class ClanScoreUI : RustPlugin
    {
        [PluginReference] private readonly Plugin ImageLibrary, ClanScore, Clans;

        private static string baseJson = string.Empty;
        private static string leaderboardJson = string.Empty;
        private static string fixedLeaderboardJson = string.Empty;
        private static string rootJson = string.Empty;
        private static string textJson = string.Empty;
        private static readonly Dictionary<ulong, UiCache> uiCache = new Dictionary<ulong, UiCache>();

        private static readonly int fixedXoffset = -65;

        public CUI.Handler CuiHandler { get; private set; }

        private static DateTime lastDataFetchTime = DateTime.MinValue;
        private static Dictionary<string, ClanStats> cachedData = default;

        private class UiCache
        {
            public int clanPage = 1;
            public string lastPage = string.Empty;
        }

        private class BetterColors
        {
            public static readonly string GreenBackgroundTransparent = "0.4509804 0.5529412 0.2705882 0.5450981";
            public static readonly string GreenText = "0.6078432 0.7058824 0.4313726 1";
            public static readonly string RedText = "0.9411765 0.4862745 0.3058824 1";
            public static readonly string Transparent = "1 1 1 0";
            public static readonly string BlackTransparent10 = "0 0 0 0.1019608";
            public static readonly string BlackTransparent20 = "0 0 0 0.2";
            public static readonly string Black = "0 0 0 1";
            public static readonly string WhiteTransparent80 = "0 0 0 0.8";
            public static readonly string LightGray = "0.9686275 0.9215686 0.8823529 1";
            public static readonly string LightGrayTransparent8 = "0.9686275 0.9215686 0.8823529 0.07843138";
            public static readonly string LightGrayTransparent12 = "0.9686275 0.9215686 0.8823529 0.1176471";
            public static readonly string LightGrayTransparent40 = "0.9686275 0.9215686 0.8823529 0.4";
            public static readonly string LightGrayTransparent73 = "0.9686275 0.9215686 0.8823529 0.7294118";
            public static readonly string RadioUiBackgroundTransparent60 = "0.1529412 0.1411765 0.1137255 0.6";
            public static readonly string RadioUiBackgroundTransparent80 = "0.1529412 0.1411765 0.1137255 0.8";
            public static readonly string WorkbenchTier1Text = "0.7098039 0.8666667 0.4352941 1";
        }

        private void GenerateStaticJson()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer clansMenu = cui.CreateContainer("ClanCoresMainUI_ClansMenu", BetterColors.Transparent, 0, 1, 0, 1, 0, 0, 0, 0, 0f, 0f, true, false, CUI.ClientPanels.HudMenu, null);
            //Background Blur
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_ClansMenu", BetterColors.BlackTransparent20, "assets/content/ui/uibackgroundblur.mat", 0, 1, 0, 1, 0, 0, 0, 0, true, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_UiBlur0", null, false);
            //Background Darker
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_ClansMenu", BetterColors.BlackTransparent20, "assets/content/ui/namefontmaterial.mat", 0, 1, 0, 1, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_UiPanel1", null, false);
            //Left Section
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_ClansMenu", BetterColors.Transparent, "assets/content/ui/namefontmaterial.mat", 0f, 0.25625f, 0f, 1f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_LeftSection", null, false);
            //Left Anchor
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_LeftSection", BetterColors.Transparent, "assets/content/ui/namefontmaterial.mat", 1, 1, 0.5f, 0.5f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_LeftAnchor", null, false);
            //Logo
            cui.CreateSimpleImage(clansMenu, "ClanCoresMainUI_LeftAnchor", GetImage("UI_ClanScoreLogo"), null, BetterColors.WhiteTransparent80, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, -298, -30, 225, 320, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_UiImage2", null, false);
            //Online Count
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeftAnchor", BetterColors.LightGray, "", 18, 0f, 0f, 0f, 0f, -298, -30, 193, 225, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7 0.7", false, "ClanCoresMainUI_OnlineCount", null, false);
            int startY = 105;
            foreach (var page in config.pages)
            {
                //Menu Button 1
                cui.CreateButton(clansMenu, "ClanCoresMainUI_LeftAnchor", BetterColors.Transparent, BetterColors.Transparent, "", 1, null, 0f, 0f, 0f, 0f, -298, -30, startY, startY + 38, $"UI_ClanScoreLead page {page.Key}", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, $"ClanCoresMainUI_MenuButton_{page.Key}", null, false);
                //Menu Button 1 Text
                cui.CreateText(clansMenu, $"ClanCoresMainUI_MenuButton_{page.Key}", BetterColors.LightGray, page.Value, 23, 0f, 0f, 0f, 0f, 12, 268, 0, 38, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7 0.7", false, $"ClanCoresMainUI_MenuButton_{page.Key}_Text", null, false);
                startY -= 38;
            }
            //Exit Button
            cui.CreateButton(clansMenu, "ClanCoresMainUI_LeftAnchor", BetterColors.Transparent, BetterColors.Transparent, "", 1, null, 0f, 0f, 0f, 0f, -298, -30, -260, -203, "UI_ClanScoreLead exit", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ExitButton", null, false);
            //Exit Button Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_ExitButton", BetterColors.RedText, "CLOSE PAGE", 32, 0f, 0f, 0f, 0f, 12, 268, 0, 57, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, BetterColors.Black, "0.7 0.7", false, "ClanCoresMainUI_ExitButtonText", null, false);
            //Right Section
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_ClansMenu", BetterColors.BlackTransparent10, "assets/content/ui/namefontmaterial.mat", 0.25625f, 1f, 0f, 1f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_RightSection", null, false);
            //Right Background
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightSection", BetterColors.BlackTransparent10, "assets/content/ui/namefontmaterial.mat", 0f, 0.9453781f, 0f, 1f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_RightBackground", null, false);
            //Right Child Anchor
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightBackground", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_RightChildAnchor", null, false);
            //Web Icon
            cui.CreateSimpleImage(clansMenu, "ClanCoresMainUI_RightBackground", null, "assets/icons/web.png", BetterColors.WorkbenchTier1Text, null, 0f, 0f, 0f, 0f, 34, 54, 690, 710, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_WebIcon", null, false);
            //Web Link
            cui.CreateInputField(clansMenu, "ClanCoresMainUI_WebIcon", BetterColors.LightGray, "rustclancores.com", 15, 0, true, 0f, 0f, 0f, 0f, 24, 164, 0, 20, "", TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, false, false, 0f, 0f, false, false, "ClanCoresMainUI_WebLink", null, false);
            //Discord Icon
            cui.CreateSimpleImage(clansMenu, "ClanCoresMainUI_RightBackground", null, "assets/icons/discord.png", BetterColors.WorkbenchTier1Text, null, 0f, 0f, 0f, 0f, 202, 222, 690, 710, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_DiscordIcon", null, false);
            //Discord Link
            cui.CreateInputField(clansMenu, "ClanCoresMainUI_DiscordIcon", BetterColors.LightGray, "discord.gg/clancores", 15, 0, true, 0f, 0f, 0f, 0f, 24, 164, 0, 20, "", TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, false, false, 0f, 0f, false, false, "ClanCoresMainUI_DiscordLink", null, false);
            //Title Background
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightBackground", BetterColors.RadioUiBackgroundTransparent80, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30, 318, 620, 680, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_TitleBackground", null, false);
            //Title Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_TitleBackground", BetterColors.LightGray, "", 32, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_TitleText", null, false);
            //Right Far Anchor
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightSection", BetterColors.Transparent, "assets/content/ui/namefontmaterial.mat", 0.9443277f, 0.9443277f, 1f, 1f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_RightFarAnchor", null, false);
            //Additional Close Button
            cui.CreateButton(clansMenu, "ClanCoresMainUI_RightFarAnchor", BetterColors.LightGrayTransparent8, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 12, 42, -42, -12, "UI_ClanScoreLead exit", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_AdditionalCloseButton", null, false);
            //Additional Close Button Icon
            cui.CreateSimpleImage(clansMenu, "ClanCoresMainUI_AdditionalCloseButton", null, "assets/icons/exit.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 8, 26, 6, 24, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_UiImage3", null, false);
            baseJson = CuiHelper.ToJson(clansMenu);
        }

        private void GenerateLeaderboardJson()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer clansMenu = cui.CreateContainer("ClanCoresMainUI_UpdateModule", BetterColors.Transparent, 0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Under, null);
            //Leaderboard Info Background
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightChildAnchor", BetterColors.RadioUiBackgroundTransparent60, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30, 870, 558, 600, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_LeaderboardInfoBackground", null, false);
            //Position
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "#", 16, 0f, 0f, 0f, 0f, 0, 28, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Position", null, false);
            //Clan Name
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN NAME", 16, 0f, 0f, 0f, 0f, 28, 165, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanName", null, false);
            //Points
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS", 16, 0f, 0f, 0f, 0f, 165, 265, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Points", null, false);
            //Raids
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "RAIDS", 16, 0f, 0f, 0f, 0f, 265, 340, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Raids", null, false);
            //Raided
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "RAIDED", 16, 0f, 0f, 0f, 0f, 340, 415, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Raided", null, false);
            //Points Gained
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS\nGAINED", 16, 0f, 0f, 0f, 0f, 415, 515, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PointsGained", null, false);
            //Points Lost
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS\nLOST", 16, 0f, 0f, 0f, 0f, 515, 615, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PointsLost", null, false);
            //Clan Kills
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nKILLS", 16, 0f, 0f, 0f, 0f, 615, 690, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanKills", null, false);
            //Clan Deaths
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nDEATHS", 16, 0f, 0f, 0f, 0f, 690, 765, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanDeaths", null, false);
            //Clan Kdr
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nKDR", 16, 0f, 0f, 0f, 0f, 765, 840, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanKdr", null, false);
            StringBuilder sb = Pool.Get<StringBuilder>();
            int startY = 528;
            for (int i = 1; i <= 12; i++)
            {
                string keyValue = sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).ToString();
                string color = i % 2 == 0 ? BetterColors.BlackTransparent20 : BetterColors.BlackTransparent10;
                //Leaderboard Clan 1
                cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightChildAnchor", color, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30, 870, startY, startY + 28, false, 0f, 0f, false, false, null, null, false, keyValue, null, false);
                //Leaderboard 1 Position
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 0, 28, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Position").ToString(), null, false); ;
                //Leaderboard 1 Clan Name
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 28, 165, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanName").ToString(), null, false);
                //Leaderboard 1 Points
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 165, 265, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Points").ToString(), null, false);
                //Leaderboard 1 Raids
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 265, 340, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Raids").ToString(), null, false);
                //Leaderboard 1 Raided
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 340, 415, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Raided").ToString(), null, false);
                //Leaderboard 1 Points Gained
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 415, 515, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_PointsGained").ToString(), null, false);
                //Leaderboard 1 Points Lost
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 515, 615, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_PointsLost").ToString(), null, false);
                //Leaderboard 1 Clan Kills
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 615, 690, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanKills").ToString(), null, false);
                //Leaderboard 1 Clan Deaths
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 690, 765, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanDeaths").ToString(), null, false);
                //Leaderboard 1 Clan Kdr
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 765, 840, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanKdr").ToString(), null, false);
                startY -= 30;
            }
            Pool.Free(ref sb);
            //Next Page Button
            cui.CreateButton(clansMenu, "ClanCoresMainUI_RightChildAnchor", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 800, 870, 170, 194, "UI_ClanScoreLead leaderboard nextPage", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_NextPageButton", null, false);
            //Next Page Button Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_NextPageButton", BetterColors.GreenText, "NEXT", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_NextPageButtonText", null, false);
            //Prev Page Button
            cui.CreateButton(clansMenu, "ClanCoresMainUI_RightChildAnchor", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 726, 796, 170, 194, "UI_ClanScoreLead leaderboard prevPage", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PrevPageButton", null, false);
            //Prev Page Button Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_PrevPageButton", BetterColors.LightGrayTransparent40, "PREV", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PrevPageButtonText", null, false);
            //Your Clan Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightChildAnchor", BetterColors.RadioUiBackgroundTransparent80, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30, 870, 30, 150, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanTab", null, false);
            //Your Clan Name
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGray, "", 27, 0f, 0f, 0f, 0f, 0, 260, 0, 120, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanName", null, false);
            //Your Points Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 272, 402, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsTab", null, false);
            //Your Points Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsTabText", null, false);
            //Your Raids Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 414, 544, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidsTab", null, false);
            //Your Raids Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourRaidsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidsTabText", null, false);
            //Your Raided Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 556, 686, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidedTab", null, false);
            //Your Raided Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourRaidedTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidedTabText", null, false);
            //Your Points Gained Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 698, 828, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsGainedTab", null, false);
            //Your Points Gained Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsGainedTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsGainedTabText", null, false);
            //Your Points Lost Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 272, 402, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsLostTab", null, false);
            //Your Points Lost Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsLostTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsLostTabText", null, false);
            //Your Clan Kills Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 414, 544, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKillsTab", null, false);
            //Your Clan Kills Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanKillsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKillsTabText", null, false);
            //Your Clan Deaths Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 556, 686, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanDeathsTab", null, false);
            //Your Clan Deaths Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanDeathsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanDeathsTabText", null, false);
            //Your Clan KDR Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 698, 828, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKDRTab", null, false);
            //Your Clan KDR Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanKDRTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKDRTabText", null, false);
            leaderboardJson = CuiHelper.ToJson(clansMenu);
        }

        private void GenerateFixedLeaderboardJson()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer clansMenu = cui.CreateContainer("ClanCoresMainUI_UpdateModule", BetterColors.Transparent, 0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Under, null);
            //Leaderboard Info Background
            cui.CreatePanel(clansMenu, "content", BetterColors.RadioUiBackgroundTransparent60, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30 + fixedXoffset, 870 + fixedXoffset, 558, 600, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_LeaderboardInfoBackground", null, false);
            //Position
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "#", 16, 0f, 0f, 0f, 0f, 0, 28, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Position", null, false);
            //Clan Name
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN NAME", 16, 0f, 0f, 0f, 0f, 28, 165, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanName", null, false);
            //Points
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS", 16, 0f, 0f, 0f, 0f, 165, 265, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Points", null, false);
            //Raids
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "RAIDS", 16, 0f, 0f, 0f, 0f, 265, 340, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Raids", null, false);
            //Raided
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "RAIDED", 16, 0f, 0f, 0f, 0f, 340, 415, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_Raided", null, false);
            //Points Gained
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS\nGAINED", 16, 0f, 0f, 0f, 0f, 415, 515, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PointsGained", null, false);
            //Points Lost
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "POINTS\nLOST", 16, 0f, 0f, 0f, 0f, 515, 615, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PointsLost", null, false);
            //Clan Kills
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nKILLS", 16, 0f, 0f, 0f, 0f, 615, 690, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanKills", null, false);
            //Clan Deaths
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nDEATHS", 16, 0f, 0f, 0f, 0f, 690, 765, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanDeaths", null, false);
            //Clan Kdr
            cui.CreateText(clansMenu, "ClanCoresMainUI_LeaderboardInfoBackground", BetterColors.LightGray, "CLAN\nKDR", 16, 0f, 0f, 0f, 0f, 765, 840, 0, 42, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_ClanKdr", null, false);
            StringBuilder sb = Pool.Get<StringBuilder>();
            int startY = 528;
            for (int i = 1; i <= 12; i++)
            {
                string keyValue = sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).ToString();
                string color = i % 2 == 0 ? BetterColors.BlackTransparent20 : BetterColors.BlackTransparent10;
                //Leaderboard Clan 1
                cui.CreatePanel(clansMenu, "content", color, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30 + fixedXoffset, 870 + fixedXoffset, startY, startY + 28, false, 0f, 0f, false, false, null, null, false, keyValue, null, false);
                //Leaderboard 1 Position
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 0, 28, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Position").ToString(), null, false); ;
                //Leaderboard 1 Clan Name
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 28, 165, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanName").ToString(), null, false);
                //Leaderboard 1 Points
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 165, 265, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Points").ToString(), null, false);
                //Leaderboard 1 Raids
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 265, 340, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Raids").ToString(), null, false);
                //Leaderboard 1 Raided
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 340, 415, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_Raided").ToString(), null, false);
                //Leaderboard 1 Points Gained
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 415, 515, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_PointsGained").ToString(), null, false);
                //Leaderboard 1 Points Lost
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 515, 615, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_PointsLost").ToString(), null, false);
                //Leaderboard 1 Clan Kills
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 615, 690, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanKills").ToString(), null, false);
                //Leaderboard 1 Clan Deaths
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 690, 765, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanDeaths").ToString(), null, false);
                //Leaderboard 1 Clan Kdr
                cui.CreateText(clansMenu, keyValue, BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 765, 840, 0, 28, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(i).Append("_ClanKdr").ToString(), null, false);
                startY -= 30;
            }
            Pool.Free(ref sb);
            //Next Page Button
            cui.CreateButton(clansMenu, "content", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 800 + fixedXoffset, 870 + fixedXoffset, 170, 194, "UI_ClanScoreLead leaderboard nextPage", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_NextPageButton", null, false);
            //Next Page Button Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_NextPageButton", BetterColors.GreenText, "NEXT", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_NextPageButtonText", null, false);
            //Prev Page Button
            cui.CreateButton(clansMenu, "content", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 726 + fixedXoffset, 796 + fixedXoffset, 170, 194, "UI_ClanScoreLead leaderboard prevPage", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PrevPageButton", null, false);
            //Prev Page Button Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_PrevPageButton", BetterColors.LightGrayTransparent40, "PREV", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_PrevPageButtonText", null, false);
            //Your Clan Tab
            cui.CreatePanel(clansMenu, "content", BetterColors.RadioUiBackgroundTransparent80, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30 + fixedXoffset, 870 + fixedXoffset, 30, 150, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanTab", null, false);
            //Your Clan Name
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGray, "", 27, 0f, 0f, 0f, 0f, 0, 260, 0, 120, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanName", null, false);
            //Your Points Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 272, 402, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsTab", null, false);
            //Your Points Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsTabText", null, false);
            //Your Raids Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 414, 544, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidsTab", null, false);
            //Your Raids Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourRaidsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidsTabText", null, false);
            //Your Raided Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 556, 686, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidedTab", null, false);
            //Your Raided Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourRaidedTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourRaidedTabText", null, false);
            //Your Points Gained Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 698, 828, 66, 108, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsGainedTab", null, false);
            //Your Points Gained Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsGainedTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsGainedTabText", null, false);
            //Your Points Lost Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 272, 402, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsLostTab", null, false);
            //Your Points Lost Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourPointsLostTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourPointsLostTabText", null, false);
            //Your Clan Kills Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 414, 544, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKillsTab", null, false);
            //Your Clan Kills Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanKillsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKillsTabText", null, false);
            //Your Clan Deaths Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 556, 686, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanDeathsTab", null, false);
            //Your Clan Deaths Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanDeathsTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanDeathsTabText", null, false);
            //Your Clan KDR Tab
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_YourClanTab", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 698, 828, 12, 54, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKDRTab", null, false);
            //Your Clan KDR Tab Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_YourClanKDRTab", BetterColors.LightGray, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_YourClanKDRTabText", null, false);
            fixedLeaderboardJson = CuiHelper.ToJson(clansMenu);
        }

        private void API_DisplayOnlyLeaderboard(BasePlayer player)
        {
            uiCache.TryAdd(player.userID, new UiCache());
            uiCache[player.userID].lastPage = "leaderboard";
            uiCache[player.userID].clanPage = 1;
            SendJson(player, fixedLeaderboardJson);
            InitialLeaderboardUpdate(player);
        }

        private void GenerateRootElement()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer clansMenu = cui.CreateContainer("ClanCoresMainUI_UpdateModule", BetterColors.Transparent, 0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Under, null);
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightBackground", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_RightChildAnchor", null, false);
            rootJson = CuiHelper.ToJson(clansMenu);
        }

        private void GenerateTextJson()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer clansMenu = cui.CreateContainer("ClanCoresMainUI_UpdateModule", BetterColors.Transparent, 0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Under, null);
            //Text Background
            cui.CreatePanel(clansMenu, "ClanCoresMainUI_RightChildAnchor", BetterColors.BlackTransparent20, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 30, 870, 30, 590, false, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_TextBackground", null, false);
            //Info Text
            cui.CreateText(clansMenu, "ClanCoresMainUI_TextBackground", BetterColors.LightGray, "", 15, 0f, 0f, 0f, 0f, 8, 832, 8, 552, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "ClanCoresMainUI_InfoText", null, false);
            textJson = CuiHelper.ToJson(clansMenu);
        }


        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
        }

        private void OnServerInitialized()
        {
            AddImage("LOGO_URL_HERE", "UI_ClanScoreLogo");
            RegisterMessages();
            GenerateStaticJson();
            GenerateLeaderboardJson();
            GenerateFixedLeaderboardJson();
            GenerateRootElement();
            GenerateTextJson();
            RegisterCommands();
        }

        private void Unload()
        {
            using CUI cui = new CUI(CuiHandler);
            foreach (var player in BasePlayer.activePlayerList)
            {
                cui.Destroy("ClanCoresMainUI_ClansMenu", player);
                cui.Destroy("ClanCoresMainUI_UpdateModule", player);
            }
        }

        private void RegisterCommands()
        {
            cmd.AddConsoleCommand("UI_ClanScoreLead", this, nameof(LeaderboardConsoleCommand));
            cmd.AddConsoleCommand("UI_ClanScoreLeadFixed", this, nameof(LeaderboardFixedConsoleCommand));
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(LeaderboardCommand));

        }

        private void LeaderboardCommand(BasePlayer player)
        {
            SendJson(player, baseJson);
            if (uiCache.ContainsKey(player.userID))
            {
                uiCache[player.userID].clanPage = 1;
                if (uiCache[player.userID].lastPage == "leaderboard")
                    SendJson(player, leaderboardJson);
                else
                    SendJson(player, textJson);
            }
            else
            {
                uiCache.Add(player.userID, new UiCache());
                uiCache[player.userID].lastPage = config.pages.ElementAt(0).Key;
                if (uiCache[player.userID].lastPage == "leaderboard")
                    SendJson(player, leaderboardJson);
                else
                    SendJson(player, textJson);
            }
            InitialElementUpdate(player);
        }

        private void LeaderboardConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            using CUI cui = new CUI(CuiHandler);
            switch (arg.Args[0])
            {
                case "exit":
                    cui.Destroy("ClanCoresMainUI_ClansMenu", player);
                    cui.Destroy("ClanCoresMainUI_UpdateModule", player);
                    break;
                case "page":
                    if (uiCache[player.userID].lastPage == arg.Args[1]) return;
                    uiCache[player.userID].lastPage = arg.Args[1];
                    cui.Destroy("ClanCoresMainUI_UpdateModule", player);
                    cui.Destroy("ClanCoresMainUI_RightChildAnchor", player);
                    SendJson(player, rootJson);
                    if (arg.Args[1] == "leaderboard")
                        SendJson(player, leaderboardJson);
                    else
                    {
                        SendJson(player, textJson);
                    }
                    InitialElementUpdate(player);
                    break;
                case "leaderboard":
                    if (arg.Args[1] == "nextPage")
                    {
                        uiCache[player.userID].clanPage++;
                    }
                    else if (uiCache[player.userID].clanPage > 1)
                    {
                        uiCache[player.userID].clanPage--;
                    }
                    break;
            }
        }

        private void LeaderboardFixedConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            using CUI cui = new CUI(CuiHandler);
            switch (arg.Args[0])
            {
                case "leaderboard":
                    if (arg.Args[1] == "nextPage")
                    {
                        uiCache[player.userID].clanPage++;
                        InitialLeaderboardUpdate(player);
                    }
                    else if (uiCache[player.userID].clanPage > 1)
                    {
                        uiCache[player.userID].clanPage--;
                        InitialLeaderboardUpdate(player);
                    }
                    break;
            }
        }

        private void InitialElementUpdate(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            StringBuilder sb = Pool.Get<StringBuilder>();
            sb.Clear().Append("ONLINE PLAYERS: ").Append(BasePlayer.activePlayerList.Count).Append('/').Append(ConVar.Server.maxplayers);
            elements.Add(cui.UpdateText("ClanCoresMainUI_OnlineCount", BetterColors.LightGray, sb.ToString(), 18, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("ClanCoresMainUI_TitleText", BetterColors.LightGray, config.pages[uiCache[player.userID].lastPage], 32, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            foreach (var page in config.pages)
            {
                string pageName = sb.Clear().Append("ClanCoresMainUI_MenuButton_").Append(page.Key).Append("_Text").ToString();
                if (uiCache[player.userID].lastPage == page.Key)
                    elements.Add(cui.UpdateText(pageName, BetterColors.WorkbenchTier1Text, page.Value, 23, font: CUI.Handler.FontTypes.RobotoCondensedBold, align: TextAnchor.MiddleLeft));
                else
                    elements.Add(cui.UpdateText(pageName, BetterColors.LightGray, page.Value, 23, font: CUI.Handler.FontTypes.RobotoCondensedBold, align: TextAnchor.MiddleLeft));
            }
            if (uiCache[player.userID].lastPage == "leaderboard")
            {
                if ((DateTime.Now - lastDataFetchTime).TotalMinutes > 5)
                {
                    lastDataFetchTime = DateTime.Now;
                    cachedData = JsonConvert.DeserializeObject<Dictionary<string, ClanStats>>(ClanScore.Call<string>("API_GetClanData"));
                }
                int startCounter = 12 * uiCache[player.userID].clanPage - 11;
                int counter = 1;
                foreach (var record in cachedData.OrderByDescending(x => x.Value.points).Skip(12 * uiCache[player.userID].clanPage - 12).Take(12))
                {
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Position");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, startCounter.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanName");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Key, 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Points");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.points.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Raids");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.raids.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Raided");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.raided.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_PointsGained");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.pointsGained.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_PointsLost");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.pointsLost.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanKills");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.clanKills.ToString(), 13));
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanDeaths");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.clanDeaths.ToString(), 13));
                    float kdr = record.Value.clanKills / (float)record.Value.clanDeaths;
                    sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanKdr");
                    elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, kdr.ToString("0.00"), 13));
                    counter++;
                    startCounter++;
                }
                bool hasNextPage = cachedData.Count - (12 * uiCache[player.userID].clanPage) > 0;
                if (uiCache[player.userID].clanPage > 1)
                {
                    elements.Add(cui.UpdateButton("ClanCoresMainUI_PrevPageButton", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, command: "UI_ClanScoreLead leaderboard prevPage", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    elements.Add(cui.UpdateText("ClanCoresMainUI_PrevPageButtonText", BetterColors.GreenText, "PREV", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
                else
                {
                    elements.Add(cui.UpdateButton("ClanCoresMainUI_PrevPageButton", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, command: "", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    elements.Add(cui.UpdateText("ClanCoresMainUI_PrevPageButtonText", BetterColors.LightGrayTransparent40, "PREV", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
                if (hasNextPage)
                {
                    elements.Add(cui.UpdateButton("ClanCoresMainUI_NextPageButton", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, command: "UI_ClanScoreLead leaderboard nextPage", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    elements.Add(cui.UpdateText("ClanCoresMainUI_NextPageButtonText", BetterColors.GreenText, "NEXT", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
                else
                {
                    elements.Add(cui.UpdateButton("ClanCoresMainUI_NextPageButton", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, command: "", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    elements.Add(cui.UpdateText("ClanCoresMainUI_NextPageButtonText", BetterColors.LightGrayTransparent40, "NEXT", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
                string clanTag = Clans.Call<string>("GetClanOf", player);
                if (clanTag == null || !cachedData.ContainsKey(clanTag))
                {
                    sb.Clear().Append("YOUR CLAN NAME\n<color=#B5DD6F>").Append("NONE").Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanName", BetterColors.LightGray, sb.ToString(), 27, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("RAIDS\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("RAIDED\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS GAINED\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsGainedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS LOST\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsLostTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("CLAN KILLS\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKillsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("CLAN DEATHS\n<color=#B5DD6F>").Append(0).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanDeathsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("CLAN KDR\n<color=#B5DD6F>").Append("NaN").Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKDRTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
                else if (cachedData.ContainsKey(clanTag))
                {
                    ClanStats data = cachedData[clanTag];
                    sb.Clear().Append("YOUR CLAN NAME\n<color=#B5DD6F>").Append(clanTag).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanName", BetterColors.LightGray, sb.ToString(), 27, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS\n<color=#B5DD6F>").Append(data.points).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("RAIDS\n<color=#B5DD6F>").Append(data.raids).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("RAIDED\n<color=#B5DD6F>").Append(data.raided).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS GAINED\n<color=#B5DD6F>").Append(data.pointsGained).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsGainedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("POINTS LOST\n<color=#B5DD6F>").Append(data.pointsLost).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsLostTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("CLAN KILLS\n<color=#B5DD6F>").Append(data.clanKills).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKillsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    sb.Clear().Append("CLAN DEATHS\n<color=#B5DD6F>").Append(data.clanDeaths).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanDeathsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                    string kdr = (data.clanKills / (float)data.clanDeaths).ToString("0.00");
                    sb.Clear().Append("CLAN KDR\n<color=#B5DD6F>").Append(kdr).Append("</color>");
                    elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKDRTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                }
            }
            else
            {
                sb.Clear().Append("HelpPage_").Append(uiCache[player.userID].lastPage);
                elements.Add(cui.UpdateText("ClanCoresMainUI_InfoText", BetterColors.LightGray, Lang(sb.ToString(), player.UserIDString), 15, align: TextAnchor.UpperLeft));
            }
            elements.Send(player);
            elements.Dispose();
            Pool.Free(ref sb);
        }
        private void InitialLeaderboardUpdate(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            StringBuilder sb = Pool.Get<StringBuilder>();
            if ((DateTime.Now - lastDataFetchTime).TotalMinutes > 5)
            {
                lastDataFetchTime = DateTime.Now;
                cachedData = JsonConvert.DeserializeObject<Dictionary<string, ClanStats>>(ClanScore.Call<string>("API_GetClanData"));
            }
            int startCounter = 12 * uiCache[player.userID].clanPage - 11;
            int counter = 1;
            foreach (var record in cachedData.OrderByDescending(x => x.Value.points).Skip(12 * uiCache[player.userID].clanPage - 12).Take(12))
            {
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Position");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, startCounter.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanName");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Key, 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Points");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.points.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Raids");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.raids.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_Raided");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.raided.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_PointsGained");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.pointsGained.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_PointsLost");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.pointsLost.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanKills");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.clanKills.ToString(), 13));
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanDeaths");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, record.Value.clanDeaths.ToString(), 13));
                float kdr = record.Value.clanKills / (float)record.Value.clanDeaths;
                sb.Clear().Append("ClanCoresMainUI_LeaderboardClan_").Append(counter).Append("_ClanKdr");
                elements.Add(cui.UpdateText(sb.ToString(), BetterColors.LightGray, kdr.ToString("0.00"), 13));
                counter++;
                startCounter++;
            }
            bool hasNextPage = cachedData.Count - (12 * uiCache[player.userID].clanPage) > 0;
            if (uiCache[player.userID].clanPage > 1)
            {
                elements.Add(cui.UpdateButton("ClanCoresMainUI_PrevPageButton", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, command: "UI_ClanScoreLeadFixed leaderboard prevPage", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                elements.Add(cui.UpdateText("ClanCoresMainUI_PrevPageButtonText", BetterColors.GreenText, "PREV", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            else
            {
                elements.Add(cui.UpdateButton("ClanCoresMainUI_PrevPageButton", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, command: "", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                elements.Add(cui.UpdateText("ClanCoresMainUI_PrevPageButtonText", BetterColors.LightGrayTransparent40, "PREV", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            if (hasNextPage)
            {
                elements.Add(cui.UpdateButton("ClanCoresMainUI_NextPageButton", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, command: "UI_ClanScoreLeadFixed leaderboard nextPage", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                elements.Add(cui.UpdateText("ClanCoresMainUI_NextPageButtonText", BetterColors.GreenText, "NEXT", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            else
            {
                elements.Add(cui.UpdateButton("ClanCoresMainUI_NextPageButton", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, command: "", font: CUI.Handler.FontTypes.RobotoCondensedBold));
                elements.Add(cui.UpdateText("ClanCoresMainUI_NextPageButtonText", BetterColors.LightGrayTransparent40, "NEXT", 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            string clanTag = Clans.Call<string>("GetClanOf", player);
            if (clanTag == null || !cachedData.ContainsKey(clanTag))
            {
                sb.Clear().Append("YOUR CLAN NAME\n<color=#B5DD6F>").Append("NONE").Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanName", BetterColors.LightGray, sb.ToString(), 27, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("RAIDS\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("RAIDED\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS GAINED\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsGainedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS LOST\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsLostTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("CLAN KILLS\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKillsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("CLAN DEATHS\n<color=#B5DD6F>").Append(0).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanDeathsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("CLAN KDR\n<color=#B5DD6F>").Append("NaN").Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKDRTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            else if (cachedData.ContainsKey(clanTag))
            {
                ClanStats data = cachedData[clanTag];
                sb.Clear().Append("YOUR CLAN NAME\n<color=#B5DD6F>").Append(clanTag).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanName", BetterColors.LightGray, sb.ToString(), 27, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS\n<color=#B5DD6F>").Append(data.points).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("RAIDS\n<color=#B5DD6F>").Append(data.raids).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("RAIDED\n<color=#B5DD6F>").Append(data.raided).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourRaidedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS GAINED\n<color=#B5DD6F>").Append(data.pointsGained).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsGainedTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("POINTS LOST\n<color=#B5DD6F>").Append(data.pointsLost).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourPointsLostTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("CLAN KILLS\n<color=#B5DD6F>").Append(data.clanKills).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKillsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                sb.Clear().Append("CLAN DEATHS\n<color=#B5DD6F>").Append(data.clanDeaths).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanDeathsTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                string kdr = (data.clanKills / (float)data.clanDeaths).ToString("0.00");
                sb.Clear().Append("CLAN KDR\n<color=#B5DD6F>").Append(kdr).Append("</color>");
                elements.Add(cui.UpdateText("ClanCoresMainUI_YourClanKDRTabText", BetterColors.LightGray, sb.ToString(), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            elements.Send(player);
            elements.Dispose();
            Pool.Free(ref sb);
        }

        private struct ClanStats
        {
            public int points;
            public int raids;
            public int raided;
            public int pointsGained;
            public int pointsLost;
            public int clanKills;
            public int clanDeaths;

        }

        private static void SendJson(BasePlayer player, string json) => CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.net.connection), null, "AddUI", json);

        private void RegisterMessages()
        {
            Dictionary<string, string> langFile = new Dictionary<string, string>();
            foreach (var page in config.pages)
                if (page.Key != "leaderboard")
                    langFile.TryAdd($"HelpPage_{page.Key}", $"THIS IS DEFAULT {page.Key} DESCRIPTION. CAN BE CHANGED IN LANG FILE!");
            lang.RegisterMessages(langFile, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static PluginConfig config = new PluginConfig();

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                commands = new List<string>() { "leader", "top" },
                pages = new Dictionary<string, string>() { { "info", "INFORMATIONS" }, { "test", "TEST PAGE" }, { "leaderboard", "LEADERBOARD" } }
            }, true);
        }

        private class PluginConfig
        {
            [JsonProperty("Commands")]
            public List<string> commands = new List<string>();

            [JsonProperty("List of Pages")]
            public Dictionary<string, string> pages = new Dictionary<string, string>();
        }

        private void AddImage(string url, string shortname, ulong skin = 0) => ImageLibrary?.CallHook("AddImage", url, shortname, skin);

        private string GetImage(string shortname, ulong skin = 0) => ImageLibrary?.Call<string>("GetImage", shortname, skin);
    }
}