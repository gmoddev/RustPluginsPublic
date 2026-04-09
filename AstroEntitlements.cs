using Carbon;
using Carbon.Components;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("AstroEntitlements", "Not_Lowest", "3.4.0")]
    public class AstroEntitlements : CarbonPlugin
    {
        private PluginConfig ConfigData;

        #region CONFIG

        public class PluginConfig
        {
            public string ApiBaseUrl = "https://your-api.com";
            public string ApiKey = "CHANGE_ME_SECURE_KEY";
            public string LinkCommand = "link";
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

        #region DATA

        private class CheckResponse
        {
            public bool success;
            public bool linked;
            public string discordId;
        }

        private class GenerateResponse
        {
            public bool success;
            public bool alreadyLinked;
        }

        #endregion

        #region INIT

        private void Init()
        {
            AddCovalenceCommand(ConfigData.LinkCommand, nameof(LinkCommand));
        }

        private Dictionary<string, string> GetHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Authorization"] = $"Bearer {ConfigData.ApiKey}"
            };
        }

        #endregion

        #region LINK POLLING

        private Dictionary<ulong, int> ActiveLinkSessions = new();

        private void StartLinkPolling(BasePlayer player)
        {
            ulong id = player.userID;

            int sessionId = 1;
            if (ActiveLinkSessions.TryGetValue(id, out var existing))
                sessionId = existing + 1;

            ActiveLinkSessions[id] = sessionId;

            RunLinkPoll(player, sessionId, 0);
        }

        private void RunLinkPoll(BasePlayer player, int sessionId, int count)
        {
            float interval = 5f;
            int maxChecks = 36;

            timer.Once(interval, () =>
            {
                if (player == null || !player.IsConnected)
                {
                    StopLinkPolling(player?.userID ?? 0);
                    return;
                }

                if (!ActiveLinkSessions.TryGetValue(player.userID, out var activeSession) || activeSession != sessionId)
                    return;

                if (count >= maxChecks)
                {
                    player.ChatMessage("Link timed out. Please try again.");
                    DestroyUI(player);
                    StopLinkPolling(player.userID);
                    return;
                }

                CheckIfLinked(player, linked =>
                {
                    if (player == null || !player.IsConnected)
                    {
                        StopLinkPolling(player?.userID ?? 0);
                        return;
                    }

                    if (!ActiveLinkSessions.TryGetValue(player.userID, out var latestSession) || latestSession != sessionId)
                        return;

                    if (linked)
                    {
                        player.ChatMessage("✅ Successfully linked!");
                        DestroyUI(player);
                        StopLinkPolling(player.userID);
                        return;
                    }

                    RunLinkPoll(player, sessionId, count + 1);
                });
            });
        }

        private void StopLinkPolling(ulong id)
        {
            if (id == 0) return;
            ActiveLinkSessions.Remove(id);
        }

        private void CheckIfLinked(BasePlayer player, Action<bool> callback)
        {
            var payload = new { steamId = player.UserIDString };
            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/check-linked",
                json,
                (code, response) =>
                {
                    if (player == null || !player.IsConnected)
                    {
                        callback(false);
                        return;
                    }

                    if (code != 200 || string.IsNullOrEmpty(response))
                    {
                        callback(false);
                        return;
                    }

                    try
                    {
                        var result = JsonConvert.DeserializeObject<CheckResponse>(response);
                        callback(result != null && result.linked);
                    }
                    catch
                    {
                        callback(false);
                    }
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        #endregion

        #region LINK FLOW

        private void LinkCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            var payload = new { steamId = basePlayer.UserIDString };
            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/check-linked",
                json,
                (code, response) =>
                {
                    if (basePlayer == null || !basePlayer.IsConnected) return;

                    if (code != 200 || string.IsNullOrEmpty(response))
                    {
                        basePlayer.ChatMessage("Failed to check link status.");
                        return;
                    }

                    try
                    {
                        var result = JsonConvert.DeserializeObject<CheckResponse>(response);

                        if (result != null && result.linked)
                            ShowLinkedUI(basePlayer, result.discordId ?? "Unknown");
                        else
                            ShowLinkUI(basePlayer);
                    }
                    catch
                    {
                        basePlayer.ChatMessage("Invalid API response.");
                    }
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        #endregion

        #region UI

        private void ShowLinkUI(BasePlayer player)
        {
            DestroyUI(player);

            using CUI cui = new CUI(CuiHandler);

            var parent = cui.v2.CreateParent(
                CUI.ClientPanels.Overlay,
                new LuiPosition(0f, 0f, 1f, 1f),
                "LinkUI"
            );

            parent.AddCursor();

            var panel = cui.v2.CreatePanel(
                parent,
                new LuiPosition(0.38f, 0.38f, 0.62f, 0.66f),
                new LuiOffset(0, 0, 0, 0),
                "0 0 0 0.9",
                "LinkUI.Panel"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0f, 0.72f, 1f, 0.92f),
                new LuiOffset(0, 0, 0, 0),
                18, "1 1 1 1",
                "Link Your Account",
                TextAnchor.MiddleCenter,
                "LinkUI.Title"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0.08f, 0.46f, 0.92f, 0.66f),
                new LuiOffset(0, 0, 0, 0),
                14, "0.9 0.9 0.9 1",
                "Generate a code to link your account in Discord",
                TextAnchor.MiddleCenter,
                "LinkUI.Subtitle"
            );

            cui.v2.CreateButton(panel,
                new LuiPosition(0.2f, 0.28f, 0.8f, 0.42f),
                new LuiOffset(0, 0, 0, 0),
                "link.generate",
                "0.2 0.6 1 1",
                false,
                "LinkUI.GenerateButton"
            );

            cui.v2.CreateText("LinkUI.GenerateButton",
                new LuiPosition(0, 0, 1, 1),
                new LuiOffset(0, 0, 0, 0),
                14, "1 1 1 1",
                "Generate Code",
                TextAnchor.MiddleCenter,
                "LinkUI.GenerateButtonText"
            );

            cui.v2.CreateButton(panel,
                new LuiPosition(0.2f, 0.1f, 0.8f, 0.24f),
                new LuiOffset(0, 0, 0, 0),
                "link.close",
                "0.8 0.2 0.2 1",
                false,
                "LinkUI.CloseButton"
            );

            cui.v2.CreateText("LinkUI.CloseButton",
                new LuiPosition(0, 0, 1, 1),
                new LuiOffset(0, 0, 0, 0),
                14, "1 1 1 1",
                "Close",
                TextAnchor.MiddleCenter,
                "LinkUI.CloseButtonText"
            );

            cui.v2.SendUi(player);
        }

        private void ShowLinkedUI(BasePlayer player, string discordId)
        {
            DestroyUI(player);

            using CUI cui = new CUI(CuiHandler);

            var parent = cui.v2.CreateParent(
                CUI.ClientPanels.Overlay,
                new LuiPosition(0, 0, 1, 1),
                "LinkUI"
            );

            parent.AddCursor();

            var panel = cui.v2.CreatePanel(
                parent,
                new LuiPosition(0.38f, 0.4f, 0.62f, 0.62f),
                new LuiOffset(0, 0, 0, 0),
                "0 0 0 0.9",
                "LinkUI.Panel"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0, 0.65f, 1, 0.88f),
                new LuiOffset(0, 0, 0, 0),
                18, "1 1 1 1",
                "Already Linked",
                TextAnchor.MiddleCenter,
                "LinkUI.Title"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0.1f, 0.35f, 0.9f, 0.6f),
                new LuiOffset(0, 0, 0, 0),
                14, "0.9 0.9 0.9 1",
                $"Discord ID:\n{discordId}",
                TextAnchor.MiddleCenter,
                "LinkUI.DiscordId"
            );

            cui.v2.CreateButton(panel,
                new LuiPosition(0.3f, 0.1f, 0.7f, 0.24f),
                new LuiOffset(0, 0, 0, 0),
                "link.close",
                "0.8 0.2 0.2 1",
                false,
                "LinkUI.CloseButton"
            );

            cui.v2.CreateText("LinkUI.CloseButton",
                new LuiPosition(0, 0, 1, 1),
                new LuiOffset(0, 0, 0, 0),
                14, "1 1 1 1",
                "Close",
                TextAnchor.MiddleCenter,
                "LinkUI.CloseButtonText"
            );

            cui.v2.SendUi(player);
        }

        private void ShowCodeUI(BasePlayer player, string code)
        {
            DestroyUI(player);

            using CUI cui = new CUI(CuiHandler);

            var parent = cui.v2.CreateParent(
                CUI.ClientPanels.Overlay,
                new LuiPosition(0f, 0f, 1f, 1f),
                "LinkUI"
            );

            parent.AddCursor();

            var panel = cui.v2.CreatePanel(
                parent,
                new LuiPosition(0.38f, 0.4f, 0.62f, 0.62f),
                new LuiOffset(0, 0, 0, 0),
                "0 0 0 0.92",
                "LinkUI.Panel"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0, 0.68f, 1, 0.9f),
                new LuiOffset(0, 0, 0, 0),
                18, "1 1 1 1",
                "Your Link Code",
                TextAnchor.MiddleCenter,
                "LinkUI.Title"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0.1f, 0.38f, 0.9f, 0.63f),
                new LuiOffset(0, 0, 0, 0),
                24, "0.2 0.8 1 1",
                code,
                TextAnchor.MiddleCenter,
                "LinkUI.Code"
            );

            cui.v2.CreateText(panel,
                new LuiPosition(0.05f, 0.25f, 0.95f, 0.38f),
                new LuiOffset(0, 0, 0, 0),
                12, "0.7 0.7 0.7 1",
                "Enter this code in Discord to link your account",
                TextAnchor.MiddleCenter,
                "LinkUI.CodeHint"
            );

            cui.v2.CreateButton(panel,
                new LuiPosition(0.3f, 0.08f, 0.7f, 0.22f),
                new LuiOffset(0, 0, 0, 0),
                "link.close",
                "0.8 0.2 0.2 1",
                false,
                "LinkUI.CloseButton"
            );

            cui.v2.CreateText("LinkUI.CloseButton",
                new LuiPosition(0, 0, 1, 1),
                new LuiOffset(0, 0, 0, 0),
                14, "1 1 1 1",
                "Close",
                TextAnchor.MiddleCenter,
                "LinkUI.CloseButtonText"
            );

            cui.v2.SendUi(player);
        }

        #endregion

        #region BUTTONS

        [ConsoleCommand("link.generate")]
        private void Generate(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            string code = GenerateCode();

            var payload = new { steamId = player.UserIDString, code };
            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/generate-code",
                json,
                (httpCode, response) =>
            {
               if (player == null || !player.IsConnected) return;

                if (httpCode != 200)
                    {
                        player.ChatMessage("Failed to generate code. Try again.");
                        return;
                    }

                ShowCodeUI(player, code);
                StartLinkPolling(player);
           },
            this,
            RequestMethod.POST,
            GetHeaders()
        );
    }

        [ConsoleCommand("link.close")]
        private void Close(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            StopLinkPolling(player.userID);
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
                "LinkUI"
            );
        }

        private string GenerateCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rand = new System.Random();
            char[] result = new char[6];

            for (int i = 0; i < result.Length; i++)
                result[i] = chars[rand.Next(chars.Length)];

            return new string(result);
        }

        #endregion
    }
}