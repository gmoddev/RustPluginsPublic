using Carbon;
using Oxide.Core.Libraries.Covalence;

namespace Carbon.Plugins
{
    [Info("AstroViolationBypass", "Not_Lowest", "1.0.0")]
    public class AstroViolationBypass : CarbonPlugin
    {
        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null) return null;

            if (player.IsAdmin || player.IsDeveloper)
                return true;

            return null;
        }
    }
}