using BepInEx;
using RoR2;
using UnityEngine.SceneManagement;
using DiscordRPC;
using DiscordRPC.Message;
using DiscordRPC.Unity;
namespace DiscordRichPresence
{
	[BepInDependency("com.bepis.r2api")]

	[BepInPlugin("com.whelanb.discord", "Discord Rich Presence", "1.0.2")]

	public class Discord : BaseUnityPlugin
	{
		DiscordRpcClient client;

		public void OnEnable()
		{
			Logger.LogInfo("Starting Discord Rich Presence");
			UnityNamedPipe pipe = new UnityNamedPipe();
			//Get your own clientid!
			client = new DiscordRpcClient("597759084187484160", -1, null, true, pipe);
			client.Initialize();
			client.OnReady += Client_OnReady;
		}

		public void OnDisable()
		{
			client.Dispose();
		}

		private void Client_OnReady(object sender, ReadyMessage args)
		{
			Logger.LogInfo("Discord Rich Presence Ready - User: " + args.User.Username);
		}

		public void Awake()
		{
			On.RoR2.Run.BeginStage += Run_BeginStage;
			SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
		}

		//If the scene being loaded is a menu scene, remove the presence
		private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
		{
			if (SceneCatalog.GetSceneDefFromScene(arg1).sceneType == SceneType.Menu)
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
					LargeImageText = RoR2.Language.GetString(scene.subtitleToken),
					
				},
				State = "Classic Run",
				Details = "Stage " + (self.stageClearCount + 1) + " - " + RoR2.Language.GetString(scene.nameToken)
			});
			orig(self);
		}
	}
}