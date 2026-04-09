using Carbon;
using System.Collections.Generic;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("AstroBarrelPickup", "Not_Lowest", "1.1.0")]
    public class AstroBarrelPickup : CarbonPlugin
    {
        private List<BarrelBreak> RecentBarrels = new();

        private class BarrelBreak
        {
            public Vector3 Position;
            public BasePlayer Player;
            public float Time;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            var prefab = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(prefab) || !prefab.Contains("barrel"))
                return;

            var player = info?.InitiatorPlayer;
            if (player == null) return;

            var weapon = GetWeapon(info);

            Puts($"{player.displayName} ({player.UserIDString}) broke {prefab} using {weapon}");

            RecentBarrels.Add(new BarrelBreak
            {
                Position = entity.transform.position,
                Player = player,
                Time = Time.realtimeSinceStartup
            });

            timer.Once(3f, CleanupOldEntries);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is DroppedItem dropped)) return;

            var item = dropped.item;
            if (item == null) return;

            if (item.parent != null) return;

            var dropPos = dropped.transform.position;

            for (int i = RecentBarrels.Count - 1; i >= 0; i--)
            {
                var barrel = RecentBarrels[i];

                if (Time.realtimeSinceStartup - barrel.Time > 3f)
                {
                    RecentBarrels.RemoveAt(i);
                    continue;
                }

                if (Vector3.Distance(dropPos, barrel.Position) > 3f)
                    continue;

                var player = barrel.Player;

                if (player != null && player.IsConnected && item != null && item.parent == null)
                {
                    if (item.amount <= 0)
                        return;

                    var newItem = ItemManager.Create(item.info, item.amount);

                    if (newItem != null)
                    {
                        player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
                    }

                    dropped.Kill();
                }
                else
                {
                    Puts("Failed: player invalid");
                }

                return;
            }
        }

        private string GetWeapon(HitInfo info)
        {
            if (info == null) return "unknown";

            var weapon = info.Weapon?.ShortPrefabName;
            if (!string.IsNullOrEmpty(weapon))
                return weapon;

            var prefab = info.WeaponPrefab?.ShortPrefabName;
            if (!string.IsNullOrEmpty(prefab))
                return prefab;

            return "unknown";
        }

        private void CleanupOldEntries()
        {
            var now = Time.realtimeSinceStartup;

            for (int i = RecentBarrels.Count - 1; i >= 0; i--)
            {
                if (now - RecentBarrels[i].Time > 3f)
                {
                    //Puts("Cleanup: expired barrel entry");
                    RecentBarrels.RemoveAt(i);
                }
            }
        }
    }
}