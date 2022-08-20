using System;
using System.Collections.Generic;
using System.Text;

namespace CaveStoryModdingFramework.Utilities
{
    public class HistoryChangingEventArgs<T> : EventArgs
    {
        public T Change { get; }
        public bool Handled { get; set; } = false;

        public HistoryChangingEventArgs(T change)
        {
            Change = change;
        }
    }

    public class ChangeTracker<T>
    {
        public event EventHandler<HistoryChangingEventArgs<T>> UndoRequested, RedoRequested;
        public bool AtPresent => CurrentIndex == PresentIndex;
        int PresentIndex = -1;
        int CurrentIndex { get; set; } = -1;
        List<T> Changes { get; set; } = new List<T>();
        public void Add(T item)
        {
            if(CurrentIndex + 1 < Changes.Count)
            {
                if (CurrentIndex < PresentIndex)
                    PresentIndex = -2;
                Changes.RemoveRange(CurrentIndex + 1, Changes.Count - (CurrentIndex + 1));
            }
            Changes.Add(item);
            Redo();
        }
        public void Undo()
        {
            if(-1 < CurrentIndex)
            {
                var args = new HistoryChangingEventArgs<T>(Changes[CurrentIndex--]);
                UndoRequested?.Invoke(this, args);
                if (!args.Handled)
                    throw new ArgumentException();
            }
        }
        public void Redo()
        {
            if (CurrentIndex < Changes.Count - 1)
            {
                var args = new HistoryChangingEventArgs<T>(Changes[++CurrentIndex]);
                RedoRequested?.Invoke(this, args);
                if (!args.Handled)
                    throw new ArgumentException();
            }
        }
        public void UpdatePresent()
        {
            PresentIndex = CurrentIndex;
        }
    }
}
