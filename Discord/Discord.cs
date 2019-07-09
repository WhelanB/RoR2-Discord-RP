using BepInEx;
using RoR2;
using UnityEngine.SceneManagement;
using DiscordRPC;
using DiscordRPC.Message;
using DiscordRPC.Unity;
using UnityEngine;
namespace DiscordRichPresence
{
	[BepInDependency("com.bepis.r2api")]

	[BepInPlugin("com.whelanb.discord", "Discord Rich Presence", "2.0.0")]

	public class Discord : BaseUnityPlugin
	{
		DiscordRpcClient client;


		public void Awake()
		{
			Logger.LogInfo("Starting Discord Rich Presence...");
			UnityNamedPipe pipe = new UnityNamedPipe();

			//Get your own clientid!
			client = new DiscordRpcClient("597759084187484160", -1, null, true, pipe);
			client.RegisterUriScheme("632360");
			client.Initialize();

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

		}

		//Be kind, rewind!
		public void OnDisable()
		{
			client.Dispose();
		}

		private void Client_OnJoin(object sender, JoinMessage args)
		{
			Logger.LogInfo("Joining Game via Discord - Steam Lobby ID: " + args.Secret);
			RoR2.SteamworksLobbyManager.JoinLobby(new CSteamID(ulong.Parse(args.Secret)));
		}

		//This is mostly handled through the Discord overlay now, so we can always accept for now
		private void Client_OnJoinRequested(object sender, JoinRequestMessage args)
		{
			//Always let people into your game for now
			client.Respond(args, true);
		}

		private void Client_OnError(object sender, ErrorMessage args)
		{
			Logger.LogError(args.Message);
		}

		//Doesn't seem to ever be invoked, other callbacks need to be subscribed to
		private void Client_OnReady(object sender, ReadyMessage args)
		{
			Logger.LogInfo("Discord Rich Presence Ready - User: " + args.User.Username);
		}

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
				client.SetPresence(new RichPresence()
				{
					State = "In Lobby",
					Details = "Preparing",
					Assets = new DiscordRPC.Assets()
					{
						LargeImageKey = "lobby",
						LargeImageText = "Join",

					},
					Secrets = new Secrets()
					{
						JoinSecret = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby.ToString()
					},
					Party = new Party()
					{
						ID = Facepunch.Steamworks.Client.Instance.Username,
						Max = Facepunch.Steamworks.Client.Instance.Lobby.MaxMembers,
						Size = Facepunch.Steamworks.Client.Instance.Lobby.NumMembers
					}
				});
			}
		}

		private void SteamworksLobbyManager_OnLobbyJoined(On.RoR2.SteamworksLobbyManager.orig_OnLobbyJoined orig, bool success)
		{
			orig(success);

			if (!success || Facepunch.Steamworks.Client.Instance == null)
				return;

			Logger.LogInfo("Discord join complete");

			ulong lobbyID = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby;

			client.SetPresence(new RichPresence()
			{
				State = "In Lobby",
				Details = "Preparing",
				Assets = new DiscordRPC.Assets()
				{
					LargeImageKey = "lobby",
					LargeImageText = "Join",

				},
				Secrets = new Secrets()
				{
					JoinSecret = lobbyID.ToString()
				},
				Party = new Party()
				{
					ID = Facepunch.Steamworks.Client.Instance.Username,
					Max = Facepunch.Steamworks.Client.Instance.Lobby.MaxMembers,
					Size = Facepunch.Steamworks.Client.Instance.Lobby.NumMembers
				}
			});
		}

		private void SteamworksLobbyManager_OnLobbyCreated(On.RoR2.SteamworksLobbyManager.orig_OnLobbyCreated orig, bool success)
		{
			orig(success);

			if (!success || Facepunch.Steamworks.Client.Instance == null)
				return;

			ulong lobbyID = Facepunch.Steamworks.Client.Instance.Lobby.CurrentLobby;

			Logger.LogInfo("Discord broadcasting new Steam lobby" + lobbyID);
			client.SetPresence(new RichPresence()
			{
				State = "In Lobby",
				Details = "Preparing",
				Assets = new DiscordRPC.Assets()
				{
					LargeImageKey = "lobby",
					LargeImageText = "Join!",

				},
				Secrets = new Secrets()
				{
					JoinSecret = lobbyID.ToString()
				},
				Party = new Party()
				{
					ID = Facepunch.Steamworks.Client.Instance.Username,
					Max = Facepunch.Steamworks.Client.Instance.Lobby.MaxMembers,
					Size = Facepunch.Steamworks.Client.Instance.Lobby.NumMembers
				}
			});


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
			SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();
			
			client.SetPresence(new RichPresence()
			{
				Assets = new DiscordRPC.Assets()
				{
					LargeImageKey = scene.sceneName,
					LargeImageText = RoR2.Language.GetString(scene.subtitleToken)
					//add player character here!
				},
				State = "Classic Run",
				Details = string.Format("Stage {0} - {1}",(self.stageClearCount + 1), RoR2.Language.GetString(scene.nameToken)),
			});
			orig(self);
		}
	}
}