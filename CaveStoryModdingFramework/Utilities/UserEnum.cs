using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace CaveStoryModdingFramework.Utilities
{
    public class SingleMap
    {
        [XmlAttribute]
        public int Value { get; set; }
        [XmlText]
        public string Name { get; set; }

        public SingleMap() { }
        public SingleMap(int value, string name)
        {
            Value = value;
            Name = name;
        }

        public override bool Equals(object obj)
        {
            if (obj is SingleMap sm)
            {
                return Value == sm.Value && Name == sm.Name;
            }
            else return base.Equals(obj);
        }
    }
    public class RangeMap : SingleMap
    {
        [XmlAttribute]
        public int End { get; set; }

        public RangeMap() { }
        public RangeMap(int start, int end, string name)
        {
            Value = start;
            End = end;
            Name = name;
        }

        public override bool Equals(object obj)
        {
            if(obj is RangeMap rm)
            {
                return End == rm.End && base.Equals(obj);
            }
            return base.Equals(obj);
        }
    }
    public enum Directions
    {
        Positive,
        Negative
    }
    public class InfiniteMap : SingleMap
    {
        [XmlAttribute]
        public Directions Direction { get; set; }

        public InfiniteMap() { }
        public InfiniteMap(int point, Directions direction, string name)
        {
            Value = point;
            Direction = direction;
            Name = name;
        }

        public override bool Equals(object obj)
        {
            if (obj is InfiniteMap im)
            {
                return Direction == im.Direction && base.Equals(obj);
            }
            return base.Equals(obj);
        }
    }
    public class UserEnum : IXmlSerializable
    {   
        public string Name { get; set; }
        public string Default { get; set; }

        public Dictionary<int, string> BasicMappings { get; } = new Dictionary<int, string>();
        public List<RangeMap> FiniteRanges { get; } = new List<RangeMap>();
        public InfiniteMap UpperBound { get; set; }
        public InfiniteMap LowerBound { get; set; }

        public UserEnum() { }
        public UserEnum(string name, string def, params SingleMap[] args)
        {
            Name = name;
            Default = def;
            for(int i = 0; i < args.Length; i++)
            {
                if(args[i] is InfiniteMap inf)
                {
                    switch(inf.Direction)
                    {
                        case Directions.Positive:
                            UpperBound = inf;
                            break;
                        case Directions.Negative:
                            LowerBound = inf;
                            break;
                    }
                }
                else if(args[i] is RangeMap range)
                {
                    FiniteRanges.Add(range);
                }
                else
                {
                    BasicMappings.Add(args[i].Value, args[i].Name);
                }
            }
        }
        
        /// <summary>
        /// Get the string corresponding to the given integer
        /// </summary>
        /// <param name="value">The integer</param>
        /// <param name="name">The string</param>
        /// <returns>Whether an exact match (other than the default) was found</returns>
        public bool TryGet(int value, out string name)
        {
            //Try direct mappings first
            if (BasicMappings.TryGetValue(value, out name))
                return true;
            //Try finite ranges
            foreach(var range in FiniteRanges)
            {
                if (range.Value <= value && value <= range.End)
                {
                    name = range.Name;
                    return true;
                }
            }
            //Try infinite ranges
            if(LowerBound != null && value <= LowerBound.Value)
            {
                name = LowerBound.Name;
                return true;
            }
            if (UpperBound != null && UpperBound.Value <= value)
            {
                name = UpperBound.Name;
                return true;
            }
            //Use default and hope for the best
            name = Default;
            return false;
        }
        
        public bool VerifyRanges()
        {
            for(int i = 0; i < FiniteRanges.Count; i++)
            {
                var r1 = FiniteRanges[i];
                for (int j = i + 1; j < FiniteRanges.Count; j++)
                {
                    var r2 = FiniteRanges[j];

                    if ((r1.Value <= r2.Value && r2.Value <= r1.End)
                     || (r1.Value <= r2.End && r2.End <= r1.End))
                        return false;
                }
            }
            return true;
        }
        public bool VerifyInfinity()
        {
            var u = UpperBound == null;
            var b = LowerBound == null;
            if (u && b) //no bounds is ok
            {
                return true;
            }
            else if(u ^ b) //a single bound can be ok if it's in the right direction
            {
                return UpperBound?.Direction == Directions.Positive
                    || LowerBound?.Direction == Directions.Negative;
            }
            else //if both are non-null, then the directions must match, and they can't overlap
            {
                return (UpperBound.Direction == Directions.Positive)
                    && (LowerBound.Direction == Directions.Negative)
                    && LowerBound.Value < UpperBound.Value;
            }
        }

        public XmlSchema GetSchema() => null;

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(nameof(Name));
            Default = reader.GetAttribute(nameof(Default));
            reader.ReadStartElement(nameof(UserEnum));
            if (reader.NodeType != XmlNodeType.None)
            {
                do
                {
                    switch (reader.LocalName)
                    {
                        case nameof(SingleMap):
                            var sm = reader.DeserializeAs<SingleMap>(null, "");
                            BasicMappings.Add(sm.Value, sm.Name);
                            break;
                        case nameof(RangeMap):
                            var rm = reader.DeserializeAs<RangeMap>(null, "");
                            FiniteRanges.Add(rm);
                            break;
                        case nameof(InfiniteMap):
                            var im = reader.DeserializeAs<InfiniteMap>(null, "");
                            switch (im.Direction)
                            {
                                case Directions.Positive:
                                    UpperBound = im;
                                    break;
                                case Directions.Negative:
                                    LowerBound = im;
                                    break;
                            }
                            break;
                    }
                } while (reader.NodeType == XmlNodeType.Element);
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString(nameof(Name), Name);
            if(Default != null)
                writer.WriteAttributeString(nameof(Default), Default);
            foreach (var item in BasicMappings)
            {
                writer.SerializeAsRoot(new SingleMap(item.Key, item.Value), nameof(SingleMap));
            }
            foreach(var range in FiniteRanges)
            {
                writer.SerializeAsRoot(range, nameof(RangeMap));
            }
            if(LowerBound != null)
                writer.SerializeAsRoot(LowerBound, nameof(InfiniteMap));
            if(UpperBound != null)
                writer.SerializeAsRoot(UpperBound, nameof(InfiniteMap));
        }

        public override bool Equals(object obj)
        {
            if(obj is UserEnum ue)
            {
                return Name == ue.Name &&
                    Default == ue.Default &&
                    BasicMappings.SequenceEqual(ue.BasicMappings) && //this seems a little hacky, but it works, so...
                    FiniteRanges.SequenceEqual(ue.FiniteRanges) &&
                    (UpperBound?.Equals(ue.UpperBound) ?? true) &&
                    (LowerBound?.Equals(ue.LowerBound) ?? true);
            }
            else return base.Equals(obj);
        }
    }
}
