// SolidWorks early-bound CAD builder for scan-turntable
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
        // Prefer Top / Ð¡Ð²ÐµÑ€Ñ…Ñƒ for Z-up turntable
        foreach (string name in planes)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("top") || n.Contains("Ð²ÐµÑ€Ñ…") || n == "ÑÐ²ÐµÑ€Ñ…Ñƒ")
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

    static Feature CutThru(ModelDoc2 model, double depth)
    {
        // Keep sketch open (like Extrude). Use FeatureCut4 (SW2026).
        Feature f = null;
        try
        {
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, false, false,
                (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.01,
                false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false,
                false,
                true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false,
                true);
        }
        catch (Exception ex) { Log("FeatureCut4 throw: " + ex.Message); }

        if (f == null)
        {
            try
            {
                f = (Feature)model.FeatureManager.FeatureCut4(
                    true, true, false,
                    (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                    depth, 0.01,
                    false, false, false, false,
                    0.0174532925199433, 0.0174532925199433,
                    false, false, false, false,
                    false,
                    true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false,
                    true);
            }
            catch (Exception ex) { Log("FeatureCut4b throw: " + ex.Message); }
        }

        if (f == null) throw new Exception("Cut failed");
        Log("Cut OK");
        return f;
    }

    static ModelDoc2 NewPart(SldWorks sw)
    {
        // Close leftover untitled docs from prior failed runs
        try
        {
            while (sw.GetDocumentCount() > 0)
            {
                ModelDoc2 open = (ModelDoc2)sw.GetFirstDocument();
                if (open == null) break;
                string t = open.GetTitle();
                sw.CloseDoc(t);
                Log("Closed leftover: " + t);
            }
        }
        catch { }

        object doc = sw.NewDocument(PartTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument part null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static void BuildPartMotor(SldWorks sw)
    {
        Log("Motor...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);
        Log("Using plane: " + top);
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, 0.021);
        Extrude(model, 0.0222);

        // Shaft Ø3 from same plane, extruded body+stickout so it protrudes 17mm
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCircleByRadius(0, 0, 0, 0.0015);
        Extrude(model, 0.0392);

        SaveDoc(model, Path.Combine(OutDir, "Motor_PM42L.SLDPRT"));
        sw.CloseDoc(model.GetTitle());
        Log("Motor OK");
    }

    static void MakeHole(ModelDoc2 model, double x, double y, double zFace, double dia)
    {
        model.ClearSelection2(true);
        bool ok = model.Extension.SelectByID2("", "FACE", x, y, zFace, false, 0, null, 0);
        if (!ok) ok = model.Extension.SelectByID2("", "FACE", x, y, 0, false, 0, null, 0);
        if (!ok) ok = model.Extension.SelectByID2("", "FACE", x, y, -zFace, false, 0, null, 0);
        Log("Hole face sel @" + x + "," + y + "," + zFace + " = " + ok);
        if (!ok) throw new Exception("Hole face not found");

        Feature f = (Feature)model.FeatureManager.SimpleHole2(
            dia,
            true, false, false,
            (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
            0.01, 0.01,
            false, false, false, false,
            0.0174532925199433, 0.0174532925199433,
            false, false,
            false, false,
            true, true,
            false, false, false);
        if (f == null)
        {
            f = (Feature)model.FeatureManager.SimpleHole2(
                dia,
                true, true, false,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                0.01, 0.01,
                false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false,
                false, false,
                true, true,
                false, false, false);
        }
        if (f == null) throw new Exception("SimpleHole2 failed dia=" + dia);
        Log("Hole OK dia=" + dia);
    }

    static void BuildPartBase(SldWorks sw)
    {
        Log("Base...");
        ModelDoc2 model = NewPart(sw);
        string top = FirstPlane(model);
        model.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);
        model.SketchManager.CreateCornerRectangle(-0.07, -0.07, 0, 0.07, 0.07, 0);
        Extrude(model, 0.006);

        // Center shaft clearance Ø6, mount holes Ø3.5 on 49.5mm pattern
        MakeHole(model, 0, 0, 0.006, 0.006);
        MakeHole(model, 0, 0.02475, 0.006, 0.0035);
        MakeHole(model, 0, -0.02475, 0.006, 0.0035);

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
        Extrude(model, 0.012);

        MakeHole(model, 0, 0, 0.012, 0.0031);

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

    static void TryMate(AssemblyDoc assy, int mateType, int align, double x1, double y1, double z1, double x2, double y2, double z2)
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
            // LockRotation=false so platform can spin
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
            catch (Exception ex2)
            {
                Log("mate fail: " + ex.Message + " / " + ex2.Message);
            }
        }
        md.ClearSelection2(true);
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        // Prefetch part into SW session (required by some versions before AddComponent)
        ModelDoc2 part = (ModelDoc2)sw.OpenDoc6(
            path,
            (int)swDocumentTypes_e.swDocPART,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "",
            ref err, ref warn);
        Log("OpenDoc6 " + Path.GetFileName(path) + " err=" + err + " warn=" + warn + " null=" + (part == null));

        // Activate assembly again
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
                // Resolve by iterating components
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
        Log("Assembly title=" + md.GetTitle());

        Component2 cBase = AddComp(sw, assy, Path.Combine(OutDir, "Base.SLDPRT"), 0, 0, 0);
        AddComp(sw, assy, Path.Combine(OutDir, "Motor_PM42L.SLDPRT"), 0, 0, -0.0222);
        AddComp(sw, assy, Path.Combine(OutDir, "Hub.SLDPRT"), 0, 0, 0.006);
        AddComp(sw, assy, Path.Combine(OutDir, "Platform.SLDPRT"), 0, 0, 0.018);

        try
        {
            md.Extension.SelectByID2(cBase.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            assy.FixComponent();
            Log("Base fixed");
        }
        catch (Exception ex) { Log("Fix: " + ex.Message); }

        // Concentric=1, Coincident=0. Do not lock rotation.
        TryMate(assy, 1, 0, 0.0015, 0, 0.01, 0.003, 0, 0.003);
        TryMate(assy, 0, 1, 0, 0, 0, 0, 0, -0.00001);
        TryMate(assy, 1, 0, 0.00155, 0, 0.012, 0.0015, 0, 0.015);
        TryMate(assy, 0, 1, 0, 0, 0.006, 0, 0, 0.006);
        TryMate(assy, 1, 0, 0.004, 0, 0.020, 0.01, 0, 0.012);
        TryMate(assy, 0, 1, 0, 0, 0.018, 0, 0, 0.018);

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
            File.AppendAllText(LogPath, System.Environment.NewLine + "=== csharp " + DateTime.Now.ToString("o") + " ===" + System.Environment.NewLine);
            SldWorks sw = GetSw();
            BuildPartMotor(sw);
            BuildPartBase(sw);
            BuildPartHub(sw);
            BuildPartPlatform(sw);
            BuildAssembly(sw);
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

