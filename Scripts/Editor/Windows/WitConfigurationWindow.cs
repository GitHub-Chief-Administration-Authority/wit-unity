﻿/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Data.Configuration;

namespace Facebook.WitAi.Windows
{
    public abstract class WitConfigurationWindow : BaseWitWindow
    {
        #region CONFIGURATION
        // Selected wit configuration
        protected int witConfigIndex = -1;
        protected WitConfiguration witConfiguration;

        // Set configuration
        protected virtual void SetConfiguration(int newConfiguration)
        {
            // Apply
            witConfigIndex = newConfiguration;

            // Get configuration
            WitConfiguration[] witConfigs = WitConfigurationUtility.WitConfigs;
            witConfiguration = witConfigs != null && witConfigIndex >= 0 && witConfigIndex < witConfigs.Length ? witConfigs[witConfigIndex] : null;
        }
        // Init tokens
        protected override void OnEnable()
        {
            base.OnEnable();
            WitAuthUtility.InitEditorTokens();
        }
        #endregion

        #region LAYOUT
        // Layout content
        protected override float LayoutContent()
        {
            // Get height
            float height = 0f;

            // Layout popup
            int index = witConfigIndex;
            WitConfigurationEditorUI.LayoutConfigurationSelect(ref index, ref height);
            // Selection changed
            if (index != witConfigIndex)
            {
                SetConfiguration(index);
            }

            // Return height
            return height;
        }
        // Get header url
        protected override string HeaderUrl
        {
            get
            {
                // Get ID
                string applicationID = witConfiguration?.application?.id;
                if (!string.IsNullOrEmpty(applicationID))
                {
                    return WitStyles.GetSettingsURL(applicationID);
                }

                // Use base
                return base.HeaderUrl;
            }
        }
        #endregion
    }
}
