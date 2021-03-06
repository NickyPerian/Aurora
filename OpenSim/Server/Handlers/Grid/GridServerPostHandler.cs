/*
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

using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;
using Aurora.DataManager;
using Aurora.Framework;

namespace OpenSim.Server.Handlers.Grid
{
    public class GridServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IGridService m_GridService;
        private IRegionConnector TelehubConnector;
        private Dictionary<UUID /*RegionID*/, UUID /*SessionID*/> SessionCache = new Dictionary<UUID, UUID>();
        
        public GridServerPostHandler(IGridService service) :
                base("POST", "/grid")
        {
            m_GridService = service;
            TelehubConnector = DataManager.RequestPlugin<IRegionConnector>();
        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();

                switch (method)
                {
                    case "register":
                        return Register(request);

                    case "deregister":
                        return Deregister(request);

                    case "get_neighbours":
                        return GetNeighbours(request);

                    case "get_region_by_uuid":
                        return GetRegionByUUID(request);

                    case "get_region_by_position":
                        return GetRegionByPosition(request);

                    case "get_region_by_name":
                        return GetRegionByName(request);

                    case "get_regions_by_name":
                        return GetRegionsByName(request);

                    case "get_region_range":
                        return GetRegionRange(request);

                    case "get_default_regions":
                        return GetDefaultRegions(request);

                    case "get_fallback_regions":
                        return GetFallbackRegions(request);

                    case "get_safe_regions":
                        return GetSafeRegions(request);

                    case "get_hyperlinks":
                        return GetHyperlinks(request);

                    case "get_region_flags":
                        return GetRegionFlags(request);

                    case "update_map":
                        return UpdateMap(request);

                    case "addagent":
                        return AddAgent(request);

                    case "removeagent":
                        return RemoveAgent(request);

                    case "getmapitems":
                        return GetMapItems(request);

                    case "removetelehub":
                        return RemoveTelehub(request);

                    case "addtelehub":
                        return AddTelehub(request);

                    case "findtelehub":
                        return FindTelehub(request);
                }
                m_log.DebugFormat("[GRID HANDLER]: unknown method {0} request {1}", method.Length, method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID HANDLER]: Exception {0}", e);
            }

            return FailureResult();

        }

        #region Method-specific handlers

        private byte[] Register(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to register region");

            int versionNumberMin = 0, versionNumberMax = 0;
            if (request.ContainsKey("VERSIONMIN"))
                Int32.TryParse(request["VERSIONMIN"].ToString(), out versionNumberMin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no minimum protocol version in request to register region");

            if (request.ContainsKey("VERSIONMAX"))
                Int32.TryParse(request["VERSIONMAX"].ToString(), out versionNumberMax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no maximum protocol version in request to register region");

            UUID sessionIDIn = UUID.Zero;
            if (request.ContainsKey("SESSIONID"))
                UUID.TryParse(request["SESSIONID"].ToString(), out sessionIDIn);
            else
                m_log.WarnFormat("[GRID HANDLER]: no sessionID in request to register region");

            // Check the protocol version
            if ((versionNumberMin > ProtocolVersions.ServerProtocolVersionMax && versionNumberMax < ProtocolVersions.ServerProtocolVersionMax))
            {
                // Can't do, there is no overlap in the acceptable ranges
                return FailureResult();
            }

            Dictionary<string, object> rinfoData = new Dictionary<string, object>();
            GridRegion rinfo = null;
            try
            {
                foreach (KeyValuePair<string, object> kvp in request)
                    rinfoData[kvp.Key] = kvp.Value.ToString();
                rinfo = new GridRegion(rinfoData);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID HANDLER]: exception unpacking region data: {0}", e);
            }

            string result = "Error communicating with grid service";

            UUID SessionID = UUID.Zero;
            if (rinfo != null)
                result = m_GridService.RegisterRegion(scopeID, rinfo, sessionIDIn, out SessionID);

            if (SessionID != UUID.Zero)
                SessionCache[rinfo.RegionID] = SessionID;

            if (result == String.Empty)
                return SuccessResult(SessionID.ToString());
            else
                return FailureResult(result);
        }

        private byte[] UpdateMap(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to register region");



            UUID sessionID = UUID.Zero;
            if (request.ContainsKey("SESSIONID"))
                UUID.TryParse(request["SESSIONID"].ToString(), out sessionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no sessionID in request to update region");

            GridRegion region = region = new GridRegion(request);

            int versionNumberMin = 0, versionNumberMax = 0;
            if (request.ContainsKey("VERSIONMIN"))
                Int32.TryParse(request["VERSIONMIN"].ToString(), out versionNumberMin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no minimum protocol version in request to register region");

            if (request.ContainsKey("VERSIONMAX"))
                Int32.TryParse(request["VERSIONMAX"].ToString(), out versionNumberMax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no maximum protocol version in request to register region");

            // Check the protocol version
            if ((versionNumberMin > ProtocolVersions.ServerProtocolVersionMax && versionNumberMax < ProtocolVersions.ServerProtocolVersionMax))
            {
                // Can't do, there is no overlap in the acceptable ranges
                return FailureResult();
            }

            string result = "Error communicating with grid service";

            result = m_GridService.UpdateMap(scopeID, region, sessionID);

            if (result == String.Empty)
                return SuccessResult();
            else
                return FailureResult(result);
        }

        private byte[] Deregister(Dictionary<string, object> request)
        {
            UUID regionID = UUID.Zero;
            if (request.ContainsKey("REGIONID"))
                UUID.TryParse(request["REGIONID"].ToString(), out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to deregister region");

            UUID sessionID = UUID.Zero;
            if (request.ContainsKey("SESSIONID"))
                UUID.TryParse(request["SESSIONID"].ToString(), out sessionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no sessionID in request to deregister region");

            bool result = m_GridService.DeregisterRegion(regionID, sessionID);

            if (result)
                return SuccessResult();
            else
                return FailureResult();

        }

        private byte[] GetNeighbours(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get neighbours");

            UUID regionID = UUID.Zero;
            if (request.ContainsKey("REGIONID"))
                UUID.TryParse(request["REGIONID"].ToString(), out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            List<GridRegion> rinfos = m_GridService.GetNeighbours(scopeID, regionID);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);

        }

        private byte[] GetRegionByUUID(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get neighbours");

            UUID regionID = UUID.Zero;
            if (request.ContainsKey("REGIONID"))
                UUID.TryParse(request["REGIONID"].ToString(), out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            GridRegion rinfo = m_GridService.GetRegionByUUID(scopeID, regionID);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetRegionByPosition(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region by position");

            int x = 0, y = 0;
            if (request.ContainsKey("X"))
                Int32.TryParse(request["X"].ToString(), out x);
            else
                m_log.WarnFormat("[GRID HANDLER]: no X in request to get region by position");
            if (request.ContainsKey("Y"))
                Int32.TryParse(request["Y"].ToString(), out y);
            else
                m_log.WarnFormat("[GRID HANDLER]: no Y in request to get region by position");

            GridRegion rinfo = m_GridService.GetRegionByPosition(scopeID, x, y);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetRegionByName(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region by name");

            string regionName = string.Empty;
            if (request.ContainsKey("NAME"))
                regionName = request["NAME"].ToString();
            else
                m_log.WarnFormat("[GRID HANDLER]: no name in request to get region by name");

            GridRegion rinfo = m_GridService.GetRegionByName(scopeID, regionName);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetRegionsByName(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get regions by name");

            string regionName = string.Empty;
            if (request.ContainsKey("NAME"))
                regionName = request["NAME"].ToString();
            else
                m_log.WarnFormat("[GRID HANDLER]: no NAME in request to get regions by name");

            int max = 0;
            if (request.ContainsKey("MAX"))
                Int32.TryParse(request["MAX"].ToString(), out max);
            else
                m_log.WarnFormat("[GRID HANDLER]: no MAX in request to get regions by name");

            List<GridRegion> rinfos = m_GridService.GetRegionsByName(scopeID, regionName, max);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetRegionRange(Dictionary<string, object> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetRegionRange");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region range");

            int xmin = 0, xmax = 0, ymin = 0, ymax = 0;
            if (request.ContainsKey("XMIN"))
                Int32.TryParse(request["XMIN"].ToString(), out xmin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no XMIN in request to get region range");
            if (request.ContainsKey("XMAX"))
                Int32.TryParse(request["XMAX"].ToString(), out xmax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no XMAX in request to get region range");
            if (request.ContainsKey("YMIN"))
                Int32.TryParse(request["YMIN"].ToString(), out ymin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no YMIN in request to get region range");
            if (request.ContainsKey("YMAX"))
                Int32.TryParse(request["YMAX"].ToString(), out ymax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no YMAX in request to get region range");


            List<GridRegion> rinfos = m_GridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetDefaultRegions(Dictionary<string, object> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetDefaultRegions");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region range");

            List<GridRegion> rinfos = m_GridService.GetDefaultRegions(scopeID);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetFallbackRegions(Dictionary<string, object> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetRegionRange");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get fallback regions");

            int x = 0, y = 0;
            if (request.ContainsKey("X"))
                Int32.TryParse(request["X"].ToString(), out x);
            else
                m_log.WarnFormat("[GRID HANDLER]: no X in request to get fallback regions");
            if (request.ContainsKey("Y"))
                Int32.TryParse(request["Y"].ToString(), out y);
            else
                m_log.WarnFormat("[GRID HANDLER]: no Y in request to get fallback regions");


            List<GridRegion> rinfos = m_GridService.GetFallbackRegions(scopeID, x, y);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetSafeRegions(Dictionary<string, object> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetRegionRange");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get fallback regions");

            int x = 0, y = 0;
            if (request.ContainsKey("X"))
                Int32.TryParse(request["X"].ToString(), out x);
            else
                m_log.WarnFormat("[GRID HANDLER]: no X in request to get fallback regions");
            if (request.ContainsKey("Y"))
                Int32.TryParse(request["Y"].ToString(), out y);
            else
                m_log.WarnFormat("[GRID HANDLER]: no Y in request to get fallback regions");


            List<GridRegion> rinfos = m_GridService.GetSafeRegions(scopeID, x, y);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

		byte[] GetHyperlinks(Dictionary<string, object> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetHyperlinks");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get linked regions");

            List<GridRegion> rinfos = m_GridService.GetHyperlinks(scopeID);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] GetRegionFlags(Dictionary<string, object> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"].ToString(), out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get neighbours");

            UUID regionID = UUID.Zero;
            if (request.ContainsKey("REGIONID"))
                UUID.TryParse(request["REGIONID"].ToString(), out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            int flags = m_GridService.GetRegionFlags(scopeID, regionID);
           // m_log.DebugFormat("[GRID HANDLER]: flags for region {0}: {1}", regionID, flags);

            Dictionary<string, object> result = new Dictionary<string, object>(); 
            result["result"] = flags.ToString();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] AddAgent(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            UUID regionID = UUID.Parse(request["REGIONID"].ToString());
            UUID agentID = UUID.Parse(request["AGENTID"].ToString());
            float X = float.Parse(request["X"].ToString());
            float Y = float.Parse(request["Y"].ToString());
            float Z = float.Parse(request["Z"].ToString());
            Vector3 Position = new Vector3(X, Y, Z);

            m_GridService.AddAgent(regionID, agentID, Position);

            return SuccessResult();
        }

        private byte[] RemoveAgent(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            UUID regionID = UUID.Parse(request["REGIONID"].ToString());
            UUID agentID = UUID.Parse(request["AGENTID"].ToString());

            m_GridService.RemoveAgent(regionID, agentID);

            return SuccessResult();
        }

        private byte[] GetMapItems(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            
            ulong regionHandle = ulong.Parse(request["REGIONHANDLE"].ToString());
            GridItemType gridItemType = (GridItemType)int.Parse(request["GRIDITEMTYPE"].ToString());

            multipleMapItemReply items = m_GridService.GetMapItems(regionHandle, gridItemType);

            result["MapItems"] = items.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] RemoveTelehub(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            UUID regionID = UUID.Zero;
            if (request.ContainsKey("REGIONID"))
                UUID.TryParse(request["REGIONID"].ToString(), out regionID);
            else
                m_log.WarnFormat("[AuroraDataServerPostHandler]: no regionID in request to remove telehub");

            UUID SessionID = UUID.Parse(request["SESSIONID"].ToString());
            if(SessionCache.ContainsKey(regionID) && SessionCache[regionID] == SessionID)
                TelehubConnector.RemoveTelehub(regionID, SessionID);
            result["result"] = "Successful";

            string xmlString = ServerUtils.BuildXmlResponse(result);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        public byte[] AddTelehub(Dictionary<string, object> request)
        {
            Telehub telehub = new Telehub();
            telehub.FromKVP(request);
            UUID SessionID = UUID.Parse(request["SESSIONID"].ToString());

            if (SessionCache.ContainsKey(telehub.RegionID) && SessionCache[telehub.RegionID] == SessionID)
                TelehubConnector.AddTelehub(telehub, SessionID);

            return SuccessResult();
        }

        public byte[] FindTelehub(Dictionary<string, object> request)
        {
            UUID regionID = UUID.Zero;
            UUID.TryParse(request["REGIONID"].ToString(), out regionID);

            Dictionary<string, object> result = new Dictionary<string, object>();
            Telehub telehub = TelehubConnector.FindTelehub(regionID);
            if (telehub != null)
                result = telehub.ToKeyValuePairs();
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[AuroraDataServerPostHandler]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        #endregion

        #region Misc

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return DocToBytes(doc);
        }

        private byte[] SuccessResult(string result)
        {
            Dictionary<string, object> sendData = new Dictionary<string,object>();

            sendData["Result"] = "Success";
            sendData["Message"] = result;

            string xmlString = ServerUtils.BuildXmlResponse(sendData);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] FailureResult()
        {
            return FailureResult(String.Empty);
        }

        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.ToArray();
        }

        #endregion
    }
}
