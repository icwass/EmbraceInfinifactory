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
//using Permissions = enum_149;
//using BondType = enum_126;
//using BondSite = class_222;
//using AtomTypes = class_175;
//using PartTypes = class_191;
using Texture = class_256;

//makes sure to call these functions in the appropriate places:
//	FakeGripper.LoadPuzzleContent()
//	FakeGripper.Unload()

public static class FakeGripper
{
	//data structs, enums, variables
	private static IDetour hook_Sim_method_1828;
	private static IDetour hook_Sim_method_1832;
	private static IDetour hook_Sim_method_1831;

	private static bool debugDisplay = false; // for troubleshooting purposes

	public static PartType partType = new PartType(); /////////////////////////////////////////////////////////////////////////////////////////////////////////////// try to change to private later

	private static Dictionary<Part, PartSimState> tempFakeGrippers = new();


	//---------------------------------------------------//
	//public APIs

	/// <summary>
	/// Creates a new FakeGripper with the specified behavior.
	/// The FakeGripper will perform until the next method_1828 call.
	/// </summary>
	/// <param name="type">The part type to be added.</param>
	/// <param name="mechanism">Whether to add to the mechanisms section or the glyphs section.</param>
	public static void create()
	{
		//
	}


	//---------------------------------------------------//
	//internal helper methods



	//---------------------------------------------------//
	//internal main methods









	//---------------------------------------------------//
	//public calls and hooking




	public static void LoadPuzzleContent()
	{
		QApi.AddPartType(partType, (part, pos, editor, renderer) => {
			if (!debugDisplay) return;
			//draw code copied from gripper
			MethodInfo Method_2002 = typeof(SolutionEditorBase).GetMethod("method_2002", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo Method_2003 = typeof(SolutionEditorBase).GetMethod("method_2003", BindingFlags.NonPublic | BindingFlags.Static);
			class_236 class236 = editor.method_1989(part, pos); // needed so we can fetch the current simulation timestep
			Texture field175 = class_238.field_1989.field_90.field_175;
			Vector2 field1984 = class236.field_1984;
			Vector2 vector2_1 = (field175.field_2056.ToVector2() / 2).Rounded();
			Method_2002.Invoke(null, new object[] { field175, field1984, vector2_1, 0.0f });
			for (int index = 0; index < 3; ++index)
			{
				class_126 class126 = class_238.field_1989.field_90.field_237;
				Vector2 vector2_2 = new Vector2((float)(class126.method_235().X / 2), -19f).Rounded();
				float num = index * 120 * ((float)Math.PI / 180f);


				Method_2003.Invoke(null, new object[] { class126, field1984, vector2_2, class236.field_1986 + num });
			}
		});

		//------------------------- HOOKING -------------------------//
		On.SolutionEditorScreen.method_50 += SES_Method_50; // removes fake grippers - otherwise fake grippers are left on the board and prevents glyph placement
		On.Solution.method_1936 += Solution_Method_1936; // drawing fake grippers is not needed - modified to prevent a gif-recorder bug
		On.SolutionEditorBase.method_1985 += SolutionEditorBase_Method_1985; // drawing fake grippers is not needed - modified to prevent a gif-recorder bug
		//hook_Sim_method_1828 = new Hook(
		//	typeof(Sim).GetMethod("method_1828", BindingFlags.Instance | BindingFlags.NonPublic),
		//	typeof(FakeGripper).GetMethod("OnSimMethod1828", BindingFlags.Static | BindingFlags.NonPublic)
		//);
		//hook_Sim_method_1832 = new Hook(
		//	typeof(Sim).GetMethod("method_1832", BindingFlags.Instance | BindingFlags.NonPublic),
		//	typeof(FakeGripper).GetMethod("OnSimMethod1832", BindingFlags.Static | BindingFlags.NonPublic)
		//);
		//hook_Sim_method_1831 = new Hook(
		//	typeof(Sim).GetMethod("method_1831", BindingFlags.Instance | BindingFlags.NonPublic),
		//	typeof(FakeGripper).GetMethod("OnSimMethod1831", BindingFlags.Static | BindingFlags.NonPublic)
		//);
	}

	public static void Unload()
	{
		//hook_Sim_method_1828.Dispose();
		//hook_Sim_method_1832.Dispose();
		//hook_Sim_method_1831.Dispose();
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
		return;

		//old stuff////////////////////////////////////////////////////////////////////////////////////////////////////////////
		if (param_5369)
		{
			///////////////
			orig(sim_self, true);
			///////////////
		}
		else
		{
			//this work-around makes sure that atoms are pulled into outputs at the right time so outputs are drawn correctly
			var sim_dyn = new DynamicData(sim_self);
			var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
			var solution = SEB.method_502();
			var tempFakeGrippers = ConveyorManager.pullFakeGrippers(solution);
			foreach (var part in tempFakeGrippers)
			{
				solution.field_3919.Add(part);
			}

			Part fakeSupraPart = new Part(FakeGripper.partType, true);
			fakeSupraPart.field_2696 = new Part[tempFakeGrippers.Count];
			for (int i = 0; i < tempFakeGrippers.Count; i++)
			{
				fakeSupraPart.field_2696[i] = tempFakeGrippers[i];
			}
			solution.field_3919.Add(fakeSupraPart);
			var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
			partSimStates.Add(fakeSupraPart, fakeSupraPart.method_1178());
			sim_dyn.Set("field_3821", partSimStates);
			///////////////
			orig(sim_self, false);
			///////////////

			solution.field_3919.Remove(fakeSupraPart);
			partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
			partSimStates.Remove(fakeSupraPart);
			sim_dyn.Set("field_3821", partSimStates);
		}
	}
	private static void OnSimMethod1831(orig_Sim_method_1831 orig, Sim sim_self)
	{
		orig(sim_self);
		return;
		ConveyorManager.loadFakeGrippers(sim_self);
	}

	//------------------------- END HOOKING -------------------------//

	public static void SES_Method_50(On.SolutionEditorScreen.orig_method_50 orig, SolutionEditorScreen ses_self, float param_5703)
	{
		if (ses_self.method_503() == enum_128.Stopped)
		{
			ConveyorManager.removeFakeGrippers(ses_self);
		}
		orig(ses_self, param_5703);
	}

	public static Maybe<Part> SolutionEditorBase_Method_1985(On.SolutionEditorBase.orig_method_1985 orig, SolutionEditorBase seb_self, Molecule param_5539)
	{
		// sometimes FakeGrippers in the gif recorder don't have a PartSimState for some reason (?!?)
		// to prevent the gif recorder from crashing, we temporarily remove them so no attempt is made to draw them
		var interface2 = seb_self.method_507();
		var dyn = new DynamicData(interface2);
		var solution = seb_self.method_502();
		var partList = solution.field_3919;
		var dict = dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
		List<Part> badParts = new();
		foreach (var part in partList.Where(x => x.method_1159() == partType && !dict.Keys.Contains(x)))
		{
			badParts.Add(part);
		}
		partList.RemoveAll(x => badParts.Contains(x));
		///////////////
		var ret = orig(seb_self, param_5539);
		///////////////
		//then we put them back
		foreach (var part in badParts)
		{
			solution.field_3919.Add(part);
		}
		return ret;
	}

	public static List<Part> Solution_Method_1936(On.Solution.orig_method_1936 orig, Solution solution_self, interface_2 param_5478)
	{
		// sometimes FakeGrippers in the gif recorder don't have a PartSimState for some reason (?!?)
		// to prevent the gif recorder from crashing, we temporarily remove them so no attempt is made to draw them
		var interface2 = param_5478;
		var dyn = new DynamicData(param_5478);
		var solution = solution_self;
		var partList = solution.field_3919;
		var dict = dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
		List<Part> badParts = new();
		foreach (var part in partList.Where(x => x.method_1159() == partType && !dict.Keys.Contains(x)))
		{
			badParts.Add(part);
		}
		partList.RemoveAll(x => badParts.Contains(x));
		///////////////
		var ret = orig(solution_self, param_5478);
		///////////////
		//then we put them back
		foreach (var part in badParts)
		{
			solution.field_3919.Add(part);
		}
		return ret;
	}


}