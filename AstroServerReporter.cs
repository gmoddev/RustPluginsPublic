using Carbon;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("AstroServerReporter", "Not_Lowest", "1.0.0")]
    public class AstroServerReporter : CarbonPlugin
    {
        private PluginConfig ConfigData;
        private string LastMapImageUrl;

        #region CONFIG

        public class PluginConfig
        {
            public string ApiBaseUrl = "https://your-api.com";
            public string ApiKey = "CHANGE_ME_SECURE_KEY";
            public string ServerId = "server-1";
            public float HeartbeatInterval = 60f; // seconds between live updates
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

        #region DATA MODELS

        private class ServerStartupPayload
        {
            public string serverId;
            public string serverName;
            public string ip;
            public int port;
            public string mapName;
            public int mapSize;
            public string mapSeed;
            public string levelUrl;        // custom map URL if applicable (Now deprecated)
            public int maxPlayers;
            //public string gameVersion; // Dont feel like implementing
            public string oxideVersion;
            public string description;
            public bool pve;
            public string[] tags;
            public long reportedAt;        // unix timestamp
        }

        private class HeartbeatPayload
        {
            public string serverId;
            public int playerCount;
            public int maxPlayers;
            public int sleepingCount;
            public float fps;
            public int entityCount;
            public long reportedAt;
            public string mapImage;
        }

        private class PlayerPayload
        {
            public string steamId;
            public string displayName;
            public string ipAddress;
            public string countryCode; 
            public long joinedAt;
        }

        private class DisconnectPayload
        {
            public string serverId;
            public string steamId;
            public string reason;
            public long leftAt;
        }

        #endregion

        #region INIT

        private bool _unloading;

        private void OnServerInitialized(bool initialized = default)
        {
            _unloading = false;
            ReportStartup();
            ScheduleHeartbeat();
        }

        private void ScheduleHeartbeat()
        {
            timer.Once(ConfigData.HeartbeatInterval, () =>
            {
                if (_unloading) return;
                ReportHeartbeat();
                ScheduleHeartbeat();
            });
        }

        private void Unload()
        {
            _unloading = true;
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

        #region STARTUP REPORT

        private void ReportStartup()
        {
            var serverInfo = ConVar.Server.hostname;
            var worldSize = ConVar.Server.worldsize;
            var worldSeed = ConVar.Server.seed.ToString();
            var mapName = World.Name ?? "Delinquent District";

            var payload = new ServerStartupPayload
            {
                serverId = ConfigData.ServerId,
                serverName = ConVar.Server.hostname,
                ip = ConVar.Server.ip,
                port = ConVar.Server.port,
                mapName = mapName,
                mapSize = worldSize,
                mapSeed = worldSeed,
                levelUrl = ConVar.Server.levelurl ?? "",
                maxPlayers = ConVar.Server.maxplayers,
                //gameVersion   = ConVar.Server.netgamever,
                oxideVersion = Version.ToString(),
                description = ConVar.Server.description ?? "",
                pve = ConVar.Server.pve,
                tags = (ConVar.Server.tags ?? "").Split(','),
                reportedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/startup",
                json,
                (code, response) =>
                {
                    if (code == 200)
                        Puts(" Startup report sent successfully.");
                    else
                        PrintWarning($" Startup report failed. HTTP {code}: {response}");
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );

            //RequestMapImage();
        }

        private void SendMapImageToApi(string url)
        {
            var payload = new
            {
                serverId = ConfigData.ServerId,
                mapImage = url
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/map-image",
                json,
                (code, response) =>
                {
                    if (code == 200)
                        Puts(" Map image sent to API.");
                    else
                        PrintWarning($" Map image send failed: {code}");
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }
        // Marked for deprecation
        private void RequestMapImage()
        {
            // The API can use the seed + size to generate/cache a map image
            // via sites like rustmaps.com or your own renderer.
            var payload = new
            {
                serverId = ConfigData.ServerId,
                seed = ConVar.Server.seed,
                size = ConVar.Server.worldsize,
                staging = ConVar.Server.branch == "staging"
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/request-map",
                json,
                (code, response) =>
                {
                    if (code == 200)
                        Puts(" Map image request sent.");
                    else
                        PrintWarning($" Map image request failed. HTTP {code}");
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        #endregion

        #region HEARTBEAT

        private void ReportHeartbeat()
        {
            var payload = new HeartbeatPayload
            {
                serverId = ConfigData.ServerId,
                playerCount = BasePlayer.activePlayerList.Count,
                maxPlayers = ConVar.Server.maxplayers,
                sleepingCount = BasePlayer.sleepingPlayerList.Count,
                fps = Performance.current.frameRate,
                entityCount = BaseNetworkable.serverEntities.Count,
                reportedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                mapImage = LastMapImageUrl
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/heartbeat",
                json,
                (code, response) =>
                {
                    if (code != 200)
                        PrintWarning($" Heartbeat failed. HTTP {code}");
                },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        #endregion

        #region MapTracking

        private void OnServerMessage(string message, string name, ulong id)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (message.Contains("[Rust.MapCache-Images] Image uploaded to backend:"))
            {
                var url = ExtractUrl(message);

                LastMapImageUrl = url;

                Puts($"Captured Map Image URL: {url}");

                // OPTIONAL: send immediately to your API
                SendMapImageToApi(url);
            }
        }

        private string ExtractUrl(string message)
        {
            var parts = message.Split(' ');
            return parts[parts.Length - 1];
        }

        #endregion

        #region PLAYER TRACKING

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            var payload = new
            {
                serverId = ConfigData.ServerId,
                player = new PlayerPayload
                {
                    steamId = player.UserIDString,
                    displayName = player.displayName,
                    ipAddress = player.net?.connection?.ipaddress?.Split(':')[0] ?? "unknown",
                    joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/player-join",
                json,
                (code, response) => { },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;

            var payload = new DisconnectPayload
            {
                serverId = ConfigData.ServerId,
                steamId = player.UserIDString,
                reason = reason ?? "unknown",
                leftAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                $"{ConfigData.ApiBaseUrl}/server/player-leave",
                json,
                (code, response) => { },
                this,
                RequestMethod.POST,
                GetHeaders()
            );
        }

        #endregion
    }
}