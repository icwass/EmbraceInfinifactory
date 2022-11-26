using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
using Quintessential.Settings;
using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace EmbraceInfinifactory;

using PartType = class_139;

public static class ConveyorManager
{
	private static IDetour hook_Sim_method_1828;
	private static IDetour hook_Sim_method_1832;
	private static IDetour hook_Sim_method_1831;

	public static Dictionary<Part, PartSimState> tempFakeGrippers = new(); // move to FakeGripper.cs eventually
		
	public static void removeFakeGrippers(SolutionEditorBase seb_self)
	{
		var solution = seb_self.method_502();
		var partList = solution.field_3919;
		var partsToRemove = new List<Part>();
		foreach (Part part in partList.Where(x => x.method_1159() == FakeGripper.partType))
		{
			partsToRemove.Add(part);
		}
		foreach (Part fake in partsToRemove)
		{
			partList.Remove(fake);
		}
	}

	public static void removeFakeGrippers(Sim sim_self)
	{
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
		var class401s = sim_dyn.Get<Dictionary<Part, Sim.class_401>>("field_3822");
		var droppedMolecules = sim_dyn.Get<List<Molecule>>("field_3828");
		foreach (Part fake in partList.Where(x => x.method_1159() == FakeGripper.partType))
		{
			if (partSimStates.ContainsKey(fake))
			{
				var maybeMol = partSimStates[fake].field_2729;
				if (maybeMol.method_1085())
				{
					Molecule mol = partSimStates[fake].field_2729.method_1087();
					droppedMolecules.Add(mol); // allows conduits to yoink the "dropped" molecule
				}
				partSimStates.Remove(fake);
			}
			if (class401s.ContainsKey(fake))
			{
				class401s.Remove(fake);
			}
		}
		removeFakeGrippers(SEB);
		sim_dyn.Set("field_3821", partSimStates);
		sim_dyn.Set("field_3822", class401s);
		sim_dyn.Set("field_3828", droppedMolecules);
	}



	public static void LoadPuzzleContent()
	{
		//


		//------------------------- HOOKING -------------------------//

		hook_Sim_method_1828 = new Hook(
			typeof(Sim).GetMethod("method_1828", BindingFlags.Instance | BindingFlags.NonPublic),
			typeof(ConveyorManager).GetMethod("OnSimMethod1828", BindingFlags.Static | BindingFlags.NonPublic)
		);
		hook_Sim_method_1832 = new Hook(
			typeof(Sim).GetMethod("method_1832", BindingFlags.Instance | BindingFlags.NonPublic),
			typeof(ConveyorManager).GetMethod("OnSimMethod1832", BindingFlags.Static | BindingFlags.NonPublic)
		);
		hook_Sim_method_1831 = new Hook(
			typeof(Sim).GetMethod("method_1831", BindingFlags.Instance | BindingFlags.NonPublic),
			typeof(ConveyorManager).GetMethod("OnSimMethod1831", BindingFlags.Static | BindingFlags.NonPublic)
		);
	}

	public static void Unload()
	{
		hook_Sim_method_1828.Dispose();
		hook_Sim_method_1832.Dispose();
		hook_Sim_method_1831.Dispose();
	}

	private delegate void orig_Sim_method_1828(Sim self);
	private delegate void orig_Sim_method_1832(Sim self, bool param_5369);
	private delegate void orig_Sim_method_1831(Sim self);
	private static void OnSimMethod1828(orig_Sim_method_1828 orig, Sim sim_self)
	{
		ConveyorManager.removeFakeGrippers(sim_self);
		orig(sim_self);
	}
	private static void OnSimMethod1832(orig_Sim_method_1832 orig, Sim sim_self, bool param_5369)
	{
		orig(sim_self, param_5369);
		if (param_5369) // then compute the pushing forces of conveyors and create fake grippers
		{
			var sim_dyn = new DynamicData(sim_self);
			var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
			var solution = SEB.method_502();
			var partList = solution.field_3919;
			var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");

			//prep work
			List<Part> gripperList = new List<Part>();
			foreach (Part part in partList)
			{
				foreach (Part key in part.field_2696)//for each gripper
				{
					if (partSimStates[key].field_2729.method_1085())//if part is holding onto a molecule
					{
						gripperList.Add(key);
					}
				}
			}

			//compute net forces
			var forceDictionary = new Dictionary<Molecule, ConveyorForce>();
			foreach (var part in partList.Where(x => x.method_1159() == MainClass.Conveyor))//for each conveyor
			{
				Type simType = typeof(Sim);
				MethodInfo Method_1833 = simType.GetMethod("method_1833", BindingFlags.NonPublic | BindingFlags.Instance);
				MethodInfo Method_1850 = simType.GetMethod("method_1850", BindingFlags.NonPublic | BindingFlags.Instance);//atom exists at location

				HexIndex hex = new HexIndex(0, 0);
				Maybe<AtomReference> maybeAtom = (Maybe<AtomReference>)Method_1850.Invoke(sim_self, new object[] { part, hex, gripperList, false });
				AtomReference atomReference;

				if (maybeAtom.method_99<AtomReference>(out atomReference) && !(bool)Method_1833.Invoke(sim_self, new object[] { atomReference.field_2277, gripperList }))
				{
					Molecule mol = atomReference.field_2277;
					ConveyorForce force = new ConveyorForce(part);
					//atom is part of a molecule that isn't held - apply passive pushing force
					if (!forceDictionary.ContainsKey(mol))
					{
						forceDictionary.Add(mol, force);
					}
					else
					{
						forceDictionary[mol] += force;
					}
				}
			}

			//push molecules with a valid net force
			foreach (var kvp in forceDictionary)
			{
				HexIndex translation;
				if (kvp.Value.isValidPush(out translation))
				{
					//create a FakeGripper for pushing it
					Molecule molecule = kvp.Key;
					HexIndex origin = molecule.method_1100().Keys.First();

					FakeGripper.create(origin, molecule, translation, HexRotation.R0);
				}
			}
		}
	}
	private static void OnSimMethod1831(orig_Sim_method_1831 orig, Sim sim_self)
	{
		orig(sim_self);
	}
}

public class ConveyorForce
{
	public int X = 0;
	public int Y = 0;
	public int Z = 0;

	private static int Limit(int n) => Math.Max(-1, Math.Min(n, 1));

	public ConveyorForce(int x, int y, int z)
	{
		this.X = x;
		this.Y = y;
		this.Z = z;
	}

	public ConveyorForce(Part part)
	{
		int rot = (part.method_1163().GetNumberOfTurns() % 6 + 6) % 6;
		this.X = (new int[6] { 1, 0, 0, -1, 0, 0})[rot];
		this.Y = (new int[6] { 0, 1, 0, 0, -1, 0})[rot];
		this.Z = (new int[6] { 0, 0, 1, 0, 0, -1})[rot];
	}

	public static ConveyorForce operator +(ConveyorForce a, ConveyorForce b)
	=> new ConveyorForce( a.X + b.X, a.Y + b.Y, a.Z + b.Z);

	public bool isValidPush(out HexIndex translation) // if valid, returns the HexIndex needed for a translation
	{
		int x = this.X;
		int y = this.Y;
		int z = this.Z;
		translation = new HexIndex(0, 0);
		HexIndex translation_x = new HexIndex(Limit(x), 0);
		HexIndex translation_y = new HexIndex(0, Limit(y));
		HexIndex translation_z = new HexIndex(-Limit(z), Limit(z));

		int ax = Math.Abs(x);
		int ay = Math.Abs(y);
		int az = Math.Abs(z);
		//all three forces might be equal in magnitude
		if (ax == ay && ax == az)
		{
			//if so, it comes down to the directions
			if (x == z)
			{
				if (x == -y)
				{
					// forces are balanced, no push is needed
					// this includes the x=y=z=0 case
					return false;
				}
				//else x == y
				translation = translation_y;
			}
			else //x != z
			{
				translation = x == y ? translation_x : translation_z;
			}
			return true;
		}
		//if not, there may be one force that is larger than the others
		if (ax > ay && ax > az)
		{
			translation = translation_x;
			return true;
		}
		if (ay > ax && ay > az)
		{
			translation = translation_y;
			return true;
		};
		if (az > ax && az > ay)
		{
			translation = translation_z;
			return true;
		}
		//otherwise two forces are equal in magnitude
		//and the third is weaker
		if (x == y || x == -z || y == z)
		{
			//the dominant forces push at an angle that isn't a multiple of 60 degrees
			//instead of pushing in a bad direction, we don't push at all
			return false;
		}
		else
		{
			//the two dominant forces sum to a force in the third direction
			//and since the third force was strictly weaker, this summed force is still dominant
			if (y == -z)
			{
				translation = new HexIndex(Limit(y), 0);
			}
			else if (x == z)
			{
				translation = new HexIndex(0, Limit(x));
			}
			else // x == -y
			{
				translation = new HexIndex(-Limit(y), Limit(y));
			}
			return true;
		}
		//this covers all cases
	}
}