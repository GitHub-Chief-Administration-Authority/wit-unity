/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi.Lib.Editor
{
    public static class WitAppInfoUtility
    {
        /// <summary>
        /// Get application info using server access token
        /// </summary>
        /// <param name="serverAccessToken">Server access token</param>
        /// <param name="onUpdateComplete">On update completed callback</param>
        public static void GetAppInfo(string serverAccessToken,
            Action<string, WitAppInfo, string> onUpdateComplete)
        {
            WitServerRequestConfiguration config = new WitServerRequestConfiguration(serverAccessToken);
            Update(config, (info, error) => onUpdateComplete?.Invoke(config.GetClientAccessToken(), info, error));
        }

        /// <summary>
        /// Update configuration info using
        /// </summary>
        /// <param name="configInfo">Configuration info</param>
        /// <param name="onUpdateComplete"></param>
        public static void Update(IWitRequestConfiguration configuration,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            // Get default application info
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            StringBuilder warnings = new StringBuilder();

            // Needs server access token
            if (string.IsNullOrEmpty(configuration.GetServerAccessToken()))
            {
                warnings.AppendLine("No server access tokens provided.");
                UpdateComplete(configuration, appInfo, warnings, onUpdateComplete);
                return;
            }

            // Needs app id
            if (string.IsNullOrEmpty(appInfo.id))
            {
                GetAppId(configuration, appInfo, warnings, onUpdateComplete);
            }
            // Update existing app info
            else
            {
                UpdateAppInfo(configuration, appInfo, warnings, onUpdateComplete);
            }
        }

        // Update all configuration specific data
        private static void GetAppId(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetAppIdRequest(configuration, null, (appId, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Application id could not be found\n{error}");
                    UpdateComplete(configuration, appInfo, warnings, onUpdateComplete);
                    return;
                }

                // Set app id
                appInfo.id = appId;

                // Update app data
                UpdateAppInfo(configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Update all application specific data
        private static void UpdateAppInfo(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetAppInfoRequest(configuration, appInfo.id, null, (info, error) =>
            {
                // Failed to update application info
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Application info update failed ({error})");
                }
                // Success
                else
                {
                    appInfo = info;
                }

                // Invalid app id
                if (string.IsNullOrEmpty(appInfo.id))
                {
                    UpdateComplete(configuration, appInfo, warnings, onUpdateComplete);
                    return;
                }

                // Update client token
                UpdateClientToken(configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Update client token
        private static void UpdateClientToken(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetClientAppToken(configuration, appInfo.id, null, (token, error) =>
            {
                // Failed to update client token
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Client token update failed ({error})");
                }
                // Got token
                else
                {
                    configuration.SetClientAccessToken(token);
                }

                // Update intents
                UpdateIntents(configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Update intents
        private static void UpdateIntents(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetIntentList(configuration, null, (intents, error) =>
            {
                // Failed to update intent list
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Intent list update failed ({error})");

                }
                // Successfully updated intent list
                else
                {
                    appInfo.intents = intents;
                }

                // Update each intent
                UpdateIntent(0, configuration, appInfo, warnings, onUpdateComplete);
            });
        }
        // Perform each
        private static void UpdateIntent(int index, IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            // Done
            if (appInfo.intents == null || index >= appInfo.intents.Length)
            {
                UpdateEntities(configuration, appInfo, warnings, onUpdateComplete);
                return;
            }

            // Get original intent info
            WitIntentInfo intent = appInfo.intents[index];

            // Perform update
            WitEditorRequestUtility.GetIntentInfo(configuration, intent.id, null, (result, error) =>
            {
                // Failed to update intent
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Intent[{index}] update failed ({error})");
                }
                // Successfully updated intent
                else
                {
                    appInfo.intents[index] = result;
                }

                // Next
                UpdateIntent(index + 1, configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Update entities
        private static void UpdateEntities(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetEntityList(configuration, null, (entities, error) =>
            {
                // Failed to update entity list
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Entity list update failed ({error})");

                }
                // Successfully updated entity list
                else
                {
                    appInfo.entities = entities;
                }

                // Update each
                UpdateEntity(0, configuration, appInfo, warnings, onUpdateComplete);
            });
        }
        // Perform each
        private static void UpdateEntity(int index, IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            // Done
            if (appInfo.entities == null || index >= appInfo.entities.Length)
            {
                UpdateTraits(configuration, appInfo, warnings, onUpdateComplete);
                return;
            }

            // Get original entity info
            WitEntityInfo entity = appInfo.entities[index];

            // Perform update
            WitEditorRequestUtility.GetEntityInfo(configuration, entity.id, null, (result, error) =>
            {
                // Failed to update entity
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Entity[{index}] update failed ({error})");
                }
                // Successfully updated intent
                else
                {
                    appInfo.entities[index] = result;
                }

                // Next
                UpdateEntity(index + 1, configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Update intents
        private static void UpdateTraits(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            WitEditorRequestUtility.GetTraitList(configuration, null, (traits, error) =>
            {
                // Failed to update trait list
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Trait list update failed ({error})");

                }
                // Successfully updated trait list
                else
                {
                    appInfo.traits = traits;
                }

                // Update each trait
                UpdateTrait(0, configuration, appInfo, warnings, onUpdateComplete);
            });
        }
        // Perform each
        private static void UpdateTrait(int index, IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            // Done
            if (index >= appInfo.traits.Length)
            {
                UpdateComplete(configuration, appInfo, warnings, onUpdateComplete);
                return;
            }

            // Get original trait info
            WitTraitInfo trait = appInfo.traits[index];

            // Perform update
            WitEditorRequestUtility.GetTraitInfo(configuration, trait.id, null, (result, error) =>
            {
                // Failed to update trait
                if (!string.IsNullOrEmpty(error))
                {
                    warnings.AppendLine($"Trait[{index}] update failed ({error})");
                }
                // Successfully updated trait
                else
                {
                    appInfo.traits[index] = result;
                }

                // Next
                UpdateTrait(index + 1, configuration, appInfo, warnings, onUpdateComplete);
            });
        }

        // Completion
        private static void UpdateComplete(IWitRequestConfiguration configuration,
            WitAppInfo appInfo, StringBuilder warnings,
            Action<WitAppInfo, string> onUpdateComplete)
        {
            // Get app name
            string appName = string.IsNullOrEmpty(appInfo.name) ? "???" : appInfo.name;

            // Success
            if (warnings.Length == 0)
            {
                VLog.D($"App Info Update Success\nWit App: {appName}");
            }
            // Warnings
            else
            {
                VLog.W($"App Info Update Warnings\nWit App: {appName}\n{warnings}");
            }

            // Callback
            onUpdateComplete?.Invoke(appInfo, warnings.ToString());
        }
    }
}