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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Globalization;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Scenes;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Aurora.Framework;
using Aurora.ScriptEngine.AuroraDotNetEngine.APIs.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.Plugins;
using Aurora.ScriptEngine.AuroraDotNetEngine.Runtime;

namespace Aurora.ScriptEngine.AuroraDotNetEngine
{
    public class ScriptData
    {
        #region Constructor

        public ScriptData(ScriptEngine engine)
        {
            m_ScriptEngine = engine;
            ScriptFrontend = Aurora.DataManager.DataManager.RequestPlugin<IScriptDataConnector>();

            NextEventDelay.Add("at_rot_target", 0);
            NextEventDelay.Add("at_target", 0);
            NextEventDelay.Add("attach", 0);
            NextEventDelay.Add("changed", 0);
            NextEventDelay.Add("collision", 0);
            NextEventDelay.Add("collision_end", 0);
            NextEventDelay.Add("collision_start", 0);
            NextEventDelay.Add("control", 0);
            NextEventDelay.Add("dataserver", 0);
            NextEventDelay.Add("email", 0);
            NextEventDelay.Add("http_response", 0);
            NextEventDelay.Add("http_request", 0);
            NextEventDelay.Add("land_collision", 0);
            NextEventDelay.Add("land_collision_end", 0);
            NextEventDelay.Add("land_collision_start", 0);
            NextEventDelay.Add("link_message", 0);
            NextEventDelay.Add("listen", 0);
            NextEventDelay.Add("money", 0);
            NextEventDelay.Add("moving_end", 0);
            NextEventDelay.Add("moving_start", 0);
            NextEventDelay.Add("no_sensor", 0);
            NextEventDelay.Add("not_at_rot_target", 0);
            NextEventDelay.Add("not_at_target", 0);
            NextEventDelay.Add("object_rez", 0);
            NextEventDelay.Add("on_rez", 0);
            NextEventDelay.Add("remote_data", 0);
            NextEventDelay.Add("run_time_permissions", 0);
            NextEventDelay.Add("sensor", 0);
            NextEventDelay.Add("state_entry", 0);
            NextEventDelay.Add("state_exit", 0);
            NextEventDelay.Add("timer", 0);
            NextEventDelay.Add("touch", 0);
            NextEventDelay.Add("touch_end", 0);
            NextEventDelay.Add("touch_start", 0);
        }

        #endregion

        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //This is the UUID of the actual script.
        public UUID ItemID;
        public SceneObjectPart part;

        private ScriptEngine m_ScriptEngine;
        public Scene World;
        public IScript Script;
        public string State;
        public bool Running = true;
        public bool Disabled = false;
        public bool Suspended = false;
        public bool Loading = true;
        public string Source;
        public int StartParam;
        public StateSource stateSource;
        public AppDomain AppDomain;
        public Dictionary<string, IScriptApi> Apis = new Dictionary<string, IScriptApi>();
        public bool TimerQueued = false;
        public bool CollisionInQueue = false;
        public bool TouchInQueue = false;
        public bool RemoveTouchEvents = false;
        public bool RemoveCollisionEvents = false;
        public bool RemoveLandCollisionEvents = false;
        public bool LandCollisionInQueue = false;
        public List<Changed> ChangedInQueue = new List<Changed>();
        public int LastControlLevel = 0;
        public int ControlEventsInQueue = 0;
        public bool StartedFromSavedState = false;
        public UUID RezzedFrom = UUID.Zero; // If rezzed from llRezObject, this is not Zero
        /// <summary>
        /// This helps make sure that we clear out previous versions so that we don't have overlapping script versions running
        /// </summary>
        public int VersionID = 0;


        public long EventDelayTicks = 0;
        public long NextEventTimeTicks = 0;
        public string AssemblyName;
        public UUID UserInventoryItemID;
        public bool PostOnRez;
        public TaskInventoryItem InventoryItem;
        public ScenePresence presence = null;
        public DetectParams[] LastDetectParams = null;
        public Object[] PluginData = new Object[0];
        private StateSave LastStateSave = null;
        private IScriptDataConnector ScriptFrontend;
        private double DefaultEventDelayTicks = (double)0.05;
        private double TouchEventDelayTicks = (double)0.1;
        private double TimerEventDelayTicks = (double)0.01;
        private double CollisionEventDelayTicks = (double)0.5;
        private Dictionary<string, long> NextEventDelay = new Dictionary<string, long>();
        public bool MovingInQueue = false;

        public bool EventsProcDataLocked = false;
        public bool InEventsProcData = false;
        public ScriptEventsProcData EventsProcData = new ScriptEventsProcData();

        #endregion

        #region Close Script

        /// <summary>
        /// This closes the scrpit, removes it from any known spots, and disposes of itself.
        /// </summary>
        /// <param name="Silent">Should we back up this script and fire state_exit?</param>
        public void CloseAndDispose(bool Silent)
        {

// this is still broken ?
            m_ScriptEngine.MaintenanceThread.SetEventSchSetIgnoreNew(this, true);
            m_ScriptEngine.MaintenanceThread.RemoveFromEventSchQueue(this);

            if (!Silent)
            {
                if (Script != null)
                    {
                    /*
                                        //Save the state
                                        ScriptDataSQLSerializer.SaveState(this, m_ScriptEngine);
                                        //Fire this directly so its not closed before its fired
                                        SetEventParams("state_exit", new DetectParams[0]);

                                        m_ScriptEngine.MaintenanceThread.ProcessQIS(new QueueItemStruct()
                                        {
                                            ID = this,
                                            CurrentlyAt = null,
                                            functionName = "state_exit",
                                            param = new object[0],
                                            llDetectParams = new DetectParams[0],
                                            VersionID = VersionID
                                        });
                     */
// dont think we should fire state_exit here
//                    m_ScriptEngine.MaintenanceThread.DoAndWaitEventSch(this, "state_exit",
//                        new DetectParams[0], VersionID, EventPriority.FirstStart, new object[0]);
                    ScriptDataSQLSerializer.SaveState(this, m_ScriptEngine);
                }
            }
            VersionID += 5;
            m_ScriptEngine.MaintenanceThread.SetEventSchSetIgnoreNew(this, false);

            //Give the user back any controls we took
            ReleaseControls();

            // Tell script not to accept new requests
            //These are fine to set as the state wont be saved again
            if (!Silent)
            {
                Running = false;
                Disabled = true;
            }

            // Remove from internal structure
            ScriptEngine.ScriptProtection.RemoveScript(this);
            if (!Silent) //Don't remove on a recompile because we'll make it under a different assembly
                ScriptEngine.ScriptProtection.RemovePreviouslyCompiled(Source);

            //Remove any errors that might be sitting around
            m_ScriptEngine.ScriptErrorReporter.RemoveError(ItemID);

            #region Clean out script parts
            part.AngularVelocity = Vector3.Zero; // Removed in SL
            part.ScheduleFullUpdate(PrimUpdateFlags.AngularVelocity); // Send changes to client.
            #endregion

            if (Script != null)
            {
                // Stop long command on script
                m_ScriptEngine.RemoveScript(part.UUID, ItemID);

                //Release the script and destroy it
                ILease lease = (ILease)RemotingServices.GetLifetimeService(Script as MarshalByRefObject);
                if (lease != null)
                    lease.Unregister(Script.Sponsor);

                Script.Close();
                Script = null;
            }

            if (AppDomain == null)
                return;

            // Tell AppDomain that we have stopped script
            m_ScriptEngine.AppDomainManager.UnloadScriptAppDomain(AppDomain);
            AppDomain = null;

            MainConsole.Instance.Output("[" + m_ScriptEngine.ScriptEngineName + "]: Closed Script " + InventoryItem.Name + " in " + part.Name, "AppendTimeStamp");
        }

        /// <summary>
        /// Removes any permissions the script may have on other avatars.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        private void ReleaseControls()
        {
            if (InventoryItem != null)
            {
                if (part != null)
                {
                    int permsMask = InventoryItem.PermsMask;
                    UUID permsGranter = InventoryItem.PermsGranter;

                    ScenePresence sp = World.GetScenePresence(permsGranter);
                    if ((permsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                    {
                        if (sp != null)
                            sp.UnRegisterControlEventsToScript(part.LocalId, ItemID);
                    }
                }

                InventoryItem.PermsMask = 0;
                InventoryItem.PermsGranter = UUID.Zero;
            }
        }

        #endregion

        #region Reset Script and State Change

        public void ResetEvents()
        {
            RemoveCollisionEvents = false;
            RemoveTouchEvents = false;
            RemoveLandCollisionEvents = false;
            TouchInQueue = false;
            LandCollisionInQueue = false;
            ChangedInQueue.Clear();
            LastControlLevel = 0;
            ControlEventsInQueue = 0;
        }

        /// <summary>
        /// This resets the script back to its default state.
        /// </summary>
        internal void Reset()
        {
            if (Script == null)
                return;
            //Release controls over people.
            ReleaseControls();
            //Remove other items from the queue.

            m_ScriptEngine.MaintenanceThread.RemoveFromEventSchQueue(this);
            
            VersionID++;
            //Reset the state to default
            State = "default";
            //Reset all variables back to their original values.
            Script.ResetVars();
            //Tell the SOP about the change.
            if (!Running) //No events!
                part.SetScriptEvents(ItemID, 0);
            else
                part.SetScriptEvents(ItemID, Script.GetStateEventFlags(State));

            //Remove MinEventDelay
            EventDelayTicks = 0;
            //Remove events that may be fired again after the user stops touching the prim, etc
            // These will be removed after the next ***_start event
            ResetEvents();
            RemoveLandCollisionEvents = true;
            RemoveCollisionEvents = true;
            RemoveTouchEvents = true;

            //Unset the events that may still be firing after the change.
            m_ScriptEngine.RemoveScript(part.UUID, ItemID);

            //Fire state_entry
            m_ScriptEngine.AddToScriptQueue(this, "state_entry", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[] { });

            m_ScriptEngine.MaintenanceThread.AddToStateSaverQueue(this, true);
            MainConsole.Instance.Output("[" + m_ScriptEngine.ScriptEngineName + "]: Reset Script " + ItemID, "AppendTimeStamp");
        }

        internal void ChangeState(string state)
        {
            if (State != state)
                {
                m_ScriptEngine.MaintenanceThread.FlushEventSchQueue(this, false);
                m_ScriptEngine.MaintenanceThread.AddEventSchQueue(this, "state_exit",
                    new DetectParams[0], VersionID, EventPriority.FirstStart, new object[0] { });
                
                State = state;

                //Remove events that may be fired again after the user stops touching the prim, etc
                // These will be removed after the next ***_start event
                RemoveLandCollisionEvents = true;
                RemoveCollisionEvents = true;
                RemoveTouchEvents = true;

                //Wipe out old events
//                VersionID++;

                //Tell the SOP about the change.
                part.SetScriptEvents(ItemID, Script.GetStateEventFlags(state));
                ScriptEngine.ScriptProtection.AddNewScript(this);

                m_ScriptEngine.MaintenanceThread.AddEventSchQueue(this, "state_entry",
                    new DetectParams[0], VersionID, EventPriority.FirstStart, new object[0] { });
                }
        }

        #endregion

        #region Helpers

        //Makes ToString look nicer
        public override string ToString()
        {
            return "UUID: " + part.UUID + ", itemID: " + ItemID;
        }

        /// <summary>
        /// Sets up the APIs for the script
        /// </summary>
        internal void SetApis()
        {
            Apis = new Dictionary<string, IScriptApi>();

            foreach (IScriptApi api in m_ScriptEngine.GetAPIs())
            {
                if (ScriptEngine.ScriptProtection.CheckAPI(api.Name))
                {
                    Apis[api.Name] = api;
                    Apis[api.Name].Initialize(m_ScriptEngine, part, part.LocalId, ItemID, ScriptEngine.ScriptProtection);
                }
            }
            foreach (KeyValuePair<string, IScriptApi> kv in Apis)
            {
                Script.InitApi(kv.Key, kv.Value);
            }
        }

        public void DisplayUserNotification(string message, string stage, bool postScriptCAPSError, bool IsError)
        {
            if (presence != null && (!PostOnRez) && postScriptCAPSError)
                if (m_ScriptEngine.ChatCompileErrorsToDebugChannel)
                    presence.ControllingClient.SendAgentAlertMessage("Script saved with errors, check debug window!", false);
                else
                    presence.ControllingClient.SendAgentAlertMessage("Script saved with errors!", false);

            if (postScriptCAPSError)
                m_ScriptEngine.ScriptErrorReporter.AddError(ItemID, new ArrayList(message.Split('\n')));

            // DISPLAY ERROR ON CONSOLE
            if (m_ScriptEngine.DisplayErrorsOnConsole)
            {
                string consoletext = IsError ? "Error " : "Warning ";
                consoletext += stage + " script:\n" + message + " itemID: " + ItemID + ", CompiledFile: " + AssemblyName;
                m_log.Error(consoletext);
            }

            // DISPLAY ERROR INWORLD
            string inworldtext = IsError ? "Error " : "Warning ";
            inworldtext += stage + " script: " + message;
            if (inworldtext.Length > 1100)
                inworldtext = inworldtext.Substring(0, 1099);

            if (m_ScriptEngine.ChatCompileErrorsToDebugChannel)
                World.SimChat(inworldtext, ChatTypeEnum.DebugChannel, 2147483647, part.AbsolutePosition, part.Name, part.UUID, false);

            m_ScriptEngine.ScriptFailCount++;
            m_ScriptEngine.ScriptErrorMessages += inworldtext;
        }

        #endregion

        #region Start Script

        /// <summary>
        /// Fires the events after the compiling has occured
        /// </summary>
        public void FireEvents()
        {
            if (RezzedFrom != UUID.Zero)
            {
                //Post the event for the prim that rezzed us
                m_ScriptEngine.AddToObjectQueue(RezzedFrom, "object_rez", new DetectParams[0],
                    -1, new object[] { part.ParentGroup.RootPart.UUID });
                RezzedFrom = UUID.Zero;
            }
            if (StartedFromSavedState)
            {
                if (PostOnRez)
                    m_ScriptEngine.AddToScriptQueue(this, "on_rez", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[] { new LSL_Types.LSLInteger(StartParam) });

                if (stateSource == StateSource.AttachedRez)
                    m_ScriptEngine.AddToScriptQueue(this, "attach", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[] { new LSL_Types.LSLString(part.AttachedAvatar.ToString()) });
                else if (stateSource == StateSource.NewRez)
                    m_ScriptEngine.AddToScriptQueue(this, "changed", new DetectParams[0], VersionID, EventPriority.FirstStart, new Object[] { new LSL_Types.LSLInteger(256) });
                else if (stateSource == StateSource.PrimCrossing)
                    // CHANGED_REGION
                    m_ScriptEngine.AddToScriptQueue(this, "changed", new DetectParams[0], VersionID, EventPriority.FirstStart, new Object[] { new LSL_Types.LSLInteger(512) });
            }
            else
            {
                m_ScriptEngine.AddToScriptQueue(this, "state_entry", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[0]);

                if (PostOnRez)
                    m_ScriptEngine.AddToScriptQueue(this, "on_rez", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[] { new LSL_Types.LSLInteger(StartParam) });

                if (stateSource == StateSource.AttachedRez)
                    m_ScriptEngine.AddToScriptQueue(this, "attach", new DetectParams[0], VersionID, EventPriority.FirstStart, new object[] { new LSL_Types.LSLString(part.AttachedAvatar.ToString()) });
            }
        }

        /// <summary>
        /// This starts the script and sets up the variables.
        /// </summary>
        /// <returns></returns>
        public void Start(bool reupload)
        {
            DateTime StartTime = DateTime.Now.ToUniversalTime();

            //Clear out the removing of events for this script.
            VersionID++;

            //Reset this
            StartedFromSavedState = false;

            //Clear out previous errors if they were not cleaned up
            m_ScriptEngine.ScriptErrorReporter.RemoveError(ItemID);

            //Find the inventory item
            part.TaskInventory.TryGetValue(ItemID, out InventoryItem);

            //Try to see if this was rezzed from someone's inventory
            UserInventoryItemID = part.FromUserInventoryItemID;

            //Try to find the avatar who started this.
            presence = World.GetScenePresence(part.OwnerID);

            #region HTML Reader

            if (ScriptEngine.ScriptProtection.AllowHTMLLinking)
            {
                //Read the URL and load it.
                if (Source.Contains("#IncludeHTML "))
                {
                    string URL = "";
                    int line = Source.IndexOf("#IncludeHTML ");
                    URL = Source.Remove(0, line);
                    URL = URL.Replace("#IncludeHTML ", "");
                    URL = URL.Split('\n')[0];
                    string webSite = Utilities.ReadExternalWebsite(URL);
                    Source = Source.Replace("#IncludeHTML " + URL, webSite);
                }
            }
            else
            {
                //Remove the line then
                if (Source.Contains("#IncludeHTML "))
                {
                    string URL = "";
                    int line = Source.IndexOf("#IncludeHTML ");
                    URL = Source.Remove(0, line);
                    URL = URL.Replace("#IncludeHTML ", "");
                    URL = URL.Split('\n')[0];
                    Source = Source.Replace("#IncludeHTML " + URL, "");
                }
            }

            #endregion

            // Attempt to find a state save to load from
            if (!reupload && Loading && ScriptFrontend != null) //Only get state saves on rezzing or region start up, in both cases, we will have the cached state as we loaded all states when the region started. 
                LastStateSave = ScriptFrontend.GetStateSave(ItemID, UserInventoryItemID, true);

            //If the saved state exists, if it isn't a reupload (something changed), and if the assembly exists, load the state save
            if (!reupload && Loading && LastStateSave != null
                && File.Exists(Path.Combine(m_ScriptEngine.ScriptEnginesPath, Path.Combine(
                    "Scripts",
                    LastStateSave.AssemblyName))))
            {
                //Retrive the previous assembly
                AssemblyName = Path.Combine(m_ScriptEngine.ScriptEnginesPath, Path.Combine(
                    "Scripts",
                    LastStateSave.AssemblyName));
            }
            else
            {
                LastStateSave = null;
                if (reupload)
                {
                    //Close the previous script
                    CloseAndDispose(true);

                    //Increment this so that we clear out the previous upload
                    VersionID++;
                }

                //Try to find a previously compiled script in this instance
                string PreviouslyCompiledAssemblyName = ScriptEngine.ScriptProtection.TryGetPreviouslyCompiledScript(Source);
                if (PreviouslyCompiledAssemblyName != null) //Already exists in this instance, so we do not need to check whether it exists
                    AssemblyName = PreviouslyCompiledAssemblyName;
                else
                {
                    try
                    {
                        m_ScriptEngine.Compiler.PerformScriptCompile(Source, ItemID, part.OwnerID, VersionID, out AssemblyName);
                        #region Errors and Warnings

                        #region Errors

                        string[] compileerrors = m_ScriptEngine.Compiler.GetErrors();

                        if (compileerrors != null && compileerrors.Length != 0)
                        {
                            string error = string.Empty;
                            foreach(string compileerror in compileerrors)
                            {
                                error += compileerror;
                            }
                            DisplayUserNotification(error, "compiling", reupload, true);
                            return;
                        }

                        #endregion

                        #region Warnings

                        if (m_ScriptEngine.ShowWarnings)
                        {
                            string[] compilewarnings = m_ScriptEngine.Compiler.GetWarnings();

                            if (compilewarnings != null && compilewarnings.Length != 0)
                            {
                                string error = string.Empty;
                                foreach(string compileerror in compileerrors)
                                {
                                    error += compileerror;
                                }
                                DisplayUserNotification(error, "compiling", reupload, false);
                                return;
                            }
                        }

                        #endregion

                        #endregion
                    }
                    catch (Exception ex)
                    {
                        DisplayUserNotification(ex.ToString(), "compiling", reupload, true);
                        return;
                    }
                }
            }

            bool useDebug = false;
            if (useDebug)
                m_log.Debug("[" + m_ScriptEngine.ScriptEngineName + "]: Stage 1 compile: " + (DateTime.Now.ToUniversalTime() - StartTime).TotalSeconds);

            //Create the app domain if needed.
            try
            {
                Script = m_ScriptEngine.AppDomainManager.LoadScript(AssemblyName, "Script.ScriptClass", out AppDomain);
                //Add now so that we don't add it too early and give it the possibility to fail
                ScriptEngine.ScriptProtection.AddPreviouslyCompiled(Source, this);
            }
            catch (System.IO.FileNotFoundException) // Not valid!!!
            {
                m_log.Error("[" + m_ScriptEngine.ScriptEngineName + "]: File not found in app domain creation. Corrupt state save! " + AssemblyName);
                ScriptEngine.ScriptProtection.RemovePreviouslyCompiled(Source);
                ScriptFrontend.DeleteStateSave(AssemblyName);
                Start(reupload); // Lets restart the script if this happens
                return;
            }
            catch (Exception ex)
            {
                DisplayUserNotification(ex.ToString(), "app domain creation", reupload, true);
                return;
            }

            ILease lease = (ILease)RemotingServices.GetLifetimeService(Script as MarshalByRefObject);
            if (lease != null) //Its null if it is all running in the same app domain
                lease.Register(Script.Sponsor);

            //If its a reupload, an avatar is waiting for the script errors
            if (reupload)
                m_ScriptEngine.ScriptErrorReporter.AddError(ItemID, new ArrayList(new string[] { "SUCCESSFULL" }));

            if (useDebug)
                m_log.Debug("[" + m_ScriptEngine.ScriptEngineName + "]: Stage 2 compile: " + (DateTime.Now.ToUniversalTime() - StartTime).TotalSeconds);

            SetApis();

            //Set the event flags
            part.SetScriptEvents(ItemID, Script.GetStateEventFlags(State));

            //Now do the full state save finding now that we have an app domain.
            if (LastStateSave != null)
            {
                ScriptDataSQLSerializer.Deserialize(this, m_ScriptEngine, LastStateSave);

                m_ScriptEngine.CreateFromData(part.UUID, ItemID, part.UUID,
                    PluginData);

                // we get new rez events on sim restart, too
                // but if there is state, then we fire the change
                // event
                StartedFromSavedState = true;
            }
            else
            {
                //Make a new state save now
            m_ScriptEngine.MaintenanceThread.AddToStateSaverQueue(this, true);
            }

            // Add it to our script memstruct so it can be found by other scripts
            ScriptEngine.ScriptProtection.AddNewScript(this);

            //All done, compiled successfully
            Loading = false;

            TimeSpan time = (DateTime.Now.ToUniversalTime() - StartTime);

            MainConsole.Instance.Output("[" + m_ScriptEngine.ScriptEngineName +
                    "]: Started Script " + InventoryItem.Name +
                    " in object " + part.Name +
                    (presence != null ? " by " + presence.Name : "") + 
                    " in region " + part.ParentGroup.Scene.RegionInfo.RegionName +
                    " in " + time.TotalSeconds + " seconds.", "AppendTimeStamp");
        }

        #endregion

        #region Event Processing

        public bool SetEventParams(string functionName, DetectParams[] qParams)
        {
            if (qParams.Length > 0)
                LastDetectParams = qParams;

            if (functionName == "control")
            {
                //For vehicles, otherwise breaks them. DO NOT REMOVE UNLESS YOU FIND A BETTER WAY TO FIX
                return true;
            }

            long NowTicks = Util.EnvironmentTickCount();

            if (EventDelayTicks != 0)
            {
                if (NowTicks < NextEventTimeTicks)
                    return false;

                NextEventTimeTicks = NowTicks + EventDelayTicks;
            }
            switch (functionName)
            {
                //Times pulled from http://wiki.secondlife.com/wiki/LSL_Delay
                case "touch": //Limits for 0.1 seconds
                case "touch_start":
                case "touch_end":
                    if (NowTicks < NextEventDelay[functionName])
                        return false;
                    NextEventDelay[functionName] = NowTicks + (long)(TouchEventDelayTicks * 100);
                    break;
                case "timer": //Settable timer limiter
                    if (NowTicks < NextEventDelay[functionName])
                        return false;
                    NextEventDelay[functionName] = NowTicks + (long)(TimerEventDelayTicks * 100);
                    break;
                case "collision": //Collision limiters taken off of reporting from WhiteStar in mantis 0004513
                case "collision_start":
                case "collision_end":
                case "land_collision":
                case "land_collision_start":
                case "land_collision_end":
                    if (NowTicks < NextEventDelay[functionName])
                        return false;
                    NextEventDelay[functionName] = NowTicks + (long)(CollisionEventDelayTicks * 100);
                    break;
                default: //Default is 0.05 seconds for event limiting
                    if (NowTicks < NextEventDelay[functionName])
                        return false;
                    NextEventDelay[functionName] = NowTicks + (long)(DefaultEventDelayTicks * 100);
                    break;
            }
            //Add the event to the stats
            part.ParentGroup.AddScriptEPS(1);
            return true;
        }

        #endregion
    }
}