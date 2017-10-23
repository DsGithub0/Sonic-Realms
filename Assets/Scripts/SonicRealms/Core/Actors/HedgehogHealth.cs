﻿using SonicRealms.Core.Internal;
using SonicRealms.Core.Moves;
using SonicRealms.Core.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace SonicRealms.Core.Actors
{
    /// <summary>
    /// Allows the controller to be harmed and gives it Sonic-style "health", which is 
    /// just whether or not you have rings.
    /// </summary>
    public class HedgehogHealth : HealthSystem
    {
        public HedgehogController Controller;

        public override float Health
        {
            get { return 1f; }
            set { if (value < 0f) Kill(); }
        }

        /// <summary>
        /// Move to perform when hurt.
        /// </summary>
        [Tooltip("Move to perform when hurt.")]
        public HurtRebound HurtReboundMove;

        /// <summary>
        /// Move to perform when killed.
        /// </summary>
        [Tooltip("Move to perform when killed.")]
        public Death DeathMove;

        /// <summary>
        /// Whether the controller is invincible due to having been hurt.
        /// </summary>
        [HideInInspector]
        public bool HurtInvincible;

        /// <summary>
        /// Countdown until the controller is no longer invincible after being hurt.
        /// </summary>
        [HideInInspector]
        public float HurtInvincibilityTimer;

        #region Injury
        /// <summary>
        /// The controller's ring collector.
        /// </summary>
        [SrFoldout("Injury")]
        [Tooltip("The controller's ring collector.")]
        public RingCounter RingCounter;

        /// <summary>
        /// The max number of rings lost when hurt.
        /// </summary>
        [SrFoldout("Injury")]
        [Tooltip("The max number of rings lost when hurt.")]
        public int RingsLost;

        /// <summary>
        /// Duration of invincibility after getting hurt (and after the rebound ends), in seconds.
        /// </summary>
        [SrFoldout("Injury")]
        [Tooltip("Duration of invincibility after getting hurt (and after the rebound ends), in seconds.")]
        public float HurtInvinciblilityTime;
        #endregion
        #region Animation
        /// <summary>
        /// Animator bool set to whether invicibility after getting hurt is on
        /// </summary>
        [SrFoldout("Animation")]
        [Tooltip("Animator bool set to whether invicibility after getting hurt is on")]
        public string HurtInvincibleBool;
        protected int HurtInvincibleBoolHash;
        #endregion
        #region Sound
        /// <summary>
        /// Audio clip to play when losing rings as the result of getting hurt.
        /// </summary>
        [SrFoldout("Sound")]
        [Tooltip("Audio clip to play when losing rings as the result of getting hurt.")]
        public AudioClip RingLossSound;

        /// <summary>
        /// Audio clip to play when losing a shield or rebounding.
        /// </summary>
        [SrFoldout("Sound")]
        [Tooltip("Audio clip to play when losing a shield or rebounding.")]
        public AudioClip ReboundSound;

        /// <summary>
        /// Audio clip to play on death.
        /// </summary>
        [SrFoldout("Sound")]
        [Tooltip("Audio clip to play on death.")]
        public AudioClip DeathSound;

        /// <summary>
        /// Any hit object that has this tag will be considered spikes.
        /// </summary>
        [SrFoldout("Sound"), Space, SrTag]
        [Tooltip("Any hit object that has this tag will be considered spikes.")]
        public string SpikeTag;

        /// <summary>
        /// Audio clip to play when hitting spikes with a shield on.
        /// </summary>
        [SrFoldout("Sound")]
        [Tooltip("Audio clip to play when hitting spikes with a shield on.")]
        public AudioClip SpikeSound;
        #endregion
        #region Events
        /// <summary>
        /// Called when the controller's death animation is complete.
        /// </summary>
        [SrFoldout("Events")]
        public UnityEvent OnDeathComplete;

        /// <summary>
        /// Called when the controller kills a badnik.
        /// </summary>
        [SrFoldout("Events")]
        public EnemyEvent OnEnemyKilled;
        #endregion

        [SrFoldout("Debug")]
        public bool DeathComplete;

        public override void Reset()
        {
            base.Reset();

            RingLossSound = null;
            ReboundSound = null;
            DeathSound = null;
            SpikeTag = "";
            SpikeSound = null;

            OnDeathComplete = new UnityEvent();
            OnEnemyKilled = new EnemyEvent();

            Controller = GetComponentInParent<HedgehogController>();

            HurtReboundMove = Controller.GetMove<HurtRebound>();
            DeathMove = Controller.GetMove<Death>();

            RingCounter = GetComponentInChildren<RingCounter>();

            RingsLost = 9001;
            HurtInvinciblilityTime = 2.0f;
        }

        public override void Awake()
        {
            base.Awake();

            OnDeathComplete = OnDeathComplete ?? new UnityEvent();
            OnEnemyKilled = OnEnemyKilled ?? new EnemyEvent();

            Controller = Controller ?? GetComponentInParent<HedgehogController>();
            HurtReboundMove = HurtReboundMove ?? Controller.GetComponent<MoveManager>().Get<HurtRebound>();
            RingCounter = RingCounter ?? GetComponentInChildren<RingCounter>();

            HurtInvincibilityTimer = 0.0f;

            HurtInvincibleBoolHash = Animator.StringToHash(HurtInvincibleBool);
        }

        public override void Start()
        {
            base.Start();
            Animator = Controller.Animator;
        }

        public override void Update()
        {
            base.Update();
            if (HurtInvincible)
            {
                HurtInvincibilityTimer -= Time.deltaTime;
                if (HurtInvincibilityTimer < 0.0f)
                {
                    HurtInvincible = Invincible = false;
                    HurtInvincibilityTimer = 0.0f;
                }
            }

            if (Controller.Animator == null)
                return;

            if(HurtInvincibleBoolHash != 0)
                Controller.Animator.SetBool(HurtInvincibleBoolHash, HurtInvincible && !IsDead);
        }

        public override void TakeDamage(float damage, Transform source)
        {
            if (Invincible)
                return;

            // Start our invincibility frames
            HurtInvincibilityTimer = HurtInvinciblilityTime;
            HurtInvincible = Invincible = true;

            // Check if the damage source is a spike (to play different sounds in response)
            var spikes = source.CompareTag(SpikeTag);

            // Perform the rebound
            HurtReboundMove.ThreatPosition = source ? (Vector2)source.position : default(Vector2);
            HurtReboundMove.OnEnd.AddListener(OnHurtReboundEnd);
            HurtReboundMove.Perform(false, true);

            // See if the player had a shield
            var shield = Controller.GetPowerup<Shield>();
            if (shield == null)
            {
                // If not, and we're out of rings, we're dead
                if (RingCounter.Rings <= 0)
                {
                    Kill();
                    return;
                }

                // Otherwise just spill the beans
                RingCounter.Spill(RingsLost);

                if (RingLossSound != null)
                    SrSoundManager.PlaySoundEffect(RingLossSound);
            }
            else
            {
                // Special spike sound
                if (spikes)
                {
                    if (SpikeSound != null)
                        SrSoundManager.PlaySoundEffect(SpikeSound);
                }
                else
                {
                    if (ReboundSound != null)
                        SrSoundManager.PlaySoundEffect(ReboundSound);
                }
            }

            OnHurt.Invoke(null);
        }

        /// <summary>
        /// Lets the player know that he killed an enemy
        /// </summary>
        /// <param name="enemy"></param>
        public void NotifyEnemyKilled(HealthSystem enemy)
        {
            OnEnemyKilled.Invoke(enemy);
        }

        public void OnHurtReboundEnd()
        {
            HurtInvincible = Invincible = true;
            HurtReboundMove.OnEnd.RemoveListener(OnHurtReboundEnd);
        }

        public void OnDeathEnd()
        {
            DeathComplete = true;
            OnDeathComplete.Invoke();
            DeathMove.OnEnd.RemoveListener(OnDeathEnd);
        }

        public override void Kill(Transform source)
        {
            if (IsDead || DeathMove.Active) return;

            // Bring the player to the front
            var spriteRenderer = Controller.RendererObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sortingLayerName = "Foreground";

            DeathMove.Perform();
            DeathMove.OnEnd.AddListener(OnDeathEnd);

            if (DeathSound != null)
                SrSoundManager.PlaySoundEffect(DeathSound);

            base.Kill(source);
        }
    }
}
