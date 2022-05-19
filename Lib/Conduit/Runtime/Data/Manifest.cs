﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Conduit
{
    /// <summary>
    /// The manifest is the core artifact generated by Conduit that contains the relevant information about the app.
    /// This information can be used to train the backend or dispatch incoming requests to methods.
    /// </summary>
    internal class Manifest
    {
        /// <summary>
        /// The App ID.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// The version of the Manifest format
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A human friendly name for the application/domain.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// List of relevant entities.
        /// </summary>
        public List<ManifestEntity> Entities { get; set; }

        /// <summary>
        /// List of relevant actions (methods).
        /// </summary>
        public List<ManifestAction> Actions { get; set; }

        /// <summary>
        /// Maps action IDs (intents) to CLR methods.
        /// </summary>
        private readonly Dictionary<string, MethodInfo> methodLookup = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Processes all actions in the manifest and associate them with the methods they should invoke.
        /// </summary>
        public void ResolveActions()
        {
            foreach (var action in this.Actions)
            {
                var lastPeriod = action.ID.LastIndexOf('.');
                var typeName = action.ID.Substring(0, lastPeriod);
                var qualifiedTypeName = $"{typeName},{action.Assembly}";
                var method = action.ID.Substring(lastPeriod + 1);

                // TODO: Support instance resolution
                var isStatic = true;

                if (isStatic)
                {
                    var targetType = Type.GetType(qualifiedTypeName);
                    var targetMethod = targetType.GetMethod(method);
                    if (targetMethod != null)
                    {
                        this.methodLookup.Add(action.Name, targetMethod);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the manifest contains the specified action.
        /// </summary>
        /// <param name="action"></param>
        /// <returns>True if the action exists, false otherwise.</returns>
        public bool ContainsAction(string @action)
        {
            return this.methodLookup.ContainsKey(action);
        }

        /// <summary>
        /// Returns the info of the method corresponding to the specified action ID.
        /// </summary>
        /// <param name="actionId"></param>
        /// <returns></returns>
        public MethodInfo GetMethod(string actionId)
        {
            return this.methodLookup[actionId];
        }

        public static Manifest FromJson(string rawJson)
        {
            var json = ConduitNode.Parse(rawJson);
            Manifest manifest = new Manifest()
            {
                ID = json["id"],
                Version = json["version"],
                Domain = json["domain"]
            };

            manifest.Entities = new List<ManifestEntity>();
            var entities = json["entities"].AsArray;
            for (int i = 0; i < entities.Count; i++)
            {
                manifest.Entities.Add(ManifestEntity.FromJson(entities[i]));
            }

            manifest.Actions = new List<ManifestAction>();
            var actions = json["actions"].AsArray;
            for (int i = 0; i < entities.Count; i++)
            {
                manifest.Actions.Add(ManifestAction.FromJson(actions[i]));
            }

            return manifest;
        }

        public ConduitObject ToJson()
        {
            ConduitObject manifest = new ConduitObject();
            manifest["id"] = ID;
            manifest["version"] = Version;
            manifest["domain"] = Domain;

            var entities = new ConduitArray();
            foreach (var entity in Entities)
            {
                entities.Add(entity.ToJson());
            }

            manifest["entities"] = entities;

            var actions = new ConduitArray();
            foreach (var action in Actions)
            {
                actions.Add(action.ToJson());
            }
            manifest["actions"] = actions;

            return manifest;
        }
    }
}