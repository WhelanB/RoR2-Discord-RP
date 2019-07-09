using BepInEx;
using RoR2;
using UnityEngine.SceneManagement;
using DiscordRPC;
using DiscordRPC.Message;
using DiscordRPC.Unity;
using UnityEngine;
using R2API;
using System;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace DiscordRichPresence
{
	[BepInDependency("com.bepis.r2api")]

	[BepInPlugin("com.whelanb.discord", "Discord Rich Presence", "2.2.1")]

	public class Discord : BaseUnityPlugin
	{
		enum PrivacyLevel
		{
			Disabled = 0,
			Presence = 1,
			Join = 2
		}

		DiscordRpcClient client;

		static PrivacyLevel currentPrivacyLevel;
		
		public void Awake()
		{
			Logger.LogInfo("Starting Discord Rich Presence...");
			UnityNamedPipe pipe = new UnityNamedPipe();
			//Get your own clientid!
			client = new DiscordRpcClient("597759084187484160", -1, null, true, pipe);
			client.RegisterUriScheme("632360");
			client.Initialize();

			currentPrivacyLevel = PrivacyLevel.Join;

			//Subscribe to join events
			client.Subscribe(DiscordRPC.EventType.Join);
			client.Subscribe(DiscordRPC.EventType.JoinRequest);

			//Setup Discord client hooks
			client.OnReady += Client_OnReady;
			client.OnError += Client_OnError;
			client.OnJoinRequested += Client_OnJoinRequested;
			client.OnJoin += Client_OnJoin;

			//When a new stage is entered, update stats
			On.RoR2.Run.BeginStage += Run_BeginStage;

			//Used to handle additional potential presence changes
			//SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;

			//Handle Presence when Lobby is created
			On.RoR2.SteamworksLobbyManager.OnLobbyCreated += SteamworksLobbyManager_OnLobbyCreated;
			//Handle Presence when Lobby is joined
			On.RoR2.SteamworksLobbyManager.OnLobbyJoined += SteamworksLobbyManager_OnLobbyJoined;
			//Handle Presence when Lobby changes
			On.RoR2.SteamworksLobbyManager.OnLobbyChanged += SteamworksLobbyManager_OnLobbyChanged;
			//Handle Presence when user leaves Lobby
			On.RoR2.SteamworksLobbyManager.LeaveLobby += SteamworksLobbyManager_LeaveLobby;

			On.RoR2.CharacterBody.Awake += CharacterBody_Awake;


			//Messy work around for hiding timer in Discord when user pauses the game during a run
			RoR2Application.onPauseStartGlobal += OnGamePaused;

			//When the user un-pauses, re-broadcast run time to Discord
			RoR2Application.onPauseEndGlobal += OnGameUnPaused;

			//Register console commands
			On.RoR2.Console.Awake += (orig, self) =>
			{
				CommandHelper.RegisterCommands(self);
				orig(self);
			};
		}

		public void OnGamePaused()
		{
			if (RoR2.Run.instance != null)
			{
				if (client.CurrentPresence != null)
				{
					SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();
					if (scene != null)
						client.SetPresence(BuildRichPresenceForStage(scene, RoR2.Run.instance, false));
				}
			}
		}

		public void OnGameUnPaused()
		{
			if (RoR2.Run.instance != null)
			{
				if (client.CurrentPresence != null)
				{
					SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();
					if (scene != null)
						client.SetPresence(BuildRichPresenceForStage(scene, RoR2.Run.instance, true));
				}
			}
		}

		private void CharacterBody_Awake(On.RoR2.CharacterBody.orig_Awake orig, CharacterBody self)
		{
			if(self.isClient)
			{
				Logger.LogInfo("Client!" + self.GetDisplayName());
				RichPresence presence = client.CurrentPresence;
				presence.Assets.SmallImageKey = self.baseNameToken;
				presence.Assets.SmallImageText = self.GetDisplayName();
			}
			orig(self);
		}

		//Remove any lingering hooks and dispose of discord client connection
		public void Dispose()
		{
			On.RoR2.Run.BeginStage -= Run_BeginStage;

			On.RoR2.SteamworksLobbyManager.OnLobbyCreated -= SteamworksLobbyManager_OnLobbyCreated;
			On.RoR2.SteamworksLobbyManager.OnLobbyJoined -= SteamworksLobbyManager_OnLobbyJoined;
			On.RoR2.SteamworksLobbyManager.OnLobbyChanged -= SteamworksLobbyManager_OnLobbyChanged;
			On.RoR2.SteamworksLobbyManager.LeaveLobby -= SteamworksLobbyManager_LeaveLobby;

			RoR2Application.onPauseStartGlobal -= OnGamePaused;
			RoR2Application.onPauseEndGlobal -= OnGameUnPaused;

			client.Unsubscribe(DiscordRPC.EventType.Join);
			client.Unsubscribe(DiscordRPC.EventType.JoinRequest);

			client.Dispose();
		}

		public RichPresence BuildLobbyPresence(ulong lobbyID, Facepunch.Steamworks.Client client)
		{
			RichPresence presence = new RichPresence()
			{
				State = "In Lobby",
				Details = "Preparing",
				Assets = new DiscordRPC.Assets()
				{
					LargeImageKey = "lobby",
					LargeImageText = "Join!",

				},
				Party = new Party()
				{
					ID = client.Username,
					Max = client.Lobby.MaxMembers,
					Size = client.Lobby.NumMembers
				}
			};

			if (currentPrivacyLevel == PrivacyLevel.Join)
			{
				presence.Secrets = new Secrets()
				{
					JoinSecret = lobbyID.ToString()
				};
			}

			return presence;
		}

		//Be kind, rewind!
		public void OnDisable()
		{
			Dispose();
		}

		private void Client_OnJoin(object sender, JoinMessage args)
		{
			Logger.LogInfo("Joining Game via Discord - Steam Lobby ID: " + args.Secret);
			RoR2.SteamworksLobbyManager.JoinLobby(new CSteamID(ulong.Parse(args.Secret)));
		}

		//This is mostly handled through the Discord overlay now, so we can always accept for now
		private void Client_OnJoinRequested(object sender, JoinRequestMessage args)
		{
			Logger.LogInfo(string.Format("User {0} asked to join lobby", args.User.Username));
			Chat.AddMessage(string.Format("Discord user {0} has asked to join your game!", args.User.Username));
			//Always let people into your game for now
			client.Respond(args, true);
		}

		private void Client_OnError(object sender, ErrorMessage args)
		{
			Logger.LogError(args.Message);
			Dispose();
		}

		//Doesn't seem to ever be invoked, other callbacks need to be subscribed to
		private void Client_OnReady(object sender, ReadyMessage args)
		{
			Logger.LogInfo("Discord Rich Presence Ready - User: " + args.User.Username);
		}

		//Currently, presence isn't cleared on run/lobby exit - TODO
		private void SteamworksLobbyManager_LeaveLobby(On.RoR2.SteamworksLobbyManager.orig_LeaveLobby orig)
		{
			//if (Facepunch.Steamworks.Client.Instance == null)
			//	return;
			//Clear for now
			//if (client != null && client.CurrentPresence != null)
			//client.ClearPresence();
			orig();
		}

		//TODO - Refactor these as they all update the Presence with the same data
		private void SteamworksLobbyManager_OnLobbyChanged(On.RoR2.SteamworksLobbyManager.orig_OnLobbyChanged orig)
		{
			orig();

			if (Facepunch.Steamworks.Client.Instance == null)
				return;

			if (SteamworksLobbyManager.isInLobby)
			{
				Logger.LogInfo("Discord re-broadcasting Steam Lobby");
				ulong lobbyID = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby;
				client.SetPresence(BuildLobbyPresence(lobbyID, Facepunch.Steamworks.Client.Instance));
			}
		}

		private void SteamworksLobbyManager_OnLobbyJoined(On.RoR2.SteamworksLobbyManager.orig_OnLobbyJoined orig, bool success)
		{
			orig(success);

			if (!success || Facepunch.Steamworks.Client.Instance == null)
				return;

			Logger.LogInfo("Discord join complete");

			ulong lobbyID = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby;

			client.SetPresence(BuildLobbyPresence(lobbyID, Facepunch.Steamworks.Client.Instance));
		}

		private void SteamworksLobbyManager_OnLobbyCreated(On.RoR2.SteamworksLobbyManager.orig_OnLobbyCreated orig, bool success)
		{
			orig(success);

			if (!success || Facepunch.Steamworks.Client.Instance == null)
				return;

			ulong lobbyID = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby;

			Logger.LogInfo("Discord broadcasting new Steam lobby" + lobbyID);
			client.SetPresence(BuildLobbyPresence(lobbyID, Facepunch.Steamworks.Client.Instance));
		}

		//If the scene being loaded is a menu scene, remove the presence
		private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
		{
			SceneDef sceneDef = SceneCatalog.GetSceneDefFromScene(arg1);
			SceneDef oldSceneDef = SceneCatalog.GetSceneDefFromScene(arg0);
			if (sceneDef != null && oldSceneDef.sceneType == (SceneType.Stage | SceneType.Intermission) && sceneDef.sceneType == SceneType.Menu && client != null)
				client.ClearPresence();
		}

		//When the game begins a new stage, update presence
		private void Run_BeginStage(On.RoR2.Run.orig_BeginStage orig, Run self)
		{
			//Grab the run start time (elapsed time does not take into account timer freeze from intermissions yet)
			//Also runs a little fast - find a better hook point!
			if (currentPrivacyLevel != PrivacyLevel.Disabled)
			{
				SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();

				if (scene != null)
				{
					client.SetPresence(BuildRichPresenceForStage(scene, self, true));
				}
			}
			orig(self);
		}

		public RichPresence BuildRichPresenceForStage(SceneDef scene, Run run, bool includeRunTime)
		{
			RichPresence presence = new RichPresence()
			{
				Assets = new DiscordRPC.Assets()
				{
					LargeImageKey = scene.sceneName,
					LargeImageText = RoR2.Language.GetString(scene.subtitleToken)
					//add player character here!
				},
				State = "Classic Run",
				Details = string.Format("Stage {0} - {1}", (run.stageClearCount + 1), RoR2.Language.GetString(scene.nameToken))
			};
			if (scene.sceneType == SceneType.Stage && includeRunTime)
			{
				presence.Timestamps = new Timestamps()
				{
					StartUnixMilliseconds = (ulong)DateTimeOffset.Now.ToUnixTimeSeconds() - ((ulong)run.GetRunStopwatch())
				};
			}
			return presence;
		}

		[ConCommand(commandName = "discord_privacy_level", flags  = ConVarFlags.None, helpText = "Set the privacy level for Discord (0 is disabled, 1 is presence, 2 is presence + join)")]
		private static void SetPrivacyLevel(ConCommandArgs args)
		{
			if(args.Count != 1)
			{
				Debug.LogError("discord_privacy_level accepts 1 parameter only");
				return;
			}

			int level;
			bool parse = int.TryParse(args[0], out level);

			if(parse)
				currentPrivacyLevel = (PrivacyLevel)level; //unchecked
			else
				Debug.LogError("Failed to parse arg - must be integer value");
			
			//TODO - if disabled, clear presence
		}
	}
}