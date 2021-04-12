using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
		// This file contains your actual script.
		//
		// You can either keep all your code here, or you can create separate
		// code files to make your program easier to navigate while coding.
		//
		// In order to add a new utility class, right-click on your project, 
		// select 'New' then 'Add Item...'. Now find the 'Space Engineers'
		// category under 'Visual C# Items' on the left hand side, and select
		// 'Utility Class' in the main area. Name it in the box below, and
		// press OK. This utility class will be merged in with your code when
		// deploying your final script.
		//
		// You can also simply create a new utility class manually, you don't
		// have to use the template if you don't want to. Just do so the first
		// time to see what a utility class looks like.
		// 
		// Go to:
		// https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
		//
		// to learn more about ingame scripts.

		IMyMotorStator PlacerRotor;
		IMyPistonBase PlacerPiston;
		IMyPistonBase ClipPiston;
		bool setupError = true;
		ReloadStates reloadState = ReloadStates.RELOAD_COMPLETE;
		List<IMyWarhead> warheads = new List<IMyWarhead>();
		Vector3D bombPosition = new Vector3D();
		int DetonationTime = 10;
		int countdown = 0;

		enum ReloadStates
		{
			RELOAD_COMPLETE = 0,
			RETRACTING_PLACER,
			MOVING_CLIP,
			LOADING_WARHEAD,
			EXTENDING_PLACER
		}

		public Program()
		{
			var clipPistons = new List<IMyPistonBase>();
			GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(clipPistons, (IMyPistonBase x) => x.CustomData.ToLower().Contains("warhead clip"));
			
			if (clipPistons.Count() > 0)
			{
				ClipPiston = clipPistons.First();

				var pistons = new List<IMyPistonBase>();
				GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistons, (IMyPistonBase x) => x.CubeGrid == ClipPiston.CubeGrid && x != ClipPiston);

				if (pistons.Count() > 0)
				{
					PlacerPiston = pistons.First();

					var rotors = new List<IMyMotorStator>();
					GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid == PlacerPiston.TopGrid);

					if (rotors.Count() > 0)
					{
						PlacerRotor = rotors.First();

						setupError = false;
					}
					else
					{
						Echo("Setup error. Make sure there is a rotor on the end of the placer piston and recompile the script.");
					}
				}
				else
				{
					Echo("Setup error. Make sure the placer piston is on the same subgrid as the clip piston and recompile the script.");
				}
			}
			else
			{
				Echo("Setup error. Make sure the clip piston has \"warhead clip\" in the custom data and recompile the script.");
			}

			if (!setupError)
			{
				Echo("Setup complete with no errors.");

				var payloadRotors = new List<IMyMotorStator>();
				GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(payloadRotors, (IMyMotorStator x) => x.CubeGrid == ClipPiston.TopGrid);

				if (payloadRotors.Count() == 0)
				{
					Echo("Warning: no payload rotors found!");
				}

				Runtime.UpdateFrequency = UpdateFrequency.Update1;
			}
		}

		public void Save()
		{
			// Called when the program needs to save its state. Use
			// this method to save your state to the Storage field
			// or some other means. 
			// 
			// This method is optional and can be removed if not
			// needed.
		}

		public void Main(string argument, UpdateType updateSource)
		{
			if (!setupError)
			{
				if ((updateSource & UpdateType.Update1) != 0)
				{
					switch (reloadState)
					{
						case ReloadStates.RELOAD_COMPLETE:
							// All done!
							break;
						case ReloadStates.RETRACTING_PLACER:
							if (PlacerPiston.CurrentPosition == PlacerPiston.MinLimit)
							{
								Echo("Moving clip.");
								reloadState = ReloadStates.MOVING_CLIP;
								GridTerminalSystem.GetBlocksOfType<IMyWarhead>(warheads);
							}
							else
							{
								PlacerPiston.Retract();
							}
							break;
						case ReloadStates.MOVING_CLIP:
							var closestWarheadDistance = GetClosestDistance(warheads, PlacerRotor);
							if (closestWarheadDistance < 0.5)
							{
								ClipPiston.Enabled = false;

								Echo("Loading warhead.");
								reloadState = ReloadStates.LOADING_WARHEAD;
							}
							else if (warheads.Count() == 0 && ClipPiston.CurrentPosition == ClipPiston.MaxLimit)
							{
								Echo("Out of warheads.");
								reloadState = ReloadStates.RELOAD_COMPLETE;
							}
							else
							{
								ClipPiston.Enabled = true;
								ClipPiston.Extend();
							}
							break;
						case ReloadStates.LOADING_WARHEAD:
							if (PlacerRotor.IsAttached)
							{
								var rotors = new List<IMyMotorStator>();
								GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid == ClipPiston.TopGrid && x.TopGrid == PlacerRotor.TopGrid);

								if (rotors.Count() > 0)
								{
									var otherRotor = rotors.First();
									otherRotor.Detach();
								}

								Echo("Extending placer.");
								reloadState = ReloadStates.EXTENDING_PLACER;
							}
							else
							{
								Echo("Attaching.");
								PlacerRotor.Attach();
							}
							break;
						case ReloadStates.EXTENDING_PLACER:
							if (PlacerPiston.CurrentPosition == PlacerPiston.MaxLimit)
							{
								Echo("Reload complete.");
								reloadState = ReloadStates.RELOAD_COMPLETE;
							}
							else
							{
								PlacerPiston.Extend();
							}
							break;
						default:
							Echo("Encountered unexpected state while reloading.");
							break;
					}
				}
				else if (argument.ToLower() == "reload")
				{
					Echo("Retracting placer piston.");
					PlacerRotor.Detach();
					reloadState = ReloadStates.RETRACTING_PLACER;
				}
				else if (argument.ToLower() == "fire")
				{
					if (PlacerRotor.IsAttached)
					{
						Echo("Deploying payload.");
						var viableWarheads = new List<IMyWarhead>();
						GridTerminalSystem.GetBlocksOfType<IMyWarhead>(viableWarheads, (IMyWarhead x) => x.CubeGrid == PlacerRotor.TopGrid);
						foreach (var warhead in viableWarheads)
						{
							warhead.DetonationTime = DetonationTime;
							warhead.StartCountdown();
						}

						if (viableWarheads.Count() > 0)
						{
							countdown = DetonationTime;
							bombPosition = PlacerRotor.GetPosition();
						}

						PlacerRotor.Detach();
						reloadState = ReloadStates.RETRACTING_PLACER;
					}
				}
				else if (argument.ToLower().StartsWith("timer"))
				{
					int.TryParse(argument.ToLower().Substring("timer".Length), out DetonationTime);
				}
			}
		}

		double GetClosestDistance(List<IMyWarhead> entities, IMyMotorStator reference)
		{
			IMyWarhead closestEntity = null;
			double closestDistance = 100;
			if (entities.Count() > 0)
			{
				foreach (var entity in entities)
				{
					var distance = Vector3D.Distance(reference.GetPosition(), entity.GetPosition());

					if (distance < closestDistance)
					{
						closestDistance = distance;
						closestEntity = entity;
					}
				}
			}

			return closestDistance;
		}
	}
}
