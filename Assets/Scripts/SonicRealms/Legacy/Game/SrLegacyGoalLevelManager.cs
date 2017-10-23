﻿using System;
using System.Collections;
using SonicRealms.Core.Actors;
using SonicRealms.Core.Internal;
using SonicRealms.Core.Utils;
using SonicRealms.Legacy.UI;
using SonicRealms.Level;
using SonicRealms.Level.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicRealms.Legacy.Game
{
    /// <summary>
    /// Modifies the game state in reaction to the target player.
    /// </summary>
    public class SrLegacyGoalLevelManager : SrLegacyLevelManager
    {
        #region UI
        /// <summary>
        /// The level's BGM.
        /// </summary>
        [SrFoldout("UI")]
        [Tooltip("The level's BGM.")]
        public AudioClip MusicIntro;

        /// <summary>
        /// The level's BGM.
        /// </summary>
        [SrFoldout("UI")]
        [Tooltip("The level's BGM.")]
        public AudioClip MusicLoop;

        /// <summary>
        /// The level's UI manager.
        /// </summary>
        [SrFoldout("UI")]
        [Tooltip("The level's UI manager.")]
        public SrLegacySonicHudManager HudManager;

        /// <summary>
        /// The level's player camera.
        /// </summary>
        [SrFoldout("UI")]
        [Tooltip("The level's player camera.")]
        public CameraController Camera;
        #endregion
        #region Screens
        /// <summary>
        /// Transition shown before starting a level and inbetween the game's scene transition.
        /// </summary>
        [SrFoldout("Screens")]
        [Tooltip("Transition shown before starting a level and inbetween the game's scene transition.")]
        public SrLegacyTransition TitleCard;

        /// <summary>
        /// Transition shown for a game over.
        /// </summary>
        [SrFoldout("Screens")]
        [Tooltip("Transition shown for a game over.")]
        public SrLegacyTransition GameOverScreen;

        /// <summary>
        /// Transition shown for a time over.
        /// </summary>
        [SrFoldout("Screens")]
        [Tooltip("Transition shown for a time over.")]
        public SrLegacyTransition TimeOverScreen;

        /// <summary>
        /// Transition shown for clearing an act. Score should be added here before going to the next level.
        /// </summary>
        [SrFoldout("Screens")]
        [Tooltip("Transition shown for clearing an act. Score should be added here before going to the next level.")]
        public SrLegacyTransition ActClearScreen;
        #endregion
        #region Level
        /// <summary>
        /// Transform representing the bottom left corner of the level boundaries.
        /// </summary>
        [SrFoldout("Level")]
        [Tooltip("Transform representing the bottom left corner of the level boundaries.")]
        public Transform LevelBoundsMin;

        /// <summary>
        /// Transform representing the upper right corner of the level boundaries.
        /// </summary>
        [SrFoldout("Level")]
        [Tooltip("Transform representing the upper right corner of the level boundaries.")]
        public Transform LevelBoundsMax;

        /// <summary>
        /// The level's character spawner.
        /// </summary>
        [SrFoldout("Level")]
        [Tooltip("The level's character spawner.")]
        public SrLegacyCharacterSpawn Spawn;

        /// <summary>
        /// The level timer.
        /// </summary>
        [Space, SrFoldout("Level")]
        [Tooltip("The level timer.")]
        public SrLegacyLevelTimer Timer;

        /// <summary>
        /// Number of seconds after which the player dies due to a time over.
        /// </summary>
        [SrFoldout("Level")]
        [Tooltip("Number of seconds after which the player dies due to a time over.")]
        public float TimeOverSeconds;
        #endregion

        /// <summary>
        /// Destination level after act clear.
        /// </summary>
        [SrFoldout("Data")]
        public NextLevelData NextLevel;

        /// <summary>
        /// The target player.
        /// </summary>
        [SrFoldout("Debug")]
        public HedgehogController Player;

        /// <summary>
        /// The player's health system.
        /// </summary>
        [SrFoldout("Debug")]
        public HedgehogHealth Health;

        /// <summary>
        /// The current checkpoint.
        /// </summary>
        [SrFoldout("Debug")]
        public GameObject Checkpoint;

        /// <summary>
        /// The player's score counter.
        /// </summary>
        [SrFoldout("Debug")]
        public ScoreCounter ScoreCounter;
        public int Score
        {
            get { return ScoreCounter ? ScoreCounter.Score : 0; }
            set { if (ScoreCounter) ScoreCounter.Score = value; }
        }

        /// <summary>
        /// The player's ring counter.
        /// </summary>
        [SrFoldout("Debug")]
        public RingCounter RingCounter;
        public int Rings
        {
            get { return RingCounter ? RingCounter.Rings : 0; }
            set { if (RingCounter) RingCounter.Rings = value; }
        }

        /// <summary>
        /// The player's live counter.
        /// </summary>
        [SrFoldout("Debug")]
        public LifeCounter LifeCounter;

        /// <summary>
        /// The player's current lives.
        /// </summary>
        public int Lives
        {
            get { return LifeCounter ? LifeCounter.Lives : 0; } set { if (LifeCounter) LifeCounter.Lives = value; } }

        /// <summary>
        /// The current level time.
        /// </summary>
        public TimeSpan Time
        {
            get { return Timer ? Timer.Time : TimeSpan.Zero; }
            set { if (Timer) Timer.Time = value; }
        }

        /// <summary>
        /// The current level time in seconds.
        /// </summary>
        public float TimeSeconds
        {
            get { return Timer ? (float)Timer.Time.TotalSeconds : 0f; }
            set { if (Timer) Timer.Time = TimeSpan.FromSeconds(value); }
        }

        protected Coroutine ScreenCoroutine;
        protected bool IsTimeOver;
        #region Level Lifecycle
        protected override void OnInitLevel()
        {
            SrSoundManager.StopAllMusic();

            var game = SrLegacyGameManager.Instance;
            var save = SrLegacyGameManager.Instance.SaveData;

            // Disable transitions (except for the title card)
            if(GameOverScreen != null) GameOverScreen.gameObject.SetActive(false);
            if(TimeOverScreen != null) TimeOverScreen.gameObject.SetActive(false);
            if(ActClearScreen != null) ActClearScreen.gameObject.SetActive(false);
            
            // Spawn the player at the last checkpoint (or the spawn, if there is no checkpoint)
            if (string.IsNullOrEmpty(save.Checkpoint)) Checkpoint = Spawn.gameObject;
            else Checkpoint = SrGameObjectUtility.Find(save.Checkpoint);

            var player = Spawn.Spawn(game.CharacterData, Checkpoint);

            var checkpoint = Checkpoint.GetComponent<Checkpoint>();
            if(checkpoint != null) checkpoint.PreActivate();

            // Add to the game's list of character objects
            game.ActiveCharacters.Add(game.CharacterData, player);
            Player = player.GetComponent<HedgehogController>();

            // Get all the player's counters
            ScoreCounter = player.GetComponent<ScoreCounter>();
            RingCounter = player.GetComponent<RingCounter>();
            LifeCounter = player.GetComponent<LifeCounter>();

            // Listen for death so we can do game over/time over sequences
            Health = player.GetComponent<HedgehogHealth>();
            if (Health != null) Health.OnDeath.AddListener(OnDeathBegin);

            // Get the save data into the components we just stored
            Lives = save.Lives;
            Score = save.Score;
            Rings = save.Rings;
            TimeSeconds = save.Time;

            // Set up the camera for the player and level boundaries
            Camera.Target = Player.transform;
            Camera.LevelBoundsMin = LevelBoundsMin;
            Camera.LevelBoundsMax = LevelBoundsMax;

            // Link with the HUD
            HudManager.Level = this;
            
            // Start the background music
            if (MusicLoop)
                SrSoundManager.PlayMainMusic(MusicIntro, MusicLoop);
            
            // Done! save, show the title card, and start the level when it's done
            game.SaveProgress();
            ShowTitleCard(StartLevel);
        }

        protected override void OnStartLevel()
        {
            StartTimer();
        }

        protected override void OnFinishLevel()
        {
            StopTimer();

            var nextLevel = NextLevel.GetNextLevelFor(SrLegacyGameManager.Instance.CharacterData);
            if (nextLevel != null) ShowActClearScreen(() => SrLegacyGameManager.Instance.LoadLevel(nextLevel));
            else ShowActClearScreen(SrLegacyGameManager.Instance.ToMenu);
        }
        #endregion

        public override void UpdateSave(SaveData saveData)
        {
            saveData.Level = SrLegacyGameManager.Instance.LevelData.name;
            saveData.Checkpoint = SrGameObjectUtility.GetPath(Checkpoint);
            saveData.Lives = Lives;
            saveData.Score = Score;
            saveData.Rings = Rings;
            saveData.Time = (float)Time.TotalSeconds;
        }

        public override void Reset()
        {
            base.Reset();
            Player = FindObjectOfType<HedgehogController>();
            Health = Player ? Player.GetComponent<HedgehogHealth>() : null;
            TimeOverSeconds = 599.9f;
        }

        public void Update()
        {
            if (Timer.Time.TotalSeconds > TimeOverSeconds && !Health.IsDead)
            {
                IsTimeOver = true;
                Health.Kill();
            }
            else
            {
                Time = Timer.Time;
            }
        }

        #region Level Status Methods
        /// <summary>
        /// Takes one off from the player's life counter and reloads the level.
        /// </summary>
        public void LoseLife()
        {
            if(TimeOverScreen != null) TimeOverScreen.gameObject.SetActive(false);

            --LifeCounter.Lives;
            if (LifeCounter.Lives > 0)
            {
                SrLegacyGameManager.Instance.SaveData.Lives = LifeCounter.Lives;
                SrLegacyGameManager.Instance.FlushSave();
                SrLegacyGameManager.Instance.ReloadLevel();
            }
            else
            {
                ShowGameOverScreen(() =>
                {
                    SrLegacyGameManager.Instance.ContinueSave();
                    SrLegacyGameManager.Instance.ToMenu();
                });
            }
        }

        /// <summary>
        /// Triggers the time over sequence, for when a player has played past the level's time limit.
        /// </summary>
        public void TimeOver()
        {
            ShowTimeOverScreen(() =>
            {
                SrLegacyGameManager.Instance.SaveData.Time = 0f;
                SrLegacyGameManager.Instance.FlushSave();
                LoseLife();
            });
        }

        /// <summary>
        /// Starts the level timer.
        /// </summary>
        public virtual void StartTimer()
        {
            Timer.enabled = true;
        }

        /// <summary>
        /// Stops the level timer.
        /// </summary>
        public virtual void StopTimer()
        {
            Timer.enabled = false;
        }
        #endregion

        #region Screen Methods
        public void StopCurrentScreen()
        {
            if(ScreenCoroutine != null) StopCoroutine(ScreenCoroutine);
            ScreenCoroutine = null;
        }

        /// <summary>
        /// Shows the title card and calls the given function when it's done playing.
        /// </summary>
        /// <param name="callback"></param>
        public void ShowTitleCard(Action callback)
        {
            StopCurrentScreen();
            ScreenCoroutine = StartCoroutine(ShowTitleCard_Coroutine(callback));
        }

        /// <summary>
        /// Shows the act clear screen and calls the given function when it's done playing. Note: this will
        /// calculate bonuses and add them to the score.
        /// </summary>
        /// <param name="callback"></param>
        public void ShowActClearScreen(Action callback)
        {
            StopCurrentScreen();
            Timer.enabled = false;
            ScreenCoroutine = StartCoroutine(ShowActClearScreen_Coroutine(callback));
        }

        /// <summary>
        /// Shows the game over screen and calls the given function when it's done playing.
        /// </summary>
        /// <param name="callback"></param>
        public void ShowGameOverScreen(Action callback)
        {
            StopCurrentScreen();
            ScreenCoroutine = StartCoroutine(ShowGameOverScreen_Coroutine(callback));
        }

        /// <summary>
        /// Shows the time over screen and calls the given function when it's done playing.
        /// </summary>
        /// <param name="callback"></param>
        public void ShowTimeOverScreen(Action callback)
        {
            StopCurrentScreen();
            ScreenCoroutine = StartCoroutine(ShowTimeOverScreen_Coroutine(callback));
        }
        #endregion
        #region Screen Coroutines
        protected IEnumerator ShowTitleCard_Coroutine(Action callback)
        {
            // Stop the level timer and game transition to let the title card come in
            if (TitleCard != null)
            {
                Timer.enabled = false;
                var levelTransition = SrLegacyGameManager.Instance.LevelTransition;
                if (levelTransition != null) levelTransition.Pause();

                // Wait for the scene to load
                var scene = SceneManager.GetActiveScene();
                yield return new WaitUntil(() => scene.isLoaded);
                // Let the title card play
                TitleCard.Enter();
                yield return new WaitWhile(() => TitleCard.State == SrLegacyTransitionState.Enter);

                // Fade in the level
                if (levelTransition != null)
                {
                    levelTransition.Play();
                    yield return new WaitWhile(() => levelTransition.IsPlaying);
                }

                // Level starts while the title card is leaving
                TitleCard.Exit();
            }
            
            callback();
        }

        protected IEnumerator ShowActClearScreen_Coroutine(Action callback)
        {
            if (ActClearScreen != null)
            {
                ActClearScreen.Enter();
                yield return new WaitWhile(() => ActClearScreen.IsPlaying);
            }
            
            callback();
        }

        protected IEnumerator ShowGameOverScreen_Coroutine(Action callback)
        {
            if (GameOverScreen != null)
            {
                GameOverScreen.Enter();
                yield return new WaitWhile(() => GameOverScreen.IsPlaying);
            }
            
            callback();
        }

        protected IEnumerator ShowTimeOverScreen_Coroutine(Action callback)
        {
            if (TimeOverScreen != null)
            {
                TimeOverScreen.Enter();
                yield return new WaitWhile(() => TimeOverScreen.IsPlaying);
            }
            
            callback();
        }
        #endregion

        #region Event Callbacks
        protected void OnDeathBegin(HealthEventArgs e)
        {
            StopTimer();

            // Wait for death sequence to finish, then show the game over or time over screen
            if (IsTimeOver) StartCoroutine(WaitForDeathComplete(TimeOver));
            else StartCoroutine(WaitForDeathComplete(LoseLife));
        }

        protected IEnumerator WaitForDeathComplete(Action callback)
        {
            yield return new WaitUntil(() => Health.DeathComplete);
            callback();
        }
        #endregion
    }
}
