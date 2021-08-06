﻿using System;
using System.Collections.Generic;
using System.Xml;

using NAppUpdate.Framework.Tasks;
using NAppUpdate.Framework.Conditions;

namespace NAppUpdate.Framework.FeedReaders
{
    public class NauXmlFeedReader : IUpdateFeedReader
    {
        private Dictionary<string, Type> _updateConditions { get; set; }
        private Dictionary<string, Type> _updateTasks { get; set; }

        #region IUpdateFeedReader Members

        public NauFeed Read(string feed)
        {
            // Lazy-load the Condition and Task objects contained in this assembly, unless some have already
            // been loaded (by a previous lazy-loading in a call to Read, or by an explicit loading)
            if (_updateTasks == null)
            {
                _updateConditions = new Dictionary<string, Type>();
                _updateTasks = new Dictionary<string, Type>();
                Utils.Reflection.FindTasksAndConditionsInAssembly(this.GetType().Assembly, _updateTasks, _updateConditions);
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(feed);

            var nau = new NauFeed();
            nau.Tasks = new List<IUpdateTask>();

            // Support for different feed versions
            XmlNode root = doc.SelectSingleNode(@"/Feed[version=""1.0""] | /Feed") ?? doc;

            // feed attributes
            nau.BaseUrl = root.Attributes["baseUrl"]?.Value;
            nau.CompressedFilePath = root.Attributes["compressedName"]?.Value;

            if (long.TryParse(root.Attributes["compressedSize"]?.Value, out var compressedSize))
                nau.CompressedSize = compressedSize;

            if (long.TryParse(root.Attributes["size"]?.Value, out var totalSize))
                nau.TotalSize = totalSize;

            // Temporary collection of attributes, used to aggregate them all with their values
            // to reduce Reflection calls
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            XmlNodeList nl = root.SelectNodes("./Tasks/*");
            if (nl == null) return nau; // TODO: wrong format, probably should throw exception

            foreach (XmlNode node in nl)
            {
                // Find the requested task type and create a new instance of it
                if (!_updateTasks.ContainsKey(node.Name))
                    continue;

                IUpdateTask task = (IUpdateTask)Activator.CreateInstance(_updateTasks[node.Name]);
                task.BaseUrl = nau.BaseUrl;
                task.UpdateConditions = new BooleanCondition();

                // Store all other task attributes, to be used by the task object later
                if (node.Attributes != null)
                {
                    foreach (XmlAttribute att in node.Attributes)
                    {
                        if ("type".Equals(att.Name))
                            continue;

                        // special attribute, which is actually a condition
                        if (att.Name.Equals("sha1"))
                            task.UpdateConditions.AddCondition(new FileChecksumCondition() { Checksum = att.Value, ChecksumType = "sha1" }, BooleanCondition.ConditionType.NOT);

                        var nm = att.Name;
                        if (nm.Equals("redirectTo"))
                            nm = "updateTo";

                        attributes.Add(nm, att.Value);
                    }
                    if (attributes.Count > 0)
                    {
                        Utils.Reflection.SetNauAttributes(task, attributes);
                        attributes.Clear();
                    }
                    // TODO: Check to see if all required task fields have been set
                }

                if (node.HasChildNodes)
                {
                    if (node["Description"] != null)
                        task.Description = node["Description"].InnerText;

                    // Read update conditions
                    if (node["Conditions"] != null)
                    {
                        IUpdateCondition conditionObject = ReadCondition(node["Conditions"]);
                        if (conditionObject != null)
                            task.UpdateConditions.AddCondition(conditionObject);
                    }
                }

                nau.Tasks.Add(task);
            }
            return nau;
        }

        private IUpdateCondition ReadCondition(XmlNode cnd)
        {
            IUpdateCondition conditionObject = null;
            if (cnd.ChildNodes.Count > 0 || "GroupCondition".Equals(cnd.Name))
            {
                BooleanCondition bc = new BooleanCondition();
                foreach (XmlNode child in cnd.ChildNodes)
                {
                    IUpdateCondition childCondition = ReadCondition(child);
                    if (childCondition != null)
                        bc.AddCondition(childCondition,
                                        BooleanCondition.ConditionTypeFromString(child.Attributes != null && child.Attributes["type"] != null
                                                                                     ? child.Attributes["type"].Value : null));
                }
                if (bc.ChildConditionsCount > 0)
                    conditionObject = bc.Degrade();
            }
            else if (_updateConditions.ContainsKey(cnd.Name))
            {
                conditionObject = (IUpdateCondition)Activator.CreateInstance(_updateConditions[cnd.Name]);

                if (cnd.Attributes != null)
                {
                    Dictionary<string, string> dict = new Dictionary<string, string>();

                    // Store all other attributes, to be used by the condition object later
                    foreach (XmlAttribute att in cnd.Attributes)
                    {
                        if ("type".Equals(att.Name))
                            continue;

                        dict.Add(att.Name, att.Value);
                    }
                    if (dict.Count > 0)
                        Utils.Reflection.SetNauAttributes(conditionObject, dict);
                }
            }
            return conditionObject;
        }

        #endregion

        //public void LoadConditionsAndTasks(System.Reflection.Assembly assembly)
        //{
        //    if (_updateTasks == null)
        //    {
        //        _updateConditions = new Dictionary<string, Type>();
        //        _updateTasks = new Dictionary<string, Type>();
        //    }
        //    Utils.Reflection.FindTasksAndConditionsInAssembly(assembly, _updateTasks, _updateConditions);
        //}
    }
}