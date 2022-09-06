using CaveStoryModdingFramework.Maps;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CaveStoryModdingFramework.Editors
{
    [Serializable]
    public class TileSelection : PropertyChangedHelper
    {
        Map contents;
        int cursorX, cursorY;
        public Map Contents { get => contents; set => SetVal(ref contents, value); }
        public int CursorX { get => cursorX; set => SetVal(ref cursorX, value); }
        public int CursorY { get => cursorY; set => SetVal(ref cursorY, value); }
        public TileSelection() : this(0, 0, new Map(1, 1, 0)) { }
        public TileSelection(int x, int y, Map contents)
        {
            CursorX = x;
            CursorY = y;
            Contents = contents;
        }
        public byte? CoordsToTile(int x, int y)
        {
            var ax = (CursorX + x) % Contents.Width;
            if (ax < 0)
                ax += contents.Width;

            var ay = (CursorY + y) % Contents.Height;
            if(ay < 0)
                ay += Contents.Height;
            return Contents.Tiles[(ay * Contents.Width) + ax];
        }
    }
    public enum TileEditorActions
    {
        None = 0,
        Draw,
        Rectangle,
        Fill,
        Replace,
        Select,
    }
    /// <summary>
    /// Used for tracking Draw and Fill changes
    /// </summary>
    [DebuggerDisplay("{Old} -> {New}")]
    public class TileChange
    {
        public byte Old { get; }
        public byte New { get; set; }
        public TileChange(byte old, byte @new)
        {
            Old = old;
            New = @new;
        }
    }
    /// <summary>
    /// Used for tracking the rectangle tool
    /// </summary>
    [DebuggerDisplay("{StartIndex} -> {Old.Width}x{Old.Height}")]
    public class RectangleChange
    {
        public int StartIndex { get; set; }
        public Map Old { get; }
        public Map New { get; }

        public RectangleChange(int startIndex, Map old, Map @new)
        {
            StartIndex = startIndex;
            Old = old;
            New = @new;
        }
    }

    public class TileEditor : PropertyChangedHelper
    {
        public Map Tiles { get; private set; }

        public ChangeTracker<object> History { get; }
        public Dictionary<int, TileChange> TileChangeQueue { get; } = new Dictionary<int, TileChange>();
        void QueueTileChange(int x, int y, byte? @new)
        {
            if(@new != null)
                QueueTileChange((y * Tiles.Width) + x, (byte)@new);
        }
        void QueueTileChange(int x, int y, byte @new)
        {
            QueueTileChange((y * Tiles.Width) + x, @new);
        }
        void QueueTileChange(int index, byte? @new)
        {
            if (@new != null)
                QueueTileChange(index, (byte)@new);
        }
        void QueueTileChange(int index, byte @new)
        {
            if (TileChangeQueue.ContainsKey(index))
                TileChangeQueue[index].New = @new;
            else
                TileChangeQueue.Add(index, new TileChange((byte)Tiles.Tiles[index], @new));
        }
        public TileSelection Selection { get; }


        public int SelectionStartX { get; private set; }
        public int SelectionStartY { get; private set; }
        public int SelectionEndX { get; private set; }
        public int SelectionEndY { get; private set; }
        TileEditorActions currentAction = TileEditorActions.None;
        public TileEditorActions CurrentAction { get => currentAction; private set => SetVal(ref currentAction, value); }
        
        public void BeginSelection(int x, int y, TileEditorActions action)
        {
            TileChangeQueue.Clear();

            SelectionStartX = SelectionEndX = x;
            SelectionStartY = SelectionEndY = y;
            CurrentAction = action;

            switch (CurrentAction)
            {
                case TileEditorActions.Draw:
                    QueueDraw(x, y);
                    break;
                case TileEditorActions.Replace:
                    QueueReplace(x, y);
                    break;
                case TileEditorActions.Fill:
                    QueueFill(x, y);
                    break;
            }
        }
        public void MoveSelection(int x, int y)
        {
            SelectionEndX = x;
            SelectionEndY = y;
            switch (CurrentAction)
            {
                case TileEditorActions.Draw:
                    QueueDraw(x, y);
                    break;
                case TileEditorActions.Replace:
                    QueueReplace(x, y);
                    break;
                case TileEditorActions.Fill:
                    QueueFill(x, y);
                    break;
            }
        }
        public void CommitSelection()
        {
            switch (CurrentAction)
            {
                case TileEditorActions.Draw:
                case TileEditorActions.Replace:
                case TileEditorActions.Fill:
                    if (TileChangeQueue.Count > 0)
                    {

                        History.Add(new Dictionary<int, TileChange>(TileChangeQueue));
                        TileChangeQueue.Clear();
                    }
                    break;
                case TileEditorActions.Rectangle:
                    CalculateRectangle(SelectionStartX, SelectionStartY, SelectionEndX, SelectionEndY);
                    break;
                case TileEditorActions.Select:
                    SelectFromMap(SelectionStartX, SelectionStartY, SelectionEndX, SelectionEndY);
                    break;
            }
            CurrentAction = TileEditorActions.None;
        }

        /// <summary>
        /// Copies the entire source map to the given position in the destination map
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="index"></param>
        static void CopyMap(Map source, Map destination, int index)
        {
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    destination.Tiles[index + (y * destination.Width) + x] = source.Tiles[(y * source.Width) + x];
                }
            }
        }
        public void OnUndoRequested(object sender, HistoryChangingEventArgs<object> e)
        {
            if (!e.Handled)
            {
                if (e.Change is Dictionary<int, TileChange> tc)
                {
                    foreach (var change in tc)
                    {
                        Tiles.Tiles[change.Key] = change.Value.Old;
                    }
                    e.Handled = true;
                }
                else if (e.Change is RectangleChange rc)
                {
                    CopyMap(rc.Old, Tiles, rc.StartIndex);
                    e.Handled = true;
                }
            }
        }
        public void OnRedoRequest(object sender, HistoryChangingEventArgs<object> e)
        {
            if (!e.Handled)
            {
                if (e.Change is Dictionary<int, TileChange> tc)
                {
                    foreach (var change in tc)
                    {
                        Tiles.Tiles[change.Key] = change.Value.New;
                    }
                    e.Handled = true;
                }
                else if (e.Change is RectangleChange rc)
                {
                    CopyMap(rc.New, Tiles, rc.StartIndex);
                    e.Handled = true;
                }
            }
        }

        void QueueDraw(int x, int y)
        {
            var s = Selection;
            for (int iy = 0; iy < s.Contents.Height; iy++)
            {
                for (int ix = 0; ix < s.Contents.Width; ix++)
                {
                    var thisX = x + ix - s.CursorX;
                    var thisY = y + iy - s.CursorY;
                    var thisTile = s.Contents.Tiles[(iy * s.Contents.Width) + ix];
                    if (thisTile != null &&
                        0 <= thisX && thisX < Tiles.Width &&
                        0 <= thisY && thisY < Tiles.Height)
                    {
                        QueueTileChange(thisX, thisY, (byte)thisTile);
                    }
                }
            }
        }
        void CalculateRectangle(int x1, int y1, int x2, int y2)
        {
            var Left = Math.Min(x1, x2).Clamp(0, Tiles.Width);
            var Width = Math.Max(x1, x2).Clamp(0, Tiles.Width) - Left + 1;

            var Top = Math.Min(y1, y2).Clamp(0, Tiles.Height);
            var Height = Math.Max(y1, y2).Clamp(0, Tiles.Height) - Top + 1;

            var old = new Map((short)Width, (short)Height);
            var @new = new Map((short)Width, (short)Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    //TODO maybe redo some of this math?
                    //it's tricky 'cause starting at 0 makes writing coords easier
                    //but stating at Left/Top makes these coords easier, so...
                    var oldt = Tiles.Tiles[((Top + y) * Tiles.Width) + Left + x];
                    var newt = Selection.CoordsToTile(x + Left - x1, y + Top - y1);

                    var outIndex = (y * Width) + x;
                    old.Tiles[outIndex] = oldt;
                    @new.Tiles[outIndex] = newt ?? oldt;
                }
            }
            History.Add(new RectangleChange((Top * Tiles.Width) + Left, old, @new));
        }

        HashSet<byte> CalculatedReplaces = new HashSet<byte>();
        void QueueReplace(int x, int y)
        {
            var hoveredTile = (byte)Tiles.Tiles[(y * Tiles.Width) + x];
            if(!CalculatedReplaces.Contains(hoveredTile))
            {
                var replacement = Selection.CoordsToTile(x, y);
                if(replacement != null)
                {
                    for (int i = 0; i < Tiles.Tiles.Count; i++)
                    {
                        if (Tiles.Tiles[i] == hoveredTile)
                        {
                            QueueTileChange(i, (byte)replacement);
                        }
                    }
                }
                CalculatedReplaces.Add(hoveredTile);
            }
        }

        [DebuggerDisplay("X = [{x1} - {x2}] Y = {y} Direction = {direction}")]
        struct FillInfo
        {
            public readonly int x1,x2,y,direction;

            public FillInfo(int a, int b, int c, int d)
            {
                this.x1 = a;
                this.x2 = b;
                this.y = c;
                this.direction = d;
            }
        }
        void QueueFill(int x, int y)
        {
            var startIndex = (y * Tiles.Width) + x;
            //span/seed fill adapted from wikipedia

            if (!TileChangeQueue.ContainsKey(startIndex))
            {
                var f = (byte)Tiles.Tiles[startIndex];

                bool Inside(int index)
                {
                    //valid index
                    return 0 <= index && index < Tiles.Tiles.Count
                        //hasn't already been changed
                        && !TileChangeQueue.ContainsKey(index) && Tiles.Tiles[index] == f;
                }
                void SafeAdd(int _x, int _y)
                {
                    QueueTileChange(_x, _y,
                        Selection.CoordsToTile(_x - SelectionStartX, _y - SelectionStartY));
                }

                var q = new Queue<FillInfo>();
                q.Enqueue(new FillInfo(x, x, y, 1));
                q.Enqueue(new FillInfo(x, x, y - 1, -1));
                while(q.Count > 0)
                {
                    var line = q.Dequeue();
                    //shortcut for lines that go off the top/bottom
                    if (line.y < 0 || Tiles.Height <= line.y)
                        continue;
                    var leftIndex = line.y * Tiles.Width;
                    var rightIndex = leftIndex + Tiles.Width - 1;
                    var leftScanner = line.x1;
                    
                    //find the leftmost tile in the row
                    if(leftIndex <= leftIndex + leftScanner && Inside(leftIndex + leftScanner))
                    {
                        while (leftIndex <= leftIndex + leftScanner - 1 && Inside(leftIndex + leftScanner - 1))
                        {
                            //fill in all tiles found on the journey
                            //EXCEPT THE STARTING TILE (that's done down below)
                            SafeAdd(--leftScanner, line.y);
                        } 
                    }
                    //leftScanner is now the leftmost tile in this row

                    //if we're overhanging, add a line in the opposite vertical direction
                    if (leftScanner < line.x1)
                        q.Enqueue(new FillInfo(leftScanner, line.x1 - 1, line.y - line.direction, -line.direction));

                    var rightScanner = line.x1;
                    //while there's still guarenteed tiles in this line...
                    while(rightScanner <= line.x2)
                    {
                        //look for eligable tiles
                        while (leftIndex + rightScanner <= rightIndex && Inside(leftIndex + rightScanner))
                        {
                            //draw them
                            SafeAdd(rightScanner, line.y);
                            //continue scanning in the current vertical direction
                            q.Enqueue(new FillInfo(leftScanner, rightScanner, line.y + line.direction, line.direction));
                            //if we passed the known right edge, add a line going the opposite vertical direction on that overhang
                            if (rightScanner > line.x2)
                                q.Enqueue(new FillInfo(line.x2 + 1, rightScanner, line.y - line.direction, -line.direction));
                            rightScanner++;
                        }
                        //skip past whatever tile we stopped at on this line
                        rightScanner++;

                        //skip any non-eligable tiles in this row EXCEPT THE LAST ONE
                        //we want to loop around to hit that instead
                        while (rightScanner < line.x2 && !Inside(leftIndex + rightScanner))
                            rightScanner++;
                        leftScanner = rightScanner;
                    }
                }
            }

            /*
             fn fill(x, y):
                //this check won't exist
              if not Inside(x, y) then return
              
            let s = new empty queue or stack
              Add (x, x, y, 1) to s
              Add (x, x, y - 1, -1) to s

              while s is not empty:
                Remove an (x1, x2, y, dy) from s
                let x = x1
                //scan for a new left edge, setting tiles as we go
                if Inside(x, y):
                  while Inside(x - 1, y):
                    Set(x - 1, y)
                    x = x - 1
                //x is now the new leftmost edge for this row
                //if we made it further left, add a line that mimics the overhang in one direction
                if x < x1:
                  Add (x, x1-1, y-dy, -dy) to s
                
                //go over the main part of the line
                while x1 <= x2:
                  
                  while Inside(x1, y):
                    Set(x1, y) //do the normal drawing
                    x1 = x1 + 1
                    //add a new line that's one above
                    Add (x, x1 - 1, y+dy, dy) to s
                    //if we've passed the right edge, add a new line one above?
                    if x1 - 1 > x2:
                      Add (x2 + 1, x1 - 1, y-dy, -dy) to s
                  //
                  x1 = x1 + 1
                  while x1 < x2 and not Inside(x1, y):
                    x1 = x1 + 1
                  x = x1
            */

        }

        //TODO this is mostly copy-paste from TKT's code, but the other one uses an Avalonia rect, so...
        static System.Drawing.Rectangle PointsToRect(int x1, int y1, int x2, int y2)
        {
            var left = Math.Min(x1, x2);
            var top = Math.Min(y1, y2);
            var right = Math.Max(x2, x1);
            var bottom = Math.Max(y2, y1);
            return System.Drawing.Rectangle.FromLTRB(left, top, right, bottom);
        }
        public void SelectFromMap(int x1, int y1, int x2, int y2)
        {
            var r = PointsToRect(x1, y1, x2, y2);
            var newSel = new Map((short)(r.Width + 1), (short)(r.Height + 1));
            int i = 0;
            for(int y = r.Top; y <= r.Bottom; y++)
            {
                for(int x = r.Left; x <= r.Right; x++)
                {
                    newSel.Tiles[i++] = Tiles.Tiles[(y * Tiles.Width) + x];
                }
            }
            Selection.Contents = newSel;
            Selection.CursorX = x2 - r.Left;
            Selection.CursorY = y2 - r.Top;
        }

        public TileEditor(string pxmPath, ChangeTracker<object> changeTracker)
        {
            Tiles = new Map(pxmPath);

            History = changeTracker;
            History.UndoRequested += OnUndoRequested;
            History.RedoRequested += OnRedoRequest;

            Selection = new TileSelection();
        }

        public void Save(string pxmPath)
        {
            Tiles.Save(pxmPath);
        }
    }
}
