using CaveStoryModdingFramework.Entities;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CaveStoryModdingFramework.Editors
{
    public class EntityChanged
    {
        public Dictionary<Entity, short> Selection { get; }
        public string Property { get; }
        public short NewValue { get; }
        public EntityChanged(Dictionary<Entity, short> selection, string property, short newValue)
        {
            Selection = selection;
            Property = property;
            NewValue = newValue;
        }
    }
    public class EntitiesCreated
    {
        public List<Entity> Entities { get; }

        public EntitiesCreated(IEnumerable<Entity> entities)
        {
            Entities = new List<Entity>(entities);
        }
        public EntitiesCreated(params Entity[] entities)
        {
            Entities = new List<Entity>(entities);
        }
    }
    public class EntityListChanged
    {
        public List<Entity> Before { get; }
        public List<Entity> After { get; }

        public List<Entity> Added { get; }
        public List<Entity> Removed { get; }

        public EntityListChanged(List<Entity> before, List<Entity> after)
        {
            Before = before;
            After = after;
            Added = After.Where(x => !Before.Contains(x)).ToList();
            Removed = Before.Where(x => !After.Contains(x)).ToList();
        }
    }
    public class EntitiesMoved
    {
        public HashSet<Entity> Entities { get; }
        public short XDiff { get; }
        public short YDiff { get; }
        public EntitiesMoved(HashSet<Entity> entities, short xDiff, short yDiff)
        {
            Entities = entities;
            XDiff = xDiff;
            YDiff = yDiff;
        }
    }
    public enum EntityEditorActions
    {
        None = 0,
        //no history
        Select,
        //commit on end
        Move,
        //commit immidietly
        Create,
        //commit immidietly
        Copy,
        //commit immidietly
        Paste
        //editing entity properties should instantly commit
        //reordering the list should wait until the reorder is done
        //  REORDER CAN JUST BE ONE FUNCTION????????
    }
    public class EntityEditor : PropertyChangedHelper
    {
        List<Entity> entities;
        public List<Entity> Entities { get => entities; private set => SetVal(ref entities, value); }
        public HashSet<Entity> Selection { get; } = new HashSet<Entity>();
        
        const string SELECTED = "Selected";
        private PropertyInfo GetSafeProperty(string propertyName)
        {
            if (propertyName.StartsWith(SELECTED))
                propertyName = propertyName.Remove(0, SELECTED.Length);
            return typeof(Entity).GetProperty(propertyName);
        }
        private T GetProperty<T>(Entity e, PropertyInfo property)
        {
            return (T)property.GetValue(e);
        }
        private T? GetProperty<T>([CallerMemberName] string propertyName = "") where T : struct
        {
            var property = GetSafeProperty(propertyName);

            T? val = null;
            foreach(var entity in Selection)
            {
                if (val == null)
                    val = GetProperty<T>(entity, property);
                else if (!val.Equals(GetProperty<T>(entity, property)))
                    return null;
            }
            return val;
        }
        private void QueuePropertyChange(short? value, [CallerMemberName] string propertyName = "")
        {
            //setting to null isn't allowed
            //just pretend a change happened so the UI updates
            if (value == null)
            {
                NotifyPropertyChanging(propertyName);
                NotifyPropertyChanged(propertyName);
            }
            else
            {
                var dict = new Dictionary<Entity, short>(Selection.Count);
                var property = GetSafeProperty(propertyName);

                foreach (var entity in Selection)
                {
                    dict.Add(entity, (short)property.GetValue(entity));
                }

                History.Add(new EntityChanged(dict, property.Name, (short)value));
            }
        }
        public short? SelectedX { get => GetProperty<short>(); set => QueuePropertyChange(value); }
        public short? SelectedY { get => GetProperty<short>(); set => QueuePropertyChange(value); }
        public short? SelectedFlag { get => GetProperty<short>(); set => QueuePropertyChange(value); }
        public short? SelectedEvent { get => GetProperty<short>(); set => QueuePropertyChange(value); }
        public short? SelectedType { get => GetProperty<short>(); set => QueuePropertyChange(value); }
        public short? SelectedBits { get => (short?)GetProperty<EntityFlags>(); set => QueuePropertyChange(value); }

        public EntityEditorActions CurrentAction { get; private set; }

        public ChangeTracker<object> History { get; }

        public EntityEditor(ChangeTracker<object> history) : this(new List<Entity>(), history) { }
        public EntityEditor(string pxePath, ChangeTracker<object> history) : this(PXE.Read(pxePath), history) { }
        public EntityEditor(List<Entity> entities, ChangeTracker<object> history)
        {
            Entities = entities;

            History = history;
            History.UndoRequested += OnUndoRequested;
            History.RedoRequested += OnRedoRequested;
        }

        void SetEntities(IEnumerable<Entity> ents)
        {
            Entities.Clear();
            Entities.AddRange(ents);
        }

        #region Looking for entities
        //TODO there should probably be a safe version of this
        //that doesn't let you add ANY entity
        //only ones that are in the current list
        //but that's gonna be like O(n^2), so...
        public void SetSelection(IEnumerable<Entity> sel)
        {
            Selection.Clear();
            foreach(var entity in sel)
                Selection.Add(entity);
        }
        public List<Entity> GetEntitiesByEvent(short eventNum)
        {
            var result = new List<Entity>();
            foreach (var e in Entities)
                if (e.Event == eventNum)
                    result.Add(e);
            return result;
        }
        public List<Entity> GetEntitiesByType(short type)
        {
            var result = new List<Entity>();
            foreach (var e in Entities)
                if (e.Type == type)
                    result.Add(e);
            return result;
        }
        public bool AnySelectedEntitiesAt(short x, short y)
        {
            foreach (var e in Selection)
                if (e.X == x && e.Y == y)
                    return true;
            return false;
        }
        public List<Entity> GetEntitiesAt(short x, short y)
        {
            var result = new List<Entity>();
            foreach (var e in Entities)
                if (e.X == x && e.Y == y)
                    result.Add(e);
            return result;
        }
        public void SelectEntitiesInRange(short x1, short y1, short x2, short y2)
        {
            //this is still my favorite way to swap variables
            if (x2 < x1)
                (x1, x2) = (x2, x1);
            if (y2 < y1)
                (y1, y2) = (y2, y1);

            Selection.Clear();
            foreach (var e in Entities)
            {
                if(x1 <= e.X && e.X <= x2 &&
                   y1 <= e.Y && e.Y <= y2)
                {
                    Selection.Add(e);
                }
            }
            TriggerSelectionChanged();
        }
        #endregion

        void TriggerSelectionChanged()
        {
            NotifyPropertyChanged(nameof(Selection));
            NotifyPropertyChanged(nameof(SelectedX));
            NotifyPropertyChanged(nameof(SelectedY));
            NotifyPropertyChanged(nameof(SelectedFlag));
            NotifyPropertyChanged(nameof(SelectedEvent));
            NotifyPropertyChanged(nameof(SelectedType));
            NotifyPropertyChanged(nameof(SelectedBits));
        }

        public void PasteEntities(short x, short y, IList<Entity> entities)
        {
            var toAdd = new List<Entity>(entities.Count);
            foreach(var e in entities)
            {
                var n = new Entity(e);
                n.X += x;
                n.Y += y;
                toAdd.Add(n);
            }
            History.Add(new EntitiesCreated(toAdd));
        }

        public void CreateEntity(short x, short y, short type)
        {
            var e = new Entity(x, y, 0, 0, type, 0);
            History.Add(new EntitiesCreated(e));
        }

        public void MoveSelection(short x, short y)
        {
            History.Add(new EntitiesMoved(Selection, x, y));
        }
        public void DeleteSelection()
        {
            if (Selection.Count > 0)
            {
                History.Add(new EntityListChanged(new List<Entity>(Entities), Entities.Where(x => !Selection.Contains(x)).ToList()));
            }
        }

        public void ReorderSelection(int amount)
        {
            if (amount == 0)
                return;
            //TODO do the reorder
        }

        private void OnUndoRequested(object sender, HistoryChangingEventArgs<object> e)
        {
            if (e.Handled)
                return;

            if (e.Change is EntitiesMoved em)
            {
                foreach (var ent in em.Entities)
                {
                    ent.X -= em.XDiff;
                    ent.Y -= em.YDiff;
                }
                e.Handled = true;
            }
            else if (e.Change is EntitiesCreated ec)
            {
                Selection.Clear();
                Entities.RemoveRange(Entities.Count - ec.Entities.Count, ec.Entities.Count);
                e.Handled = true;
            }
            else if (e.Change is EntityChanged ech)
            {
                var prop = typeof(Entity).GetProperty(ech.Property);
                NotifyPropertyChanging(SELECTED + prop.Name);
                foreach (var ent in ech.Selection)
                {
                    prop.SetValue(ent.Key, ent.Value);
                }
                NotifyPropertyChanged(SELECTED + prop.Name);
                e.Handled = true;
            }
            else if(e.Change is EntityListChanged elc)
            {
                SetEntities(elc.Before);
                if (elc.Removed.Count > 0 && elc.Added.Count <= 0) //stops selection when Before/After are the same
                    SetSelection(elc.Removed);
                e.Handled = true;
            }
        }
        private void OnRedoRequested(object sender, HistoryChangingEventArgs<object> e)
        {
            if (e.Handled)
                return;

            if (e.Change is EntitiesMoved em)
            {
                foreach (var ent in em.Entities)
                {
                    ent.X += em.XDiff;
                    ent.Y += em.YDiff;
                }
                e.Handled = true;
            }
            else if (e.Change is EntitiesCreated ec)
            {
                Selection.Clear();
                foreach (var ent in ec.Entities)
                {
                    Entities.Add(ent);
                    Selection.Add(ent);
                }
                e.Handled = true;
            }
            else if (e.Change is EntityChanged ech)
            {
                var prop = typeof(Entity).GetProperty(ech.Property);
                NotifyPropertyChanging(SELECTED + prop.Name);
                foreach (var ent in ech.Selection)
                {
                    prop.SetValue(ent.Key, ech.NewValue);
                }
                NotifyPropertyChanged(SELECTED + prop.Name);
                e.Handled = true;
            }
            else if(e.Change is EntityListChanged elc)
            {
                SetEntities(elc.After);
                if(elc.Added.Count > 0 && elc.Removed.Count <= 0) //stops selection when Before/After are the same
                    SetSelection(elc.Added);
                e.Handled = true;
            }
        }

        public void Save(string path)
        {
            PXE.Write(Entities, path);
        }
    }
}
