﻿using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using IPA;
using IPA.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberDiscordPresence
{
	public class Plugin : IBeatSaberPlugin
    {
        public string Name => "Discord Presence";
        public string Version => "2.2.0";


        private const string MenuSceneName = "MenuCore";
		private const string GameSceneName = "GameCore";

		private const string DiscordAppID = "445053620698742804";
		public static readonly DiscordRpc.RichPresence Presence = new DiscordRpc.RichPresence();

        private IPA.Logging.Logger logger;

        private GameplayCoreSceneSetupData _mainSetupData;
        private GameplayCoreSceneSetup _gameplaySetup;
        private MainFlowCoordinator _mainFlowCoordinator;

        private bool _init;
        private FieldInfo _gameplayCoreSceneSetupDataField = null;
        private FieldInfo _oneColorBeatmapCharacteristic = null;
        private Component _z;

        private MonoBehaviour gameObject = null;
        
        private GameMode _gamemode = GameMode.Standard;


        public void Init(IPA.Logging.Logger log)
        {
            if (_init) return;
            _init = true;

            logger = log;

            gameObject = Resources.FindObjectsOfTypeAll<MonoBehaviour>().First(c => c.GetType().Name == "PluginComponent");

            try
            {
                Console.WriteLine("Discord Presence - Initializing");

                Console.WriteLine("Discord Presence - Starting Discord RichPresence");
                var handlers = new DiscordRpc.EventHandlers();
                DiscordRpc.Initialize(DiscordAppID, ref handlers, false, string.Empty);

                Console.WriteLine("Discord Presence - Fetching nonpublic fields");

                _gameplayCoreSceneSetupDataField = typeof(SceneSetup<GameplayCoreSceneSetupData>).GetField("_sceneSetupData", BindingFlags.NonPublic | BindingFlags.Instance);
                _oneColorBeatmapCharacteristic = typeof(GameplayCoreSceneSetup).GetField("_oneColorBeatmapCharacteristic", BindingFlags.NonPublic | BindingFlags.Instance);
#if DEBUG
                Console.WriteLine("Discord Presence - Field SceneSetup<GameplayCoreSceneSetupData>._sceneSetupData: " + _gameplayCoreSceneSetupDataField);
#endif
                if (_gameplayCoreSceneSetupDataField == null)
                {
                    Console.WriteLine("Discord Presence - Unable to fetch SceneSetup<GameplayCoreSceneSetupData>._sceneSetupData");
                    return;
                }

                Console.WriteLine("Discord Presence - Init done !");
            }
            catch (Exception e)
            {
                Console.WriteLine("Discord Presence - Unable to initialize plugin:\n" + e);
            }
        }

        public void OnApplicationQuit()
		{
            DiscordRpc.Shutdown();
        }

        public void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            UpdatePresence(newScene);
        }

        private void UpdatePresence(Scene newScene)
        {

            if (newScene.name == MenuSceneName)
			{
				//Menu scene loaded
				Presence.details = "In Menu";
				Presence.state = string.Empty;
				Presence.startTimestamp = default(long);
				Presence.largeImageKey = "default";
				Presence.largeImageText = "Beat Saber";
				Presence.smallImageKey = "";
				Presence.smallImageText = "";
				DiscordRpc.UpdatePresence(Presence);
			}
			else if (newScene.name == GameSceneName)
			{
                gameObject.StartCoroutine(UpdatePresenceAfterFrame());
			}
		}

        private IEnumerator UpdatePresenceAfterFrame()
        {
            // Wait for next frame
            yield return true;

            // Fetch all required objects
            _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().FirstOrDefault();
            _gameplaySetup = Resources.FindObjectsOfTypeAll<GameplayCoreSceneSetup>().FirstOrDefault();

            if (_z == null)
            {
                _z = Resources.FindObjectsOfTypeAll<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "Z");
                if (_z == null)
                    Console.WriteLine("Discord Presence - No element of type \"Z\" found. Maybe the game isn't running a version of ScoreSaber supporting replay ?");
            }

            if (_gameplaySetup != null)
                _mainSetupData = _gameplayCoreSceneSetupDataField.GetValue(_gameplaySetup) as GameplayCoreSceneSetupData;
#if DEBUG
            Console.WriteLine("Discord Presence - _gameplaySetup: " + _gameplaySetup);
            Console.WriteLine("Discord Presence - _gameplayCoreSceneSetupDataField: " + _gameplayCoreSceneSetupDataField);
            Console.WriteLine("Discord Presence - _mainSetupData: " + _mainSetupData);
            GetFlowTypeHumanReadable(); // Used to debug print values
#endif
            // Check if every required object is found
            if (_mainSetupData == null || _gameplaySetup == null || _mainFlowCoordinator == null)
            {
                Console.WriteLine("Discord Presence - Error finding the scriptable objects required to update presence. (_mainSetupData: " + (_mainSetupData == null ? "N/A" : "OK") + ", _gameplaySetup: " + (_gameplaySetup == null ? "N/A" : "OK") + ", _mainFlowCoordinator: " + (_mainFlowCoordinator == null ? "N/A" : "OK"));
                Presence.details = "Playing";
                DiscordRpc.UpdatePresence(Presence);
                yield break;
            }

            // Set presence main values
            IDifficultyBeatmap diff = _mainSetupData.difficultyBeatmap;
            
            Presence.details = $"{diff.level.songName} | {diff.difficulty.Name()}";
            Presence.state = "";

            //if()

            Presence.state = "";

            if (_z != null)
            {
                Console.WriteLine("--------------------------");
                FieldInfo[] fields = _z.GetType().GetFields((BindingFlags)(-1));
                foreach(FieldInfo fi in fields)
                {
                    if(fi.FieldType.Name == "Mode" && fi.GetValue(_z).ToString() == "Playback")
                        Presence.state += "[Replay] ";
                    //Console.WriteLine("Discord Presence - [" + fi.Name + ": " + fi.FieldType.Name + "] => " + fi.GetValue(_z));
                }
            }

            if (diff.level.levelID.Contains('∎'))
            {
                Presence.state += "Custom | ";
            }

            if(_mainSetupData.practiceSettings != null)
                Presence.state += "Practice | ";

            Presence.state += GetFlowTypeHumanReadable() + " ";
#if DEBUG
            Console.WriteLine("Discord Presence - diff.parentDifficultyBeatmapSet.beatmapCharacteristic: " + diff.parentDifficultyBeatmapSet.beatmapCharacteristic);
            Console.WriteLine("Discord Presence - _gameplaySetup._oneColorBeatmapCharacteristic: " + typeof(GameplayCoreSceneSetup).GetField("_oneColorBeatmapCharacteristic", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_gameplaySetup));
#endif
            // Update gamemode (Standard / One Saber / No Arrow)
            if (_mainSetupData.gameplayModifiers.noArrows || diff.parentDifficultyBeatmapSet.beatmapCharacteristic.ToString().ToLower().Contains("noarrow"))
                _gamemode = GameMode.NoArrows;
            else if (diff.parentDifficultyBeatmapSet.beatmapCharacteristic == (BeatmapCharacteristicSO)_oneColorBeatmapCharacteristic.GetValue(_gameplaySetup))
                _gamemode = GameMode.OneSaber;
            else _gamemode = GameMode.Standard;

            string gameplayModeText = _gamemode == GameMode.OneSaber ? "One Saber" : _gamemode == GameMode.NoArrows ? "No Arrow" : "Standard";
            Presence.state += gameplayModeText;

            // Set music speak
            if (_mainSetupData.practiceSettings != null)
            {
                if (_mainSetupData.practiceSettings.songSpeedMul != 1.0f)
                    Presence.state += " | Speed x" + _mainSetupData.practiceSettings.songSpeedMul;
            }
            else
            {
                if (_mainSetupData.gameplayModifiers.songSpeedMul != 1.0f)
                    Presence.state += " | Speed x" + _mainSetupData.gameplayModifiers.songSpeedMul;
            }

            // Set common gameplay modifiers
            if (_mainSetupData.gameplayModifiers.noFail)
                Presence.state += " | No Fail";
            if (_mainSetupData.gameplayModifiers.instaFail)
                Presence.state += " | Instant Fail";
            if (_mainSetupData.playerSpecificSettings.swapColors)
                Presence.state += " | Mirrored";
            if (_mainSetupData.gameplayModifiers.disappearingArrows)
                Presence.state += " | Disappearing Arrows";
            if (_mainSetupData.gameplayModifiers.ghostNotes)
                Presence.state += " | Ghost Notes";


            Presence.largeImageKey = "default";
            Presence.largeImageText = "Beat Saber";
            Presence.smallImageKey = GetFlowTypeHumanReadable() == "Party" ? "party" : _gamemode == GameMode.OneSaber ? "one_saber" : _gamemode == GameMode.NoArrows ? "no_arrows" : "solo";
            Presence.smallImageText = gameplayModeText;
            Presence.startTimestamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            // Set startTimestamp offset if we are in training mode
            if (_mainSetupData.practiceSettings != null)
            {
#if DEBUG
                Console.WriteLine("Discord Presence - _mainSetupData.practiceSettings.startSongTime: " + _mainSetupData.practiceSettings.startSongTime);
#endif
                if (_mainSetupData.practiceSettings.startInAdvanceAndClearNotes)
                {
                    Presence.startTimestamp -= (long)Mathf.Max(0f, _mainSetupData.practiceSettings.startSongTime - 3f);
                }
                else
                {
                    Presence.startTimestamp -= (long)_mainSetupData.practiceSettings.startSongTime;
                }
            }

            DiscordRpc.UpdatePresence(Presence);
        }

        public void OnUpdate()
		{
			DiscordRpc.RunCallbacks();
		}


        private string GetFlowTypeHumanReadable()
        {
            Type t = _mainFlowCoordinator.childFlowCoordinator.GetType();
            Console.WriteLine("Discord Presence - Current Flow Coordinator: " + t);
            if (t == typeof(ArcadeFlowCoordinator))
                return "Arcade"; // Unused ?
            if (t == typeof(PartyFreePlayFlowCoordinator))
                return "Party";
            if (t == typeof(SimpleDemoFlowCoordinator))
                return "Demo"; // Unused ?
            if (t == typeof(CampaignFlowCoordinator))
                return "Campaign";
            return "Solo";
        }

        public void OnLevelWasLoaded(int level) { }
        public void OnLevelWasInitialized(int level) { }
        public void OnFixedUpdate() { }
        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) { }
        public void OnSceneUnloaded(Scene scene) { }
        public void OnApplicationStart() { }

        private enum GameMode
        {
            Standard,
            OneSaber,
            NoArrows
        }
    }
}
