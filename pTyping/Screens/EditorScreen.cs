using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Furball.Engine;
using Furball.Engine.Engine;
using Furball.Engine.Engine.Graphics;
using Furball.Engine.Engine.Graphics.Drawables;
using Furball.Engine.Engine.Graphics.Drawables.Primitives;
using Furball.Engine.Engine.Graphics.Drawables.Tweens;
using Furball.Engine.Engine.Graphics.Drawables.Tweens.TweenTypes;
using Furball.Engine.Engine.Graphics.Drawables.UiElements;
using Furball.Engine.Engine.Helpers;
using Furball.Engine.Engine.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using pTyping.Drawables;
using pTyping.Editor;
using pTyping.Editor.Tools;
using pTyping.Songs;

namespace pTyping.Screens {

    public class EditorScreen : Screen {
        private TextDrawable _currentTimeDrawable;

        private Texture2D                  _noteTexture;
        private UiProgressBarDrawable      _progressBar;
        private TexturedDrawable           _recepticle;

        public EditorTool       CurrentTool;
        public List<EditorTool> EditorTools;

        public readonly EditorState State = new();

        private readonly List<ManagedDrawable> _selectionRects = new();

        private readonly List<ManagedDrawable> _toolOptions = new();

        public override void Initialize() {
            base.Initialize();

            //Create a copy of the song so that we dont edit it globally
            //TODO: should `Song` be a struct?
            this.State.Song = pTypingGame.CurrentSong.Value.Copy();

            pTypingGame.MusicTrack.Stop();

            #region Gameplay preview

            this._noteTexture = ContentManager.LoadMonogameAsset<Texture2D>("note");

            Vector2 recepticlePos = new(FurballGame.DEFAULT_WINDOW_WIDTH * 0.15f, FurballGame.DEFAULT_WINDOW_HEIGHT / 2f);
            this._recepticle = new TexturedDrawable(this._noteTexture, recepticlePos) {
                Scale      = new(0.55f),
                OriginType = OriginType.Center
            };

            this.Manager.Add(this._recepticle);

            foreach (Note note in this.State.Song.Notes)
                this.CreateNote(note);

            #endregion

            #region Playfield decorations

            LinePrimitiveDrawable playfieldTopLine = new(new Vector2(1, recepticlePos.Y - 50), FurballGame.DEFAULT_WINDOW_WIDTH, 0) {
                ColorOverride = Color.Gray,
                Clickable     = false,
                CoverClicks   = false
            };
            LinePrimitiveDrawable playfieldBottomLine = new(new Vector2(1, recepticlePos.Y + 50), FurballGame.DEFAULT_WINDOW_WIDTH, 0) {
                ColorOverride = Color.Gray,
                Clickable     = false,
                CoverClicks   = false
            };
            this.Manager.Add(playfieldTopLine);
            this.Manager.Add(playfieldBottomLine);

            RectanglePrimitiveDrawable playfieldBackgroundCover = new(new(0, recepticlePos.Y - 50), new(FurballGame.DEFAULT_WINDOW_WIDTH, 100), 0f, true) {
                ColorOverride = new(100, 100, 100, 100),
                Depth         = 0.9f,
                Clickable     = false,
                CoverClicks   = false
            };
            this.Manager.Add(playfieldBackgroundCover);

            #region background image

            this.Manager.Add(pTypingGame.CurrentSongBackground);

            pTypingGame.CurrentSongBackground.Tweens.Add(
            new ColorTween(
            TweenType.Color,
            pTypingGame.CurrentSongBackground.ColorOverride,
            new(0.3f, 0.3f, 0.3f),
            pTypingGame.CurrentSongBackground.TimeSource.GetCurrentTime(),
            pTypingGame.CurrentSongBackground.TimeSource.GetCurrentTime() + 1000
            )
            );
            pTypingGame.LoadBackgroundFromSong(this.State.Song);

            #endregion

            #endregion

            #region Visualization drawables

            this.State.SelectedNotes.CollectionChanged += this.UpdateSelectionRects;

            #endregion

            #region UI

            #region Progress bar

            this._progressBar = new UiProgressBarDrawable(
            new Vector2(0, FurballGame.DEFAULT_WINDOW_HEIGHT),
            FurballGame.DEFAULT_FONT,
            new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH - 200, 40),
            Color.Gray,
            Color.DarkGray,
            Color.White
            ) {
                OriginType = OriginType.BottomLeft
            };

            this.Manager.Add(this._progressBar);

            #endregion

            #region Current time

            this._currentTimeDrawable = new TextDrawable(new Vector2(10, FurballGame.DEFAULT_WINDOW_HEIGHT - 50), FurballGame.DEFAULT_FONT, "", 30) {
                OriginType = OriginType.BottomLeft
            };

            this.Manager.Add(this._currentTimeDrawable);

            #endregion

            #region Playback buttons

            Texture2D editorButtonsTexture2D = ContentManager.LoadMonogameAsset<Texture2D>("editorbuttons", ContentSource.User);

            TexturedDrawable playButton = new(
            editorButtonsTexture2D,
            new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH - 150, FurballGame.DEFAULT_WINDOW_HEIGHT),
            TexturePositions.EDITOR_PLAY
            ) {
                Scale      = new(0.5f, 0.5f),
                OriginType = OriginType.BottomRight
            };
            TexturedDrawable pauseButton = new(
            editorButtonsTexture2D,
            new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH - 100, FurballGame.DEFAULT_WINDOW_HEIGHT),
            TexturePositions.EDITOR_PAUSE
            ) {
                Scale      = new(0.5f, 0.5f),
                OriginType = OriginType.BottomRight
            };
            TexturedDrawable rightButton = new(
            editorButtonsTexture2D,
            new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH, FurballGame.DEFAULT_WINDOW_HEIGHT),
            TexturePositions.EDITOR_RIGHT
            ) {
                Scale      = new(0.5f, 0.5f),
                OriginType = OriginType.BottomRight
            };
            TexturedDrawable leftButton = new(
            editorButtonsTexture2D,
            new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH - 50, FurballGame.DEFAULT_WINDOW_HEIGHT),
            TexturePositions.EDITOR_LEFT
            ) {
                Scale      = new(0.5f, 0.5f),
                OriginType = OriginType.BottomRight
            };

            playButton.OnClick += delegate {
                pTypingGame.PlayMusic();
            };

            pauseButton.OnClick += delegate {
                pTypingGame.PauseResumeMusic();
            };

            leftButton.OnClick += delegate {
                if (this.State.Song.Notes.Count > 0)
                    pTypingGame.MusicTrack.SeekTo(this.State.Song.Notes.First().Time);
            };

            rightButton.OnClick += delegate {
                if (this.State.Song.Notes.Count > 0)
                    pTypingGame.MusicTrack.SeekTo(this.State.Song.Notes.Last().Time);
            };

            this.Manager.Add(playButton);
            this.Manager.Add(pauseButton);
            this.Manager.Add(rightButton);
            this.Manager.Add(leftButton);

            #endregion

            #region Tool selection

            this.EditorTools = EditorTool.GetAllTools();

            float y = 10;
            foreach (EditorTool tool in this.EditorTools) {
                UiTickboxDrawable tickboxDrawable = new(new(10, y), tool.Name, 35, false, true) {
                    ToolTip = tool.Tooltip
                };
                
                tickboxDrawable.OnClick += delegate {
                    this.ChangeTool(tool.GetType());
                };

                tool.TickBoxDrawable = tickboxDrawable;

                this.Manager.Add(tickboxDrawable);

                y += tickboxDrawable.Size.Y + 10;
            }

            #endregion

            #endregion

            this.ChangeTool(typeof(SelectTool));
            
            FurballGame.InputManager.OnKeyDown     += this.OnKeyPress;
            FurballGame.InputManager.OnMouseScroll += this.OnMouseScroll;
            FurballGame.InputManager.OnMouseDown   += this.OnClick;
            FurballGame.InputManager.OnMouseMove   += this.OnMouseMove;
            FurballGame.InputManager.OnMouseDrag   += this.OnMouseDrag;

            pTypingGame.UserStatusEditing();
        }

        public void UpdateSelectionRects(object _, NotifyCollectionChangedEventArgs __) {
            this._selectionRects.ForEach(x => this.Manager.Remove(x));

            this._selectionRects.Clear();

            foreach (NoteDrawable selectedNote in this.State.SelectedNotes) {
                RectanglePrimitiveDrawable rect = new() {
                    RectSize      = new(100, 100),
                    Filled        = false,
                    Thickness     = 2f,
                    ColorOverride = Color.Gray,
                    Clickable     = false,
                    Hoverable     = false,
                    CoverClicks   = false,
                    CoverHovers   = false,
                    OriginType    = OriginType.Center,
                    TimeSource    = pTypingGame.MusicTrack
                };

                rect.Tweens.Add(
                new VectorTween(
                TweenType.Movement,
                new(PlayerScreen.NOTE_START_POS.X, PlayerScreen.NOTE_START_POS.Y + selectedNote.Note.YOffset),
                PlayerScreen.RECEPTICLE_POS,
                (int)(selectedNote.Note.Time - ConVars.BaseApproachTime.Value),
                (int)selectedNote.Note.Time
                ) {
                    KeepAlive = true
                }
                );

                this._selectionRects.Add(rect);

                this.Manager.Add(rect);
            }
        }

        private void OnMouseDrag(object sender, ((Point lastPosition, Point newPosition), string cursorName) e) {
            this.CurrentTool?.OnMouseDrag(e.Item1.newPosition);
        }

        public void CreateNote(Note note, bool isNew = false) {
            NoteDrawable noteDrawable = new(
            new Vector2(PlayerScreen.NOTE_START_POS.X, PlayerScreen.NOTE_START_POS.Y + note.YOffset),
            this._noteTexture,
            pTypingGame.JapaneseFont,
            50
            ) {
                TimeSource    = pTypingGame.MusicTrack,
                ColorOverride = note.Color,
                LabelTextDrawable = {
                    Text  = $"{note.Text}",
                    Scale = new(1f)
                },
                Scale      = new(0.55f, 0.55f),
                OriginType = OriginType.Center,
                Note       = note
            };

            noteDrawable.Tweens.Add(
            new VectorTween(
            TweenType.Movement,
            new(PlayerScreen.NOTE_START_POS.X, PlayerScreen.NOTE_START_POS.Y + note.YOffset),
            PlayerScreen.RECEPTICLE_POS,
            (int)(note.Time - ConVars.BaseApproachTime.Value),
            (int)note.Time
            ) {
                KeepAlive = true
            }
            );

            this.Manager.Add(noteDrawable);
            this.State.Notes.Add(noteDrawable);
            if (isNew)
                this.State.Song.Notes.Add(note);
        }

        public void ChangeTool(Type type) {
            EditorTool newTool = this.EditorTools.First(x => x.GetType() == type);

            object toolAsRealType = Convert.ChangeType(newTool, type);
            
            if (newTool == this.CurrentTool) return;

            foreach (EditorTool tool in this.EditorTools)
                tool.TickBoxDrawable.Selected.Value = tool == newTool;

            this.CurrentTool?.DeselectTool(this);

            this.CurrentTool = newTool;

            newTool?.SelectTool(this, ref this.Manager);

            this._toolOptions.ForEach(x => this.Manager.Remove(x));
            this._toolOptions.Clear();

            //Gets all fields with the ToolOptionAttribute
            List<FieldInfo> result = ObjectHelper.GetAllFieldsWithAttribute(type, typeof(ToolOptionAttribute));

            float y = 10;
            foreach (FieldInfo field in result) {
                string name    = field.GetCustomAttribute<ToolOptionAttribute>()!.Name;
                string tooltip = field.GetCustomAttribute<ToolOptionAttribute>()!.ToolTip;
                
                //The drawable that you interact with
                ManagedDrawable drawable;
                //The label showing what it is
                TextDrawable labelDrawable = new(new(FurballGame.DEFAULT_WINDOW_WIDTH - 10, y), pTypingGame.JapaneseFont, name, 30) {
                    OriginType = OriginType.TopRight,
                    ToolTip    = tooltip
                };
                y += labelDrawable.Size.Y + 5f;

                //Checks the type that the attribute is attatched to
                switch (field.FieldType.ToString()) {
                    //Is it a Bindable<string>
                    case "Furball.Engine.Engine.Helpers.Bindable`1[System.String]": {
                        //Get the value of the type
                        Bindable<string> value = (Bindable<string>)field.GetValue(toolAsRealType);

                        //The text box you type in
                        UiTextBoxDrawable textBox = new(new(FurballGame.DEFAULT_WINDOW_WIDTH - 10, y), pTypingGame.JapaneseFont, value.Value, 30, 300) {
                            OriginType = OriginType.TopRight
                        };

                        //When the textbox is updated, update value
                        void OnUpdate(object sender, char c) {
                            value.Value = textBox.Text;
                        }

                        textBox.OnLetterTyped   += OnUpdate;
                        textBox.OnLetterRemoved += OnUpdate;

                        //When the value changes, update the text box
                        value.OnChange += delegate(object _, string s) {
                            textBox.Text = s;
                        };
                        
                        y += textBox.Size.Y + 10f;
                        
                        drawable = textBox;

                        break;
                    }
                    //Is it a Bindable<int>
                    case "Furball.Engine.Engine.Helpers.Bindable`1[System.Int32]": {
                        //Get the value of the type
                        Bindable<int> value = (Bindable<int>)field.GetValue(toolAsRealType);

                        //The text box you type in
                        UiTextBoxDrawable textBox = new(new(FurballGame.DEFAULT_WINDOW_WIDTH - 10, y), pTypingGame.JapaneseFont, value.Value.ToString(), 30, 300) {
                            OriginType = OriginType.TopRight
                        };

                        //When the textbox is updated, update value
                        void OnUpdate(object sender, char c) {
                            if (int.TryParse(textBox.Text, out int result)) {
                                value.Value           = result;
                                textBox.ColorOverride = Color.White;
                            } else {
                                textBox.ColorOverride = Color.Red;
                            }
                        }

                        textBox.OnLetterTyped   += OnUpdate;
                        textBox.OnLetterRemoved += OnUpdate;

                        //When the value changes, update the text box
                        value.OnChange += delegate(object _, int s) {
                            textBox.Text = s.ToString();

                            textBox.ColorOverride = Color.White;
                        };

                        y += textBox.Size.Y + 10f;

                        drawable = textBox;

                        break;
                    }
                    default: {
                        drawable = new BlankDrawable();

                        break;
                    }
                }

                this._toolOptions.Add(labelDrawable);
                this._toolOptions.Add(drawable);
                this.Manager.Add(labelDrawable);
                this.Manager.Add(drawable);
            }
        }

        private void OnMouseMove(object sender, (Point mousePos, string cursorName) e) {
            double currentTime  = pTypingGame.MusicTrack.GetCurrentTime();
            double reticuleXPos = this._recepticle.Position.X;
            double noteStartPos = FurballGame.DEFAULT_WINDOW_WIDTH + 100;

            double speed = (noteStartPos - reticuleXPos) / ConVars.BaseApproachTime.Value;

            double relativeMousePosition = (e.mousePos.X - reticuleXPos) * 0.925;

            double timeAtCursor = relativeMousePosition / speed + currentTime;

            TimingPoint timingPoint = this.State.Song.CurrentTimingPoint(timeAtCursor);

            double noteLength = this.State.Song.DividedNoteLength(timeAtCursor);

            timeAtCursor += noteLength / 2d;

            double roundedTime = timeAtCursor - (timeAtCursor - timingPoint.Time) % noteLength;

            this.State.MouseTime = roundedTime;

            this.CurrentTool?.OnMouseMove(e.mousePos);
        }

        private void OnClick(object sender, ((MouseButton mouseButton, Point position) args, string cursorName) e) {
            this.CurrentTool?.OnMouseClick(e.args);
        }

        public static bool InPlayfield(Point pos) => pos.Y < PlayerScreen.RECEPTICLE_POS.Y + 40f && pos.Y > PlayerScreen.RECEPTICLE_POS.Y - 40f;

        protected override void Dispose(bool disposing) {
            FurballGame.InputManager.OnKeyDown     -= this.OnKeyPress;
            FurballGame.InputManager.OnMouseScroll -= this.OnMouseScroll;
            FurballGame.InputManager.OnMouseDown   -= this.OnClick;
            FurballGame.InputManager.OnMouseMove   -= this.OnMouseMove;
            FurballGame.InputManager.OnMouseDrag   -= this.OnMouseDrag;

            this.State.SelectedNotes.CollectionChanged -= this.UpdateSelectionRects;

            base.Dispose(disposing);
        }

        private void OnMouseScroll(object sender, (int scrollAmount, string) args) {
            this.TimelineMove(args.scrollAmount <= 0);
        }

        public void TimelineMove(bool right) {
            double currentTime = pTypingGame.MusicTrack.CurrentTime * 1000d;

            double noteLength = this.State.Song.DividedNoteLength(currentTime);
            double timeToSeekTo = Math.Round((pTypingGame.MusicTrack.CurrentTime * 1000d - this.State.Song.CurrentTimingPoint(currentTime).Time) / noteLength) *
                                  noteLength;

            timeToSeekTo += this.State.Song.CurrentTimingPoint(currentTime).Time;

            if (right)
                timeToSeekTo += noteLength;
            else
                timeToSeekTo -= noteLength;

            pTypingGame.MusicTrack.SeekTo(Math.Max(timeToSeekTo, 0));
        }

        public void DeleteSelectedNotes() {
            for (int i = 0; i < this.State.SelectedNotes.Count; i++) {
                NoteDrawable note = this.State.SelectedNotes[i];

                this.Manager.Remove(note);
                this.State.Song.Notes.Remove(note.Note);
                this.State.Notes.Remove(note);
            }
        }

        private void OnKeyPress(object sender, Keys key) {
            this.CurrentTool?.OnKeyPress(key);
            
            switch (key) {
                case Keys.Space:
                    pTypingGame.PauseResumeMusic();
                    break;
                case Keys.Left: {
                    this.TimelineMove(false);

                    break;
                }
                case Keys.Right: {
                    this.TimelineMove(true);

                    break;
                }
                case Keys.Escape:
                    //If the user has some notes selected, clear them
                    if (this.State.SelectedNotes.Count != 0) {
                        this.State.SelectedNotes.Clear();
                        return;
                    }

                    pTypingGame.MenuClickSound.Play();

                    // Exit the editor
                    ScreenManager.ChangeScreen(new SongSelectionScreen(true));
                    break;
                case Keys.Delete: {
                    // Delete the selected notes
                    this.DeleteSelectedNotes();
                    this.State.SelectedNotes.Clear();
                    break;
                }
                case Keys.S when FurballGame.InputManager.HeldKeys.Contains(Keys.LeftControl): {
                    // Save the song if ctrl+s is pressed
                    this.State.Song.Save();
                    SongManager.UpdateSongs();
                    break;
                }
                case Keys.D1: {
                    this.ChangeTool(typeof(SelectTool));
                    break;
                }
                case Keys.D2: {
                    this.ChangeTool(typeof(CreateTool));
                    break;
                }
            }
        }

        public override void Update(GameTime gameTime) {
            this.State.CurrentTime = pTypingGame.MusicTrack.GetCurrentTime();

            for (int i = 0; i < this.State.Notes.Count; i++) {
                NoteDrawable noteDrawable = this.State.Notes[i];

                if (this.State.CurrentTime > noteDrawable.Note.Time + 10 || this.State.CurrentTime < noteDrawable.Note.Time - ConVars.BaseApproachTime.Value) 
                    noteDrawable.Visible = false;
                else 
                    noteDrawable.Visible = true;
            }

            int milliseconds = (int)Math.Floor(this.State.CurrentTime         % 1000d);
            int seconds      = (int)Math.Floor(this.State.CurrentTime / 1000d % 60d);
            int minutes      = (int)Math.Floor(this.State.CurrentTime         / 1000d / 60d);

            this._currentTimeDrawable.Text = $"Time: {minutes:00}:{seconds:00}:{milliseconds:000}";
            this._progressBar.Progress     = (float)pTypingGame.MusicTrack.CurrentTime * 1000f / (float)pTypingGame.MusicTrack.Length;

            base.Update(gameTime);
        }
    }
}
