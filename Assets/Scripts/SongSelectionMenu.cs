using UnityEngine;

namespace CandyCruisers
{
    public sealed class SongSelectionMenu
    {
        public bool IsOpen { get; private set; }
        public int FocusedSong { get; private set; }
        public void Open() { IsOpen = true; FocusedSong = 0; }
        public void Close() => IsOpen = false;

        public readonly struct Layout
        {
            public readonly Rect Panel;
            public readonly int Rows;
            public readonly float RowHeight;
            private readonly float buttonHeight;
            public Layout(Vector2 screen, Rect safeArea, float scale, bool mobile = false)
            {
                var safe = SafeRect(screen, safeArea, scale);
                RowHeight = mobile ? Mathf.Min(Mathf.Clamp(screen.x / scale * .12f, 48, 72),
                    Mathf.Max(36, (safe.height - 112) / 3)) : 48;
                buttonHeight = RowHeight - 4;
                float fixedHeight = 104 + buttonHeight * 2;
                Rows = Mathf.Clamp(Mathf.FloorToInt((safe.height - 16 - fixedHeight) / RowHeight), 1, 8);
                var size = new Vector2(Mathf.Min(520, safe.width - 16), fixedHeight + Rows * RowHeight);
                Panel = new Rect(safe.center - size / 2, size);
            }
            public Rect Row(int index) => new Rect(Panel.x + 12, Panel.y + 82 + index * RowHeight, Panel.width - 24, RowHeight - 4);
            public Rect Previous => new Rect(Panel.x + 12, Panel.yMax - 16 - buttonHeight * 2, (Panel.width - 32) / 2, buttonHeight);
            public Rect Next => new Rect(Previous.xMax + 8, Previous.y, Previous.width, buttonHeight);
            public Rect Back => new Rect(Panel.x + 12, Panel.yMax - 10 - buttonHeight, Panel.width - 24, buttonHeight);
        }

        private static Rect SafeRect(Vector2 screen, Rect safeArea, float scale) =>
            new Rect(safeArea.x / scale, (screen.y - safeArea.yMax) / scale, safeArea.width / scale, safeArea.height / scale);

        public static Rect ButtonRect(Vector2 screen, Rect safeArea, float scale, bool mobile = false)
        {
            var safe = SafeRect(screen, safeArea, scale);
            float width = Mathf.Min(180, (safe.width - 24) / 2);
            float height = mobile ? Mathf.Clamp(screen.x / scale * .12f, 44, 72) : 44;
            return new Rect(safe.xMax - width - 8, safe.y + 8, width, height);
        }

        public bool HandleKey(KeyCode key, GameplayMusicPlayer music, Layout layout)
        {
            if (!IsOpen) return false;
            if (key == KeyCode.Escape || key == KeyCode.P || key == KeyCode.S) Close();
            else if (key == KeyCode.UpArrow) FocusedSong = Mathf.Max(0, FocusedSong - 1);
            else if (key == KeyCode.DownArrow) FocusedSong = Mathf.Min(music.TrackCount - 1, FocusedSong + 1);
            else if (key == KeyCode.LeftArrow) ChangePage(-1, music.TrackCount, layout.Rows);
            else if (key == KeyCode.RightArrow) ChangePage(1, music.TrackCount, layout.Rows);
            else if (key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter) Toggle(music);
            return true;
        }

        public void Activate(Vector2 point, GameplayMusicPlayer music, Layout layout)
        {
            int start = FocusedSong / layout.Rows * layout.Rows;
            for (int row = 0; row < layout.Rows && start + row < music.TrackCount; row++)
                if (layout.Row(row).Contains(point)) { FocusedSong = start + row; Toggle(music); return; }
            if (layout.Previous.Contains(point)) ChangePage(-1, music.TrackCount, layout.Rows);
            else if (layout.Next.Contains(point)) ChangePage(1, music.TrackCount, layout.Rows);
            else if (layout.Back.Contains(point)) Close();
        }

        private void ChangePage(int direction, int count, int rows) =>
            FocusedSong = Mathf.Clamp(FocusedSong / rows + direction, 0, Mathf.Max(0, (count - 1) / rows)) * rows;

        private void Toggle(GameplayMusicPlayer music)
        {
            var names = music.SoundtrackNames;
            if (FocusedSong >= 0 && FocusedSong < names.Length)
                music.SetSongSelected(names[FocusedSong], !SongSelectionSettings.IsSelected(names[FocusedSong]));
        }

        public static void DrawButton(Rect rect, Font font) => DrawControl(rect, "Song Selection (S)", font, 15);

        public void Draw(GameplayMusicPlayer music, Layout layout, Vector2 canvas, Font font)
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .97f);
            GUI.DrawTexture(new Rect(Vector2.zero, canvas), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(layout.Panel, "");
            var title = new Rect(layout.Panel.x + 12, layout.Panel.y + 8, layout.Panel.width - 24, 32);
            DrawLabel(title, "SONG SELECTION", font, 22, TextAnchor.MiddleCenter);
            var names = music.SoundtrackNames;
            int count = SongSelectionSettings.SelectedCount(names);
            DrawLabel(new Rect(title.x, title.yMax + 2, title.width, 22), count + " of " + names.Length + " selected", null, 15, TextAnchor.MiddleCenter);
            DrawLabel(new Rect(title.x, title.yMax + 24, title.width, 20), "Keep at least one song selected.", null, 13, TextAnchor.MiddleCenter);
            int start = FocusedSong / layout.Rows * layout.Rows;
            for (int row = 0; row < layout.Rows && start + row < names.Length; row++)
            {
                int index = start + row;
                bool selected = SongSelectionSettings.IsSelected(names[index]);
                var rect = layout.Row(row);
                GUI.color = index == FocusedSong ? new Color(.5f, .9f, 1) : Color.white;
                if (selected && count == 1) GUI.color = new Color(.65f, .7f, .75f);
                GUI.Box(rect, "", GUI.skin.button);
                var box = new Rect(rect.x + 10, rect.center.y - 11, 22, 22);
                GUI.Box(box, "");
                if (selected)
                {
                    GUI.DrawTexture(new Rect(box.x + 4, box.y + 4, 14, 14), Texture2D.whiteTexture);
                    GUI.color = new Color(.05f, .15f, .2f);
                    DrawLabel(box, "✓", null, 18, TextAnchor.MiddleCenter);
                }
                GUI.color = Color.white;
                DrawLabel(new Rect(rect.x + 42, rect.y, rect.width - 50, rect.height),
                    SongSelectionSettings.DisplayName(names[index]), null, 18, TextAnchor.MiddleLeft);
            }
            int page = start / layout.Rows, pages = Mathf.Max(1, Mathf.CeilToInt(names.Length / (float)layout.Rows));
            GUI.color = page > 0 ? Color.white : Color.gray;
            DrawControl(layout.Previous, "< Previous", null, 16);
            GUI.color = page + 1 < pages ? Color.white : Color.gray;
            DrawControl(layout.Next, "Next >  " + (page + 1) + "/" + pages, null, 16);
            GUI.color = Color.white;
            DrawControl(layout.Back, "Back (Esc)", null, 18);
            GUI.color = previous;
        }

        private static void DrawControl(Rect rect, string text, Font font, int size)
        {
            GUI.Box(rect, "", GUI.skin.button);
            DrawLabel(rect, text, font, size, TextAnchor.MiddleCenter);
        }

        private static void DrawLabel(Rect rect, string text, Font font, int size, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = alignment };
            while (style.fontSize > 9 && style.CalcSize(new GUIContent(text)).x > rect.width - 8) style.fontSize--;
            GUI.Label(rect, text, style);
        }
    }
}
