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
using Permissions = enum_149;
using PartTypes = class_191;
using Texture = class_256;

public static class ConveyorManager
{
	private static IDetour hook_Sim_method_1832;
	public static PartType Conveyor;

	private static Texture HexBase;
	private static Texture[] ConveyorBelts;
	private static class_126 ConveyorLighting;

	public static void LoadPuzzleContent()
	{
		HexBase = class_235.method_615("embraceInfinifactory/textures/parts/base");
		ConveyorLighting = new class_126(class_235.method_615("embraceInfinifactory/textures/parts/conveyor/rail.lighting/left"),
											class_235.method_615("embraceInfinifactory/textures/parts/conveyor/rail.lighting/right"),
											class_235.method_615("embraceInfinifactory/textures/parts/conveyor/rail.lighting/bottom"),
											class_235.method_615("embraceInfinifactory/textures/parts/conveyor/rail.lighting/top"));
		ConveyorBelts = new Texture[80];
		for (int i = 0; i < 80; i++)
		{
			string num = (i < 10 ? "0" : "") + i.ToString();
			ConveyorBelts[i] = class_235.method_615("embraceInfinifactory/textures/parts/conveyor/belt.array/belt_" + num);
		}


		Conveyor = new PartType()
		{
			/*ID*/field_1528 = "embrace-infinifactory-conveyor",
			/*Name*/field_1529 = class_134.method_253("Conveyor", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("Passively moves an atom, or group of atoms, in the indicated direction", string.Empty),
			/*Cost*/field_1531 = 2,
			/*Force-rotatable*/field_1536 = true,//default=false, but true for arms and the berlo, which are 1-hex big but can be rotated individually
			/*Is a Glyph?*/field_1539 = true,//default=false
			/*Hex Footprint*/field_1540 = new HexIndex[1] { new HexIndex(0, 0) },//default=emptyList
			/*Icon*/field_1547 = class_235.method_615("embraceInfinifactory/textures/parts/icons/conveyor"),
			/*Hover Icon*/field_1548 = class_235.method_615("embraceInfinifactory/textures/parts/icons/conveyor_hover"),
			/*Glow (Shadow)*/field_1549 = class_238.field_1989.field_97.field_382,//1-hex
			/*Stroke (Outline)*/field_1550 = class_238.field_1989.field_97.field_383,//1-hex
			/*Permissions*/field_1551 = Permissions.Track,
		};

		QApi.AddPartType(Conveyor, (part, pos, editor, renderer) => {

			var vec2 = new Vector2(42f, 49f);
			renderer.method_526(HexBase, new HexIndex(0,0), new Vector2(-1f, -1f), vec2, 0);

			var vector2_3 = (ConveyorLighting.method_235().ToVector2() / 2).Rounded();
			renderer.method_527(ConveyorLighting, new HexIndex(0, 0), new Vector2(0.0f, 0.0f), vector2_3, (float) 0.0);

			int index = 0;
			if (editor.method_503() != enum_128.Stopped)
			{
				index = (int)((double)new struct_27(Time.Now().Ticks).method_603() * 60.0) % ConveyorBelts.Length;
			}
			renderer.method_521(ConveyorBelts[index], vec2 + new Vector2(-1f, -33f));
		});

		QApi.AddPartTypeToPanel(Conveyor, PartTypes.field_1770); //inserts part type after Track in the parts tray







		//------------------------- HOOKING -------------------------//
		hook_Sim_method_1832 = new Hook(
			typeof(Sim).GetMethod("method_1832", BindingFlags.Instance | BindingFlags.NonPublic),
			typeof(ConveyorManager).GetMethod("OnSimMethod1832", BindingFlags.Static | BindingFlags.NonPublic)
		);
	}

	public static void Unload()
	{
		hook_Sim_method_1832.Dispose();
	}

	private delegate void orig_Sim_method_1832(Sim self, bool param_5369);
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
			foreach (var part in partList.Where(x => x.method_1159() == Conveyor))//for each conveyor
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