using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities.Player {
    [OrderAfter(typeof(PlayerController))]
    public class PlayerAnimationController : NetworkBehaviour {

        //---Static Variables
        private static readonly WaitForSeconds BlinkDelay = new(0.1f);
        private static readonly Vector3 ZeroPointFive = new(0.5f, 0.5f, 0.5f);

        //---Public Variables
        public bool deathUp, wasTurnaround, enableGlow;
        public GameObject models;

        //---Serialized Variables
        [SerializeField] private Avatar smallAvatar, largeAvatar;
        [SerializeField] private ParticleSystem dust, sparkles, drillParticle, giantParticle, fireParticle, bubblesParticle;
        [SerializeField] private GameObject smallModel, largeModel, largeShellExclude, blueShell, propellerHelmet, propeller;
        [SerializeField] public float pipeDuration = 2f, deathUpTime = 0.6f, deathForce = 7f;
        [SerializeField] private AudioClip normalDrill, propellerDrill;
        [SerializeField] private LoopingSoundPlayer dustPlayer, drillPlayer;
        [SerializeField] private LoopingSoundData wallSlideData, shellSlideData, spinnerDrillData, propellerDrillData;

        //---Components
        private readonly List<Renderer> renderers = new();
        private PlayerController controller;
        private Animator animator;
        private Rigidbody2D body;
        private MaterialPropertyBlock materialBlock;
        private AudioSource drillParticleAudio;

        //---Properties
        public Color GlowColor { get; set; }

        //---Private Variables
        private Enums.PlayerEyeState eyeState;
        private float propellerVelocity;
        private Vector3 modelRotationTarget;
        private bool modelRotateInstantly;
        private Coroutine blinkRoutine;
        private PlayerColors skin;

        public void Awake() {
            controller = GetComponent<PlayerController>();
            animator = GetComponent<Animator>();
            body = GetComponent<Rigidbody2D>();
            drillParticleAudio = drillParticle.GetComponent<AudioSource>();
        }

        public override void Spawned() {

            DisableAllModels();

            if (!controller.Object.HasInputAuthority)
                GameManager.Instance.CreateNametag(controller);

            PlayerData data = Object.InputAuthority.GetPlayerData(Runner);

            if (ScriptableManager.Instance.skins[data ? data.SkinIndex : 0] is PlayerColorSet colorSet) {
                skin = colorSet.GetPlayerColors(controller.character);
            }

            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

            modelRotationTarget = models.transform.rotation.eulerAngles;

            enableGlow = SessionData.Instance.Teams || !Object.HasInputAuthority;
            GlowColor = Utils.Utils.GetPlayerColor(Runner, controller.Object.InputAuthority);

            if (blinkRoutine == null)
                blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        public override void Render() {
            if (GameData.Instance.GameStartTimer.IsRunning) {
                DisableAllModels();
                return;
            }

            UpdateAnimatorVariables();
            HandleAnimations();
            SetFacingDirection();
            InterpolateFacingDirection();
            HandleMiscStates();
        }

        public override void FixedUpdateNetwork() {
            HandleMegaScale();
        }

        private void HandleMegaScale() {
            if (controller.GiantEndTimer.IsActive(Runner)) {
                transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, (controller.GiantEndTimer.RemainingTime(Runner) ?? 0f) / (controller.giantStartTime * 0.5f)) * 2.6f));
            } else {
                transform.localScale = controller.State switch {
                    Enums.PowerupState.MiniMushroom => ZeroPointFive,
                    Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - ((controller.GiantStartTimer.RemainingTime(Runner) ?? 0f) / controller.giantStartTime)) * 2.6f)),
                    _ => Vector3.one,
                };
            }
        }

        public void HandleAnimations() {
            if (GameData.Instance.GameEnded) {
                models.SetActive(true);

                // Disable Particles
                SetParticleEmission(drillParticle, false);
                SetParticleEmission(sparkles, false);
                SetParticleEmission(dust, false);
                SetParticleEmission(giantParticle, false);
                SetParticleEmission(fireParticle, false);
                SetParticleEmission(bubblesParticle, false);
                return;
            }

            float deathTimer = 3f - (controller.PreRespawnTimer.RemainingTime(Runner) ?? 0f);

            // Particles
            SetParticleEmission(drillParticle, !controller.IsDead && controller.IsDrilling);
            SetParticleEmission(sparkles, !controller.IsDead && controller.IsStarmanInvincible);
            SetParticleEmission(dust, !controller.IsDead && (controller.WallSlideLeft || controller.WallSlideRight || (controller.IsOnGround && (controller.IsSkidding || (controller.IsCrouching && body.velocity.sqrMagnitude > 0.25f))) || (((controller.IsSliding && body.velocity.sqrMagnitude > 0.25f) || controller.IsInShell) && controller.IsOnGround)) && !controller.CurrentPipe);
            SetParticleEmission(giantParticle, !controller.IsDead && controller.State == Enums.PowerupState.MegaMushroom && controller.GiantStartTimer.ExpiredOrNotRunning(Runner));
            SetParticleEmission(fireParticle, !controller.IsRespawning && controller.FireDeath && controller.IsDead && deathTimer > deathUpTime);
            SetParticleEmission(bubblesParticle, controller.IsSwimming);

            if (controller.IsDrilling)
                drillParticleAudio.clip = controller.State == Enums.PowerupState.PropellerMushroom ? propellerDrill : normalDrill;

            if (controller.IsCrouching || controller.IsSliding || controller.IsSkidding) {
                dust.transform.localPosition = Vector2.zero;
            } else if (controller.WallSlideLeft || controller.WallSlideRight) {
                dust.transform.localPosition = new Vector2(controller.MainHitbox.size.x * 0.75f * (controller.WallSlideLeft ? -1 : 1), controller.MainHitbox.size.y * 0.75f);
            }

            dustPlayer.SetSoundData((controller.IsInShell || controller.IsSliding || controller.IsCrouchedInShell) ? shellSlideData : wallSlideData);
            drillPlayer.SetSoundData(controller.State == Enums.PowerupState.PropellerMushroom ? propellerDrillData : spinnerDrillData);

            bubblesParticle.transform.localPosition = new(bubblesParticle.transform.localPosition.x, controller.WorldHitboxSize.y);

            if (controller.cameraController.IsControllingCamera)
                HorizontalCamera.SizeIncreaseTarget = (controller.IsSpinnerFlying || controller.IsPropellerFlying) ? 0.5f : 0f;
        }

        private IEnumerator BlinkRoutine() {
            while (true) {
                yield return new WaitForSeconds(3f + (Random.value * 6f));
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.FullBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }

        private void SetFacingDirection() {

            //TODO: refactor

            if (GameData.Instance.GameEnded || controller.IsFrozen)
                return;

            //rotChangeTarget = models.transform.rotation.eulerAngles;
            float delta = Time.deltaTime;
            float deathTimer = 3f - (controller.PreRespawnTimer.RemainingTime(Runner) ?? 0f);

            bool rotChangeFacingDirection = false;
            modelRotateInstantly = false;

            if (controller.IsInKnockback) {
                modelRotationTarget.Set(0, controller.FacingRight ? 110 : 250, 0);
                modelRotateInstantly = true;

            } else if (controller.IsDead) {
                if (animator.GetBool("firedeath") && deathTimer > deathUpTime) {
                    modelRotationTarget.Set(-15, controller.FacingRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, 180, 0);
                }
                modelRotateInstantly = true;

            } else if (animator.GetBool("inShell") && (!controller.OnSpinner || Mathf.Abs(body.velocity.x) > 0.3f)) {
                modelRotationTarget += Mathf.Abs(body.velocity.x) / controller.RunningMaxSpeed * delta * new Vector3(0, 1400 * (controller.FacingRight ? -1 : 1));
                modelRotateInstantly = true;

            } else if (wasTurnaround || controller.IsSkidding || controller.IsTurnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                bool flip = controller.FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.IsSkidding);
                modelRotationTarget.Set(0, flip ? 250 : 110, 0);
                modelRotateInstantly = true;

            } else {
                if (controller.OnSpinner && controller.IsOnGround && Mathf.Abs(body.velocity.x) < 0.3f && !controller.HeldEntity) {
                    modelRotationTarget += controller.OnSpinner.spinSpeed * delta * Vector3.up;
                    modelRotateInstantly = true;
                    rotChangeFacingDirection = true;
                } else if (controller.IsSpinnerFlying || controller.IsPropellerFlying) {
                    modelRotationTarget += new Vector3(0, -1200 - ((controller.PropellerLaunchTimer.RemainingTime(Runner) ?? 0f) * 1400) - (controller.IsDrilling ? 900 : 0) + (controller.IsPropellerFlying && controller.PropellerSpinTimer.ExpiredOrNotRunning(Runner) && body.velocity.y < 0 ? 700 : 0), 0) * delta;
                    modelRotateInstantly = true;
                } else if (controller.WallSlideLeft || controller.WallSlideRight) {
                    modelRotationTarget.Set(0, controller.WallSlideRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, controller.FacingRight ? 110 : 250, 0);
                }
            }

            propellerVelocity = Mathf.Clamp(propellerVelocity + (1200 * ((controller.IsSpinnerFlying || controller.IsPropellerFlying || controller.UsedPropellerThisJump) ? -1 : 1) * delta), -2500, -300);

            if (rotChangeFacingDirection)
                controller.FacingRight = models.transform.eulerAngles.y < 180;

            wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
        }

        private void InterpolateFacingDirection() {
            if (GameData.Instance.GameEnded || controller.IsFrozen)
                return;

            propeller.transform.Rotate(Vector3.forward, propellerVelocity * Time.deltaTime);

            if (modelRotateInstantly || wasTurnaround) {
                models.transform.rotation = Quaternion.Euler(modelRotationTarget);
            } else {
                float maxRotation = 2000f * Time.deltaTime;
                float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                x += Mathf.Clamp(modelRotationTarget.x - x, -maxRotation, maxRotation);
                y += Mathf.Clamp(modelRotationTarget.y - y, -maxRotation, maxRotation);
                z += Mathf.Clamp(modelRotationTarget.z - z, -maxRotation, maxRotation);
                models.transform.rotation = Quaternion.Euler(x, y, z);
            }
        }

        private void SetParticleEmission(ParticleSystem particle, bool value) {
            if (value) {
                if (particle.isStopped)
                    particle.Play();
            } else {
                if (particle.isPlaying)
                    particle.Stop();
            }
        }

        public void UpdateAnimatorVariables() {

            bool right = controller.PreviousInputs.buttons.IsSet(PlayerControls.Right);
            bool left = controller.PreviousInputs.buttons.IsSet(PlayerControls.Left);

            animator.SetBool("onLeft", controller.WallSlideLeft);
            animator.SetBool("onRight", controller.WallSlideRight);
            animator.SetBool("onGround", controller.IsOnGround || controller.IsStuckInBlock || (Runner.SimulationTime <= controller.CoyoteTime - 0.05f));
            animator.SetBool("invincible", controller.IsStarmanInvincible);
            animator.SetBool("skidding", controller.IsSkidding);
            animator.SetBool("propeller", controller.IsPropellerFlying);
            animator.SetBool("propellerSpin", controller.PropellerSpinTimer.IsActive(Runner));
            animator.SetBool("propellerStart", controller.PropellerLaunchTimer.IsActive(Runner));
            animator.SetBool("crouching", controller.IsCrouching);
            animator.SetBool("groundpound", controller.IsGroundpounding);
            animator.SetBool("sliding", controller.IsSliding);
            animator.SetBool("knockback", controller.IsInKnockback);
            animator.SetBool("facingRight", (left ^ right) ? right : controller.FacingRight);
            animator.SetBool("flying", controller.IsSpinnerFlying);
            animator.SetBool("drill", controller.IsDrilling);
            animator.SetFloat("velocityY", body.velocity.y);
            animator.SetBool("doublejump", controller.ProperJump && controller.JumpState == PlayerController.PlayerJumpState.DoubleJump);
            animator.SetBool("triplejump", controller.ProperJump && controller.JumpState == PlayerController.PlayerJumpState.TripleJump);
            animator.SetBool("holding", controller.HeldEntity);
            animator.SetBool("head carry", controller.HeldEntity && controller.HeldEntity is FrozenCube);
            animator.SetBool("carry_start", controller.HeldEntity && controller.HeldEntity is FrozenCube && (Runner.SimulationTime - controller.HoldStartTime) < controller.pickupTime);
            animator.SetBool("pipe", controller.CurrentPipe);
            animator.SetBool("blueshell", controller.State == Enums.PowerupState.BlueShell);
            animator.SetBool("mini", controller.State == Enums.PowerupState.MiniMushroom);
            animator.SetBool("mega", controller.State == Enums.PowerupState.MegaMushroom);
            animator.SetBool("inShell", controller.IsInShell || (controller.State == Enums.PowerupState.BlueShell && (controller.IsCrouching || controller.IsGroundpounding || controller.IsSliding) && (controller.GroundpoundStartTimer.RemainingTime(Runner) ?? 0f) <= 0.15f));
            animator.SetBool("turnaround", controller.IsTurnaround);
            animator.SetBool("swimming", controller.IsSwimming && !controller.IsGroundpounding && !controller.IsDrilling);
            animator.SetBool("a_held", controller.PreviousInputs.buttons.IsSet(PlayerControls.Jump));
            animator.SetBool("fireballKnockback", controller.IsWeakKnockback);
            animator.SetBool("knockforwards", controller.IsForwardsKnockback);

            float animatedVelocity = body.velocity.magnitude;
            if (controller.IsStuckInBlock) {
                animatedVelocity = 0;
            } else if (controller.IsPropellerFlying) {
                animatedVelocity = 2f;
            } else if (controller.State == Enums.PowerupState.MegaMushroom && (left || right)) {
                animatedVelocity = 4.5f;
            } else if (left ^ right && !controller.hitRight && !controller.hitLeft) {
                animatedVelocity = Mathf.Max(controller.OnIce ? 2.7f : 2f, animatedVelocity);
            } else if (controller.OnIce) {
                animatedVelocity = 0;
            }
            animator.SetFloat("velocityX", animatedVelocity);
        }

        private void HandleMiscStates() {
            if (controller.GiantEndTimer.IsActive(Runner)) {
                transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, (controller.GiantEndTimer.RemainingRenderTime(Runner) ?? 0f) / (controller.giantStartTime * 0.5f)) * 2.6f));
            } else {
                transform.localScale = controller.State switch {
                    Enums.PowerupState.MiniMushroom => ZeroPointFive,
                    Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - ((controller.GiantStartTimer.RemainingRenderTime(Runner) ?? 0f) / controller.giantStartTime)) * 2.6f)),
                    _ => Vector3.one,
                };
            }

            //Shader effects
            if (materialBlock == null) {
                materialBlock = new();

                // Customizable player color
                materialBlock.SetVector("OverallsColor", skin?.overallsColor.linear ?? Color.clear);
                materialBlock.SetVector("ShirtColor", skin?.shirtColor != null ? skin.shirtColor.linear : Color.clear);
                materialBlock.SetFloat("HatUsesOverallsColor", (skin?.hatUsesOverallsColor ?? false) ? 1 : 0);

                if (enableGlow)
                    materialBlock.SetColor("GlowColor", GlowColor);
            }

            materialBlock.SetFloat("RainbowEnabled", controller.IsStarmanInvincible ? 1f : 0f);
            int ps = controller.State switch {
                Enums.PowerupState.FireFlower => 1,
                Enums.PowerupState.PropellerMushroom => 2,
                Enums.PowerupState.IceFlower => 3,
                _ => 0
            };
            materialBlock.SetFloat("PowerupState", ps);
            materialBlock.SetFloat("EyeState", (int) (controller.IsDead ? Enums.PlayerEyeState.Death : eyeState));
            materialBlock.SetFloat("ModelScale", transform.lossyScale.x);

            Vector3 giantMultiply = Vector3.one;
            float giantTimeRemaining = controller.GiantTimer.RemainingTime(Runner) ?? 0f;
            if (controller.State == Enums.PowerupState.MegaMushroom && controller.GiantTimer.IsRunning && giantTimeRemaining < 4) {
                float v = ((Mathf.Sin(giantTimeRemaining * 20f) + 1f) * 0.45f) + 0.1f;
                giantMultiply = new Vector3(v, 1, v);
            }
            materialBlock.SetVector("MultiplyColor", giantMultiply);

            foreach (Renderer r in renderers)
                r.SetPropertyBlock(materialBlock);

            //hit flash
            float remainingDamageInvincibility = controller.DamageInvincibilityTimer.RemainingRenderTime(Runner) ?? 0f;
            models.SetActive(!controller.IsRespawning && (GameData.Instance.GameEnded || controller.IsDead || !(remainingDamageInvincibility > 0 && remainingDamageInvincibility * (remainingDamageInvincibility <= 0.75f ? 5 : 2) % 0.2f < 0.1f)));

            //Model changing
            bool large = controller.State >= Enums.PowerupState.Mushroom;

            largeModel.SetActive(large);
            smallModel.SetActive(!large);
            blueShell.SetActive(controller.State == Enums.PowerupState.BlueShell);

            largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
            propellerHelmet.SetActive(controller.State == Enums.PowerupState.PropellerMushroom);
            animator.avatar = large ? largeAvatar : smallAvatar;
            animator.runtimeAnimatorController = large ? controller.character.largeOverrides : controller.character.smallOverrides;


            float newZ = -4;
            if (controller.IsDead)
                newZ = -6;
            else if (controller.CurrentPipe)
                newZ = 1;
            else if (controller.FrozenCube)
                newZ = 3;

            transform.position = new(transform.position.x, transform.position.y, newZ);
        }

        public void HandleDeathAnimation() {
            if (!controller.IsDead || controller.IsRespawning)
                return;

            float deathTimer = 3f - (controller.PreRespawnTimer.RemainingTime(Runner) ?? 0f);

            if (deathTimer < deathUpTime) {
                deathUp = false;
                body.gravityScale = 0;
                body.velocity = Vector2.zero;
                if (deathTimer < (deathUpTime * 0.5f)) {
                    animator.Play("deadstart");
                    animator.ResetTrigger("respawn");
                }
            } else {
                if (!deathUp && body.position.y > GameManager.Instance.LevelMinY) {
                    body.velocity = new Vector2(0, deathForce);
                    deathUp = true;
                    if (animator.GetBool("firedeath") && Runner.IsForward) {
                        controller.PlaySound(Enums.Sounds.Player_Voice_LavaDeath);
                        controller.PlaySound(Enums.Sounds.Player_Sound_LavaHiss);
                    }
                    animator.SetTrigger("deathup");
                }
                body.gravityScale = 1.2f;
                body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
            }
            if (Runner.IsForward && Object.HasInputAuthority && deathTimer + Runner.DeltaTime > (3 - 0.43f) && deathTimer < (3 - 0.43f))
                controller.fadeOut.FadeOutAndIn(0.33f, .1f);

            if (body.position.y < GameManager.Instance.LevelMinY - transform.lossyScale.y) {
                //models.SetActive(false);
                body.velocity = Vector2.zero;
                body.gravityScale = 0;
            }
        }

        public void HandlePipeAnimation() {
            if (!controller.CurrentPipe)
                return;

            controller.UpdateHitbox();

            PipeManager pe = controller.CurrentPipe;

            body.isKinematic = true;
            body.velocity = controller.PipeDirection;

            if (controller.PipeTimer.Expired(Runner)) {
                if (controller.PipeEntering) {
                    //teleport to other pipe

                    if (pe.otherPipe.bottom == pe.bottom)
                        controller.PipeDirection *= -1;

                    Vector2 offset = controller.PipeDirection * (pipeDuration * 0.5f);
                    if (pe.otherPipe.bottom) {
                        float size = controller.MainHitbox.size.y * transform.localScale.y;
                        offset.y += size;
                    }
                    Vector3 tpPos = new Vector3(pe.otherPipe.transform.position.x, pe.otherPipe.transform.position.y, 1) - (Vector3) offset;
                    controller.networkRigidbody.TeleportToPosition(tpPos);
                    controller.cameraController.Recenter(tpPos + (Vector3) offset);
                    controller.PipeTimer = TickTimer.CreateFromSeconds(Runner, pipeDuration * 0.5f);
                    controller.PipeEntering = false;
                    controller.CurrentPipe = pe.otherPipe;
                } else {
                    //end pipe animation
                    controller.CurrentPipe = null;
                    body.isKinematic = false;
                    controller.IsOnGround = false;
                    controller.JumpState = PlayerController.PlayerJumpState.None;
                    controller.IsCrouching = false;
                    controller.PipeReentryTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
                    body.velocity = Vector2.zero;
                }
            }
        }

        public void DisableAllModels() {
            smallModel.SetActive(false);
            largeModel.SetActive(false);
            blueShell.SetActive(false);
            propellerHelmet.SetActive(false);
            animator.avatar = smallAvatar;
        }
    }
}
