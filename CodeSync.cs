
using System.Collections.Generic;
using System.Security;

namespace Oxide.Plugins
{
    [Info("Code Sync", "Wulf & Low", "2.0.5")]
    [Description("Automatically allows players access to code locks based on tool cupboard authorization + team")]
    public class CodeSync : CovalencePlugin
    {

        #region Lock Handling

        private BuildingManager.Building GetBuilding(BaseEntity entity)
        {
            var parent = entity.GetParentEntity();

            if (parent == null)
                return null;

            BuildingManager.Building building;

            if (parent.prefabID == 4211374971 || parent.prefabID == 95147612)
                building = parent.GetBuildingPrivilege()?.GetBuilding();
            else
                building = (parent as DecayEntity)?.GetBuilding();

            if (building == null || building.buildingPrivileges == null || building.buildingPrivileges.Count == 0)
                return null;

            return building;
        }

        private CodeLock GetCupboardLock(BuildingManager.Building building)
        {
            return building.buildingPrivileges[0].GetSlot(BaseEntity.Slot.Lock) as CodeLock;
        }

        private object CanUseLockedEntity(BasePlayer basePlayer, CodeLock codeLock)
        {
            if (!codeLock.IsLocked())
                return null;
            var building = GetBuilding(codeLock);
            if (building == null)
                return null;
            var tc = building.buildingPrivileges[0];
            var tcLock = GetCupboardLock(building);

            if (tcLock == null)
                return null;
            if (tcLock.whitelistPlayers.Contains(basePlayer.userID) || tcLock.guestPlayers.Contains(basePlayer.userID))
                return true;
            var authList = tc.authorizedPlayers;
            if (authList == null || authList.Count == 0)
                return null;

            foreach (var authId in authList)
            {
                var team = RelationshipManager.ServerInstance.FindPlayersTeam(authId);
                if (team == null) continue;

                if (team.members.Contains(basePlayer.userID))
                    return true;
            }

            return null;
        }

        #endregion

    }
}