﻿/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.data;
using com.facebook.witai.lib;
using com.facebook.witai.utility;
using UnityEngine;

namespace com.facebook.witai.configuration
{
    public static class WitConfigurationUtility
    {

        #if UNITY_EDITOR
        public static void UpdateData(this WitConfiguration configuration,
            Action onUpdateComplete = null)
        {
            EditorForegroundRunner.Run(() => DoUpdateData(configuration, onUpdateComplete));
        }

        private static void DoUpdateData(WitConfiguration configuration, Action onUpdateComplete)
        {
            if (!string.IsNullOrEmpty(
                WitAuthUtility.GetAppServerToken(configuration.application.id)))
            {
                var intentsRequest = configuration.ListIntentsRequest();
                intentsRequest.onResponse = (r) =>
                {
                    var entitiesRequest = configuration.ListEntitiesRequest();
                    entitiesRequest.onResponse = (er) =>
                    {
                        var traitsRequest = configuration.ListTraitsRequest();
                        traitsRequest.onResponse =
                            (tr) => OnUpdateData(tr,
                                (dataResponse) => UpdateTraitList(configuration, dataResponse),
                                onUpdateComplete);
                        OnUpdateData(er,
                            (entityResponse) => UpdateEntityList(configuration, entityResponse),
                            traitsRequest.Request);
                    };
                    OnUpdateData(r, (response) => UpdateIntentList(configuration, response),
                        entitiesRequest.Request);
                };

                configuration.application?.UpdateData(intentsRequest.Request);
            }
        }

        private static void OnUpdateData(WitRequest request, Action<WitResponseNode> updateComponent, Action onUpdateComplete)
        {
            if (request.StatusCode == 200)
            {
                updateComponent(request.ResponseData);
            }
            else
            {
                Debug.LogError($"Request for {request} failed: {request.StatusDescription}");
            }

            EditorForegroundRunner.Run(onUpdateComplete);
        }

        private static void UpdateIntentList(this WitConfiguration configuration, WitResponseNode intentListWitResponse)
        {
            var intentList = intentListWitResponse.AsArray;
            var n = intentList.Count;
            configuration.intents = new WitIntent[n];
            for (int i = 0; i < n; i++)
            {
                var intent = WitIntent.FromJson(intentList[i]);
                intent.witConfiguration = configuration;
                configuration.intents[i] = intent;
                intent.UpdateData();
            }
        }

        private static void UpdateEntityList(this WitConfiguration configuration, WitResponseNode entityListWitResponse)
        {
            var entityList = entityListWitResponse.AsArray;
            var n = entityList.Count;
            configuration.entities = new WitEntity[n];
            for (int i = 0; i < n; i++)
            {
                var entity = WitEntity.FromJson(entityList[i]);
                entity.witConfiguration = configuration;
                configuration.entities[i] = entity;
                entity.UpdateData();
            }
        }

        public static void UpdateTraitList(this WitConfiguration configuration, WitResponseNode traitListWitResponse)
        {
            var traitList = traitListWitResponse.AsArray;
            var n = traitList.Count;
            configuration.traits = new WitTrait[n];
            for (int i = 0; i < n; i++) {
                var trait = WitTrait.FromJson(traitList[i]);
                trait.witConfiguration = configuration;
                configuration.traits[i] = trait;
                trait.UpdateData();
            }
        }

        /// <summary>
        /// Gets the app info and client id that is associated with the server token being used
        /// </summary>
        /// <param name="serverToken">The server token to use to get the app config</param>
        /// <param name="action"></param>
        public static void FetchAppConfigFromServerToken(this WitConfiguration configuration, string serverToken, Action action)
        {
            if (WitAuthUtility.IsServerTokenValid(serverToken))
            {
                FetchApplicationFromServerToken(configuration, serverToken,
                    () => { FetchClientToken(configuration, () => { configuration.UpdateData(action); }); });
            }
            else
            {
                Debug.LogError($"No server token set for {configuration.name}.");
            }
        }

        private static void FetchApplicationFromServerToken(WitConfiguration configuration, string serverToken, Action response)
        {
            var listRequest = WitRequestFactory.ListAppsRequest(serverToken, 10000);
            listRequest.onResponse = (r) =>
            {
                if (r.StatusCode == 200)
                {
                    var applications = r.ResponseData.AsArray;
                    for (int i = 0; i < applications.Count; i++)
                    {
                        if (applications[i]["is_app_for_token"].AsBool)
                        {
                            if (null != configuration.application)
                            {
                                configuration.application.UpdateData(applications[i]);
                            }
                            else
                            {
                                configuration.application = WitApplication.FromJson(applications[i]);
                            }

                            EditorForegroundRunner.Run(() =>
                            {
                                WitAuthUtility.SetAppServerToken(configuration.application.id,
                                    serverToken);
                                response?.Invoke();
                            });
                            break;
                        }
                    }

                }
                else
                {
                    Debug.LogError(r.StatusDescription);
                }
            };
            listRequest.Request();
        }

        private static void FetchClientToken(WitConfiguration configuration, Action action)
        {
            if (!string.IsNullOrEmpty(configuration.application?.id))
            {
                var tokenRequest = configuration.GetClientToken(configuration.application.id);
                tokenRequest.onResponse = (r) =>
                {
                    if (r.StatusCode == 200)
                    {
                        configuration.clientAccessToken = r.ResponseData["client_token"];
                        EditorForegroundRunner.Run(action);
                    }
                    else
                    {
                        Debug.LogError(r.StatusDescription);
                    }
                };
                tokenRequest.Request();
            }
        }
#endif
    }
}
