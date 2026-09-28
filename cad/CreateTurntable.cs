// SolidWorks CAD builder: Minebea PM35L-N48 + GT2 pulley
// Outline dims from product-page drawing (file_07_132.gif / Minebea std):
//   body Ø35, L=22.2 MAX, boss Ø10×1.5, flange 0.8, mount P=42±0.2, holes 2-Ø3.2
//   shaft Ød1 (2/3 mm option) — using Ø3 for GT2 press-fit; l1=10 mm from boss
// Note: user URL file_07_40.gif is a PM42 outline (Ø42 / P=49.5) — not used for PM35.
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
    static string LogPath = Path.Combine(OutDir, "sw_create.log");

    // PM35L-N48 mechanical (Minebea outline / std dims)
    const double BodyOd = 0.035;
    const double BodyLen = 0.0222;
    const double BossOd = 0.010;
    const double BossH = 0.0015;
    const double ShaftDia = 0.003;      // d1 Ø3 (2/3 option; Ø3 for pulley)
    const double ShaftL1 = 0.010;       // from boss face; tip stickout past pulley = 2 mm
    const double MountPitch = 0.042;    // P = 42 mm
    const double MountHole = 0.0035;    // clearance for Ø3.2 / M3

    // GT2 timing pulley ≈ Ø8 mm, axial 6 mm, tip protrudes 2 mm
    // 14T GT2 → PD≈8.91 (too big for Ø8). Use 12T: PD=12*2/π≈7.64, OD≈8.0
    const int PulleyTeeth = 12;
    const double Gt2Pitch = 0.002;
    const double PulleyOd = 0.008;
    const double PulleyLen = 0.006;
    const double TipPastPulley = 0.002;

    static void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + m;
        File.AppendAllText(LogPath, line + System.Environment.NewLine);
        Console.WriteLine(line);
    }

    static string FirstPlane(ModelDoc2 model)
    {
        var planes = new System.Collections.Generic.List<string>();
        Feature feat = (Feature)model.FirstFeature();
        while (feat != null)
        {
            if (feat.GetTypeName2() == "RefPlane")
                planes.Add(feat.Name);
            feat = (Feature)feat.GetNextFeature();
        }
        Log("Planes: " + string.Join(", ", planes));
        if (planes.Count == 0) throw new Exception("No RefPlane");
        foreach (string name in planes)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("top") || n.Contains("верх") || n == "сверху")
                return name;
        }
        return planes.Count > 1 ? planes[1] : planes[0];
    }

    static void SaveDoc(ModelDoc2 model, string path)
    {
        int errs = 0, warns = 0;
        bool ok = model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errs, ref warns);
        Log("SaveAs " + path + " ok=" + ok + " e=" + errs + " w=" + warns);
        if (!ok) throw new Exception("SaveAs failed: " + path);
    }

    static Feature Extrude(ModelDoc2 model, double depth)
    {
        Feature f = (Feature)model.FeatureManager.FeatureExtrusion2(
            true, false, false,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.0,
            false, false, false, false,
            0.0, 0.0,
            false, false, false, false,
            true, true, true,
            (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null)
        {
            f = (Feature)model.FeatureManager.FeatureExtrusion2(
                true, true, false,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0,
                false, false, false, false,
                0.0, 0.0,
                false, false, false, false,
                true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        }
        if (f == null) throw new Exception("Extrude failed d=" + depth);
        Log("Extrude OK d=" + depth);
        return f;
    }

    static void MakeHole(ModelDoc2 model, double x, double y, double zFace, double dia)
    {
        model.ClearSelection2(true);
        bool ok = model.Extension.SelectByID2("", "FACE", x, y, zFace, false, 0, null, 0);
        if (!ok) ok = model.Extension.SelectByRay(x, y, zFace + 0.02, 0, 0, -1, 0.0005, 2, false, 0, 0);
        if (!ok) ok = model.Extension.SelectByID2("", "FACE", x, y, 0, false, 0, null, 0);
        Log("Hole face sel @" + x + "," + y + "," + zFace + " = " + ok);
        if (!ok) throw new Exception("Hole face not found");

        Feature f = (Feature)model.FeatureManager.SimpleHole2(
            dia, true, false, false,
            (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
            0.01, 0.01, false, false, false, false,
            0.0174532925199433, 0.0174532925199433,
            false, false, false, false, true, true, false, false, false);
        if (f == null)
        {
            f = (Feature)model.FeatureManager.SimpleHole2(
                dia, true, true, false,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                0.01, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, true, true, false, false, false);
        }
        if (f == null) throw new Exception("SimpleHole2 failed dia=" + dia);
        Log("Hole OK dia=" + dia);
    }

    static void CloseAll(SldWorks sw)
    {
        try
        {
            while (sw.GetDocumentCount() > 0)
            {
                ModelDoc2 open = (ModelDoc2)sw.GetFirstDocument();
                if (open == null) break;
                string t = open.GetTitle();
                sw.CloseDoc(t);
                Log("Closed: " + t);
            }
        }
        catch (Exception ex) { Log("CloseAll: " + ex.Message); }
    }

    static ModelDoc2 NewPart(SldWorks sw)
    {
        object doc = sw.NewDocument(PartTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument part null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static void BuildPartMotor(SldWorks sw)
    {
        Log("Motor PM35L-N48...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);

        // Body Ø35 × 22.2
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(model, BodyLen);

        // Centering boss Ø10: extrude BodyLen+BossH from same plane (protrudes 1.5 mm)
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, BossOd / 2.0);
        Extrude(model, BodyLen + BossH);

        // Shaft Ø3 through body+boss+l1
        double shaftLen = BodyLen + BossH + ShaftL1;
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(model, shaftLen);

        SaveDoc(model, Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log(string.Format("Motor OK Ø{0} L={1} bossØ{2}x{3} shaftØ{4} l1={5}mm",
            BodyOd * 1000, BodyLen * 1000, BossOd * 1000, BossH * 1000, ShaftDia * 1000, ShaftL1 * 1000));
    }

    static void BuildPartPulley(SldWorks sw)
    {
        double pd = PulleyTeeth * Gt2Pitch / Math.PI;
        Log(string.Format("GT2 pulley: z={0} pitch=2mm PD={1:F3}mm OD={2}mm L={3}mm boreØ{4}mm tipPast={5}mm",
            PulleyTeeth, pd * 1000, PulleyOd * 1000, PulleyLen * 1000, ShaftDia * 1000, TipPastPulley * 1000));

        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);

        // Annulus: outer Ø8 + bore Ø3 in one sketch, extrude 6 mm
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, PulleyOd / 2.0);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(model, PulleyLen);

        SaveDoc(model, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log("Pulley OK");
    }

    static void BuildPartBase(SldWorks sw)
    {
        Log("Base PM35 mount P=42...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCornerRectangle(-0.07, -0.07, 0, 0.07, 0.07, 0);
        Extrude(model, 0.006);

        // Center clearance for boss+shaft (~Ø11), mount holes on 42 mm
        MakeHole(model, 0, 0, 0.006, 0.011);
        double half = MountPitch / 2.0;
        MakeHole(model, 0, half, 0.006, MountHole);
        MakeHole(model, 0, -half, 0.006, MountHole);

        SaveDoc(model, Path.Combine(OutDir, "Base.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log("Base OK");
    }

    static void BuildPartHub(SldWorks sw)
    {
        Log("Hub...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, 0.010);
        Extrude(model, 0.006);
        MakeHole(model, 0, 0, 0.006, ShaftDia + 0.0001);
        SaveDoc(model, Path.Combine(OutDir, "Hub.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log("Hub OK");
    }

    static void BuildPartPlatform(SldWorks sw)
    {
        Log("Platform...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, 0.065);
        Extrude(model, 0.005);
        MakeHole(model, 0, 0, 0.005, 0.008);
        SaveDoc(model, Path.Combine(OutDir, "Platform.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log("Platform OK");
    }

    static void TryMate(AssemblyDoc assy, int mateType, int align,
        double x1, double y1, double z1, double x2, double y2, double z2)
    {
        ModelDoc2 md = (ModelDoc2)assy;
        md.ClearSelection2(true);
        bool a = md.Extension.SelectByID2("", "FACE", x1, y1, z1, false, 1, null, 0);
        bool b = md.Extension.SelectByID2("", "FACE", x2, y2, z2, true, 1, null, 0);
        Log("mate " + mateType + " sel=" + a + "/" + b);
        if (!a || !b) return;
        int err = 0;
        try
        {
            object m = assy.AddMate5(mateType, align, false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out err);
            Log("mate5=" + (m != null) + " err=" + err);
        }
        catch (Exception ex)
        {
            try
            {
                object m = assy.AddMate3(mateType, align, false, 0, 0, 0, 0, 0, 0, 0, 0, false, out err);
                Log("mate3=" + (m != null) + " err=" + err);
            }
            catch (Exception ex2) { Log("mate fail: " + ex.Message + " / " + ex2.Message); }
        }
        md.ClearSelection2(true);
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        ModelDoc2 part = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
        Log("OpenDoc6 " + Path.GetFileName(path) + " err=" + err + " warn=" + warn + " null=" + (part == null));

        ModelDoc2 asmModel = (ModelDoc2)assy;
        sw.ActivateDoc3(asmModel.GetTitle(), false, 0, ref err);

        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); }
        catch (Exception ex) { Log("AddComponent5: " + ex.Message); }
        if (c == null)
        {
            try { c = assy.AddComponent4(path, "", x, y, z); }
            catch (Exception ex) { Log("AddComponent4: " + ex.Message); }
        }
        if (c == null)
        {
            try
            {
                bool added = assy.AddComponent(path, x, y, z);
                Log("AddComponent bool=" + added);
                if (added)
                {
                    object[] comps = (object[])assy.GetComponents(false);
                    if (comps != null && comps.Length > 0)
                        c = (Component2)comps[comps.Length - 1];
                }
            }
            catch (Exception ex) { Log("AddComponent: " + ex.Message); }
        }
        if (c == null) throw new Exception("AddComponent failed " + path);
        Log("Added " + c.Name2);
        return c;
    }

    static void BuildAssembly(SldWorks sw)
    {
        Log("Assembly...");
        object doc = sw.NewDocument(AsmTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument assembly null");
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;
        AssemblyDoc assy = (AssemblyDoc)md;

        // Mount face at z=0: body below, boss 0..1.5, shaft tip at BossH+ShaftL1
        double shaftTip = BossH + ShaftL1;                 // 11.5 mm
        double pulleyZ = shaftTip - TipPastPulley - PulleyLen; // 11.5-2-6 = 3.5 mm

        Log(string.Format("Placement: shaftTip={0}mm pulleyZ={1}..{2}mm (tip past={3}mm)",
            shaftTip * 1000, pulleyZ * 1000, (pulleyZ + PulleyLen) * 1000, TipPastPulley * 1000));

        Component2 cBase = AddComp(sw, assy, Path.Combine(OutDir, "Base.SLDPRT"), 0, 0, 0);
        AddComp(sw, assy, Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT"), 0, 0, -BodyLen);
        AddComp(sw, assy, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"), 0, 0, pulleyZ);
        AddComp(sw, assy, Path.Combine(OutDir, "Hub.SLDPRT"), 0, 0, shaftTip + 0.001);
        AddComp(sw, assy, Path.Combine(OutDir, "Platform.SLDPRT"), 0, 0, shaftTip + 0.008);

        try
        {
            md.Extension.SelectByID2(cBase.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            assy.FixComponent();
            Log("Base fixed");
        }
        catch (Exception ex) { Log("Fix: " + ex.Message); }

        // Best-effort concentric/coincident mates
        TryMate(assy, 1, 0, ShaftDia / 2, 0, pulleyZ + PulleyLen / 2, PulleyOd / 2, 0, pulleyZ + PulleyLen / 2);
        TryMate(assy, 0, 1, 0, 0, pulleyZ, 0, 0, pulleyZ);

        md.EditRebuild3();
        SaveDoc(md, Path.Combine(OutDir, "Turntable.SLDASM"));
        Log("Assembly OK");
    }

    static SldWorks GetSw()
    {
        SldWorks sw = null;
        try
        {
            sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            Log("Attached to running SolidWorks");
        }
        catch
        {
            Log("Starting SolidWorks via COM...");
            Type t = Type.GetTypeFromProgID("SldWorks.Application");
            sw = (SldWorks)Activator.CreateInstance(t);
            System.Threading.Thread.Sleep(15000);
        }
        sw.Visible = true;
        Log("RevisionNumber=" + sw.RevisionNumber());
        return sw;
    }

    static int Main()
    {
        try
        {
            Directory.CreateDirectory(OutDir);
            File.AppendAllText(LogPath, System.Environment.NewLine + "=== csharp PM35+GT2 " +
                DateTime.Now.ToString("o") + " ===" + System.Environment.NewLine);
            SldWorks sw = GetSw();
            CloseAll(sw);
            // Skip motor if already rebuilt this session — always rebuild all for consistency
            BuildPartMotor(sw);
            BuildPartPulley(sw);
            BuildPartBase(sw);
            BuildPartHub(sw);
            BuildPartPlatform(sw);
            BuildAssembly(sw);
            CloseAll(sw);
            Log("ALL DONE");
            return 0;
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            return 1;
        }
    }
}
