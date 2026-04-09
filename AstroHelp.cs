using Carbon;
using Carbon.Components;
using Oxide;
using Oxide.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using System.Security;

namespace Carbon.Plugins
{
    [Info("AstroHelp", "Not_Lowest", "1.0.0")]
    public class AstroHelp : CarbonPlugin
    {
        private PluginConfig ConfigData;

        #region CONFIG

        public class PluginConfig
        {
            public bool GuiOrTextBased = true;
            public List<string> HelpLines = new List<string>
            { }; // This has to be empty for some reason??? I wonder if its because I load config wrong.
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
                ConfigData.HelpLines = new List<string>(ConfigData.HelpLines);
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

        #region COMMANDS

        [ChatCommand("help")]
        private void CmdHelp(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (ConfigData.GuiOrTextBased)
                ShowHelpUI(player);
            else
            {
                foreach (var line in ConfigData.HelpLines)
                    player.ChatMessage(line);
            }
        }

        #endregion

        #region UI

        private const float LineHeight = 0.07f;
        private const float LineSpacing = 0.005f;

        private void ShowHelpUI(BasePlayer player)
        {
            DestroyUI(player);
            NextTick(() =>
            {
                using CUI cui = new CUI(CuiHandler);

                int lineCount = 0;
                foreach (var line in ConfigData.HelpLines)
                    if (!string.IsNullOrWhiteSpace(line)) lineCount++;

                int totalLines = ConfigData.HelpLines.Count;
                float contentH = lineCount * (LineHeight + LineSpacing);
                float panelH = contentH + 0.22f;
                float panelYMin = 0.5f - (panelH / 2f);
                float panelYMax = panelYMin + panelH;

                var parent = cui.v2.CreateParent(
                    CUI.ClientPanels.Overlay,
                    new LuiPosition(0f, 0f, 1f, 1f),
                    "HelpUI"
                );

                parent.AddCursor();

                var panel = cui.v2.CreatePanel(
                    parent,
                    new LuiPosition(0.35f, panelYMin, 0.65f, panelYMax),
                    new LuiOffset(0, 0, 0, 0),
                    "0 0 0 0.92",
                    "HelpUI.Panel"
                );

                cui.v2.CreateText(panel,
                    new LuiPosition(0f, 0.88f, 1f, 1f),
                    new LuiOffset(0, 0, 0, 0),
                    18, "1 1 1 1",
                    "Server Help",
                    TextAnchor.MiddleCenter,
                    "HelpUI.Title"
                );

                cui.v2.CreatePanel(panel,
                    new LuiPosition(0.05f, 0.855f, 0.95f, 0.862f),
                    new LuiOffset(0, 0, 0, 0),
                    "0.2 0.6 1 0.6",
                    "HelpUI.Divider"
                );

                float cursor = 0.84f;

                for (int i = 0; i < totalLines; i++)
                {
                    string line = ConfigData.HelpLines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue; // skip cursor advance too


                    float top = cursor;
                    float bottom = top - LineHeight;
                    cursor = bottom - LineSpacing;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    bool hasSplit = line.Contains(" -- ");
                    string left = hasSplit ? line.Split(new[] { " -- " }, System.StringSplitOptions.None)[0] : line;
                    string right = hasSplit ? " -- " + line.Split(new[] { " -- " }, System.StringSplitOptions.None)[1] : "";

                    cui.v2.CreateText(panel,
                        new LuiPosition(0.06f, bottom, hasSplit ? 0.38f : 0.94f, top),
                        new LuiOffset(0, 0, 0, 0),
                        13, "0.2 0.8 1 1",
                        left,
                        TextAnchor.MiddleLeft,
                        $"HelpUI.LineLeft{i}"
                    );

                    if (hasSplit)
                    {
                        cui.v2.CreateText(panel,
                            new LuiPosition(0.38f, bottom, 0.94f, top),
                            new LuiOffset(0, 0, 0, 0),
                            13, "0.85 0.85 0.85 1",
                            right,
                            TextAnchor.MiddleLeft,
                            $"HelpUI.LineRight{i}"
                        );
                    }
                }

                cui.v2.CreateButton(panel,
                    new LuiPosition(0.1f, 0.02f, 0.9f, 0.1f),
                    new LuiOffset(0, 0, 0, 0),
                    "help.close",
                    "0.8 0.2 0.2 1",
                    false,
                    "HelpUI.CloseButton"
                );

                cui.v2.CreateText("HelpUI.CloseButton",
                    new LuiPosition(0, 0, 1, 1),
                    new LuiOffset(0, 0, 0, 0),
                    14, "1 1 1 1",
                    "Close",
                    TextAnchor.MiddleCenter,
                    "HelpUI.CloseButtonText"
                );

                cui.v2.SendUi(player);
            });
        }

        #endregion

        #region BUTTONS

        [ConsoleCommand("help.close")]
        private void CloseHelp(ConsoleSystem.Arg arg)
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
                "HelpUI"
            );
        }
        #endregion
    }
}