﻿using System.Collections.Generic;
using System.Linq;
using SonicRealms.Core.Internal;
using SonicRealms.Legacy.UI;
using UnityEngine;

namespace SonicRealms.Legacy.Game
{
    public class SrLegacyGameManager : MonoBehaviour
    {
        public static SrLegacyGameManager Instance;

        [Header("Settings")]
        public SrLegacyResolutionSettings ResolutionChoices;

        [Header("Data")]
        public SrLegacySceneTransition LevelTransition;
        public string MenuSceneName;
        public List<SrLegacyLevelData> Levels;
        public List<SrLegacyCharacterData> Characters;

        /// <summary>
        /// The currently loaded save. Progress will be recorded on this save.
        /// </summary>
        [Header("Game Status")]
        public SaveData SaveData;

        public GlobalSaveData GlobalSaveData;

        /// <summary>
        /// The data for the character being played.
        /// </summary>
        public SrLegacyCharacterData CharacterData;

        /// <summary>
        /// The data for the level being played.
        /// </summary>
        public SrLegacyLevelData LevelData;

        /// <summary>
        /// The current level's manager.
        /// </summary>
        [Space]
        public SrLegacyLevelManager Level;

        public Dictionary<SrLegacyCharacterData, GameObject> ActiveCharacters;
        public GameObject ActiveCharacter
        {
            get
            {
                if (ActiveCharacters.Count == 0) return null;
                return ActiveCharacters.Values.First();
            }
        }

        public void Reset()
        {
            LevelTransition = null;
            Levels.Clear();
            Characters.Clear();
        }

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            ActiveCharacters = new Dictionary<SrLegacyCharacterData, GameObject>();
            LoadGlobalSave();

            DontDestroyOnLoad(gameObject);
        }

        public void SetResolutionFullscreen(int screenSizeIndex)
        {
            GlobalSaveData.Fullscreen = true;
            GlobalSaveData.FullscreenScreenSize = screenSizeIndex;
        }

        public void SetResolutionWindowed(int aspectIndex, int screenSizeIndex)
        {
            GlobalSaveData.Fullscreen = false;
            GlobalSaveData.WindowedAspect = aspectIndex;
            GlobalSaveData.WindowedScreenSize = screenSizeIndex;
        }

        public void FlushSoundSettings()
        {
            GlobalSaveData.MasterVolume = (int)(SrSoundManager.MasterVolume*100);
            GlobalSaveData.MusicVolume = (int)(SrSoundManager.MusicVolume*100);
            GlobalSaveData.FxVolume = (int)(SrSoundManager.FxVolume*100);

            GlobalSaveData.Flush();
        }

        public void StartNewGame(string fileName)
        {
            StartNewGame(fileName, Characters.FirstOrDefault());
        }

        public void StartNewGame(string fileName, SrLegacyCharacterData character)
        {
            var originalData = SaveManager.Load(fileName);
            if (originalData != null)
            {
                Debug.LogWarning(string.Format("Overwrote file {0}. Old data: {1}.",
                    fileName,
                    originalData));
            }

            SaveData = SaveManager.Create(fileName);
            SaveData.Lives = 3;
            SaveData.Character = character.name;

            CharacterData = character;
            LoadLevel(Levels.First());
        }

        public SrLegacyLevelData GetLevelData(string levelName)
        {
            return Levels.FirstOrDefault(data => data.name == levelName);
        }

        public SrLegacyCharacterData GetCharacterData(string characterName)
        {
            return Characters.FirstOrDefault(data => data.name == characterName);
        }

        public GameObject GetCharacter(string characterName)
        {
            return ActiveCharacters.FirstOrDefault(pair => pair.Key.name == characterName).Value;
        }

        public void ToMenu()
        {
            LevelTransition.Begin(MenuSceneName);
        }

        public void LoadLevel(string levelName)
        {
            LoadLevel(Levels.FirstOrDefault(data => data.name == levelName));
        }

        public void LoadLevel(SrLegacyLevelData levelData)
        {
            UnloadLevel();

            LevelData = levelData;
            SaveData.Level = levelData.name;

            LevelTransition.OnNextScene.AddListener(InitCurrentLevel);
            LevelTransition.Begin(levelData.Scene);
        }

        public void UnloadLevel()
        {
            ActiveCharacters.Clear();
            LevelData = null;
        }

        public void ReloadLevel()
        {
            if (LevelData == null)
            {
                Debug.LogError("Can't reload the level because there isn't one currently playing.");
                return;
            }

            LoadLevel(LevelData);
        }

        public void LoadGame(SaveData data)
        {
            if (data == null)
            {
                Debug.LogError("Tried to load a null save file!");
                return;
            }

            SaveData = data;
            LevelData = GetLevelData(data.Level);

            if(!string.IsNullOrEmpty(data.Character))
                CharacterData = GetCharacterData(data.Character);
        }

        /// <summary>
        /// Updates the loaded save with current progress but does not write the save to the disk.
        /// </summary>
        public void UpdateSave()
        {
            UpdateSave(SaveData);
        }

        /// <summary>
        /// Updates the given save with the current progress but does not write to disk.
        /// </summary>
        /// <param name="saveData"></param>
        public void UpdateSave(SaveData saveData)
        {
            if (Level != null)
                Level.UpdateSave(saveData);
        }

        /// <summary>
        /// Overwrites the loaded save file with current progress and writes the file to disk.
        /// </summary>
        public void SaveProgress()
        {
            if (SaveData != null)
                SaveProgress(SaveData.Name);
        }

        /// <summary>
        /// Writes current progress to the given save and writes the save to disk. If the file does not exist,
        /// it will be created under the given name.
        /// </summary>
        /// <param name="fileName">The name of the save file to write to.</param>
        public void SaveProgress(string fileName)
        {
            if (SaveData == null)
                SaveData = SaveManager.Create(fileName);
            else
                SaveData.Name = fileName;

            UpdateSave(SaveData);
            SaveData.Flush();
        }

        /// <summary>
        /// Writes the save without updating it with the newest level progress.
        /// </summary>
        public void FlushSave()
        {
            if (SaveData != null)
                SaveData.Flush();
        }

        /// <summary>
        /// Resets the current save so it can keep playing after a game over.
        /// </summary>
        public void ContinueSave()
        {
            // Reset everything but current level and character
            SaveData.Lives = 3;
            SaveData.Checkpoint = null;
            SaveData.Time = 0f;
            SaveData.Rings = 0;
            SaveData.Score = 0;
            FlushSave();
        }

        private void UpdateResolution()
        {
            if (GlobalSaveData.Fullscreen)
            {
                SetResolution(ResolutionChoices.GetFullscreenEntry(GlobalSaveData.FullscreenScreenSize).ToEntry());
            }
            else
            {
                SetResolution(
                    ResolutionChoices.GetWindowedEntry(GlobalSaveData.WindowedAspect)
                        .ToEntry(GlobalSaveData.WindowedScreenSize));
            }
        }

        private void SetResolution(SrLegacyResolutionSettings.Entry resolution)
        {
            Screen.SetResolution(resolution.Resolution.Width, resolution.Resolution.Height, resolution.Fullscreen);
        }

        private void LoadGlobalSave()
        {
            var globalSave = SaveManager.LoadGlobalSave();
            if (globalSave == null)
            {
                globalSave = SaveManager.CreateGlobalSave();

                globalSave.MasterVolume = 100;
                globalSave.MusicVolume = 100;
                globalSave.FxVolume = 100;

                globalSave.Flush();
            }

            GlobalSaveData = globalSave;

            SrSoundManager.MasterVolume = globalSave.MasterVolume/100f;
            SrSoundManager.MusicVolume = globalSave.MusicVolume/100f;
            SrSoundManager.FxVolume = globalSave.FxVolume/100f;
        }

        public void InitCurrentLevel()
        {
            LevelTransition.OnNextScene.RemoveListener(InitCurrentLevel);

            // Find the level manager
            var levelManager = SrGameObjectUtility.Find(LevelData.LevelManager, true);
            if (levelManager == null)
            {
                Debug.LogWarning(string.Format("Level manager '{0}' could not be found in the scene.",
                    LevelData.LevelManager));
                return;
            }

            if (!(Level = levelManager.GetComponent<SrLegacyLevelManager>()))
            {
                Debug.LogWarning(string.Format("Level manager '{0}' has no Level Manager component attached.", 
                    Level));
                return;
            }

            if(SaveData != null && !string.IsNullOrEmpty(SaveData.Name))
                LoadGame(SaveData);

            Level.InitLevel();
        }
    }
}
