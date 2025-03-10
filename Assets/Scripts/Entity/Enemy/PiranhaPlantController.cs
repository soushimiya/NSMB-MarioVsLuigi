﻿using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {
    public class PiranhaPlantController : KillableEntity {

        //---Networked Variables
        [Networked] public TickTimer PopupCountdownTimer { get; set; }
        [Networked] public TickTimer ChompTimer { get; set; }
        [Networked] public float PopupAnimationTime { get; set; }

        //---Serialized Variables
        [SerializeField] private Transform interpolationTarget;
        [SerializeField] private float playerDetectSize = 1;
        [SerializeField] private float popupTimerRequirement = 6f, popupDistance = 0.5f;
        [SerializeField] private float popupTime = 0.5f, chompTime = 2f;

        //---Private Variables
        private Interpolator<float> popupAnimationTimeInterpolator;

        public override void Spawned() {
            base.Spawned();
            PopupCountdownTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
            popupAnimationTimeInterpolator = GetInterpolator<float>(nameof(PopupAnimationTime));
        }

        public override void Render() {
            if (IsFrozen)
                return;

            interpolationTarget.localPosition = new(0, (popupAnimationTimeInterpolator.Value - 1) * popupDistance, 0);
            animator.SetBool("active", ChompTimer.IsRunning);
            animator.SetBool("chomping", PopupAnimationTime > 0.99f);
            sRenderer.enabled = !IsDead;
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            GameData gm = GameData.Instance;
            if (gm) {
                if (gm.GameEnded) {
                    animator.enabled = false;
                    return;
                }

                if (!gm.IsMusicEnabled)
                    return;
            }

            if (IsDead) {
                hitbox.enabled = false;
                PopupAnimationTime = 0;
                ChompTimer = TickTimer.None;
                return;
            }

            if (!Utils.Utils.GetTileAtWorldLocation(transform.position + (Vector3.down * 0.1f))) {
                // No tile at our origin, so our pipe was destroyed.
                Kill();
                return;
            }

            if (IsFrozen)
                return;

            bool chomping = ChompTimer.IsRunning;
            if (chomping) {
                // Currently chomping.
                if (ChompTimer.Expired(Runner)) {
                    // End chomping
                    PopupCountdownTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
                    ChompTimer = TickTimer.None;
                }
            } else {
                // Not chomping, run the countdown timer.
                if (PopupCountdownTimer.Expired(Runner)) {
                    Collider2D closePlayer = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, playerDetectSize, Layers.MaskOnlyPlayers);
                    if (!closePlayer) {
                        // No players nearby. pop up.
                        ChompTimer = TickTimer.CreateFromSeconds(Runner, chompTime);
                        PopupCountdownTimer = TickTimer.None;
                    }
                }
            }

            float change = (1f / popupTime) * Runner.DeltaTime * (chomping ? 1 : -1);
            PopupAnimationTime = Mathf.Clamp01(PopupAnimationTime + change);
            hitbox.enabled = PopupAnimationTime >= 0.01f;
            hitbox.transform.localPosition = new(0, (PopupAnimationTime - 1) * popupDistance, 0);
        }

        public void PlayChompSound() {
            PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Chomp);
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player) {
            // Don't use player.InstakillsEnemies as we don't want sliding to kill us.
            if (player.IsStarmanInvincible || player.IsInShell || player.State == Enums.PowerupState.MegaMushroom) {
                Kill();
            } else {
                player.Powerdown(false);
            }
        }

        //---KillableEntity overrides
        public override void RespawnEntity() {
            if (!IsDead)
                return;

            IsActive = true;
            IsDead = false;
            IsFrozen = false;
            FacingRight = false;
            WasSpecialKilled = false;
            WasGroundpounded = false;
            ComboCounter = 0;

            PopupCountdownTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
        }

        public override void Kill() {
            IsDead = true;
            Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, transform.position + Vector3.up);
        }

        public override void SpecialKill(bool right, bool groundpound, int combo) {
            Kill();
        }

        public override bool InteractWithIceball(FireballMover iceball) {
            if (IsDead)
                return false;

            if (!IsFrozen) {
                Runner.Spawn(PrefabList.Instance.Obj_FrozenCube, transform.position, onBeforeSpawned: (runner, obj) => {
                    FrozenCube cube = obj.GetComponent<FrozenCube>();
                    cube.OnBeforeSpawned(this);
                });
            }
            return true;
        }

        public override void OnIsDeadChanged() {
            if (IsDead && GameData.Instance.GameState == Enums.GameState.Playing) {
                PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Death);
                PlaySound(IsFrozen ? Enums.Sounds.Enemy_Generic_FreezeShatter : Enums.Sounds.Enemy_Shell_Kick);
                GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, transform.position + Vector3.up * 0.5f);
            }

            sRenderer.enabled = !IsDead;
        }

#if UNITY_EDITOR
        //---Debug
        public void OnDrawGizmosSelected() {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawSphere(transform.position + (Vector3) (playerDetectSize * 0.5f * Vector2.up), playerDetectSize);
        }
#endif
    }
}
