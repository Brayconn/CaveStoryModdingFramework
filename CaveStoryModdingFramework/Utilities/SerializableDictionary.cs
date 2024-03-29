﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace CaveStoryModdingFramework
{
    public class SerializableDictionary<K,V> : Dictionary<K, V>, IXmlSerializable
    {
        public string KeyName { get; set; } = "Key";
        public string ItemName { get; set; } = "Item";

        private XmlSerializer valueSerializer;
        public SerializableDictionary() : base()
        {

        }
        public SerializableDictionary(IDictionary<K, V> source)
        {
            foreach (var item in source)
                this.Add(item.Key, item.Value);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            K ReadKey(string name)
            {
                return (K)Convert.ChangeType(reader.GetAttribute(name), typeof(K));
            }

            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if(!empty)
            {
                if (typeof(V) == typeof(string))
                {
                    while (reader.IsStartElement(ItemName))
                    {
                        var key = ReadKey(KeyName);
                        object value = reader.ReadElementContentAsString(ItemName,"");
                        this.Add(key, (V)value);
                    }
                }
                else if (typeof(V) == typeof(ISurfaceSource))
                {
                    while (reader.IsStartElement(ItemName))
                    {
                        var key = ReadKey(KeyName);
                        var type = reader.GetAttribute(SurfaceSource.XmlType);
                        object value;
                        switch (type)
                        {
                            case SurfaceSource.XmlFile:
                                var fileSer = new XmlSerializer(typeof(SurfaceSourceFile), new XmlRootAttribute(ItemName));
                                value = fileSer.Deserialize(reader);
                                break;
                            case SurfaceSource.XmlRuntime:
                                var runtimeSer = new XmlSerializer(typeof(SurfaceSourceRuntime), new XmlRootAttribute(ItemName));
                                value = runtimeSer.Deserialize(reader);
                                break;
                            case SurfaceSource.XmlIndex:
                                var indexSer = new XmlSerializer(typeof(SurfaceSourceIndex), new XmlRootAttribute(ItemName));
                                value = indexSer.Deserialize(reader);
                                break;
                            default:
                                throw new ArgumentException("Invalid type!");
                        }
                        this.Add(key, (V)value);
                    }
                }
                else
                {
                    var valueSerializer = new XmlSerializer(typeof(V), new XmlRootAttribute(ItemName));
                    while (reader.IsStartElement(ItemName))
                    {
                        var key = ReadKey(KeyName);
                        var value = valueSerializer.Deserialize(reader);
                        this.Add(key, (V)value);

                        if (reader.LocalName == ItemName && reader.NodeType == XmlNodeType.EndElement)
                            reader.ReadEndElement();
                    }
                }
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            void SerializeItem(KeyValuePair<K, V> item, XmlSerializer serializer, string prependKey = null, string prependValue = null)
            {
                //TODO there has GOT to be a better way to do this...
                //serialize the value to an element
                var valueDoc = new XmlDocument();
                var valueNav = valueDoc.CreateNavigator();
                using (var itemWriter = valueNav.AppendChild())
                {
                    serializer.Serialize(itemWriter, item.Value, ns);
                }
                //prepend anything the user wanted
                if (prependKey != null && prependValue != null)
                {
                    var prependAttrib = valueDoc.CreateAttribute(prependKey);
                    prependAttrib.Value = prependValue;
                    valueDoc.DocumentElement.Attributes.Prepend(prependAttrib);
                }
                //create the Key attribute and prepend it
                var keyAttrib = valueDoc.CreateAttribute(KeyName);
                keyAttrib.Value = item.Key.ToString();
                valueDoc.DocumentElement.Attributes.Prepend(keyAttrib);                

                //write the final node
                writer.WriteNode(valueNav, false);
            }

            if (typeof(V) == typeof(string))
            {
                foreach(var item in this)
                {
                    writer.WriteStartElement(ItemName);
                    writer.WriteAttributeString(KeyName, item.Key.ToString());
                    writer.WriteValue(item.Value);
                    writer.WriteEndElement();
                }
            }
            else if (typeof(V) == typeof(ISurfaceSource))
            {
                foreach(var item in this)
                {
                    XmlSerializer serializer = null;
                    string prependType = null;
                    if (item.Value is SurfaceSourceFile)
                    {
                        serializer = new XmlSerializer(typeof(SurfaceSourceFile), new XmlRootAttribute(ItemName));
                        prependType = SurfaceSource.XmlFile;
                    }
                    else if (item.Value is SurfaceSourceIndex)
                    {
                        serializer = new XmlSerializer(typeof(SurfaceSourceIndex), new XmlRootAttribute(ItemName));
                        prependType = SurfaceSource.XmlIndex;
                    }
                    else if (item.Value is SurfaceSourceRuntime)
                    {
                        serializer = new XmlSerializer(typeof(SurfaceSourceRuntime), new XmlRootAttribute(ItemName));
                        prependType = SurfaceSource.XmlRuntime;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid type!");
                    }
                    SerializeItem(item, serializer, SurfaceSource.XmlType, prependType);
                }
            }
            else
            {
                var valueSerializer = new XmlSerializer(typeof(V), new XmlRootAttribute(ItemName));
                foreach (var item in this)
                {
                    SerializeItem(item, valueSerializer);
                }
            }
        }
    }
}
