// Close locked docs then rebuild motor + pulley on Front plane
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
    static string LogPath = Path.Combine(OutDir, "sw_rebuild.log");

    const double BodyOd = 0.035, PlateT = 0.0008, CanLen = 0.0214, RearBossH = 0.001;
    const double BossOd = 0.010, BossH = 0.0015, ShaftDia = 0.003, ShaftL1 = 0.010;
    const double MountP = 0.042, MountHole = 0.0032, EarR = 0.0035;
    const double WireW = 0.0127, WireRadial = 0.0055, WireThetaDeg = 45.0;
    const int Teeth = 12;
    const double Pitch = 0.002, PulleyOd = 0.008, PulleyLen = 0.006, TipPast = 0.002;
    const double FlangeOd = 0.0086, FlangeT = 0.0005;

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
        Log("Planes: " + string.Join(", ", planes));
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
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok + " e=" + e);
        if (!ok) throw new Exception("SaveAs failed");
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
        Log("Extrude " + (depth * 1000) + "mm rev=" + reverse);
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
        if (f == null) throw new Exception("CutBlind fail");
        Log("CutBlind " + (depth * 1000) + "mm");
        return f;
    }

    static Feature CutThru(ModelDoc2 model)
    {
        Feature f = (Feature)model.FeatureManager.FeatureCut4(
            true, false, false,
            (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
            0.01, 0.01, false, false, false, false,
            0.0174532925199433, 0.0174532925199433,
            false, false, false, false, false, true, true,
            false, false, false,
            (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null)
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, true, false,
                (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
                0.01, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null) throw new Exception("CutThru fail");
        Log("CutThru OK");
        return f;
    }

    static void ForceCloseAll(SldWorks sw)
    {
        try
        {
            bool ok = sw.CloseAllDocuments(true);
            Log("CloseAllDocuments = " + ok);
        }
        catch (Exception ex) { Log("CloseAllDocuments: " + ex.Message); }
        System.Threading.Thread.Sleep(1000);
    }

    static ModelDoc2 NewPart(SldWorks sw)
    {
        object doc = sw.NewDocument(PartTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static Feature MakeZAxis(ModelDoc2 model)
    {
        model.ClearSelection2(true);
        // Top ∩ Right = Z axis in standard SW
        model.Extension.SelectByID2("Top Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        model.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        model.InsertAxis2(true);
        Feature ax = null, f = (Feature)model.FirstFeature();
        while (f != null)
        {
            if (f.GetTypeName2() == "RefAxis") ax = f;
            f = (Feature)f.GetNextFeature();
        }
        Log("Axis = " + (ax != null ? ax.Name : "null"));
        return ax;
    }

    static void BuildMotor(SldWorks sw)
    {
        Log("=== Motor ===");
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, PlateT, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, MountHole / 2.0);
        try { CutThru(m); }
        catch
        {
            try { CutBlind(m, PlateT + 0.001); }
            catch (Exception ex) { Log("holes skip: " + ex.Message); m.SketchManager.InsertSketch(true); }
        }

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, CanLen, true);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.004);
        Extrude(m, CanLen + RearBossH, true);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BossOd / 2.0);
        Extrude(m, PlateT + BossH, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PlateT + BossH + ShaftL1, false);

        double th = WireThetaDeg * Math.PI / 180.0;
        double ux = Math.Cos(th), uy = Math.Sin(th);
        double vx = -Math.Sin(th), vy = Math.Cos(th);
        double r0 = BodyOd / 2.0 - 0.0005, r1 = BodyOd / 2.0 + WireRadial, hw = WireW / 2.0;
        double[] xs = { ux*r0+vx*(-hw), ux*r0+vx*hw, ux*r1+vx*hw, ux*r1+vx*(-hw) };
        double[] ys = { uy*r0+vy*(-hw), uy*r0+vy*hw, uy*r1+vy*hw, uy*r1+vy*(-hw) };
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        for (int i = 0; i < 4; i++)
            m.SketchManager.CreateLine(xs[i], ys[i], 0, xs[(i + 1) % 4], ys[(i + 1) % 4], 0);
        Extrude(m, 0.012, true);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        for (int i = 0; i < 4; i++)
            m.SketchManager.CreateLine(xs[i], ys[i], 0, xs[(i + 1) % 4], ys[(i + 1) % 4], 0);
        Extrude(m, PlateT + 0.002, false);

        // Save to new name first, then replace
        string tmp = Path.Combine(OutDir, "Motor_PM35L_N48_new.SLDPRT");
        SaveDoc(m, tmp);
        sw.CloseDoc(m.GetTitle());
        string final = Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT");
        if (File.Exists(final)) File.Delete(final);
        File.Move(tmp, final);
        Log("Motor replaced");
    }

    static void BuildPulley(SldWorks sw)
    {
        double pd = Teeth * Pitch / Math.PI;
        double tipR = PulleyOd / 2.0;
        double rootR = (pd - 0.0012) / 2.0;
        if (rootR < ShaftDia / 2.0 + 0.0008) rootR = ShaftDia / 2.0 + 0.0008;
        Log(string.Format("=== Pulley GT2 {0}T PD={1:F2} OD={2} ===", Teeth, pd * 1000, PulleyOd * 1000));

        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Blank with bore
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, tipR);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen, false);

        // Cut tooth GAP on +X, pattern around Z
        double halfGap = Math.PI / Teeth * 0.55;
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        double a1 = -halfGap, a2 = halfGap;
        double ix1 = rootR * Math.Cos(a1 * 0.65), iy1 = rootR * Math.Sin(a1 * 0.65);
        double ix2 = rootR * Math.Cos(a2 * 0.65), iy2 = rootR * Math.Sin(a2 * 0.65);
        double ox1 = (tipR + 0.0004) * Math.Cos(a1), oy1 = (tipR + 0.0004) * Math.Sin(a1);
        double ox2 = (tipR + 0.0004) * Math.Cos(a2), oy2 = (tipR + 0.0004) * Math.Sin(a2);
        m.SketchManager.CreateLine(ix1, iy1, 0, ox1, oy1, 0);
        m.SketchManager.CreateLine(ox1, oy1, 0, ox2, oy2, 0);
        m.SketchManager.CreateLine(ox2, oy2, 0, ix2, iy2, 0);
        m.SketchManager.CreateLine(ix2, iy2, 0, ix1, iy1, 0);
        Feature gap = CutBlind(m, PulleyLen + 0.0002);

        Feature ax = MakeZAxis(m);
        m.ClearSelection2(true);
        m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
        if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
        Feature pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
            Teeth, 2.0 * Math.PI / Teeth, false, "NULL", false, false, false);
        Log("Pattern = " + (pat != null));

        // Near flange
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, FlangeOd / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, FlangeT, false);

        string tmp = Path.Combine(OutDir, "Pulley_GT2_12T_new.SLDPRT");
        SaveDoc(m, tmp);
        sw.CloseDoc(m.GetTitle());
        string final = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        if (File.Exists(final)) File.Delete(final);
        File.Move(tmp, final);
        Log("Pulley replaced");
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref err, ref warn);
        ModelDoc2 asmModel = (ModelDoc2)assy;
        sw.ActivateDoc3(asmModel.GetTitle(), false, 0, ref err);
        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); } catch { }
        if (c == null) try { c = assy.AddComponent4(path, "", x, y, z); } catch { }
        if (c == null && assy.AddComponent(path, x, y, z))
        {
            object[] comps = (object[])assy.GetComponents(false);
            if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
        }
        if (c == null) throw new Exception("Add fail " + path);
        Log("Added " + c.Name2);
        return c;
    }

    static void BuildAssembly(SldWorks sw)
    {
        Log("=== Assembly ===");
        object adoc = sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 model = (ModelDoc2)sw.ActiveDoc;
        Component2 motor = AddComp(sw, asm, Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT"), 0, 0, 0);
        try
        {
            model.Extension.SelectByID2(motor.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            asm.FixComponent();
        }
        catch { }
        double shaftTip = PlateT + BossH + ShaftL1;
        double pulleyZ = shaftTip - TipPast - PulleyLen;
        AddComp(sw, asm, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"), 0, 0, pulleyZ);
        Log(string.Format("tip={0:F2} pulley={1:F2}..{2:F2}", shaftTip * 1000, pulleyZ * 1000, (pulleyZ + PulleyLen) * 1000));
        model.EditRebuild3();
        string tmp = Path.Combine(OutDir, "Motor_PM35L_N48_Pulley_new.SLDASM");
        SaveDoc(model, tmp);
        sw.CloseDoc(model.GetTitle());
        string final = Path.Combine(OutDir, "Motor_PM35L_N48_Pulley.SLDASM");
        if (File.Exists(final)) File.Delete(final);
        File.Move(tmp, final);
        Log("Assembly replaced");
    }

    static void Preview(SldWorks sw, string path, string bmp)
    {
        int e = 0, w = 0;
        ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (m == null) return;
        m.ShowNamedView2("*Isometric", -1);
        m.ViewZoomtofit2();
        PartDoc part = (PartDoc)m;
        object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);
        if (bodies != null)
            foreach (object ob in bodies)
            {
                double[] box = (double[])((Body2)ob).GetBodyBox();
                Log(string.Format("BOX {0}: X[{1:F1}..{2:F1}] Y[{3:F1}..{4:F1}] Z[{5:F1}..{6:F1}]",
                    Path.GetFileName(path), box[0]*1000, box[3]*1000, box[1]*1000, box[4]*1000, box[2]*1000, box[5]*1000));
            }
        m.SaveBMP(bmp, 1200, 900);
        sw.CloseDoc(m.GetTitle());
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== force-rebuild " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        Log("Attached");
        ForceCloseAll(sw);
        BuildMotor(sw);
        BuildPulley(sw);
        BuildAssembly(sw);
        Preview(sw, Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT"), Path.Combine(OutDir, "_preview_motor.bmp"));
        Preview(sw, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"), Path.Combine(OutDir, "_preview_pulley.bmp"));
        Log("DONE");
    }
}
