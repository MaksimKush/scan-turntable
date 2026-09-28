// Revopoint-like 2-motor turntable — FDM / Ender 3 (220×220 bed, ~210 mm max part)
// Platform Ø200 mm. Yoke split: base + 2 arms (screw join). 2× PM42L-048-EPAO.
// USER-OWNED MOTOR: do not regenerate Motor_PM42L_048_EPAO.SLDPRT from this script.
// Prefer BuildRevopointLike.cs for pinion+rim azimuth and enclosed housing.
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Program
{
    static string OutDir = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    static string PartTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Part.PRTDOT";
    static string AsmTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Assembly.ASMDOT";
    static string LogPath = Path.Combine(OutDir, "sw_revopoint.log");

    // Ender 3 printable limit (margin on 220×220)
    const double MaxPrint = 0.210;

    // PM42
    const double MountP = 0.0495;
    const double MountHole = 0.0035;
    const double PlateT = 0.0008;
    const double BossH = 0.0015;
    const double ShaftL1 = 0.017;
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;

    // Structure — all single-piece footprints ≤ 210 mm
    const double BaseOd = 0.200;          // Ø200 base (fits bed)
    const double BaseT = 0.008;           // 8 mm, walls OK for PETG/PLA
    const double HubOd = 0.060;
    const double HubH = 0.016;
    const double HubBore = 0.003;

    // Yoke split for print: base plate + L/R arms (assembled span >210 OK)
    const double YokeBaseL = 0.210;       // 210 mm along X (max print)
    const double YokeBaseW = 0.055;       // 55 mm depth
    const double YokeBaseT = 0.008;
    const double ArmT = 0.010;            // 10 mm thick
    const double ArmW = 0.050;            // 50 mm depth
    const double ArmH = 0.100;            // 100 mm tall — print flat on side OK
    const double ArmInset = 0.005;        // from yoke-base ends
    const double TiltAxisZ = 0.075;       // above yoke base top
    const double TiltHole = 0.0085;       // clearance for Ø8 shaft

    // Platform Ø200 — user requirement
    const double PlatformOd = 0.200;
    const double PlatformT = 0.006;

    // Tilt shaft (suggest metal Ø8 rod; plastic OK if short)
    const double TiltShaftOd = 0.008;
    // span ≈ yoke base - 2*inset, through both arms
    const double TiltShaftLen = 0.220;    // note: print vertical or use metal rod

    const double ScrewHole = 0.0032;      // M3 clearance
    const double ScrewPitch = 0.030;      // arm↔base join spacing

    static void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + m;
        File.AppendAllText(LogPath, line + System.Environment.NewLine);
        Console.WriteLine(line);
    }

    static string FrontPlane(ModelDoc2 model)
    {
        var planes = new System.Collections.Generic.List<string>();
        Feature feat = (Feature)model.FirstFeature();
        while (feat != null)
        {
            if (feat.GetTypeName2() == "RefPlane") planes.Add(feat.Name);
            feat = (Feature)feat.GetNextFeature();
        }
        foreach (string name in planes)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("front") || n.Contains("спереди")) return name;
        }
        return planes[0];
    }

    static void SaveDoc(ModelDoc2 model, string path)
    {
        int e = 0, w = 0;
        bool ok = model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e, ref w);
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok);
        if (!ok) throw new Exception("SaveAs failed " + path);
    }

    static Feature Extrude(ModelDoc2 model, double depth, bool reverse)
    {
        Feature f = (Feature)model.FeatureManager.FeatureExtrusion2(
            true, false, reverse,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.0, false, false, false, false, 0.0, 0.0,
            false, false, false, false, true, true, true,
            (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null)
            f = (Feature)model.FeatureManager.FeatureExtrusion2(
                true, false, !reverse,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null) throw new Exception("Extrude fail");
        return f;
    }

    static Feature CutBlind(ModelDoc2 model, double depth)
    {
        Feature f = (Feature)model.FeatureManager.FeatureCut4(
            true, false, false,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.01, false, false, false, false,
            0.0174532925199433, 0.0174532925199433,
            false, false, false, false, false, true, true,
            false, false, false,
            (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null)
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, false, true,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null) throw new Exception("Cut fail");
        return f;
    }

    static ModelDoc2 NewPart(SldWorks sw)
    {
        sw.NewDocument(PartTpl, 0, 0, 0);
        return (ModelDoc2)sw.ActiveDoc;
    }

    static void ReplaceFile(string tmp, string final)
    {
        if (File.Exists(final))
        {
            try { File.SetAttributes(final, FileAttributes.Normal); File.Delete(final); }
            catch { Log("kept " + Path.GetFileName(tmp)); return; }
        }
        File.Move(tmp, final);
    }

    static void LogBox(string name, double dx, double dy, double dz)
    {
        bool ok = dx <= MaxPrint + 1e-6 && dy <= MaxPrint + 1e-6;
        Log(string.Format("PRINT {0}: {1:F0}×{2:F0}×{3:F0} mm  bedOK={4} (limit {5:F0})",
            name, dx * 1000, dy * 1000, dz * 1000, ok, MaxPrint * 1000));
    }

    // Base Ø200×8 — fits Ender3 flat
    static void BuildBase(SldWorks sw)
    {
        Log("=== Base_Revopoint Ø200 ===");
        LogBox("Base_Revopoint", BaseOd, BaseOd, BaseT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BaseOd / 2.0);
        Extrude(m, BaseT, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.0065); // boss clearance
        try { CutBlind(m, BaseT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Base_Revopoint_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Base_Revopoint.SLDPRT"));
    }

    static void BuildHub(SldWorks sw)
    {
        Log("=== Hub_Azimuth ===");
        LogBox("Hub_Azimuth", HubOd, HubOd, HubH);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, HubOd / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, HubBore / 2.0);
        Extrude(m, HubH, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        double rp = 0.022;
        for (int i = 0; i < 4; i++)
        {
            double a = i * Math.PI / 2.0 + Math.PI / 4.0;
            m.SketchManager.CreateCircleByRadius(rp * Math.Cos(a), rp * Math.Sin(a), 0, ScrewHole / 2.0);
        }
        try { CutBlind(m, HubH + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Hub_Azimuth_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Hub_Azimuth.SLDPRT"));
    }

    // Yoke base 210×55×8 with arm screw holes at ends + hub pattern
    static void BuildYokeBase(SldWorks sw)
    {
        Log("=== Yoke_Base ===");
        LogBox("Yoke_Base", YokeBaseL, YokeBaseW, YokeBaseT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        double hx = YokeBaseL / 2.0, hy = YokeBaseW / 2.0;
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-hx, -hy, 0, hx, hy, 0);
        Extrude(m, YokeBaseT, false);

        // Hub M3 pattern
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        double rp = 0.022;
        for (int i = 0; i < 4; i++)
        {
            double a = i * Math.PI / 2.0 + Math.PI / 4.0;
            m.SketchManager.CreateCircleByRadius(rp * Math.Cos(a), rp * Math.Sin(a), 0, ScrewHole / 2.0);
        }
        // Arm join holes (2 per end)
        double ax = hx - ArmInset - ArmT / 2.0;
        m.SketchManager.CreateCircleByRadius(-ax, ScrewPitch / 2.0, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-ax, -ScrewPitch / 2.0, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(ax, ScrewPitch / 2.0, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(ax, -ScrewPitch / 2.0, 0, ScrewHole / 2.0);
        try { CutBlind(m, YokeBaseT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Yoke_Base_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Yoke_Base.SLDPRT"));
    }

    // One tilt arm: printable flat (ArmW × ArmH × ArmT) with tilt hole + base screws + motor2 mount (left only)
    static void BuildArm(SldWorks sw, bool left)
    {
        string name = left ? "Yoke_Arm_L" : "Yoke_Arm_R";
        Log("=== " + name + " ===");
        // Print flat on largest face: ArmW × ArmH footprint, thickness ArmT
        LogBox(name, ArmW, ArmH, ArmT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        // Sketch in XY: width=ArmW (Y), height=ArmH modeled as extrude thickness ArmT along Z
        // For FDM: part is ArmW × ArmT rectangle extruded ArmH — then tilt hole on side.
        // Simpler: rectangle ArmW × ArmH extruded ArmT (print on ArmW×ArmH face).
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-ArmW / 2.0, 0, 0, ArmW / 2.0, ArmH, 0);
        Extrude(m, ArmT, false);

        // Tilt hole through thickness at Z_axis height from bottom of arm
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, TiltAxisZ, 0, TiltHole / 2.0);
        // Base screw holes near bottom (join to yoke base)
        m.SketchManager.CreateCircleByRadius(ScrewPitch / 2.0, 0.006, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-ScrewPitch / 2.0, 0.006, 0, ScrewHole / 2.0);
        if (left)
        {
            // Motor2 mount P=49.5 on arm face (holes through ArmT)
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ + MountP / 2.0, 0, MountHole / 2.0);
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ - MountP / 2.0, 0, MountHole / 2.0);
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ, 0, 0.006); // shaft/boss clearance (also tilt)
        }
        try { CutBlind(m, ArmT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, name + "_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, name + ".SLDPRT"));
    }

    // Platform Ø200×6 — primary user part
    static void BuildPlatform(SldWorks sw)
    {
        Log("=== Platform_Tilt Ø200 ===");
        LogBox("Platform_Tilt", PlatformOd, PlatformOd, PlatformT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, PlatformOd / 2.0);
        Extrude(m, PlatformT, false);

        // Center + peripheral M3 for optional clamps; cross-bore pockets for tilt shaft clamps
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.004);
        // two clamp holes along X at ±40 mm for shaft saddles (printed separate or zip-tie)
        m.SketchManager.CreateCircleByRadius(0.040, 0, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-0.040, 0, 0, ScrewHole / 2.0);
        try { CutBlind(m, PlatformT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Platform_Tilt_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Platform_Tilt.SLDPRT"));
    }

    // Shaft — note: 220 mm long; prefer metal Ø8; plastic print vertical
    static void BuildTiltShaft(SldWorks sw)
    {
        Log("=== Shaft_Tilt (prefer metal Ø8×220) ===");
        LogBox("Shaft_Tilt", TiltShaftOd, TiltShaftOd, TiltShaftLen);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, TiltShaftOd / 2.0);
        Extrude(m, TiltShaftLen, false);
        string tmp = Path.Combine(OutDir, "Shaft_Tilt_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Shaft_Tilt.SLDPRT"));
    }

    // Small shaft clamps (2×) — print flat, screw to platform
    static void BuildShaftClamp(SldWorks sw)
    {
        Log("=== Clamp_TiltShaft ===");
        double cx = 0.024, cy = 0.016, cz = 0.010;
        LogBox("Clamp_TiltShaft", cx, cy, cz);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-cx / 2.0, -cy / 2.0, 0, cx / 2.0, cy / 2.0, 0);
        Extrude(m, cz, false);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, TiltShaftOd / 2.0 + 0.0003);
        m.SketchManager.CreateCircleByRadius(0.008, 0, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-0.008, 0, 0, ScrewHole / 2.0);
        try { CutBlind(m, cz + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }
        string tmp = Path.Combine(OutDir, "Clamp_TiltShaft_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Clamp_TiltShaft.SLDPRT"));
    }

    static void SetTransform(Component2 c, double[] r9, double x, double y, double z)
    {
        MathTransform t = c.Transform2;
        double[] a = (double[])t.ArrayData;
        for (int i = 0; i < 9; i++) a[i] = r9[i];
        a[9] = x; a[10] = y; a[11] = z; a[12] = 1;
        t.ArrayData = a;
        c.Transform2 = t;
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, ModelDoc2 md, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref err, ref warn);
        sw.ActivateDoc3(md.GetTitle(), false, 0, ref err);
        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); } catch { }
        if (c == null) try { c = assy.AddComponent4(path, "", x, y, z); } catch { }
        if (c == null && assy.AddComponent(path, x, y, z))
        {
            object[] comps = (object[])assy.GetComponents(false);
            if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
        }
        if (c == null) throw new Exception("Add fail " + path);
        SetTransform(c, new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }, x, y, z);
        Log(string.Format("add {0} @ ({1:F1},{2:F1},{3:F1})", Path.GetFileName(path), x * 1000, y * 1000, z * 1000));
        return c;
    }

    static void BuildAssembly(SldWorks sw)
    {
        Log("=== Turntable_Revopoint2Motor ===");
        sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;

        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pulley = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        string baseP = Path.Combine(OutDir, "Base_Revopoint.SLDPRT");
        string hubP = Path.Combine(OutDir, "Hub_Azimuth.SLDPRT");
        string yokeBase = Path.Combine(OutDir, "Yoke_Base.SLDPRT");
        string armL = Path.Combine(OutDir, "Yoke_Arm_L.SLDPRT");
        string armR = Path.Combine(OutDir, "Yoke_Arm_R.SLDPRT");
        string platP = Path.Combine(OutDir, "Platform_Tilt.SLDPRT");
        string shaftP = Path.Combine(OutDir, "Shaft_Tilt.SLDPRT");
        string clampP = Path.Combine(OutDir, "Clamp_TiltShaft.SLDPRT");

        Component2 cBase = AddComp(sw, asm, md, baseP, 0, 0, 0);
        try { md.Extension.SelectByID2(cBase.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0); asm.FixComponent(); } catch { }

        double zM1 = BaseT;
        AddComp(sw, asm, md, motor, 0, 0, zM1);

        double tip1 = zM1 + PlateT + BossH + ShaftL1;
        double zP1 = tip1 - TipPast - PulleyLen;
        AddComp(sw, asm, md, pulley, 0, 0, zP1);

        double zHub = zP1 + PulleyLen;
        AddComp(sw, asm, md, hubP, 0, 0, zHub);

        double zYoke = zHub + HubH;
        AddComp(sw, asm, md, yokeBase, 0, 0, zYoke);

        // Arms: part extruded ArmT along Z, sketch ArmW×ArmH in XY with bottom at Y=0.
        // Place at ends of yoke: rotate so ArmT is along X (thickness = wall).
        // Part local Z = thickness → world ±X; local Y (height) → world Z; local X (width) → world Y
        // Left arm outside: x = -(YokeBaseL/2 - ArmInset - ArmT) ... 
        // After rot localZ→worldX: origin at inner face. Put origin at x_left.
        double xArmL = -(YokeBaseL / 2.0 - ArmInset - ArmT);
        double xArmR = +(YokeBaseL / 2.0 - ArmInset - ArmT);
        // R: localX→Y (0,1,0), localY→Z (0,0,1), localZ→X (1,0,0) for left (thickness +X into assembly)
        // For left arm on -X side, want thickness growing toward -X (outward) or +X (inward)?
        // Arm sits at end of yoke base; thickness along X. Place left arm centerline at xArmL.
        // Use localZ→-X for left so body goes outward: n7=-1,n8=0,n9=0; localX→Y; localY→Z
        Component2 cAL = AddComp(sw, asm, md, armL, xArmL, 0, zYoke);
        SetTransform(cAL, new double[] { 0, 1, 0, 0, 0, 1, -1, 0, 0 }, xArmL, 0, zYoke);

        Component2 cAR = AddComp(sw, asm, md, armR, xArmR, 0, zYoke);
        // Right: localZ→+X (outward)
        SetTransform(cAR, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xArmR, 0, zYoke);

        double zTilt = zYoke + TiltAxisZ;

        // Tilt shaft along X
        Component2 cSh = AddComp(sw, asm, md, shaftP, -TiltShaftLen / 2.0, 0, zTilt);
        SetTransform(cSh, new double[] { 0, 1, 0, 0, 0, 1, 1, 0, 0 }, -TiltShaftLen / 2.0, 0, zTilt);

        // Platform Ø200 horizontal
        AddComp(sw, asm, md, platP, 0, 0, zTilt - PlatformT / 2.0);

        // Clamps on platform
        AddComp(sw, asm, md, clampP, 0.040, 0, zTilt + PlatformT / 2.0);
        AddComp(sw, asm, md, clampP, -0.040, 0, zTilt + PlatformT / 2.0);

        // Motor2 on left arm — shaft along +X (inward)
        double xM2 = xArmL - 0.002;
        Component2 cM2 = AddComp(sw, asm, md, motor, xM2, 0, zTilt);
        SetTransform(cM2, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xM2, 0, zTilt);

        double tip2x = xM2 + PlateT + BossH + ShaftL1;
        double xP2 = tip2x - TipPast - PulleyLen;
        Component2 cP2 = AddComp(sw, asm, md, pulley, xP2, 0, zTilt);
        SetTransform(cP2, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xP2, 0, zTilt);

        Log(string.Format("M1 tipZ={0:F1} yokeZ={1:F1} tiltZ={2:F1} platform=Ø{3:F0} armsX={4:F1}/{5:F1}",
            tip1 * 1000, zYoke * 1000, zTilt * 1000, PlatformOd * 1000, xArmL * 1000, xArmR * 1000));

        md.EditRebuild3();
        md.ShowNamedView2("*Isometric", -1);
        md.ViewZoomtofit2();
        md.SaveBMP(Path.Combine(OutDir, "_preview_revopoint.bmp"), 1400, 1000);

        string tmp = Path.Combine(OutDir, "Turntable_Revopoint2Motor_new.SLDASM");
        SaveDoc(md, tmp);
        sw.CloseDoc(md.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Turntable_Revopoint2Motor.SLDASM"));
        Log("Assembly OK");
    }

    static void WritePrintNotes()
    {
        string path = Path.Combine(OutDir, "PRINT_Ender3_notes.txt");
        File.WriteAllText(path,
@"Печать под Ender 3 (стол 220×220 мм, запас → макс. деталь ~210 мм)
================================================================
Платформа: Platform_Tilt.SLDPRT — круг Ø200 × 6 мм (одна деталь, плашмя).

Список печатных деталей (габарит XY × Z) vs лимит 210 мм:
  Base_Revopoint.SLDPRT     Ø200 × 8          OK (200≤210)
  Hub_Azimuth.SLDPRT        Ø60 × 16          OK
  Yoke_Base.SLDPRT          210 × 55 × 8      OK (ровно на пределе)
  Yoke_Arm_L.SLDPRT         50 × 100 × 10     OK (печатать плашмя на 50×100)
  Yoke_Arm_R.SLDPRT         50 × 100 × 10     OK
  Platform_Tilt.SLDPRT      Ø200 × 6          OK
  Clamp_TiltShaft.SLDPRT    24 × 16 × 10      OK ×2
  Shaft_Tilt.SLDPRT         Ø8 × 220          длина 220 — лучше металлический прут Ø8;
                                              пластик: вертикально (высота Ender ~250)

Непечатные / покупные:
  Motor_PM42L_048_EPAO ×2
  Pulley_GT2_12T ×2
  винты M3, опционально подшипники 8 мм в отверстия наклона

Сборка (кинематика как Revopoint):
  Motor1 (азимут, Z) на Base → шкив GT2 → Hub → Yoke_Base + Arm_L/R
  Motor2 (наклон, X) на Arm_L → шкив GT2 → вал наклона → Platform Ø200

FDM: PLA/PETG, стенки ≥2 мм (у нас 6–10 мм), без лишних свесов;
  арки: плашмя; вилы: две стойки + основание на винтах M3.
");
        Log("Wrote " + path);
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== revopoint Ender3 " + DateTime.Now.ToString("o") + " ===\r\n");
        if (PlatformOd > MaxPrint + 1e-9) throw new Exception("Platform exceeds bed");
        if (BaseOd > MaxPrint + 1e-9) throw new Exception("Base exceeds bed");
        if (YokeBaseL > MaxPrint + 1e-9) throw new Exception("Yoke_Base exceeds bed");

        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        try { sw.CloseAllDocuments(true); } catch { }
        System.Threading.Thread.Sleep(700);

        BuildBase(sw);
        BuildHub(sw);
        BuildYokeBase(sw);
        BuildArm(sw, true);
        BuildArm(sw, false);
        BuildPlatform(sw);
        BuildTiltShaft(sw);
        BuildShaftClamp(sw);
        BuildAssembly(sw);
        WritePrintNotes();
        Log("DONE");
    }
}
