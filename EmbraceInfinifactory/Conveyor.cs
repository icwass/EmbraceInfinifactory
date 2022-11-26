using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
using Quintessential.Settings;
using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace EmbraceInfinifactory
{
	public static class ConveyorManager
	{
		private static Dictionary<Part, PartSimState> tempFakeGrippers = new();
		private static class_139 fakeGripper => FakeGripper.partType;// MainClass.FakeGripper // class_191.field_1769

		public static List<Part> pullFakeGrippers(Solution solution, bool invalidsOnly = false)
		{
			var partList = solution.field_3919;
			List<Part> tempFakeGrippers = new();
			foreach (var part in partList.Where(x => x.method_1159() == fakeGripper))
			{
				tempFakeGrippers.Add(part);
			}
			partList.RemoveAll(x => tempFakeGrippers.Contains(x));
			return tempFakeGrippers;
		}
		public static void pushFakeGrippers(Solution solution, List<Part> tempFakeGrippers)
		{
			foreach (var part in tempFakeGrippers)
			{
				solution.field_3919.Add(part);
			}
		}
		public static void computeFakeGrippers(Sim sim_self)
		{
			//
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

			//create fake grippers for molecules that have a valid net force
			foreach (var kvp in forceDictionary)
			{
				HexIndex translation;
				if (kvp.Value.isValidPush(out translation))
				{
					//create a fake Part and PartSimState for pushing it
					Molecule mol = kvp.Key;
					Part fakePart = new Part(fakeGripper, true);
					var fakePart_dyn = new DynamicData(fakePart);
					fakePart_dyn.Set("field_2692", mol.method_1100().Keys.First() + translation);

					var fakePartSimstate = fakePart.method_1178();
					fakePartSimstate.field_2729 = (Maybe<Molecule>)mol;
					fakePartSimstate.field_2742 = true;
					fakePartSimstate.field_2735 = translation;

					tempFakeGrippers.Add(fakePart,fakePartSimstate);

					////method for making rotations
					//
					//var origin = mol.method_1100().Keys.First();
					//var hexRotation = new HexRotation(2);
					//fakePart_dyn.Set("field_2692", origin);
					//mol.method_1116(origin, hexRotation);
					//fakePart_dyn.Set("field_2693",fakePart.method_1163() + hexRotation);
					//var fakePartSimstate = fakePart.method_1178();
					//fakePartSimstate.field_2729 = (Maybe<Molecule>)mol;
					//fakePartSimstate.field_2727 += hexRotation;
					//fakePartSimstate.field_2741 = hexRotation;
				}
			}
		}

		public static void loadFakeGrippers(Sim sim_self)
		{
			var sim_dyn = new DynamicData(sim_self);
			var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
			var solution = SEB.method_502();
			var partList = solution.field_3919;
			var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
			foreach (var kvp in tempFakeGrippers)
			{
				var fakePart = kvp.Key;
				var fakePartSimstate = kvp.Value;
				partList.Add(fakePart);
				partSimStates.Add(fakePart, fakePartSimstate);
				var maybeMol = fakePartSimstate.field_2729;
				if (maybeMol.method_1085())
				{
					Molecule mol = fakePartSimstate.field_2729.method_1087();
					mol.method_1118(fakePartSimstate.field_2735);
				}
			}
			tempFakeGrippers.Clear();
			sim_dyn.Set("field_3821", partSimStates);
		}
		
		public static void removeFakeGrippers(SolutionEditorBase seb_self)
		{
			var solution = seb_self.method_502();
			var partList = solution.field_3919;
			var partsToRemove = new List<Part>();
			foreach (Part part in partList.Where(x => x.method_1159() == fakeGripper))
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
			foreach (Part fake in partList.Where(x => x.method_1159() == fakeGripper))
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
}