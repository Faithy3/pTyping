using System;
using System.Collections.Generic;
using System.Linq;
using Furball.Engine;
using Furball.Engine.Engine;
using Furball.Engine.Engine.Audio;
using Furball.Engine.Engine.Graphics;
using Furball.Engine.Engine.Graphics.Drawables;
using Furball.Engine.Engine.Graphics.Drawables.Primitives;
using Furball.Engine.Engine.Graphics.Drawables.Tweens;
using Furball.Engine.Engine.Graphics.Drawables.Tweens.TweenTypes;
using Furball.Engine.Engine.Graphics.Drawables.UiElements;
using pTyping.Songs;
using pTyping.Player;
using pTyping.Drawables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

namespace pTyping.Screens {
	public class PlayerScreen : Screen {
		public PlayerScore Score = new();

		private TextDrawable     _scoreDrawable;
		private TextDrawable     _accuracyDrawable;
		private TextDrawable     _comboDrawable;
		private TextDrawable     _typingIndicator;
		private TexturedDrawable _recepticle;
		private UiButtonDrawable _skipButton;
		
		public SoundEffect HitSound   = new();
		
		private List<NoteDrawable> _notes = new();
		
		public static int ScoreExcellent = 1500;
		public static int ScoreGood = 1000;
		public static int ScoreFair = 500;
		public static int ScorePoor = 0;

		public static int ScorePerCharacter = 500;
		public static int ScoreCombo        = 10;
		public static int ScoreComboMax     = 1000;
		
		public static int TimingExcellent = 20;
		public static int TimingGood = 50;
		public static int TimingFair = 100;
		public static int TimingPoor = 200;
		
		public static readonly Vector2 RecepticlePos = new(FurballGame.DEFAULT_WINDOW_WIDTH * 0.15f, FurballGame.DEFAULT_WINDOW_HEIGHT / 2f);

		public static readonly Vector2 NoteStartPos = new(FurballGame.DEFAULT_WINDOW_WIDTH + 100, FurballGame.DEFAULT_WINDOW_HEIGHT / 2f);

		public PlayerScreen() {
			if (pTypingGame.CurrentSong.Value.Notes.Count == 0) {
				//TODO notify the user the map did not load correctly, for now, we just send back to the song selection menu
				FurballGame.Instance.ChangeScreen(new SongSelectionScreen(false));
			}

			#region UI
			this._scoreDrawable    = new TextDrawable(new Vector2(5, 5), FurballGame.DEFAULT_FONT, $"{this.Score.Score:00000000}", 60);
			this._accuracyDrawable = new TextDrawable(new Vector2(5, 5 + this._scoreDrawable.Size.Y), FurballGame.DEFAULT_FONT, $"{this.Score.Accuracy * 100:0.00}%", 60) {};
			this._comboDrawable = new TextDrawable(new Vector2(RecepticlePos.X, RecepticlePos.Y - 70), FurballGame.DEFAULT_FONT, $"{this.Score.Combo}x", 70) {
				OriginType = OriginType.BottomCenter
			};

			this.Manager.Add(this._scoreDrawable);
			this.Manager.Add(this._accuracyDrawable);
			this.Manager.Add(this._comboDrawable);

			this._skipButton            = new UiButtonDrawable(new(FurballGame.DEFAULT_WINDOW_WIDTH, FurballGame.DEFAULT_WINDOW_HEIGHT), "Skip Intro", FurballGame.DEFAULT_FONT, 50, Color.Blue, Color.White, Color.White, new(0));
			this._skipButton.OriginType = OriginType.BottomRight;
			this._skipButton.Visible    = false;
			
			this._skipButton.OnClick += this.SkipButtonClick;
			
			this.Manager.Add(this._skipButton);
			#endregion
			
			#region Recepticle

			Texture2D noteTexture = ContentManager.LoadMonogameAsset<Texture2D>("note");
			
			this._recepticle = new TexturedDrawable(noteTexture, RecepticlePos) {
				Scale = new(0.55f),
				OriginType = OriginType.Center
			};

			this.Manager.Add(this._recepticle);
			#endregion
			
			#region Notes
			foreach (Note note in pTypingGame.CurrentSong.Value.Notes) {
				NoteDrawable noteDrawable = new(new(NoteStartPos.X, NoteStartPos.Y + note.YOffset), noteTexture, pTypingGame.UniFont, 30) {
					TimeSource    = pTypingGame.MusicTrack,
					ColorOverride = note.Color,
					LabelTextDrawable = {
						Text  = $"{note.Text}\n{string.Join("\n", note.ThisCharacterRomaji)}",
						Scale = new(1f)
					},
					Scale = new(0.55f, 0.55f),
					OriginType = OriginType.Center
				};

				noteDrawable.Tweens.Add(
					new VectorTween(
						TweenType.Movement,
						new(NoteStartPos.X, NoteStartPos.Y + note.YOffset),
						RecepticlePos,
						(int)(note.Time - Config.BaseApproachTime * (1 - pTypingGame.CurrentSong.Value.CurrentTimingPoint(note.Time).Tempo / 500d + 1)),
						(int)note.Time));

				this.Manager.Add(noteDrawable);

				noteDrawable.Note = note;
				this._notes.Add(noteDrawable);
			}
			#endregion

			#region Playfield decorations
			LinePrimitiveDrawable playfieldTopLine    = new(new Vector2(1, RecepticlePos.Y - 50),FurballGame.DEFAULT_WINDOW_WIDTH, 0) {
				ColorOverride = Color.Gray
			};
			LinePrimitiveDrawable playfieldBottomLine = new(new Vector2(1, RecepticlePos.Y + 50),FurballGame.DEFAULT_WINDOW_WIDTH, 0) {
				ColorOverride = Color.Gray
			};
			this.Manager.Add(playfieldTopLine);
			this.Manager.Add(playfieldBottomLine);

			RectanglePrimitiveDrawable playfieldBackgroundCover = new(new(0, RecepticlePos.Y - 50), new(FurballGame.DEFAULT_WINDOW_WIDTH, 100), 0f, true) {
				ColorOverride = new(100, 100, 100, 100),
				Depth = 0.9f
			};
			this.Manager.Add(playfieldBackgroundCover);
			
			#region background image
			this.Manager.Add(pTypingGame.CurrentSongBackground);
			
			pTypingGame.CurrentSongBackground.Tweens.Add(new ColorTween(TweenType.Color, pTypingGame.CurrentSongBackground.ColorOverride, new(1f * (1f - Config.BackgroundDim) , 1f * (1f - Config.BackgroundDim), 1f * (1f - Config.BackgroundDim)), pTypingGame.CurrentSongBackground.TimeSource.GetCurrentTime(), pTypingGame.CurrentSongBackground.TimeSource.GetCurrentTime() + 1000));
			pTypingGame.LoadBackgroundFromSong(pTypingGame.CurrentSong.Value);
			#endregion
			#region typing indicator
			this._typingIndicator = new(RecepticlePos, pTypingGame.UniFont, "", 50) {
				OriginType = OriginType.Center
			};
			
			this.Manager.Add(this._typingIndicator);
			#endregion
			#endregion
			
			this.HitSound.Load(ContentManager.LoadRawAsset("hitsound.wav", ContentSource.User));

			this.HitSound.Volume = Config.Volume;
			
			this.Play();

			FurballGame.InputManager.OnKeyDown    += this.OnKeyPress;
			FurballGame.Instance.Window.TextInput += this.OnCharacterTyped;
		}
		private void SkipButtonClick(object sender, Point e) {
			pTypingGame.MusicTrack.SeekTo(pTypingGame.CurrentSong.Value.Notes.First().Time - 3000);
		}

		protected override void Dispose(bool disposing) {
			pTypingGame.MusicTrack.Stop();
			// pTypingGame.MusicTrack.Free();
			
			FurballGame.InputManager.OnKeyDown    -= this.OnKeyPress;
			FurballGame.Instance.Window.TextInput -= this.OnCharacterTyped;

			base.Dispose(disposing);
		}

		public void OnKeyPress(object sender, Keys key) {
			if (key == Keys.Escape)
				pTypingGame.PauseResumeMusic();
		}

		public void OnCharacterTyped(object sender, TextInputEventArgs args) {
			foreach (NoteDrawable noteDrawable in this._notes) {
				Note note = noteDrawable.Note;
				// checks if the current note is already hit, if so, skip to next note
				if (note.IsHit) continue;

				bool breakOut = false;

				List<string> romajiToType   = note.ThisCharacterRomaji;
				
				//list of all the possible romaji "paths" we can take while typing
				List<string> filteredRomaji = romajiToType.Where(romaji => romaji.StartsWith(note.TypedRomaji)).ToList();

				foreach (string romaji in filteredRomaji) {
					double timeDifference = Math.Abs(pTypingGame.MusicTrack.GetCurrentTime() - note.Time);
					if (romaji[note.TypedRomaji.Length] == args.Character && timeDifference < TimingPoor) {
						if (noteDrawable.Type(romaji, timeDifference)) {
							this.HitSound.Play();
							this.NoteUpdate(true, note);
							
							breakOut = true;
						}
						this.ShowTypingIndicator(args.Character);
						
						break;
					}
				}
				
				if (pTypingGame.CurrentSong.Value.AllNotesHit()) 
					this.EndScore();

				this.UpdateNoteText();

				// This acts as a psuedo notelock, preventing you from typing the next note if the current one still has remaining letters
				if (note.Time - pTypingGame.MusicTrack.GetCurrentTime() > 0 ) break;
				if (breakOut) break;
			}
		}

		private void ShowTypingIndicator(char character) {
			this._typingIndicator.Text = character.ToString();
			
			this._typingIndicator.Tweens.Add(new ColorTween(TweenType.Color, Color.White, new(255,255,255,0), FurballGame.Time, FurballGame.Time + 400));
			this._typingIndicator.Tweens.Add(new VectorTween(TweenType.Scale, new(1.5f), new(3), FurballGame.Time, FurballGame.Time              + 400));
		}

		private void UpdateNoteText() {
			foreach (NoteDrawable noteDrawable in this._notes) {
				noteDrawable.LabelTextDrawable.Text = $"{noteDrawable.Note.Text}\n{string.Join("\n", noteDrawable.Note.ThisCharacterRomaji)}";
			}
		}

		private void NoteUpdate(bool wasHit, Note note) {
			int numberHit  = 0;
			int numberMiss = 0;
			foreach (NoteDrawable noteDrawable in this._notes) {
				switch (noteDrawable.Note.HitResult) {
					case HitResult.Excellent:
					case HitResult.Good:
					case HitResult.Fair:
					case HitResult.Poor:
						numberHit++;
						break;
					case HitResult.Miss:
						numberMiss++;
						break;
				}
			}

			if (numberHit + numberMiss == 0) this.Score.Accuracy = 1d;
			else {
				this.Score.Accuracy = numberHit / ((double)numberHit + (double)numberMiss);
			}

			if (wasHit) {
				int scoreToAdd = 0;
				switch (note.HitResult) {
					case HitResult.Excellent:
						scoreToAdd = ScoreExcellent;
						break;
					case HitResult.Fair:
						scoreToAdd = ScoreFair;
						break;
					case HitResult.Good:
						scoreToAdd = ScoreGood;
						break;
					case HitResult.Poor:
						scoreToAdd = ScorePoor;
						break;
				}
				this.Score.Score += scoreToAdd + (this.Score.Combo - 1) * 10;
				this.Score.Combo++;
			} else {
				this.Score.Combo =  0;
			}

			Color hitColor = Color.Red;
			switch (note.HitResult) {
				case HitResult.Excellent: {
					this.Score.ExcellentHits++;
					hitColor = Color.Blue;
					break;
				}
				case HitResult.Good: {
					this.Score.GoodHits++;
					hitColor = Color.Green;
					break;
				}
				case HitResult.Fair: {
					this.Score.FairHits++;
					hitColor = Color.Yellow;
					break;
				}
				case HitResult.Poor: {
					this.Score.PoorHits++;
					hitColor = Color.Orange;
					break;
				}
				case HitResult.Miss: {
					this.Score.MissHits++;
					hitColor = Color.Red;
					break;
				}
			}
			this._comboDrawable.Tweens.Add(new ColorTween(TweenType.Color, this._comboDrawable.ColorOverride, hitColor, this._comboDrawable.TimeSource.GetCurrentTime(), this._comboDrawable.TimeSource.GetCurrentTime() + 100));
			this._comboDrawable.Tweens.Add(new ColorTween(TweenType.Color, hitColor, Color.White, this._comboDrawable.TimeSource.GetCurrentTime() +100, this._comboDrawable.TimeSource.GetCurrentTime() + 1100));
		}

		public override void Update(GameTime gameTime) {
			for (int i = 0; i < this._notes.Count; i++) {
				if (this._notes[i].Note.IsHit) continue;

				NoteDrawable noteDrawable = this._notes[i];

				if (pTypingGame.MusicTrack.CurrentTime * 1000 - noteDrawable.Note.Time > TimingPoor) {
					noteDrawable.Miss();
					this.NoteUpdate(false, noteDrawable.Note);
					
					if (pTypingGame.CurrentSong.Value.AllNotesHit()) {
						this.EndScore();
					}
				}
			}
			
			this._scoreDrawable.Text    = $"{this.Score.Score:00000000}";
			this._accuracyDrawable.Text = $"{this.Score.Accuracy * 100:0.00}%";
			this._comboDrawable.Text    = $"{this.Score.Combo}x";

			if (pTypingGame.CurrentSong.Value.Notes.First().Time - pTypingGame.MusicTrack.GetCurrentTime() > 3000) {
				this._skipButton.Visible = true;
			} else {
				this._skipButton.Visible = false;
			}

			base.Update(gameTime);
		}

		public void EndScore() {
			FurballGame.Instance.ChangeScreen(new ScoreResultsScreen(this.Score));
		}

		public void Play() {
			// string qualifiedAudioPath = Path.Combine(pTypingGame.CurrentSong.Value.FileInfo.DirectoryName ?? string.Empty, pTypingGame.CurrentSong.Value.AudioPath);
			
			// pTypingGame.LoadMusic(ContentManager.LoadRawAsset(qualifiedAudioPath, ContentSource.External));
			pTypingGame.PlayMusic();
		}
	}
}
