using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Utils;

namespace NSMB.Entities.Collectable.Powerups {
    public class PowerupCollectBasic : MonoBehaviour, IPowerupCollect {

        public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {

            Enums.PowerupState newState = powerup.powerupScriptable.state;

            NetworkRunner runner = player.Runner;

            //reserve if it's the same item
            if (player.State == newState)
                return PowerupReserveResult.ReserveNewPowerup;

            //reserve if we cant fit with our new hitbox
            if (player.State == Enums.PowerupState.MiniMushroom && player.IsOnGround && runner.GetPhysicsScene2D().Raycast(player.body.position, Vector2.up, 0.3f, Layers.MaskSolidGround))
                return PowerupReserveResult.ReserveNewPowerup;

            Powerup currentPowerup = player.State.GetPowerupScriptable();
            Powerup newPowerup = powerup.powerupScriptable;

            sbyte currentPowerupStatePriority = currentPowerup ? currentPowerup.statePriority : (sbyte) -1;
            sbyte newPowerupItemPriority = newPowerup ? newPowerup.itemPriority : (sbyte) -1;

            //reserve if we have a higher priority item
            if (currentPowerupStatePriority > newPowerupItemPriority)
                return PowerupReserveResult.ReserveNewPowerup;

            player.PreviousState = player.State;
            player.State = newState;
            player.powerupFlash = 2;
            player.IsCrouching |= player.ForceCrouchCheck();
            player.IsPropellerFlying = false;
            player.UsedPropellerThisJump = false;
            player.IsDrilling &= player.IsSpinnerFlying;
            player.PropellerLaunchTimer = TickTimer.None;
            player.IsInShell = false;

            // dont give us an extra mushroom
            if (player.PreviousState == Enums.PowerupState.NoPowerup || (player.PreviousState == Enums.PowerupState.Mushroom && newState != Enums.PowerupState.Mushroom))
                return PowerupReserveResult.NoneButPlaySound;

            return PowerupReserveResult.ReserveOldPowerup;
        }
    }
}
