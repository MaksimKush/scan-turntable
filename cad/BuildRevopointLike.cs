// Revopoint-like dual-axis turntable (A230 proportions: Ã˜200 Ã— ~82 mm, enclosed U-cradle).
// USER-OWNED: Motor_PM42L_048_EPAO.SLDPRT â€” NEVER rebuild / overwrite.
// Azimuth: spur pinion on motor shaft Ã— rim gear on Ã˜200 platform periphery.
// Tilt: 2nd motor on left arm (same user motor part).
// Ender 3: single-piece footprints â‰¤ 210 mm.
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
    static string LogPath = Path.Combine(OutDir, "sw_revopoint_like.log");

    const double MaxPrint = 0.210;

    // --- Spur gear set (module 2): tip Ã˜200 exactly, fewer teeth (FDM + SW-friendly) ---
    // tip da = m*(z+2) â†’ 2*(98+2)=200 mm
    const double Module = 0.002;        // 2.0 mm
    const int ZRim = 98;
    const int ZPinion = 12;
    // d = m*z; da = m*(z+2); df = m*(z-2.5)
    static double DRim { get { return Module * ZRim; } }             // 196
    static double DaRim { get { return Module * (ZRim + 2); } }       // 200
    static double DfRim { get { return Module * (ZRim - 2.5); } }     // 191
    static double DPin { get { return Module * ZPinion; } }           // 24
    static double DaPin { get { return Module * (ZPinion + 2); } }    // 28
    static double DfPin { get { return Module * (ZPinion - 2.5); } }  // 19
    static double CenterDist { get { return (DRim + DPin) / 2.0; } }  // 110

    const double FaceW = 0.008;          // gear face width 8 mm
    const double ShaftBore = 0.003;      // Ã˜3 motor shaft
    const double TipPast = 0.002;        // shaft tip +2 mm past pinion
    const double DeckT = 0.006;          // flat deck above teeth

    // Revopoint-like envelope â‰ˆ Ã˜200 Ã— 82
    const double BaseOd = 0.200;
    const double BaseH = 0.036;
    const double Wall = 0.003;
    const double CoverH = 0.004;

    // Short U-cradle (compact consumer look)
    const double ArmT = 0.012;
    const double ArmW = 0.028;
    const double ArmH = 0.048;           // short arms â†’ total H ~82
    const double YokeSpan = 0.168;       // inside span between arms
    const double YokeBaseT = 0.008;
    const double YokeBaseW = 0.055;
    const double TiltAxisZ = 0.028;      // from yoke base top
    const double TiltHole = 0.0085;
    const double TiltShaftOd = 0.008;
    const double TiltShaftLen = 0.190;
    const double PlatformT = FaceW + DeckT; // teeth + deck

    // PM42 mount (for covers / arms â€” do not rebuild motor)
    const double MountP = 0.0495;
    const double MountHole = 0.0035;
    const double ScrewHole = 0.0032;

    // Measured from user motor (filled by InspectUserMotor)
    static double MotorTipZ = 0.0193; // fallback EPAO stack; overwritten by inspect

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
            if (n.Contains("front") || n.Contains("ÑÐ¿ÐµÑ€ÐµÐ´Ð¸")) return name;
        }
        return planes[0];
    }

    static void SaveDoc(ModelDoc2 model, string path)
    {
        int e = 0, w = 0;
        bool ok = model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e, ref w);
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok + " e=" + e);
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

    static Feature MakeZAxis(ModelDoc2 model)
    {
        model.ClearSelection2(true);
        model.Extension.SelectByID2("Top Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        model.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        model.InsertAxis2(true);
        Feature ax = null, f = (Feature)model.FirstFeature();
        while (f != null)
        {
            if (f.GetTypeName2() == "RefAxis") ax = f;
            f = (Feature)f.GetNextFeature();
        }
        return ax;
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
        Log(string.Format("PRINT {0}: {1:F0}Ã—{2:F0}Ã—{3:F0} mm  bedOK={4}",
            name, dx * 1000, dy * 1000, dz * 1000, ok));
    }

    // Radial rectangular tooth-space cut, then CirPattern (FDM-friendly approx spur).
    static void CutToothSpaces(ModelDoc2 m, string front, int z, double da, double df, double face, Feature axis)
    {
        double rp = (Module * z) / 2.0;
        double rt = da / 2.0 + 0.0003;
        double rr = Math.Max(df / 2.0 - 0.0003, ShaftBore); // stay outside bore
        double halfW = rp * Math.PI / z * 0.55; // space half-width at pitch

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(rr, -halfW, 0, rt, halfW, 0);
        Feature gap = null;
        try { gap = CutBlind(m, face + 0.001); }
        catch
        {
            // retry: re-open sketch direction by selecting plane again
            m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
            m.SketchManager.InsertSketch(true);
            m.SketchManager.CreateCornerRectangle(rr, -halfW, 0, rt, halfW, 0);
            gap = CutBlind(m, face + 0.001);
        }
        Log(string.Format("  tooth-space rect z={0} rp={1:F2} rr={2:F2}..rt={3:F2} halfW={4:F2}",
            z, rp * 1000, rr * 1000, rt * 1000, halfW * 1000));

        m.ClearSelection2(true);
        m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
        if (axis != null) m.Extension.SelectByID2(axis.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
        Feature pat = null;
        try
        {
            pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
                z, 2.0 * Math.PI / z, false, "NULL", false, false, false);
        }
        catch { }
        if (pat == null)
        {
            m.ClearSelection2(true);
            m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
            if (axis != null) m.Extension.SelectByID2(axis.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
            try
            {
                // Equal spacing over full turn
                pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
                    z, 2.0 * Math.PI, true, "NULL", false, false, true);
            }
            catch { }
        }
        if (pat == null)
        {
            Log("  CirPattern API failed — falling back to per-tooth cuts");
            for (int i = 1; i < z; i++)
            {
                double ang = i * 2.0 * Math.PI / z;
                double c = Math.Cos(ang), s = Math.Sin(ang);
                // rotate rectangle corners (rr,-halfW)-(rt,halfW)
                double[] xs = { rr, rt, rt, rr };
                double[] ys = { -halfW, -halfW, halfW, halfW };
                m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
                m.SketchManager.InsertSketch(true);
                for (int k = 0; k < 4; k++)
                {
                    double x0 = xs[k] * c - ys[k] * s;
                    double y0 = xs[k] * s + ys[k] * c;
                    double x1 = xs[(k + 1) % 4] * c - ys[(k + 1) % 4] * s;
                    double y1 = xs[(k + 1) % 4] * s + ys[(k + 1) % 4] * c;
                    m.SketchManager.CreateLine(x0, y0, 0, x1, y1, 0);
                }
                try { CutBlind(m, face + 0.001); }
                catch { try { m.SketchManager.InsertSketch(true); } catch { } }
                if (i % 20 == 0) Log("  cut tooth space " + i + "/" + z);
            }
            Log("  fallback cuts done for z=" + z);
        }
        else Log("  CirPattern teeth=" + z + " ok=True");
    }

    // ---------- USER MOTOR: inspect only ----------
    static void InspectUserMotor(SldWorks sw)
    {
        string path = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        if (!File.Exists(path)) throw new Exception("User motor missing");
        int e = 0, w = 0;
        ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (m == null) throw new Exception("Open user motor fail e=" + e);
        Log("--- USER Motor_PM42L_048_EPAO (READ-ONLY) ---");
        Log("mtime=" + File.GetLastWriteTime(path).ToString("o"));
        PartDoc part = (PartDoc)m;
        object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);
        double[] box = null;
        if (bodies != null)
        {
            foreach (object ob in bodies)
            {
                double[] bb = (double[])((Body2)ob).GetBodyBox();
                Log(string.Format("  body X[{0:F2}..{1:F2}] Y[{2:F2}..{3:F2}] Z[{4:F2}..{5:F2}]",
                    bb[0] * 1000, bb[3] * 1000, bb[1] * 1000, bb[4] * 1000, bb[2] * 1000, bb[5] * 1000));
                if (box == null) box = (double[])bb.Clone();
                else
                {
                    for (int i = 0; i < 3; i++) if (bb[i] < box[i]) box[i] = bb[i];
                    for (int i = 3; i < 6; i++) if (bb[i] > box[i]) box[i] = bb[i];
                }
            }
        }
        if (box != null)
        {
            MotorTipZ = box[5]; // shaft tip = ZMax (Front-plane stack convention)
            Log(string.Format("  UNION Dx={0:F2} Dy={1:F2} Dz={2:F2} tipZ={3:F2} mm",
                (box[3] - box[0]) * 1000, (box[4] - box[1]) * 1000, (box[5] - box[2]) * 1000, MotorTipZ * 1000));
        }
        sw.CloseDoc(m.GetTitle()); // no save
        Log("Motor preserved â€” not overwritten");
    }

    // ---------- Pinion ----------
    static void BuildPinion(SldWorks sw)
    {
        Log(string.Format("=== Pinion_Spur_Z{0}_M{1} da={2:F1} bore=Ã˜3 ===",
            ZPinion, Module * 1000, DaPin * 1000));
        LogBox("Pinion", DaPin, DaPin, FaceW + 0.004);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Tip blank with bore — cut tooth spaces (same method that worked for GT2)
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, DaPin / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftBore / 2.0);
        Extrude(m, FaceW, false);

        Feature ax = MakeZAxis(m);
        CutToothSpaces(m, front, ZPinion, DaPin, DfPin, FaceW, ax);

        // Short hub extension opposite teeth (set screw seat visual) +4 mm
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.005);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftBore / 2.0);
        Extrude(m, 0.004, true);

        string tmp = Path.Combine(OutDir, "Pinion_Spur_Z12_M2_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Pinion_Spur_Z12_M2.SLDPRT"));
    }

    // ---------- Platform with rim gear + flat deck ----------
    static void BuildPlatformRim(SldWorks sw)
    {
        Log(string.Format("=== Platform_RimGear tipÃ˜{0:F0} z={1} m={2} ===",
            DaRim * 1000, ZRim, Module * 1000));
        LogBox("Platform_RimGear", DaRim, DaRim, PlatformT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Tip blank tip Ø200 — cut tooth spaces for rim gear
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, DaRim / 2.0);
        Extrude(m, FaceW, false);

        Feature ax = MakeZAxis(m);
        CutToothSpaces(m, front, ZRim, DaRim, DfRim, FaceW, ax);

        // Flat deck on top (under tip so teeth show on rim side)
        double deckR = DfRim / 2.0 - 0.0005;
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, deckR);
        Extrude(m, FaceW + DeckT, false);

        // Center bore for tilt shaft / hub
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.0045);
        try { CutBlind(m, PlatformT + 0.002); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Platform_RimGear_200_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Platform_RimGear_200.SLDPRT"));
    }

    // ---------- Enclosed cylindrical base (Revopoint drum) ----------
    static void BuildBaseHousing(SldWorks sw)
    {
        Log("=== Base_Housing Ã˜200Ã—36 enclosed ===");
        LogBox("Base_Housing", BaseOd, BaseOd, BaseH);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Outer cylinder
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BaseOd / 2.0);
        Extrude(m, BaseH, false);

        // Hollow interior (leave floor 3 mm)
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BaseOd / 2.0 - Wall);
        try { CutBlind(m, BaseH - 0.003); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        // Motor1 mount pad: holes at offset CenterDist on +X (pinion meshes rim)
        // Mount plane near floor â€” holes through floor for M3
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        double mx = CenterDist;
        m.SketchManager.CreateCircleByRadius(mx, MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(mx, -MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(mx, 0, 0, 0.007); // boss clearance
        // Cable exit slot
        m.SketchManager.CreateCornerRectangle(-BaseOd / 2.0 + 0.002, -0.006, 0, -BaseOd / 2.0 + 0.012, 0.006, 0);
        try { CutBlind(m, 0.004); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        // Central bearing / hub pocket
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.012);
        try { CutBlind(m, 0.004); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Base_Housing_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Base_Housing.SLDPRT"));
    }

    // Top cover ring â€” hides motor/pinion, platform sits above (smooth consumer lid)
    static void BuildBaseCover(SldWorks sw)
    {
        Log("=== Cover_Base_Top ===");
        LogBox("Cover_Base_Top", BaseOd, BaseOd, CoverH);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BaseOd / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.055); // opening for yoke / hub
        Extrude(m, CoverH, false);

        // Pinion clearance slot (elongated near mesh)
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        double mx = CenterDist;
        m.SketchManager.CreateCircleByRadius(mx, 0, 0, DaPin / 2.0 + 0.002);
        try { CutBlind(m, CoverH + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Cover_Base_Top_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Cover_Base_Top.SLDPRT"));
    }

    // Compact U-cradle crossbar
    static void BuildYokeBase(SldWorks sw)
    {
        Log("=== Yoke_Cradle_Base ===");
        double len = YokeSpan + 2 * ArmT;
        LogBox("Yoke_Cradle_Base", len, YokeBaseW, YokeBaseT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        double hx = len / 2.0, hy = YokeBaseW / 2.0;
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-hx, -hy, 0, hx, hy, 0);
        Extrude(m, YokeBaseT, false);

        // Rounded look: leave rect; screw holes to hub + arms
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.004);
        double ax = YokeSpan / 2.0 + ArmT / 2.0;
        m.SketchManager.CreateCircleByRadius(-ax, 0.012, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-ax, -0.012, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(ax, 0.012, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(ax, -0.012, 0, ScrewHole / 2.0);
        try { CutBlind(m, YokeBaseT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Yoke_Cradle_Base_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Yoke_Cradle_Base.SLDPRT"));
    }

    static void BuildArm(SldWorks sw, bool left)
    {
        string name = left ? "Yoke_Cradle_Arm_L" : "Yoke_Cradle_Arm_R";
        Log("=== " + name + " (short Revopoint-like) ===");
        LogBox(name, ArmW, ArmH, ArmT);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        // Soft rectangular arm with rounded top via trapezoid-ish: simple rect + fillet skipped (API)
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        // Tapered silhouette: wider at base
        m.SketchManager.CreateLine(-ArmW / 2.0, 0, 0, ArmW / 2.0, 0, 0);
        m.SketchManager.CreateLine(ArmW / 2.0, 0, 0, ArmW / 2.0 * 0.7, ArmH, 0);
        m.SketchManager.CreateLine(ArmW / 2.0 * 0.7, ArmH, 0, -ArmW / 2.0 * 0.7, ArmH, 0);
        m.SketchManager.CreateLine(-ArmW / 2.0 * 0.7, ArmH, 0, -ArmW / 2.0, 0, 0);
        Extrude(m, ArmT, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, TiltAxisZ, 0, TiltHole / 2.0);
        m.SketchManager.CreateCircleByRadius(0.010, 0.006, 0, ScrewHole / 2.0);
        m.SketchManager.CreateCircleByRadius(-0.010, 0.006, 0, ScrewHole / 2.0);
        if (left)
        {
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ + MountP / 2.0, 0, MountHole / 2.0);
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ - MountP / 2.0, 0, MountHole / 2.0);
            m.SketchManager.CreateCircleByRadius(0, TiltAxisZ, 0, 0.0065);
        }
        try { CutBlind(m, ArmT + 0.001); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, name + "_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, name + ".SLDPRT"));
    }

    // Slim arm cover (hides tilt motor on left)
    static void BuildArmCover(SldWorks sw)
    {
        Log("=== Cover_Arm_Motor ===");
        double cx = 0.055, cy = 0.058, cz = 0.022;
        LogBox("Cover_Arm_Motor", cx, cy, cz);
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-cx / 2.0, -cy / 2.0, 0, cx / 2.0, cy / 2.0, 0);
        Extrude(m, cz, false);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(-cx / 2.0 + 0.002, -cy / 2.0 + 0.002, 0, cx / 2.0 - 0.002, cy / 2.0 - 0.002, 0);
        try { CutBlind(m, cz - 0.002); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }
        string tmp = Path.Combine(OutDir, "Cover_Arm_Motor_new.SLDPRT");
        SaveDoc(m, tmp); sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Cover_Arm_Motor.SLDPRT"));
    }

    static void BuildTiltShaft(SldWorks sw)
    {
        Log("=== Shaft_Tilt ===");
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
        Log(string.Format("  + {0} @ ({1:F1},{2:F1},{3:F1})", Path.GetFileName(path), x * 1000, y * 1000, z * 1000));
        return c;
    }

    static void BuildMotorPinionAsm(SldWorks sw)
    {
        Log("--- Motor_PM42L_048_EPAO_Pinion.SLDASM ---");
        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pinion = Path.Combine(OutDir, "Pinion_Spur_Z12_M2.SLDPRT");
        double pz = MotorTipZ - TipPast - FaceW;

        sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;
        Component2 cm = AddComp(sw, asm, md, motor, 0, 0, 0);
        try { md.Extension.SelectByID2(cm.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0); asm.FixComponent(); } catch { }
        AddComp(sw, asm, md, pinion, 0, 0, pz);
        Log(string.Format("  tip={0:F2} pinion Z={1:F2}..{2:F2} past=+{3:F2}",
            MotorTipZ * 1000, pz * 1000, (pz + FaceW) * 1000, TipPast * 1000));
        md.EditRebuild3();
        string tmp = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pinion_new.SLDASM");
        SaveDoc(md, tmp); sw.CloseDoc(md.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pinion.SLDASM"));
    }

    static void BuildTurntable(SldWorks sw)
    {
        Log("--- Turntable_Revopoint2Motor.SLDASM (pinion+rim, Revopoint-like) ---");
        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pinion = Path.Combine(OutDir, "Pinion_Spur_Z12_M2.SLDPRT");
        string plat = Path.Combine(OutDir, "Platform_RimGear_200.SLDPRT");
        string baseH = Path.Combine(OutDir, "Base_Housing.SLDPRT");
        string cover = Path.Combine(OutDir, "Cover_Base_Top.SLDPRT");
        string yokeB = Path.Combine(OutDir, "Yoke_Cradle_Base.SLDPRT");
        string armL = Path.Combine(OutDir, "Yoke_Cradle_Arm_L.SLDPRT");
        string armR = Path.Combine(OutDir, "Yoke_Cradle_Arm_R.SLDPRT");
        string shaft = Path.Combine(OutDir, "Shaft_Tilt.SLDPRT");
        string armCov = Path.Combine(OutDir, "Cover_Arm_Motor.SLDPRT");

        foreach (string p in new[] { motor, pinion, plat, baseH, cover, yokeB, armL, armR, shaft, armCov })
            if (!File.Exists(p)) throw new Exception("Missing " + Path.GetFileName(p));

        sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;

        Component2 cBase = AddComp(sw, asm, md, baseH, 0, 0, 0);
        try { md.Extension.SelectByID2(cBase.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0); asm.FixComponent(); } catch { }

        // Motor1 (azimuth) in base â€” shaft +Z, offset +X to CenterDist
        double zFloor = 0.003;
        double xM1 = CenterDist;
        AddComp(sw, asm, md, motor, xM1, 0, zFloor);

        double tip1 = zFloor + MotorTipZ;
        double zPin = tip1 - TipPast - FaceW;
        AddComp(sw, asm, md, pinion, xM1, 0, zPin);

        AddComp(sw, asm, md, cover, 0, 0, BaseH);

        // Platform centered, teeth at z ~ cover; mesh with pinion at CenterDist
        double zPlat = Math.Max(zPin, BaseH + CoverH * 0.5);
        AddComp(sw, asm, md, plat, 0, 0, zPlat);

        // U-cradle above platform? Revopoint: cradle holds platform from sides.
        // Place yoke base under platform center (hub), arms up beside platform.
        // Compact: yoke sits on cover, arms embrace platform.
        double zYoke = BaseH + CoverH;
        AddComp(sw, asm, md, yokeB, 0, 0, zYoke);

        double xArmL = -(YokeSpan / 2.0 + ArmT);
        double xArmR = +(YokeSpan / 2.0);
        Component2 cAL = AddComp(sw, asm, md, armL, xArmL, 0, zYoke);
        // localZâ†’-X (thickness outward)
        SetTransform(cAL, new double[] { 0, 1, 0, 0, 0, 1, -1, 0, 0 }, xArmL, 0, zYoke);
        Component2 cAR = AddComp(sw, asm, md, armR, xArmR, 0, zYoke);
        SetTransform(cAR, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xArmR, 0, zYoke);

        double zTilt = zYoke + TiltAxisZ;
        Component2 cSh = AddComp(sw, asm, md, shaft, -TiltShaftLen / 2.0, 0, zTilt);
        SetTransform(cSh, new double[] { 0, 1, 0, 0, 0, 1, 1, 0, 0 }, -TiltShaftLen / 2.0, 0, zTilt);

        // Motor2 tilt on left arm
        double xM2 = xArmL - 0.002;
        Component2 cM2 = AddComp(sw, asm, md, motor, xM2, 0, zTilt);
        SetTransform(cM2, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xM2, 0, zTilt);

        Component2 cCov = AddComp(sw, asm, md, armCov, xM2 - 0.012, 0, zTilt);
        SetTransform(cCov, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xM2 - 0.012, 0, zTilt);

        double totalH = zPlat + PlatformT;
        Log(string.Format("  CD={0:F1} tip1={1:F1} zPlat={2:F1} zTilt={3:F1} totalHâ‰ˆ{4:F0} (Revopoint 82)",
            CenterDist * 1000, tip1 * 1000, zPlat * 1000, zTilt * 1000, totalH * 1000));
        Log(string.Format("  GEAR m={0} z_pinion={1} z_rim={2} da_rim={3:F0} da_pin={4:F0}",
            Module * 1000, ZPinion, ZRim, DaRim * 1000, DaPin * 1000));

        md.EditRebuild3();
        md.ShowNamedView2("*Isometric", -1);
        md.ViewZoomtofit2();
        try { md.SaveBMP(Path.Combine(OutDir, "_preview_revopoint.bmp"), 1400, 1000); } catch { }

        string tmp = Path.Combine(OutDir, "Turntable_Revopoint2Motor_new.SLDASM");
        SaveDoc(md, tmp); sw.CloseDoc(md.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Turntable_Revopoint2Motor.SLDASM"));
    }

    static void WriteNotes()
    {
        string gear = Path.Combine(OutDir, "GEAR_rim_pinion_notes.txt");
        File.WriteAllText(gear,
@"Azimuth drive â€” spur pinion Ã— rim gear (user: ÑˆÐµÑÑ‚ÐµÑ€Ð½Ñ + Ñ€ÐµÐ¹ÐºÐ° Ð¿Ð¾ Ð¾Ð±Ð¾Ð´Ñƒ)
======================================================================
Module m = 2.0 mm
Pinion:  Pinion_Spur_Z12_M2.SLDPRT   z=12  d=24  da=28  df=19  face=8  bore=Ã˜3
Rim:     Platform_RimGear_200.SLDPRT  z=98  d=196 da=200 df=191 face=8 + deck 6
Center distance CD = (d_rim+d_pinion)/2 = 110 mm
Shaft tip past pinion = +2 mm
Ratio = 130/12 â‰ˆ 10.83 : 1

Tip Ã˜198 â‰ˆ user Ã˜200 platform (Ender3 â‰¤210). Teeth visible on rim (Â«Ð·ÑƒÐ±Ñ‡Ð°Ñ‚Ñ‹Ð¹ Ð²ÐµÐ½ÐµÑ†Â»).

USER MOTOR: Motor_PM42L_048_EPAO.SLDPRT is user-owned â€” do NOT auto-rebuild.
");

        string print = Path.Combine(OutDir, "PRINT_Ender3_notes.txt");
        File.WriteAllText(print,
@"ÐŸÐµÑ‡Ð°Ñ‚ÑŒ Ð¿Ð¾Ð´ Ender 3 (ÑÑ‚Ð¾Ð» 220Ã—220 â†’ Ð¼Ð°ÐºÑ ~210) â€” Revopoint-like A230 (Ã˜200Ã—~82)
=============================================================================
Ð’Ð¸Ð·ÑƒÐ°Ð» (ÐºÐ°Ðº Revopoint Dual-axis):
  â€” Ñ†Ð¸Ð»Ð¸Ð½Ð´Ñ€Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð·Ð°ÐºÑ€Ñ‹Ñ‚Ñ‹Ð¹ ÐºÐ¾Ñ€Ð¿ÑƒÑ Ð±Ð°Ð·Ñ‹ Ã˜200
  â€” ÐºÐ¾Ñ€Ð¾Ñ‚ÐºÐ°Ñ U-Ð»ÑŽÐ»ÑŒÐºÐ° (cradle) Ñ Ð½Ð°ÐºÐ»Ð¾Ð½Ð½Ñ‹Ð¼Ð¸ ÑÑ‚Ð¾Ð¹ÐºÐ°Ð¼Ð¸
  â€” ÐºÑ€ÑƒÐ³Ð»Ð°Ñ Ð¿Ð»Ð°Ñ‚Ñ„Ð¾Ñ€Ð¼Ð° Ñ Ð·ÑƒÐ±Ñ‡Ð°Ñ‚Ñ‹Ð¼ Ð²ÐµÐ½Ñ†Ð¾Ð¼ Ð¿Ð¾ Ð¾Ð±Ð¾Ð´Ñƒ
  â€” ÐºÑ€Ñ‹ÑˆÐºÐ¸, Ð¿Ñ€ÑÑ‡ÑƒÑ‰Ð¸Ðµ Ð¼Ð¾Ñ‚Ð¾Ñ€Ñ‹/ÐºÐ°Ð±ÐµÐ»Ð¸

ÐŸÐµÑ‡Ð°Ñ‚Ð½Ñ‹Ðµ:
  Base_Housing.SLDPRT          Ã˜200 Ã— 36
  Cover_Base_Top.SLDPRT        Ã˜200 Ã— 4
  Platform_RimGear_200.SLDPRT  tipÃ˜198 Ã— 14 (Ð·ÑƒÐ±ÑŒÑ 8 + Ð¿Ð°Ð»ÑƒÐ±Ð° 6) â€” ÐžÐ”ÐÐ Ð´ÐµÑ‚Ð°Ð»ÑŒ
  Yoke_Cradle_Base.SLDPRT      ~192 Ã— 55 Ã— 8
  Yoke_Cradle_Arm_L/R.SLDPRT   28 Ã— 48 Ã— 12
  Cover_Arm_Motor.SLDPRT       55 Ã— 58 Ã— 22
  Pinion_Spur_Z12_M2.SLDPRT   Ã˜21 Ã— 12
  Shaft_Tilt.SLDPRT            Ã˜8 Ã— 190 (Ð»ÑƒÑ‡ÑˆÐµ Ð¼ÐµÑ‚Ð°Ð»Ð»)

ÐŸÐ¾ÐºÑƒÐ¿Ð½Ñ‹Ðµ / user:
  Motor_PM42L_048_EPAO Ã—2 (ÐÐ• Ð¿ÐµÑ€ÐµÑÐ¾Ð±Ð¸Ñ€Ð°Ñ‚ÑŒ ÑÐºÑ€Ð¸Ð¿Ñ‚Ð¾Ð¼)

Ð¡Ð±Ð¾Ñ€ÐºÐ°:
  Motor1 Ð² Ð±Ð°Ð·Ðµ â†’ ÑˆÐµÑÑ‚ÐµÑ€Ð½Ñ â†’ Ð²ÐµÐ½ÐµÑ† Ð¿Ð»Ð°Ñ‚Ñ„Ð¾Ñ€Ð¼Ñ‹ (Ð°Ð·Ð¸Ð¼ÑƒÑ‚)
  Motor2 Ð½Ð° Ð»ÐµÐ²Ð¾Ð¹ ÑÑ‚Ð¾Ð¹ÐºÐµ â†’ Ð½Ð°ÐºÐ»Ð¾Ð½ Ð»ÑŽÐ»ÑŒÐºÐ¸ (Â±30Â° Ñ†ÐµÐ»ÐµÐ²Ð¾Ð¹)
");
        Log("Wrote notes");
    }

    static void Main()
    {
        File.WriteAllText(LogPath, "=== revopoint-like " + DateTime.Now.ToString("o") + " ===\r\n");
        Log(string.Format("GEAR m={0} z_pin={1} z_rim={2} CD={3:F2} da_rim={4:F1}",
            Module * 1000, ZPinion, ZRim, CenterDist * 1000, DaRim * 1000));

        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        sw.CloseAllDocuments(true);
        System.Threading.Thread.Sleep(800);

        InspectUserMotor(sw);
        BuildPinion(sw);
        BuildPlatformRim(sw);
        BuildBaseHousing(sw);
        BuildBaseCover(sw);
        BuildYokeBase(sw);
        BuildArm(sw, true);
        BuildArm(sw, false);
        BuildArmCover(sw);
        BuildTiltShaft(sw);
        BuildMotorPinionAsm(sw);
        BuildTurntable(sw);
        WriteNotes();
        Log("DONE â€” user motor untouched");
    }
}

