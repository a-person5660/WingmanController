using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        IMyBroadcastListener _myBroadcastListener;
        IMyBroadcastListener _myBroadcastListener2;
        string myBroadcastTag;
        string defaultBroadcastTag = "wingmanTestTag";

        List<IMyFlightMovementBlock> flightAIList = new List<IMyFlightMovementBlock>();
        List<IMyBasicMissionBlock> basicAIList = new List<IMyBasicMissionBlock>();
        List<IMyPathRecorderBlock> pathRecorderList = new List<IMyPathRecorderBlock>();
        List<IMyDefensiveCombatBlock> defensiveAIList = new List<IMyDefensiveCombatBlock>();
        List<IMyOffensiveCombatBlock> offensiveAIList = new List<IMyOffensiveCombatBlock>();

        // input arguments format will be
        // execute "block name" -tag "tag" = 1
        // execute "block name" -tag "tag" -property "target property" "new value" = 2
        // execute "block name" -tag "tag" -action "action name" = 2
        // execute "block name" -action "action name" = 1
        // cant use property and action at the same time because i wouldn't be able to tell what order they are in without some extra work i cant be bothered to do
        // to do: arguments to change individual AI block settings

        public Program()
        {
            _commands["execute"] = Execute;
            myBroadcastTag = Me.CustomData;
            _myBroadcastListener2 = IGC.RegisterBroadcastListener(defaultBroadcastTag);
            _myBroadcastListener2.SetMessageCallback(defaultBroadcastTag);
            _myBroadcastListener = IGC.RegisterBroadcastListener(myBroadcastTag);
            _myBroadcastListener.SetMessageCallback(myBroadcastTag);
        }

        public void Execute()
        {
            bool tagBool = _commandLine.Switch("tag");
            bool propertyBool = _commandLine.Switch("property");
            bool actionBool = _commandLine.Switch("action");

            string broadcastString = null;

            if (propertyBool && actionBool)
            {
                Echo("Cannot set property and action at the same time");
                return;
            }

            string tag = "";
            if (tagBool)
            {
                tag = _commandLine.Argument(2);
                if (tag == null)
                {
                    Echo("Cannot set tag, no tag specified");
                    return;
                }
            }

            for (int i = 1; i < _commandLine.ArgumentCount; i++)
            {
                if(tagBool && i == 2)
                {
                    continue; // don't include the tag in the arguments to be sent
                }
                if (broadcastString == null)
                    broadcastString = _commandLine.Argument(i);
                else
                    broadcastString = broadcastString + ";" + _commandLine.Argument(i);
            }
            if (broadcastString == null)
            {
                Echo("Cannot set behaviour, invalid argument specified");
                return;
            }
            if (tagBool)
            {
                IGC.SendBroadcastMessage(tag, broadcastString);
                Echo("Broadcasting arguments: " + broadcastString + ". to tag: " + tag);
            }
            else
            {
                IGC.SendBroadcastMessage(defaultBroadcastTag, broadcastString);
                Echo("Broadcasting arguments: " + broadcastString + ". to tag: " + defaultBroadcastTag);
            }
        }

        public void setProperty(IMyTerminalBlock block, string propertyName, float input)
        {
            ITerminalProperty property = block.GetProperty(propertyName);
            if (property != null)
            {
                Echo("Setting property " + propertyName + " to " + input.ToString());
                block.SetValue<float>(propertyName, input);
            }
            else
                Echo("Property not found: " + propertyName);
        }

        public void doAction(IMyTerminalBlock block, string actionName)
        {
            ITerminalAction action = block.GetActionWithName(actionName);
            if (action != null)
            {
                Echo("Applying action: " + action.Name.ToString() + " to block: " + block.CustomName);
                List<ITerminalProperty> properties = new List<ITerminalProperty>();
                block.GetProperties(properties);
                foreach(var property in properties)
                {
                    if (property.Id == actionName)
                    {
                        Echo("Property same as action name: " + property.Id);
                        try { ITerminalProperty<bool> prop = property.As<bool>(); prop.SetValue(block, true); Echo("Set property " + property.Id + " to: True"); }
                        catch { Echo("Error setting property " + property.Id); return; }
                    }
                }
                action.Apply(block);
            }
            else
                Echo("Action not found: " + actionName);
        }

        public void Process(MyIGCMessage myIGCMessage)
        {
            if (myIGCMessage.Data is string)
            {
                bool tagBool = myIGCMessage.Tag == myBroadcastTag; // if the message is directed at us then set tagBool to true
                bool propertyBool = false;
                bool actionBool = false;

                string data = myIGCMessage.Data.ToString();
                string[] dataArray = data.Split(';');
                Echo("Received IGC Public Message");
                Echo("Tag=" + myIGCMessage.Tag);
                for (int i = 0; i < dataArray.Length; i++)
                {
                    Echo("Data=" + dataArray[i]);
                    Echo("Index=" + i.ToString());
                }

                if (dataArray.Length == 1)
                {
                    propertyBool = false;
                    actionBool = false;
                }
                if (dataArray.Length == 2)
                {
                    propertyBool = false;
                    actionBool = true;
                }
                else if(dataArray.Length == 3)
                {
                    propertyBool = true;
                    actionBool = false;
                }
                else
                {
                    propertyBool = false;
                    actionBool = false;
                }

                Echo(dataArray.Length.ToString() + " arguments received");

                Echo("PropertyBool=" + propertyBool.ToString());
                Echo("ActionBool=" + actionBool.ToString());

                GridTerminalSystem.GetBlocksOfType<IMyFlightMovementBlock>(flightAIList, block => block.IsSameConstructAs(Me));
                GridTerminalSystem.GetBlocksOfType<IMyBasicMissionBlock>(basicAIList, block => block.IsSameConstructAs(Me));
                GridTerminalSystem.GetBlocksOfType<IMyPathRecorderBlock>(pathRecorderList, block => block.IsSameConstructAs(Me));
                GridTerminalSystem.GetBlocksOfType<IMyDefensiveCombatBlock>(defensiveAIList, block => block.IsSameConstructAs(Me));
                GridTerminalSystem.GetBlocksOfType<IMyOffensiveCombatBlock>(offensiveAIList, block => block.IsSameConstructAs(Me));
                List<IMyTerminalBlock> AIBlockList = new List<IMyTerminalBlock>();
                AIBlockList.AddRange(basicAIList);
                AIBlockList.AddRange(pathRecorderList);
                AIBlockList.AddRange(defensiveAIList);
                AIBlockList.AddRange(offensiveAIList);

                foreach (IMyFlightMovementBlock block in flightAIList)
                {
                    if (dataArray.Contains("off")) // if instruction is "off" then disable all AI, else enable flight AI
                    {
                        doAction(block, "ActivateBehavior_Off");
                    }
                    else
                    {
                        doAction(block, "ActivateBehavior_On");
                    }
                }
                foreach (IMyTerminalBlock block in AIBlockList)
                {
                    if (block.CustomName == dataArray[0])
                    {
                        doAction(block, "ActivateBehavior_On");
                        Echo("Found block " + block.CustomName);
                        if (propertyBool == true)
                        {
                            try { float.Parse(dataArray[2]); } catch { Echo("Error converting input into float"); return; }
                            setProperty(block, dataArray[1], float.Parse(dataArray[2]));
                        }
                        if (actionBool == true)
                        {
                            doAction(block, dataArray[1]);
                        }
                    }
                    else
                    {
                        doAction(block, "ActivateBehavior_Off");
                    }
                }
            }
            else
            {
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) > 0 && Me.CustomData != "DCC")
            {
                while (_myBroadcastListener.HasPendingMessage)
                {
                    MyIGCMessage myIGCMessage = _myBroadcastListener.AcceptMessage();
                    Process(myIGCMessage);
                }
                while (_myBroadcastListener2.HasPendingMessage)
                {
                    MyIGCMessage myIGCMessage = _myBroadcastListener2.AcceptMessage();
                    Process(myIGCMessage);
                }
            }
            else if (_commandLine.TryParse(argument))
            {
                Action commandAction;

                // Retrieve the first argument. Switches are ignored.
                string command = _commandLine.Argument(0);

                // Now we must validate that the first argument is actually specified, 
                // then attempt to find the matching command delegate.
                if (command == null)
                {
                    Echo("No command specified");
                }
                else if (_commands.TryGetValue(_commandLine.Argument(0), out commandAction))
                {
                    // We have found a command. Invoke it.
                    commandAction();
                }
                else
                {
                    Echo($"Unknown command {command}");
                }
            }
        }
    }
}
