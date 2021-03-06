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

using System;
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using integer = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLInteger;
using vector = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.Vector3;
using rotation = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.Quaternion;
using key = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLString;
using LSL_List = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.list;
using LSL_String = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLString;
using LSL_Float = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLInteger;
using Aurora.ScriptEngine.AuroraDotNetEngine;
using Aurora.ScriptEngine.AuroraDotNetEngine.APIs.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.CompilerTools;

namespace Aurora.ScriptEngine.AuroraDotNetEngine.Runtime
{
    public partial class ScriptBaseClass : MarshalByRefObject
    {
        public IAA_Api m_AA_Functions;

        public void ApiTypeAA(IScriptApi api)
        {
            if (!(api is IAA_Api))
                return;

            m_AA_Functions = (IAA_Api)api;
        }

        public void AASetCloudDensity(LSL_Float density)
        {
            m_AA_Functions.AASetCloudDensity(density);
        }

        public void AAUpdateDatabase(LSL_String key, LSL_String value, LSL_String token)
        {
            m_AA_Functions.AAUpdateDatabase(key, value, token);
        }

        public LSL_List AAQueryDatabase(LSL_String key, LSL_String token)
        {
            return m_AA_Functions.AAQueryDatabase(key, token);
        }

        public LSL_String AASerializeXML(LSL_List keys, LSL_List values)
        {
            return m_AA_Functions.AASerializeXML(keys, values);
        }

        public LSL_List AADeserializeXMLKeys(LSL_String xmlFile)
        {
            return m_AA_Functions.AADeserializeXMLKeys(xmlFile);
        }

        public LSL_List AADeserializeXMLValues(LSL_String xmlFile)
        {
            return m_AA_Functions.AADeserializeXMLValues(xmlFile);
        }

        public void AASetConeOfSilence(LSL_Float radius)
        {
            m_AA_Functions.AASetConeOfSilence(radius);
        }

        public void AAJoinCombatTeam(LSL_String team)
        {
            m_AA_Functions.AAJoinCombatTeam(team);
        }

        public void AAJoinCombat()
        {
            m_AA_Functions.AAJoinCombat();
        }

        public void AALeaveCombat()
        {
            m_AA_Functions.AALeaveCombat();
        }

        public LSL_Float AAGetHealth()
        {
            return m_AA_Functions.AAGetHealth();
        }

        public LSL_String AAGetTeam()
        {
            return m_AA_Functions.AAGetTeam();
        }

        public LSL_List AAGetTeamMembers()
        {
            return m_AA_Functions.AAGetTeamMembers();
        }

        public LSL_String AAGetLastOwner()
        {
            return m_AA_Functions.AAGetLastOwner();
        }

        public LSL_String AAGetLastOwner(LSL_String PrimID)
        {
            return m_AA_Functions.AAGetLastOwner(PrimID);
        }

        public void AASayDistance(int channelID, float Distance, string text)
        {
            m_AA_Functions.AASayDistance(channelID, Distance, text);
        }

        public void AASayTo(string userID, string text)
        {
            m_AA_Functions.AASayTo(userID, text);
        }

        public bool AAGetWalkDisabled(string userID)
        {
            return m_AA_Functions.AAGetWalkDisabled(userID);
        }

        public void AASetWalkDisabled(string userID, bool Value)
        {
            m_AA_Functions.AASetWalkDisabled(userID, Value);
        }

        public bool AAGetFlyDisabled(string userID)
        {
            return m_AA_Functions.AAGetFlyDisabled(userID);
        }

        public void AARaiseError(string message)
        {
            m_AA_Functions.AARaiseError(message);
        }

        public void AASetFlyDisabled(string userID, bool Value)
        {
            m_AA_Functions.AASetFlyDisabled(userID, Value);
        }

        public string AAAvatarFullName2Key(string username)
        {
            return m_AA_Functions.AAAvatarFullName2Key(username);
        }

        public void osCauseDamage(string avatar, double damage)
        {
            m_AA_Functions.osCauseDamage(avatar, damage);
        }

        public void osCauseDamage(string avatar, double damage, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            m_AA_Functions.osCauseDamage(avatar, damage, regionName, position, lookat);
        }

        public void osCauseHealing(string avatar, double healing)
        {
            m_AA_Functions.osCauseHealing(avatar, healing);
        }

        public void AASetCenterOfGravity(LSL_Types.Vector3 position)
        {
            m_AA_Functions.AASetCenterOfGravity(position);
        }
    }
}
