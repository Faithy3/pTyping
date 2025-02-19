using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FontStashSharp;
using Furball.Engine;
using Furball.Engine.Engine;
using Furball.Engine.Engine.DevConsole;
using Furball.Engine.Engine.Graphics;
using Furball.Engine.Engine.Graphics.Drawables;
using Furball.Engine.Engine.Graphics.Drawables.Managers;
using Furball.Engine.Engine.Graphics.Drawables.Tweens;
using Furball.Engine.Engine.Graphics.Drawables.Tweens.TweenTypes;
using Furball.Engine.Engine.Helpers;
using Furball.Engine.Engine.Input;
using Furball.Engine.Engine.Input.Events;
using Furball.Engine.Engine.Localization;
using Furball.Engine.Engine.Timing;
using Furball.Vixie;
using Furball.Volpe.Evaluation;
using JetBrains.Annotations;
using Kettu;
using ManagedBass;
using pTyping.Engine;
using pTyping.Graphics;
using pTyping.Graphics.Drawables;
using pTyping.Graphics.Menus;
using pTyping.Graphics.Online;
using pTyping.Online;
using pTyping.Online.Tataku;
using pTyping.Shared;
using pTyping.Shared.Beatmaps;
using pTyping.Shared.Scores;
using Silk.NET.Input;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using sowelipisona;
using TagLib;
using Color = Furball.Vixie.Backends.Shared.Color;
using ConVars = pTyping.Engine.ConVars;

namespace pTyping;

public enum Localizations {
	MenuRevision,
	Changelog
}

// ReSharper disable once InconsistentNaming
public class pTypingGame : FurballGame {
	public static readonly Vector2 BackButtonScale = new Vector2(0.12f);

	public static Texture BackButtonTexture;
	public static Texture DefaultBackground;

	public static AudioStream           MusicTrack;
	public static OffsetTimeSource      MusicTrackTimeSource;
	public static AudioStreamTimeSource MusicTrackTimeSourceNoOffset;
	public static SoundEffectPlayer     MenuClickSound;
	public static Scheduler             MusicTrackScheduler;

	public static readonly Bindable<Beatmap> CurrentSong = new Bindable<Beatmap>(null);

	public static TextDrawable     VolumeSelector;
	public static TexturedDrawable CurrentSongBackground;

	public static OnlineManager OnlineManager;

	public static BeatmapDatabase BeatmapDatabase;
	public static FileDatabase    FileDatabase;

	public static ScoreDatabase ScoreDatabase;

	public static byte[] JapaneseFontData;
	public static FontSystem JapaneseFont = new FontSystem(new FontSystemSettings {
		FontResolutionFactor = 2f,
		KernelWidth          = 1,
		KernelHeight         = 1,
		TextureWidth         = 2048,
		TextureHeight        = 2048,
		PremultiplyAlpha     = false
	});

	public static UserCardDrawable MenuPlayerUserCard;

	public static Texture LocalLeaderboardButtonTexture;
	public static Texture FriendLeaderboardButtonTexture;
	public static Texture GlobalLeaderboardButtonTexture;

	private          double         _musicTrackSchedulerDelta;
	private readonly List<Drawable> _userPanelDrawables = new List<Drawable>();

	private DrawableManager _userPanelManager;
	private DrawableManager _settingsManager;
	private ChatDrawable    _chatDrawable;

	public static  NotificationManager NotificationManager;
	private static TextDrawable        _OnlineUsersText;
	private static MusicLoopState      _CurrentLoopState;
	private        pScreen             _currentRealScreen;

	public pTypingGame() : base(new MenuScreen()) {
		pTypingConfig.Instance.Load();

		//We set this flag so that we can fully control the FPS without the engine mucking it up
		BypassFurballFpsLimit = pTypingConfig.Instance.FpsBasedOnMonitorHz;
	}

	[CanBeNull]
	public static Drawable GetUserCard() {
		if (OnlineManager.State != ConnectionState.LoggedIn)
			return null;

		if (MenuPlayerUserCard is not null)
			return MenuPlayerUserCard;

		MenuPlayerUserCard = new UserCardDrawable(Vector2.Zero, OnlineManager.Player) {
			Scale = new Vector2(0.3f)
		};

		MenuPlayerUserCard.Player.OnChange                               += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.TotalScore.OnChange              += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.RankedScore.OnChange             += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.Accuracy.OnChange                += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.PlayCount.OnChange               += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.Action.OnChange                  += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.Action.Value.Action.OnChange     += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.Action.Value.Mode.OnChange       += (_, _) => MenuPlayerUserCard.UpdateDrawable();
		MenuPlayerUserCard.Player.Value.Action.Value.ActionText.OnChange += (_, _) => MenuPlayerUserCard.UpdateDrawable();

		return MenuPlayerUserCard;
	}

	public static void PlayMusic() {
		//If theres no music track registered, this will crash, so lets just NOP the function
		if (MusicTrack == null)
			return;

		MusicTrack.Play();

		MusicTrack.Volume = ConVars.Volume.Value.Value;
	}

	public static void PauseResumeMusic() {
		if (MusicTrack == null)
			return;

		if (MusicTrack.PlaybackState == PlaybackState.Playing)
			MusicTrack.Pause();
		else
			MusicTrack.Resume();
	}

	public static void LoadMusic(byte[] data) {
		if (MusicTrack != null) {
			MusicTrack.Stop();
			try {
				AudioEngine.DisposeStream(MusicTrack);
			}
			catch {
				/* */
			}
		}
		
		MusicTrack        = AudioEngine.CreateStream(data);
		MusicTrack.Volume = ConVars.Volume.Value.Value;

		MusicTrackTimeSourceNoOffset = new AudioStreamTimeSource(MusicTrack);
		MusicTrackTimeSource         = new OffsetTimeSource(MusicTrackTimeSourceNoOffset, 0);

		SetSongLoopState(_CurrentLoopState);
	}

	public static void LoadBackButtonTexture() {
		BackButtonTexture ??= ContentManager.LoadTextureFromFileCached("backbutton.png", ContentSource.User);
	}

	private static void SetBackgroundTexture(Texture tex) {
		CurrentSongBackground.Texture = tex;

		CurrentSongBackground.Scale = new Vector2(1f / ((float)CurrentSongBackground.Texture.Height / DEFAULT_WINDOW_HEIGHT));
	}

	public static void LoadBackgroundFromSong(Beatmap song) {
		Texture backgroundTex = null;

		if (song.FileCollection.Background == null) {
			try {
				File tags = File.Create(new BeatmapSongFileAbstraction(FileDatabase, song));

				if (tags.Tag.Pictures.Length != 0) {
					IPicture cover = tags.Tag.Pictures.FirstOrDefault(x => x.Type == PictureType.FrontCover, null);
					if (cover != null)
						backgroundTex = Texture.CreateTextureFromByteArray(cover.Data.Data);
				}

				tags.Dispose();
			}
			catch (Exception ex) {
				Logger.Log($"Failed to load song tags, i wonder why? {ex}", LoggerLevelSongManagerUpdateInfo.Instance);
			}

			DefaultBackground ??= ContentManager.LoadTextureFromFileCached("background.png", ContentSource.User);

			backgroundTex ??= DefaultBackground;
		}
		else {
			backgroundTex = song.FileCollection.Background != null ? Texture.CreateTextureFromByteArray(FileDatabase.GetFile(song.FileCollection.Background.Hash))
				: DefaultBackground;
		}

		SetBackgroundTexture(backgroundTex);
	}

	protected override void LoadContent() {
		byte[] menuClickSoundData = ContentManager.LoadRawAsset("menuhit.wav", ContentSource.User);
		MenuClickSound = AudioEngine.CreateSoundEffectPlayer(menuClickSoundData);

		// MenuClickSound.Volume = ConVars.Volume.Value.Value;
		AudioEngine.MusicVolume  = ConVars.Volume.Value.Value;
		AudioEngine.SampleVolume = ConVars.Volume.Value.Value * 0.5;

		ConVars.Volume.OnChange += delegate {
			// MenuClickSound.Volume = volume.Value;
			// MusicTrack.Volume     = ConVars.Volume.Value.Value;

			AudioEngine.MusicVolume  = ConVars.Volume.Value.Value;
			AudioEngine.SampleVolume = ConVars.Volume.Value.Value * 0.5;

			// MenuClickSound.Volume    = ConVars.Volume.Value.Value;

			if (VolumeSelector is not null)
				VolumeSelector.Text = $"Volume: {ConVars.Volume.Value.Value * 100d:00.##}";
		};

		DefaultBackground = ContentManager.LoadTextureFromFileCached("background.png", ContentSource.User);

		JapaneseFontData = ContentManager.LoadRawAsset("unifont.ttf", ContentSource.User);

		try {
			JapaneseFont = ContentManager.LoadSystemFont(
				"Aller",
				FontStyle.Regular,
				new FontSystemSettings {
					FontResolutionFactor = 2f,
					KernelWidth          = 1,
					KernelHeight         = 1,
					PremultiplyAlpha     = false
				}
			);
			JapaneseFont.AddFont(JapaneseFontData);

			JapaneseFont = ContentManager.LoadSystemFont(
				"Aller",
				FontStyle.Regular,
				new FontSystemSettings {
					FontResolutionFactor = 2f,
					KernelWidth          = 1,
					KernelHeight         = 1,
					PremultiplyAlpha     = false
				}
			);
			JapaneseFont.AddFont(JapaneseFontData);
		}
		catch {
			JapaneseFont.AddFont(JapaneseFontData);
			JapaneseFont.AddFont(JapaneseFontData);
		}

		LocalLeaderboardButtonTexture  = ContentManager.LoadTextureFromFileCached("local-leaderboard-button.png");
		FriendLeaderboardButtonTexture = ContentManager.LoadTextureFromFileCached("friend-leaderboard-button.png");
		GlobalLeaderboardButtonTexture = ContentManager.LoadTextureFromFileCached("global-leaderboard-button.png");
	}

	public static void ChangeGlobalVolume(float mouseScroll) {
		VolumeSelector.Tweens.Clear();

		VolumeSelector.Tweens.Add(new FloatTween(TweenType.Fade, VolumeSelector.ColorOverride.A / 255f, 1f, Time, Time + 200));

		VolumeSelector.Tweens.Add(new FloatTween(TweenType.Fade, 1f, 0f, Time + 2200, Time + 3200));

		if (mouseScroll > 0)
			ConVars.Volume.Value = new Value.Number(Math.Clamp(ConVars.Volume.Value.Value + 0.05d, 0d, 1d));
		else
			ConVars.Volume.Value = new Value.Number(Math.Clamp(ConVars.Volume.Value.Value - 0.05d, 0d, 1d));
	}

	public static void SelectNewSong() {
		BeatmapSet[] sets = BeatmapDatabase.Realm.All<BeatmapSet>().ToArray();

		//If there are no maps in the set, return
		if (!sets.Any())
			return;

		BeatmapSet picked = sets[Random.Next(sets.Length)];

		//Something probably went wrong here...
		if (picked.Beatmaps.Count == 0)
			return;

		CurrentSong.Value = picked.Beatmaps[0];
	}

	private static void CheckMusicState() {
		if (MusicTrack == null)
			return;

		if (_CurrentLoopState == MusicLoopState.Loop && MusicTrack.PlaybackState == PlaybackState.Stopped)
			PlayMusic();

		if (_CurrentLoopState == MusicLoopState.LoopFromPreviewPoint && MusicTrack.PlaybackState == PlaybackState.Stopped) {
			PlayMusic();
			MusicTrack.CurrentPosition = CurrentSong.Value?.Info.PreviewTime ?? 0;
		}

		if (_CurrentLoopState == MusicLoopState.NewSong && MusicTrack.CurrentPosition > MusicTrack.Length - 0.1d) {
			SelectNewSong();
			PlayMusic();
		}
	}

	protected override void Update(double deltaTime) {
		base.Update(deltaTime);
		DiscordManager.Update(deltaTime);

		CheckMusicState();

		this._musicTrackSchedulerDelta += deltaTime * 1000;
		if (this._musicTrackSchedulerDelta > 10) {
			if (MusicTrack != null)
				MusicTrackScheduler.Update((int)MusicTrack.CurrentPosition);
			this._musicTrackSchedulerDelta = 0;
		}

		OnlineManager.Update(deltaTime);

		if (this._userPanelManager.Visible)
			this._userPanelManager.Update(deltaTime);

		if (this._settingsManager.Visible)
			this._settingsManager.Update(deltaTime);

		NotificationManager.Update(deltaTime);
	}

	protected override void Draw(double gameTime) {
		base.Draw(gameTime);

		if (this._userPanelManager.Visible)
			this._userPanelManager.Draw(gameTime, DrawableBatch);

		if (this._settingsManager.Visible)
			this._settingsManager.Draw(gameTime, DrawableBatch);

		NotificationManager.Draw(gameTime, DrawableBatch);
	}

	public static void SubmitScore(Beatmap song, Score score) {
		ScoreDatabase.Realm.Write(
			() => {
				score.BeatmapId = song.Id;
				ScoreDatabase.Realm.Add(score);
			}
		);

		//TODO: submit with mods
		if (OnlineManager.State == ConnectionState.LoggedIn && score.User.Username == OnlineManager.Username())
			OnlineManager.SubmitScore(score).Wait();
	}

	protected override void OnClosing() {
		MusicTrackScheduler.Dispose(0);

		OnlineManager.Logout();

		DiscordManager.Dispose();

		OffsetManager.Save();

		pTypingConfig.Instance.Save();

		base.OnClosing();
	}

	protected override void Initialize() {
		DiscordManager.Initialize();

		this.BeforeScreenChange += this.BeforeOnScreenChange;
		this.AfterScreenChange  += this.AfterOnScreenChange;

		OffsetManager.Initialize();

		CurrentSongBackground = new TexturedDrawable(Texture.CreateWhitePixelTexture(), new Vector2(DEFAULT_WINDOW_WIDTH / 2f, DEFAULT_WINDOW_HEIGHT / 2f)) {
			Depth       = 1f,
			OriginType  = OriginType.Center,
			Hoverable   = false,
			Clickable   = false,
			CoverClicks = false,
			CoverHovers = false
		};

		this.OnRelayout += this.RelayoutBackgroundDrawable;

		NotificationManager = new NotificationManager();

		base.Initialize();

		ScreenshotManager.Initialize();

		TooltipDrawable.TextDrawable.SetFont(JapaneseFont, 20);

		//TODO: move these to the config (please)
		DevConsole.VolpeEnvironment.SetVariable(ConVars.Volume);
		DevConsole.VolpeEnvironment.SetVariable(ConVars.BackgroundDim);

		//TODO: move these functions into an array and add them as an array
		DevConsole.VolpeEnvironment.AddBuiltin(ConVars.Login);
		DevConsole.VolpeEnvironment.AddBuiltin(ConVars.SendMessage);
		DevConsole.VolpeEnvironment.AddBuiltin(ConVars.Logout);
		DevConsole.VolpeEnvironment.AddBuiltin(ConVars.LoadUTypingReplay);
		DevConsole.VolpeEnvironment.AddBuiltin(ConVars.LoadAutoReplay);

		OnlineManager = new TatakuOnlineManager(pTypingConfig.Instance.ServerWebsocketUrl, pTypingConfig.Instance.ServerWebUrl);
		OnlineManager.Initialize();

		OnlineManager.OnLogout     += this.OnLogout;
		OnlineManager.OnDisconnect += this.OnDisconnect;

		if (pTypingConfig.Instance.Username != string.Empty)
			OnlineManager.Login();

		VolumeSelector = new TextDrawable(new Vector2(DEFAULT_WINDOW_WIDTH, DEFAULT_WINDOW_HEIGHT), DefaultFont, $"Volume {ConVars.Volume.Value.Value}", 50) {
			OriginType  = OriginType.BottomRight,
			Clickable   = false,
			CoverClicks = false
		};

		//Set the opacity to 0
		VolumeSelector.Tweens.Add(new FloatTween(TweenType.Fade, 0f, 0f, 0, 0));

		InputManager.OnMouseScroll += delegate(object _, MouseScrollEventArgs eventArgs) {
			if (InputManager.HeldKeys.Contains(Key.AltLeft))
				ChangeGlobalVolume(eventArgs.ScrollAmount.Y);
		};

		DebugOverlayDrawableManager.Add(VolumeSelector);

		TypingConversions.LoadConversion(); //todo: support IMEs for more languages, and make it customizable by the user

		BeatmapDatabase = new BeatmapDatabase(DataFolder);
		BeatmapDatabase.Realm.Refresh();

		FileDatabase = new FileDatabase(DataFolder);

		ScoreDatabase = new ScoreDatabase(DataFolder);
		ScoreDatabase.Realm.Refresh();

		ImportChecker.ImportMaps();

		MusicTrackScheduler = new Scheduler();

		OnlineManager.OnlinePlayers.CollectionChanged += this.UpdateUserPanel;

		this._settingsManager = new DrawableManager();

		this._userPanelManager = new DrawableManager();
		this._userPanelManager.Add(
			new TexturedDrawable(WhitePixel, new Vector2(0)) {
				Scale         = new Vector2(DEFAULT_WINDOW_WIDTH, DEFAULT_WINDOW_HEIGHT),
				Depth         = 1.5f,
				ColorOverride = new Color(0, 0, 0, 100)
			}
		);
		this._userPanelManager.Add(
			_OnlineUsersText = new TextDrawable(new Vector2(10), JapaneseFont, "Online Users: 0", 50) {
				Depth = 0f
			}
		);
		this._userPanelManager.Visible = false;
		this.UpdateUserPanel(null, null);

		this._chatDrawable = new ChatDrawable(new Vector2(10, DEFAULT_WINDOW_HEIGHT - 10), new Vector2(DEFAULT_WINDOW_WIDTH - 20, DEFAULT_WINDOW_HEIGHT * 0.4f)) {
			OriginType = OriginType.BottomLeft
		};
		this._userPanelManager.Add(this._chatDrawable);

		GraphicsBackend.Current.ScreenshotTaken += this.OnScreenshotTaken;
		CurrentSong.OnChange                    += this.OnSongChange;

		SelectNewSong();
		PlayMusic();
	}

	// ReSharper disable once InconsistentNaming
	private enum pTypingKeybinds {
		TakeScreenshot,
		ToggleFullscreen,
		ToggleUserPanel,
		OpenSettings
	}

	private Keybind _takeScreenshot;
	private Keybind _toggleFullscreen;
	private Keybind _toggleUserPanel;
	private Keybind _openSettings;
	public override void RegisterKeybinds() {
		base.RegisterKeybinds();

		this._toggleUserPanel = new Keybind(pTypingKeybinds.ToggleUserPanel, "Toggle User Panel", Key.F1, this.ToggleUserPanel);
		this._takeScreenshot  = new Keybind(pTypingKeybinds.TakeScreenshot, "Take Screenshot", Key.F2, _ => GraphicsBackend.Current.TakeScreenshot());
		this._toggleFullscreen = new Keybind(
			pTypingKeybinds.ToggleFullscreen,
			"Toggle Fullscreen",
			Key.F3,
			_ => {
				this.ChangeScreenSize((int)this.WindowManager.WindowSize.X, (int)this.WindowManager.WindowSize.Y, !this.WindowManager.Fullscreen);
			}
		);
		this._openSettings = new Keybind(pTypingKeybinds.OpenSettings, "Open Settings Menu", Key.O, this.ToggleSettingsMenu);

		InputManager.RegisterKeybind(this._takeScreenshot);
		InputManager.RegisterKeybind(this._toggleFullscreen);
		InputManager.RegisterKeybind(this._toggleUserPanel);
		InputManager.RegisterKeybind(this._openSettings);
	}

	private void ToggleSettingsMenu(FurballKeyboard keyboard) {
		this.ToggleSettingsMenu(true);
	}

	private SettingsForm _settings;
	public void ToggleSettingsMenu(bool needControl) {
		//If we arent holding control, ignore this keypress
		if (!InputManager.ControlHeld && needControl) return;

		if (this._settings == null) {
			this._settings = new SettingsForm {
				Visible = false,
				Depth   = -100
			};
			this._settings.OnTryClose += (_, _) => this.ToggleSettingsMenu(false);
			this._settingsManager.Add(this._settings);
		}

		//If the form is already changing state, then stop it from being changed until its done
		if (this._settings.StateChanging) return;

		if (this._settings.Visible)
			this._settings.FadeOutFromOne(100);
		else
			this._settings.FadeInFromZero(100);

		this._settings.StateChanging = true;
		GameTimeScheduler.ScheduleMethod(
			_ => {
				this._settings.Visible       = !this._settings.Visible;
				this._settings.StateChanging = false;
			},
			Time + 100
		);
	}

	public override void UnregisterKeybinds() {
		base.UnregisterKeybinds();

		InputManager.UnregisterKeybind(this._takeScreenshot);
		InputManager.UnregisterKeybind(this._toggleFullscreen);
		InputManager.UnregisterKeybind(this._toggleUserPanel);
		InputManager.UnregisterKeybind(this._openSettings);
	}

	public void ToggleUserPanel(FurballKeyboard keyboard) {
		//If we arent logged in, ignore this press
		if (OnlineManager.State != ConnectionState.LoggedIn) return;

		//Toggle visibility
		this._userPanelManager.Visible = !this._userPanelManager.Visible;

		//Deselect the chat text input field
		InputManager.ReleaseTextFocus(this._chatDrawable.MessageInputDrawable);

		//Set the visibility of all drawables in the user panel
		foreach (Drawable drawable in this._userPanelManager.Drawables)
			drawable.Visible = this._userPanelManager.Visible;
	}

	private void RelayoutBackgroundDrawable(object _, Vector2 newSize) {
		if (this._currentRealScreen?.Manager.EffectedByScaling ?? false)
			newSize = new(this._currentRealScreen.Manager.Size.X / this._currentRealScreen.Manager.Size.Y * 720f, WindowHeight);

		CurrentSongBackground.Position = new Vector2(newSize.X / 2f, newSize.Y / 2f);
	}

	private void OnDisconnect(object sender, EventArgs e) {
		if (this._userPanelManager != null)
			this._userPanelManager.Visible = false;
	}

	private void OnLogout(object sender, EventArgs e) {
		if (this._userPanelManager != null)
			this._userPanelManager.Visible = false;
	}

	private void BeforeOnScreenChange(object sender, Screen e) {
		if (e is pScreen s)
			this._currentRealScreen = s;
	}
	private void OnSongChange(object sender, Beatmap song) {
		if (song.FileCollection.Audio != null)
			LoadMusic(FileDatabase.GetFile(song.FileCollection.Audio.Hash));
		else
			MusicTrack.Stop(); //if the song has no music, just stop playing the current one for now...

		LoadBackgroundFromSong(song);

		UpdateCurrentOnlineStatus(this._currentRealScreen);
	}

	public static void UpdateCurrentOnlineStatus(pScreen screen) {
		if (OnlineManager.State != ConnectionState.LoggedIn || screen == null) return;

		UserActionType actionType = UserActionType.Unknown;
		string         final      = "Unknown";

		//TODO: display a custom status for no songs
		if (CurrentSong.Value != null) {
			BeatmapSet set = CurrentSong.Value.Parent.First();

			switch (screen.OnlineUserActionType) {
				case ScreenUserActionType.Listening:
					actionType = UserActionType.Idle;
					final      = $"Listening to {set.Artist} - {set.Title}";
					break;
				case ScreenUserActionType.Editing:
					final = pTypingConfig.Instance.Username == CurrentSong.Value.Info.Mapper
						? $"Editing {set.Artist} - {set.Title} [{CurrentSong.Value.Info.DifficultyName}] by {CurrentSong.Value.Info.Mapper}"
						: $"Modding {set.Artist} - {set.Title} [{CurrentSong.Value.Info.DifficultyName}] by {CurrentSong.Value.Info.Mapper}";
					actionType = UserActionType.Editing;
					break;
				case ScreenUserActionType.ChoosingSong:
					final      = "Choosing a song!";
					actionType = UserActionType.Idle;
					break;
				case ScreenUserActionType.Playing:
					final      = $"Playing {set.Artist} - {set.Title} [{CurrentSong.Value.Info.DifficultyName}]";
					actionType = UserActionType.Ingame;
					break;
				case ScreenUserActionType.Lobbying:
					final      = $"Partying to {set.Artist} - {set.Title} [{CurrentSong.Value.Info.DifficultyName}]";
					actionType = UserActionType.Idle;
					break;
				case ScreenUserActionType.Multiplaying:
					final      = $"Multiplaying {set.Artist} - {set.Title} [{CurrentSong.Value.Info.DifficultyName}]";
					actionType = UserActionType.Ingame;
					break;
			}
		}

		OnlineManager.ChangeUserAction(new UserAction(actionType, final));
	}

	private void SetBackgroundFadeFromScreen() {
		// ReSharper disable once CompareOfFloatsByEqualityOperator
		if (this._currentRealScreen.BackgroundFadeAmount != -1f)
			CurrentSongBackground.Tweens.Add(
				new ColorTween(
					TweenType.Color,
					CurrentSongBackground.ColorOverride,
					new Color(this._currentRealScreen.BackgroundFadeAmount, this._currentRealScreen.BackgroundFadeAmount, this._currentRealScreen.BackgroundFadeAmount),
					CurrentSongBackground.TimeSource.GetCurrentTime(),
					CurrentSongBackground.TimeSource.GetCurrentTime() + 1000
				)
			);
	}

	public void UpdateLetterboxing() {
		if (!pTypingConfig.Instance.Letterboxing) {
			this._currentRealScreen.Manager.EffectedByScaling = false;
			return;
		}

		this._currentRealScreen.Manager.EffectedByScaling = true;

		this._currentRealScreen.Manager.Position = new Vector2(pTypingConfig.Instance.LetterboxingX, pTypingConfig.Instance.LetterboxingY);
		this._currentRealScreen.Manager.Size     = new Vector2(pTypingConfig.Instance.LetterboxingW, pTypingConfig.Instance.LetterboxingH);

		this.RelayoutBackgroundDrawable(this, new Vector2(WindowWidth, WindowHeight));
	}

	private void AfterOnScreenChange(object _, Screen screen) {
		pScreen actualScreen = screen as pScreen;

		this.WindowManager.WindowTitle = screen is not pScreen ? "pTyping" : $"pTyping - {actualScreen.Name}";

		if (actualScreen == null)
			return;

		this._currentRealScreen = actualScreen;

		this.SetBackgroundFadeFromScreen();

		if (actualScreen.ForceSpeedReset)
			MusicTrack?.SetSpeed(1f);

		SetSongLoopState(actualScreen.LoopState);

		UpdateCurrentOnlineStatus(actualScreen);

		if (pTypingConfig.Instance.FpsBasedOnMonitorHz) {
			int? videoModeRefreshRate = this.WindowManager.Monitor.VideoMode.RefreshRate ?? 60;

			double targetFps = actualScreen.ScreenType switch {
				ScreenType.Gameplay => pTypingConfig.Instance.UnlimitedFpsGameplay ? -1 : videoModeRefreshRate.Value * pTypingConfig.Instance.GameplayFpsMult,
				ScreenType.Menu     => pTypingConfig.Instance.UnlimitedFpsMenu ? -1 : videoModeRefreshRate.Value     * pTypingConfig.Instance.MenuFpsMult,
				_                   => throw new ArgumentOutOfRangeException()
			};

			this.SetTargetFps(targetFps);
		}

		this.UpdateLetterboxing();
	}

	private static void SetSongLoopState(MusicLoopState loopState) {
		if (MusicTrack == null)
			return;

		_CurrentLoopState = loopState;
		switch (loopState) {
			case MusicLoopState.Loop:
				MusicTrack.Loop = true;
				break;
			case MusicLoopState.LoopFromPreviewPoint:
				MusicTrack.Loop = false;
				break;
			case MusicLoopState.NewSong:
				MusicTrack.Loop = false;
				break;
			case MusicLoopState.None:
				MusicTrack.Loop = false;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private void OnScreenshotTaken(object sender, Image e) {
		Task.Factory.StartNew(
			() => {
				if (OnlineManager.State == ConnectionState.LoggedIn) {
					string id = OnlineManager.SendScreenshot(e);

					InputManager.Clipboard = $"{pTypingConfig.Instance.ServerWebUrl}/screenshots/{id}";

					ScreenshotManager.SaveScreenshot(e, true, id);

					NotificationManager.CreateNotification(NotificationManager.NotificationImportance.Info, "Screenshot uploaded and copied to clipboard!");
				}
				else {
					ScreenshotManager.SaveScreenshot(e, false);
				}
			}
		);
		NotificationManager.CreateNotification(NotificationManager.NotificationImportance.Info, "Saving screenshot!");
	}

	protected override void InitializeLocalizations() {
		//default language is already english, and im an english speaker, so no need to set it here

		LocalizationManager.AddDefaultTranslation(Localizations.MenuRevision, "Revision {0}");
		LocalizationManager.AddDefaultTranslation(Localizations.Changelog, "Changelog");

		base.InitializeLocalizations();
	}

	private void UpdateUserPanel(object sender, object e) {
		GameTimeScheduler.ScheduleMethod(
			_ => {
				_OnlineUsersText.Text = $"Online Users: {OnlineManager.OnlinePlayers.Count(x => !x.Bot)} ({OnlineManager.OnlinePlayers.Count})";

				this._userPanelDrawables.ForEach(x => { this._userPanelManager.Remove(x); });
				this._userPanelDrawables.Clear();

				Vector2 pos = new Vector2(10, 10 + _OnlineUsersText.Size.Y + 10);
				foreach (OnlinePlayer player in OnlineManager.OnlinePlayers) {
					UserCardDrawable drawable = player.GetUserCard();
					drawable.MoveTo(pos);
					pos.X += drawable.Size.X + 10;

					if (pos.X + drawable.Size.X > DEFAULT_WINDOW_WIDTH) {
						pos.X =  10;
						pos.Y += drawable.Size.Y + 10;
					}

					drawable.ClearEvents();
					drawable.OnClick += delegate(object _, MouseButtonEventArgs a) {
						switch (a.Button) {
							case MouseButton.Left: {
								lock (OnlineManager.KnownChannels) {
									if (!OnlineManager.KnownChannels.Contains(player.Username))
										OnlineManager.KnownChannels.Add(player.Username);
								}
								break;
							}
							case MouseButton.Right:
								if (OnlineManager.Host != null) return;

								OnlineManager.SpectatePlayer(player);
								break;
						}
					};

					this._userPanelDrawables.Add(drawable);
					this._userPanelManager.Add(drawable);
				}
			},
			Time
		);
	}
}
