﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.WitAi.CallbackHandlers
{
    /// <summary>
    /// Triggers an event when no intents were recognized in an utterance.
    /// </summary>
    [AddComponentMenu("Wit.ai/Response Matchers/Out Of Domain")]
    public class OutOfScopeUtteranceHandler : WitResponseHandler
    {
        [SerializeField] private UnityEvent onOutOfDomain = new UnityEvent();

        protected override void OnHandleResponse(WitResponseNode response)
        {
            if (null == response) return;

            if (response["intents"].Count == 0)
            {
                onOutOfDomain?.Invoke();
            }
        }
    }
}
