using Carbon;
using Carbon.Components;
using Oxide;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("AstroWelcome", "Not_Lowest", "1.0.0")]
    public class AstroWelcome : CarbonPlugin
    {
        #region CONFIG

        private PluginConfig ConfigData;

        public class PluginConfig
        {
            public string ServerName = "Delinquent District";
            public string Description = "Welcome to the server! Please read the rules and have fun.";
            public List<string> Tips = new List<string>
            {
            };
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                ConfigData = Config.ReadObject<PluginConfig>();
            }
            catch
            {
                PrintWarning("Config corrupted, regenerating...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(ConfigData, true);
        }

        #endregion

        #region INIT

        private void Init()
        {
            AddCovalenceCommand("AstroWelcome.Welcome", nameof(WelcomeCommand));
        }

        #endregion

        #region HOOKS

        private HashSet<ulong> _shownOnConnect = new HashSet<ulong>(); // Per Server

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (_shownOnConnect.Contains((ulong)player.userID)) return;

            _shownOnConnect.Add((ulong)player.userID);

            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected) return;
                ShowWelcomeUI(player);
            });
        }

        #endregion

        #region COMMAND

        [ChatCommand("Welcome"), Cooldown(5_000)]
        private void WelcomeCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            ShowWelcomeUI(player);
        }

        #endregion

        #region UI

        private const string UIName = "WelcomeUI";

        private void ShowWelcomeUI(BasePlayer player)
        {
            DestroyUI(player);

            using CUI cui = new CUI(CuiHandler);

            // Fullscreen overlay
            var parent = cui.v2.CreateParent(
                CUI.ClientPanels.Overlay,
                new LuiPosition(0f, 0f, 1f, 1f),
                UIName
            );

            parent.AddCursor();

            // Dark backdrop
            cui.v2.CreatePanel(
                parent,
                new LuiPosition(0f, 0f, 1f, 1f),
                new LuiOffset(0, 0, 0, 0),
                "0 0 0 0.6",
                "WelcomeUI.Backdrop"
            );

            // Main panel — centered card
            var panel = cui.v2.CreatePanel(
                parent,
                new LuiPosition(0.3f, 0.15f, 0.7f, 0.88f),
                new LuiOffset(0, 0, 0, 0),
                "0.08 0.08 0.08 0.97",
                "WelcomeUI.Panel"
            );

            // Top accent bar
            cui.v2.CreatePanel(
                panel,
                new LuiPosition(0f, 0.92f, 1f, 1f),
                new LuiOffset(0, 0, 0, 0),
                "0.2 0.6 1 1",
                "WelcomeUI.AccentBar"
            );

            // Server name in accent bar
            cui.v2.CreateText(
                "WelcomeUI.AccentBar",
                new LuiPosition(0f, 0f, 1f, 1f),
                new LuiOffset(0, 0, 0, 0),
                18, "1 1 1 1",
                ConfigData.ServerName,
                TextAnchor.MiddleCenter,
                "WelcomeUI.ServerName"
            );

            // Description
            cui.v2.CreateText(
                panel,
                new LuiPosition(0.05f, 0.76f, 0.95f, 0.91f),
                new LuiOffset(0, 0, 0, 0),
                13, "0.85 0.85 0.85 1",
                ConfigData.Description,
                TextAnchor.MiddleCenter,
                "WelcomeUI.Description"
            );

            // Divider
            cui.v2.CreatePanel(
                panel,
                new LuiPosition(0.05f, 0.745f, 0.95f, 0.752f),
                new LuiOffset(0, 0, 0, 0),
                "0.2 0.6 1 0.4",
                "WelcomeUI.Divider"
            );

            // Tips header
            cui.v2.CreateText(
                panel,
                new LuiPosition(0.05f, 0.68f, 0.95f, 0.74f),
                new LuiOffset(0, 0, 0, 0),
                13, "0.2 0.6 1 1",
                "— SERVER TIPS —",
                TextAnchor.MiddleCenter,
                "WelcomeUI.TipsHeader"
            );

            // Tips list — evenly space up to 8 tips
            int maxTips = Mathf.Min(ConfigData.Tips.Count, 8);
            float tipsAreaTop = 0.66f;
            float tipsAreaBottom = 0.12f;
            float slotHeight = (tipsAreaTop - tipsAreaBottom) / Mathf.Max(maxTips, 1);

            for (int i = 0; i < maxTips; i++)
            {
                float yMax = tipsAreaTop - (i * slotHeight);
                float yMin = yMax - slotHeight;

                // Bullet dot
                cui.v2.CreatePanel(
                    panel,
                    new LuiPosition(0.05f, yMin + (slotHeight * 0.38f), 0.075f, yMin + (slotHeight * 0.62f)),
                    new LuiOffset(0, 0, 0, 0),
                    "0.2 0.6 1 0.8",
                    $"WelcomeUI.Bullet{i}"
                );

                // Tip text
                cui.v2.CreateText(
                    panel,
                    new LuiPosition(0.09f, yMin, 0.97f, yMax),
                    new LuiOffset(0, 0, 0, 0),
                    12, "0.9 0.9 0.9 1",
                    ConfigData.Tips[i],
                    TextAnchor.MiddleLeft,
                    $"WelcomeUI.Tip{i}"
                );
            }

            // Close button
            cui.v2.CreateButton(
                panel,
                new LuiPosition(0.2f, 0.03f, 0.8f, 0.1f),
                new LuiOffset(0, 0, 0, 0),
                "astrowelcome.close",
                "0.2 0.6 1 1",
                false,
                "WelcomeUI.CloseButton"
            );

            cui.v2.CreateText(
                "WelcomeUI.CloseButton",
                new LuiPosition(0f, 0f, 1f, 1f),
                new LuiOffset(0, 0, 0, 0),
                14, "1 1 1 1",
                "Close",
                TextAnchor.MiddleCenter,
                "WelcomeUI.CloseText"
            );

            cui.v2.SendUi(player);
        }

        #endregion

        #region BUTTONS

        [ConsoleCommand("astrowelcome.close")]
        private void CloseCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            DestroyUI(player);
        }

        #endregion

        #region UTIL

        private void DestroyUI(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            var connection = player.net?.connection;
            if (connection == null) return;

            CommunityEntity.ServerInstance.ClientRPC(
                RpcTarget.Player("DestroyUI", connection),
                UIName
            );
        }

        #endregion
    }
}