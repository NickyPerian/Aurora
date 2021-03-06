﻿/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess
{
    public class HGInventoryAccessModule : BasicInventoryAccessModule, INonSharedRegionModule, IInventoryAccessModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static HGAssetMapper m_assMapper;
        public static HGAssetMapper AssetMapper
        {
            get { return m_assMapper; }
        }

//        private bool m_Initialized = false;

        #region INonSharedRegionModule

        public override string Name
        {
            get { return "HGInventoryAccessModule"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryAccessModule", "");
                if (name == Name)
                {
                    m_Enabled = true;
                    //m_log.InfoFormat("[HG INVENTORY ACCESS MODULE]: {0} enabled.", Name);
                }
            }
        }

        public override void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            base.AddRegion(scene);
            m_assMapper = new HGAssetMapper(scene);
            scene.EventManager.OnNewInventoryItemUploadComplete += UploadInventoryItem;

        }

        #endregion

        #region Event handlers

        public void UploadInventoryItem(UUID avatarID, UUID assetID, string name, int userlevel)
        {
            string userAssetServer = string.Empty;
            if (IsForeignUser(avatarID, out userAssetServer))
            {
                m_assMapper.Post(assetID, avatarID, userAssetServer);
            }
        }

        #endregion

        #region Overrides of Basic Inventory Access methods
        /// 
        /// CapsUpdateInventoryItemAsset
        ///
        public override string CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            string response = base.CapsUpdateInventoryItemAsset(remoteClient, itemID, data);

            System.Collections.Hashtable hash = (System.Collections.Hashtable) LLSD.LLSDDeserialize(Utils.StringToBytes(response));
            LLSDAssetUploadComplete llsdRequest = new LLSDAssetUploadComplete();
            LLSDHelpers.DeserialiseOSDMap(hash, llsdRequest);

            UploadInventoryItem(remoteClient.AgentId, UUID.Parse(llsdRequest.new_asset), "", 0);

            return response;
        }

        ///
        /// DeleteToInventory
        ///
        public override UUID DeleteToInventory(DeRezAction action, UUID folderID, List<SceneObjectGroup> objectGroups, UUID agentID)
        {
            UUID ret = UUID.Zero;

            // HACK: Only works for lists of length one.
            // Intermediate version, just to make things compile
            foreach (SceneObjectGroup g in objectGroups)
                ret = InternalDeleteToInventory(action, folderID, g, agentID);
            
            return ret;
        }

        public UUID InternalDeleteToInventory(DeRezAction action, UUID folderID,
                SceneObjectGroup objectGroup, UUID agentID)
        {
            UUID assetID = base.DeleteToInventory(action, folderID, new List<SceneObjectGroup>() { objectGroup }, agentID);

            if (!assetID.Equals(UUID.Zero))
            {
                UploadInventoryItem(agentID, assetID, "", 0);
            }
            else
                m_log.Debug("[HGScene]: Scene.Inventory did not create asset");

            return assetID;
        }

        ///
        /// RezObject
        ///
        public override SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                                   UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                                   bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            m_log.DebugFormat("[HGScene] RezObject itemID={0} fromTaskID={1}", itemID, fromTaskID);

            //if (fromTaskID.Equals(UUID.Zero))
            //{
            InventoryItemBase item = new InventoryItemBase(itemID);
            item.Owner = remoteClient.AgentId;
            item = m_Scene.InventoryService.GetItem(item);
            //if (item == null)
            //{ // Fetch the item
            //    item = new InventoryItemBase();
            //    item.Owner = remoteClient.AgentId;
            //    item.ID = itemID;
            //    item = m_assMapper.Get(item, userInfo.RootFolder.ID, userInfo);
            //}
            string userAssetServer = string.Empty;
            if (item != null && IsForeignUser(remoteClient.AgentId, out userAssetServer))
            {
                m_assMapper.Get(item.AssetID, remoteClient.AgentId, userAssetServer);

            }
            //}

            // OK, we're done fetching. Pass it up to the default RezObject
            return base.RezObject(remoteClient, itemID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                                  RezSelected, RemoveItem, fromTaskID, attachment);

        }

        public override void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver)
        {
            string userAssetServer = string.Empty;
            if (IsForeignUser(sender, out userAssetServer))
                m_assMapper.Get(item.AssetID, sender, userAssetServer);

            if (IsForeignUser(receiver, out userAssetServer))
                m_assMapper.Post(item.AssetID, receiver, userAssetServer);
        }

        public override bool IsForeignUser(UUID userID, out string assetServerURL)
        {
            assetServerURL = string.Empty;
            UserAccount account = null;
            if (m_Scene.UserAccountService != null)
                account = m_Scene.UserAccountService.GetUserAccount(m_Scene.RegionInfo.ScopeID, userID);

            if (account == null) // foreign
            {
                ScenePresence sp = null;
                if (m_Scene.TryGetScenePresence(userID, out sp))
                {
                    AgentCircuitData aCircuit = m_Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                    if (aCircuit.ServiceURLs.ContainsKey("AssetServerURI"))
                    {
                        assetServerURL = aCircuit.ServiceURLs["AssetServerURI"].ToString();
                        assetServerURL = assetServerURL.Trim(new char[] { '/' }); return true;
                    }
                }
            }

            return false;
        }

        #endregion

        protected override InventoryItemBase GetItem(UUID agentID, UUID itemID)
        {
            InventoryItemBase item = base.GetItem(agentID, itemID);

            string userAssetServer = string.Empty;
            if (IsForeignUser(agentID, out userAssetServer))
                m_assMapper.Get(item.AssetID, agentID, userAssetServer);

            return item;
        }
    }
}
