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
//using BondType = enum_126;
//using BondSite = class_222;
//using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;

public class MainClass : QuintessentialMod
{
	public static PartType Eviscerator, Laser;
	public static PartType Welder;

	public static Texture HexBase, WelderFaceplate;
	public static Texture[] EvisceratorTextures;
	public static Texture[] WelderFlare, WelderBeamX, WelderBeamY, WelderBeamZ, WelderWeld, WelderGlyphWeld;

	public static Sound snd_eviscerator, snd_laser, snd_welder;

	public static Texture debug_push, debug_arrow;

	public static void playSound(Sound SOUND, float VOLUME, Sim sim = null, SolutionEditorBase seb = null)
	{
		float FACTOR = 1f;
		if (sim != null)
		{
			seb = new DynamicData(sim).Get<SolutionEditorBase>("field_3818");
		}
		if (seb != null)
		{
			if (seb is class_194) // GIF recording, so mute
			{
				FACTOR = 0.0f;
			}
			else if (seb is SolutionEditorScreen)
			{
				var seb_dyn = new DynamicData(seb);
				bool isQuickMode = seb_dyn.Get<Maybe<int>>("field_4030").method_1085();
				FACTOR = isQuickMode ? 0.5f : 1f;
			}
		}
		class_158.method_376(SOUND.field_4061, class_269.field_2109 * VOLUME * FACTOR, false);
	}

	public override Type SettingsType => typeof(MySettings);
	public class MySettings
	{
		[SettingsLabel("Use Infinifactory-style gif frames.")]
		public bool replaceGifFrames = false;
	}
	public override void ApplySettings()
	{
		base.ApplySettings();
		var SET = (MySettings)Settings;
		if (SET.replaceGifFrames)
		{
			class_238.field_1989.field_99.field_698 = class_235.method_615("embraceInfinifactory/textures/solution_editor/gif_frame");
			class_238.field_1989.field_99.field_699 = class_235.method_615("embraceInfinifactory/textures/solution_editor/gif_frame_pipeline");
		}
		else
		{
			class_238.field_1989.field_99.field_698 = class_235.method_615("textures/solution_editor/gif_frame");
			class_238.field_1989.field_99.field_699 = class_235.method_615("textures/solution_editor/gif_frame_pipeline");
		}
	}
	public override void Load()
	{
		Settings = new MySettings();
	}

	public override void LoadPuzzleContent()
	{
		debug_push = class_235.method_615("embraceInfinifactory/textures/parts/conveyor/debug_push");
		debug_arrow = class_235.method_615("embraceInfinifactory/textures/parts/conveyor/debug_arrow");

		snd_eviscerator = class_238.field_1991.field_1842;//class_235.method_616("sounds/glyph_evisceration");
		snd_laser = class_238.field_1991.field_1842;
		snd_welder = class_238.field_1991.field_1827;

		HexBase = class_235.method_615("embraceInfinifactory/textures/parts/base");
		WelderFaceplate = class_235.method_615("embraceInfinifactory/textures/parts/welder/faceplate");

		EvisceratorTextures = new Texture[4];
		for (int i = 0; i < 4; i++)
		{
			EvisceratorTextures[i] = class_235.method_615("embraceInfinifactory/textures/parts/eviscerator/spikes" + i);
		}

		WelderFlare = new Texture[6];
		for (int i = 0; i < 6; i++)
		{
			WelderFlare[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/flare.array/flare_0" + i);
		}
		WelderBeamX = new Texture[16];
		WelderBeamY = new Texture[16];
		WelderBeamZ = new Texture[16];
		for (int i = 0; i < 16; i++)
		{
			string num = (i < 10 ? "0" : "") + i.ToString();
			WelderBeamX[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/beamX.array/beam_" + num);
			WelderBeamY[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/beamY.array/beam_" + num);
			WelderBeamZ[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/beamZ.array/beam_" + num);
		}
		WelderWeld = new Texture[24];
		for (int i = 0; i < 24; i++)
		{
			string num = (i < 10 ? "0" : "") + i.ToString();
			WelderWeld[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/weld.array/bond_" + num);
		}
		WelderGlyphWeld = new Texture[10];
		for (int i = 0; i < 10; i++)
		{
			string num = (i < 10 ? "0" : "") + i.ToString();
			WelderGlyphWeld[i] = class_235.method_615("embraceInfinifactory/textures/parts/welder/glyph_weld.array/welder_weld_" + num);
		}

		Eviscerator = new PartType()
		{
			/*ID*/field_1528 = "embrace-infinifactory-eviscerator",
			/*Name*/field_1529 = class_134.method_253("Eviscerator", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("Eviscerators are capable of quickly destroying atoms.", string.Empty),
			/*Cost*/field_1531 = 50,
			/*Is a Glyph*/field_1539 = true,
			/*Hex Footprint*/field_1540 = new HexIndex[1] { new HexIndex(0, 0) },//default=emptyList
			/*Can be Rotated*/field_1546 = false,
			/*Part Icon*/field_1547 = class_238.field_1989.field_90.field_245.field_303, //replace later with actual graphic
			/*Hovered Part Icon*/field_1548 = class_238.field_1989.field_90.field_245.field_304, //replace later with actual graphic
			/*Glow (Shadow)*/field_1549 = class_238.field_1989.field_97.field_382,//1-hex
			/*Stroke (Outline)*/field_1550 = class_238.field_1989.field_97.field_383,//1-hex
			/*Permissions*/field_1551 = Permissions.Disposal,
		};

		QApi.AddPartType(Eviscerator, (part, pos, editor, renderer) => {

			var vec2 = new Vector2(42f, 49f);
			renderer.method_526(class_238.field_1989.field_90.field_169, new HexIndex(0, 0), new Vector2(0f, 0f), vec2, 0);

			vec2 = new Vector2(28f, 28f);
			renderer.method_526(EvisceratorTextures[0], new HexIndex(0, 0), new Vector2(0f, 0f), vec2, 0);
			renderer.method_526(EvisceratorTextures[1], new HexIndex(0, 0), new Vector2(0f, 0f), vec2, 0);
			renderer.method_526(EvisceratorTextures[2], new HexIndex(0, 0), new Vector2(0f, 0f), vec2, 0);
			renderer.method_526(EvisceratorTextures[3], new HexIndex(0, 0), new Vector2(0f, 0f), vec2, 0);
		});

		Laser = new PartType()
		{
			/*ID*/field_1528 = "embrace-infinifactory-laser",
			/*Name*/field_1529 = class_134.method_253("Laser", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("Lasers, like eviscerators, are used to destroy blocks. They have an infinite range.", string.Empty),
			/*Cost*/field_1531 = 50,
			/*Force-rotatable*/field_1536 = true,//default=false, but true for arms and the berlo, which are 1-hex big but can be rotated individually
			/*Is a Glyph*/field_1539 = true,
			/*Hex Footprint*/field_1540 = new HexIndex[1] { new HexIndex(0, 0) },//default=emptyList
			/*Part Icon*/field_1547 = class_238.field_1989.field_90.field_245.field_303, //replace later with actual graphic
			/*Hovered Part Icon*/field_1548 = class_238.field_1989.field_90.field_245.field_304, //replace later with actual graphic
			/*Glow (Shadow)*/field_1549 = class_238.field_1989.field_97.field_382,//1-hex
			/*Stroke (Outline)*/field_1550 = class_238.field_1989.field_97.field_383,//1-hex
			/*Permissions*/field_1551 = Permissions.Disposal,
		};

		QApi.AddPartType(Laser, (part, pos, editor, renderer) => {
			//draw code
			Texture field169 = HexBase;
			Vector2 vector2 = (field169.field_2056.ToVector2() / 2).Rounded() + new Vector2(0.0f, 1f);
			renderer.method_521(field169, vector2);

			Vector2 vector2_24 = new Vector2(41f, 48f);
			renderer.method_521(debug_arrow, vector2_24 + new Vector2(-9f, -21f));
		});

		Welder = new PartType()
		{
			/*ID*/field_1528 = "embrace-infinifactory-welder",
			/*Name*/field_1529 = class_134.method_253("Welder", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("Welders use concentrated DELETED energy to bond adjacent atoms. Although two welders are required to create a welder \"beam\", more than two may be used to create complex welding matrices.", string.Empty),
			/*Cost*/field_1531 = 10,
			/*Is a Glyph*/field_1539 = true,
			/*Hex Footprint*/field_1540 = new HexIndex[1] { new HexIndex(0, 0) },//default=emptyList
			/*Part Icon*/field_1547 = class_235.method_615("embraceInfinifactory/textures/parts/icons/welder"),
			/*Hovered Part Icon*/field_1548 = class_235.method_615("embraceInfinifactory/textures/parts/icons/welder_hover"),
			/*Glow (Shadow)*/field_1549 = class_238.field_1989.field_97.field_382,//1-hex
			/*Stroke (Outline)*/field_1550 = class_238.field_1989.field_97.field_383,//1-hex
			/*Permissions*/field_1551 = Permissions.Bonder,
		};

		QApi.AddPartType(Welder, (part, pos, editor, renderer) => {
			//draw code
			Texture field169 = HexBase;
			Vector2 vector2 = (field169.field_2056.ToVector2() / 2).Rounded();
			renderer.method_521(field169, vector2);
			renderer.method_521(WelderFaceplate, vector2 + new Vector2(-1f,-1f));

			HexIndex hex = part.method_1161();
			var adjacentX = hex + new HexIndex(1, 0);
			var adjacentY = hex + new HexIndex(0, 1);
			var adjacentZ = hex + new HexIndex(-1, 1);
			bool boolX = false;
			bool boolY = false;
			bool boolZ = false;
			foreach (Part solutionPart in editor.method_502().field_3919.Where(x => x.method_1159() == Welder))
			{
				var partHex = solutionPart.method_1161();
				if (partHex == adjacentX)
				{
					boolX = true;
				}
				else if (partHex == adjacentY)
				{
					boolY = true;
				}
				else if (partHex == adjacentZ)
				{
					boolZ = true;
				}
			}

			var index = (int)((double)new struct_27(Time.Now().Ticks).method_603() * 30.0) % WelderBeamX.Length;
			if (boolX) renderer.method_521(WelderBeamX[index], new Vector2(10f, 51f));
			if (boolY) renderer.method_521(WelderBeamY[index], new Vector2(30f, 18f));
			if (boolZ) renderer.method_521(WelderBeamZ[index], new Vector2(70f, 18f));

			index = (int)((double)new struct_27(Time.Now().Ticks).method_603() * 30.0) % WelderFlare.Length;
			renderer.method_521(WelderFlare[index], new Vector2(25f, 24f));
		});





		QApi.RunAfterCycle((sim_self, flag)=>
		{
			var sim_dyn = new DynamicData(sim_self);
			Type simType = typeof(Sim);
			MethodInfo Method_1850 = simType.GetMethod("method_1850", BindingFlags.NonPublic | BindingFlags.Instance);//atom exists at location
			var seb = sim_dyn.Get<SolutionEditorBase>("field_3818");
			var dict = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
			bool playEviscerate = false;
			bool playLaser = false;
			bool playWeld = false;
			HashSet<HexIndex> welderHexes = new();

			foreach (var part in dict.Keys)
			{
				//
				PartType partType = part.method_1159();

				if (partType == Eviscerator)
				{
					AtomReference atomReference;
					Maybe<AtomReference> maybeAtomReference = (Maybe<AtomReference>)Method_1850.Invoke(sim_self, new object[] { part, new HexIndex(0, 0), new List<Part>(), false });
					bool atomExists = maybeAtomReference.method_99<AtomReference>(out atomReference);
					if (atomExists)
					{
						playEviscerate = true;
						var arg = class_187.field_1742.method_492(part.method_1161());
						seb.field_3935.Add(new class_228(seb, (enum_7)1, arg + new Vector2(147f, 47f), class_238.field_1989.field_90.field_242, 30f, Vector2.Zero, 0.0f));
						seb.field_3936.Add(new class_228(seb, (enum_7)1, arg + new Vector2(80f, 0.0f), class_238.field_1989.field_90.field_240, 30f, Vector2.Zero, 0.0f));
						atomReference.field_2277.method_1107(atomReference.field_2278);
					}
				}
				else if (partType == Welder)
				{
					welderHexes.Add(part.method_1161());
				}
				else if (partType == Laser)
				{
					HexIndex pos = part.method_1161();
					int rotation = (part.method_1163().GetNumberOfTurns() % 6 + 6) % 6;

					Func<HexIndex, bool> onDeathRow;

					switch (rotation)
					{
						default:
						case 0: onDeathRow = hex => hex.R == pos.R && hex.Q > pos.Q; break;
						case 1: onDeathRow = hex => hex.Q == pos.Q && hex.R > pos.R; break;
						case 2: onDeathRow = hex => hex.Q + hex.R == pos.Q + pos.R && hex.R > pos.R; break;
						case 3: onDeathRow = hex => hex.R == pos.R && hex.Q < pos.Q; break;
						case 4: onDeathRow = hex => hex.Q == pos.Q && hex.R < pos.R; break;
						case 5: onDeathRow = hex => hex.Q + hex.R == pos.Q + pos.R && hex.R < pos.R; break;
					}

					var molecules = sim_dyn.Get<List<Molecule>>("field_3823");
					HashSet<HexIndex> deathRow = new();
					HashSet<HexIndex> killedRow = new();

					foreach (var molecule in molecules)
					{
						foreach (var hex in molecule.method_1100().Keys)
						{
							if (onDeathRow(hex)) deathRow.Add(hex);
						}
					}

					foreach (var hex in deathRow)
					{
						AtomReference atomReference;
						Maybe<AtomReference> maybeAtomReference = (Maybe<AtomReference>)struct_18.field_1431;
						molecules = sim_dyn.Get<List<Molecule>>("field_3823");
						foreach (var molecule in molecules)
						{
							Atom atom;
							if (molecule.method_1100().TryGetValue(hex, out atom))
							{

								maybeAtomReference = (Maybe<AtomReference>)new AtomReference(molecule, hex, atom.field_2275, atom, flag);
								break;
							}
						}

						bool atomExists = maybeAtomReference.method_99<AtomReference>(out atomReference);
						if (atomExists)
						{
							playLaser = true;
							var arg = class_187.field_1742.method_492(hex);
							seb.field_3935.Add(new class_228(seb, (enum_7)1, arg + new Vector2(147f, 47f), class_238.field_1989.field_90.field_242, 30f, Vector2.Zero, 0.0f));
							seb.field_3936.Add(new class_228(seb, (enum_7)1, arg + new Vector2(80f, 0.0f), class_238.field_1989.field_90.field_240, 30f, Vector2.Zero, 0.0f));
							atomReference.field_2277.method_1107(atomReference.field_2278);
							killedRow.Add(hex);
						}
					}

					if (playLaser)
					{
						HexIndex target = killedRow.First() - pos;
						HexIndex unit = new HexIndex(Math.Max(-1, Math.Min(target.Q, 1)), Math.Max(-1, Math.Min(target.R, 1)));
						target = pos;
						while (killedRow.Count > 0)
						{
							target += unit;
							if (killedRow.Contains(target)) killedRow.Remove(target);
							var arg = class_187.field_1742.method_492(target);
							seb.field_3936.Add(new class_228(seb, (enum_7)1, arg + new Vector2(80f, 0.0f), class_238.field_1989.field_90.field_240, 30f, Vector2.Zero, 0.0f));
						}
					}
				}
			}
			//perform welds
			List<class_222> welds = new();
			foreach (var hex in welderHexes)
			{
				//
				var adjacentX = hex + new HexIndex(1, 0);
				var adjacentY = hex + new HexIndex(0, 1);
				var adjacentZ = hex + new HexIndex(-1, 1);
				if (welderHexes.Contains(adjacentX)) welds.Add(new class_222(hex, adjacentX, enum_126.Standard, (Maybe<AtomType>)struct_18.field_1431));
				if (welderHexes.Contains(adjacentY)) welds.Add(new class_222(hex, adjacentY, enum_126.Standard, (Maybe<AtomType>)struct_18.field_1431));
				if (welderHexes.Contains(adjacentZ)) welds.Add(new class_222(hex, adjacentZ, enum_126.Standard, (Maybe<AtomType>)struct_18.field_1431));
			}
			foreach (var weld in welds)
			{
				var hexA = weld.field_1920;
				var hexB = weld.field_1921;
				AtomReference atomReferenceA, atomReferenceB;
				Maybe<AtomReference> maybeAtomReferenceA = (Maybe<AtomReference>)struct_18.field_1431;
				Maybe<AtomReference> maybeAtomReferenceB = (Maybe<AtomReference>)struct_18.field_1431;
				var molecules = sim_dyn.Get<List<Molecule>>("field_3823");
				foreach (var molecule in molecules)
				{
					Atom atom;
					if (molecule.method_1100().TryGetValue(hexA, out atom))
					{
						maybeAtomReferenceA = (Maybe<AtomReference>)new AtomReference(molecule, hexA, atom.field_2275, atom, flag);
						break;
					}
				}
				foreach (var molecule in molecules)
				{
					Atom atom;
					if (molecule.method_1100().TryGetValue(hexB, out atom))
					{
						maybeAtomReferenceB = (Maybe<AtomReference>)new AtomReference(molecule, hexB, atom.field_2275, atom, flag);
						break;
					}
				}

				bool atomAExists = maybeAtomReferenceA.method_99<AtomReference>(out atomReferenceA);
				bool atomBExists = maybeAtomReferenceB.method_99<AtomReference>(out atomReferenceB);
				if (atomAExists && atomBExists) {
					Molecule molecule = atomReferenceA.field_2277;
					if (atomReferenceA.field_2277 != atomReferenceB.field_2277)
					{
						molecules.Remove(atomReferenceA.field_2277);
						molecules.Remove(atomReferenceB.field_2277);
						molecule = molecule.method_1119(atomReferenceB.field_2277);
						molecules.Add(molecule);
					}
					class_200 class200 = new class_200()
					{
						field_1814 = enum_126.Standard,
						field_1815 = class_238.field_1989.field_83.field_145, // class_235.method_615("textures/bonds/standard");
						field_1816 = class_238.field_1989.field_83.field_146, // class_235.method_615("textures/bonds/standard_normals");
						field_1817 = WelderWeld,
						field_1819 = WelderGlyphWeld,
					};
					BondEffect bondEffect = new BondEffect(seb, (enum_7)1, class200.field_1817, 60f, class200.field_1818);
					if (molecule.method_1112(class200.field_1814, hexA, hexB, (Maybe<BondEffect>)bondEffect))
					{
						Vector2 vector2_9 = class_162.method_413(class_187.field_1742.method_492(hexA), class_187.field_1742.method_492(hexB), 0.5f);
						Texture[] field1819 = class200.field_1819;
						double num = (double)class_187.field_1742.method_492(hexB - hexA).Angle();
						class_228 class228 = new class_228(seb, (enum_7)1, vector2_9, field1819, 30f, Vector2.Zero, (float)num);
						seb.field_3935.Add(class228);
						playWeld = true;
					}
				}
				sim_dyn.Set("field_3823", molecules);
			}

			if (playEviscerate) playSound(snd_eviscerator, 0.3f, sim_self);
			if (playLaser) playSound(snd_laser, 0.3f, sim_self);
			if (playWeld) playSound(snd_welder, 0.2f, sim_self);
		});

		QApi.AddPartTypeToPanel(Welder, PartTypes.field_1772); //inserts part type after Bonder in the parts tray
		QApi.AddPartTypeToPanel(Eviscerator, PartTypes.field_1781); //inserts part type after Disposal in the parts tray
		QApi.AddPartTypeToPanel(Laser, PartTypes.field_1781); //inserts part type after Disposal in the parts tray

		ConveyorManager.LoadPuzzleContent();
		FakeGripper.LoadPuzzleContent();
	}

	public override void Unload()
	{

		ConveyorManager.Unload();
		FakeGripper.Unload();
	}

	public override void PostLoad()
	{
		//
	}
}