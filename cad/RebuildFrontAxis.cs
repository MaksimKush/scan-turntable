// Rebuild Motor_PM35L_N48 + Pulley_GT2_12T on Front Plane (extrude along Z).
// Previous build used Top Plane → features grew along Y; pulley teeth pattern failed.
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

    // PM35L-N48 (Minebea file_07_132 / std dims)
    const double BodyOd = 0.035;
    const double PlateT = 0.0008;
    const double CanLen = 0.0214;
    const double RearBossH = 0.001;
    const double BossOd = 0.010;
    const double BossH = 0.0015;
    const double ShaftDia = 0.003;
    const double ShaftL1 = 0.010;
    const double MountP = 0.042;
    const double MountHole = 0.0032;
    const double EarR = 0.0035;
    const double WireW = 0.0127;
    const double WireRadial = 0.0055;
    const double WireFromCl = 0.0158;
    const double WireThetaDeg = 45.0;

    // GT2 12T ≈ Ø8 × 6, tip of shaft sticks out 2 mm
    const int Teeth = 12;
    const double Pitch = 0.002;
    const double PulleyOd = 0.008;
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;
    const double FlangeOd = 0.0086;
    const double FlangeT = 0.0005;

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
            if (n.Contains("front") || n.Contains("спереди") || n == "спереди") return name;
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

    // Extrude along +Z from Front plane (Dir=false) or -Z (Dir=true)
    static Feature Extrude(ModelDoc2 model, double depth, bool reverse)
    {
        Feature f = (Feature)model.FeatureManager.FeatureExtrusion2(
            true, false, reverse,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.0,
            false, false, false, false, 0.0, 0.0,
            false, false, false, false,
            true, true, true,
            (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null)
            f = (Feature)model.FeatureManager.FeatureExtrusion2(
                true, false, !reverse,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0,
                false, false, false, false, 0.0, 0.0,
                false, false, false, false,
                true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null) throw new Exception("Extrude fail d=" + depth);
        Log("Extrude OK d=" + (depth * 1000) + "mm rev=" + reverse);
        return f;
    }

    static Feature CutThru(ModelDoc2 model)
    {
        Feature f = null;
        try
        {
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, false, false,
                (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
                0.01, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        }
        catch (Exception ex) { Log("CutThru a: " + ex.Message); }
        if (f == null)
        {
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, true, false,
                (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondBlind,
                0.01, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        }
        if (f == null) throw new Exception("CutThru fail");
        Log("CutThru OK");
        return f;
    }

    static Feature CutBlind(ModelDoc2 model, double depth, bool reverse)
    {
        Feature f = (Feature)model.FeatureManager.FeatureCut4(
            true, false, reverse,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.01, false, false, false, false,
            0.0174532925199433, 0.0174532925199433,
            false, false, false, false, false, true, true,
            false, false, false,
            (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null)
            f = (Feature)model.FeatureManager.FeatureCut4(
                true, false, !reverse,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.01, false, false, false, false,
                0.0174532925199433, 0.0174532925199433,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false, true);
        if (f == null) throw new Exception("CutBlind fail");
        Log("CutBlind OK d=" + (depth * 1000) + "mm");
        return f;
    }

    static void CloseAll(SldWorks sw)
    {
        try
        {
            while (sw.GetDocumentCount() > 0)
            {
                ModelDoc2 d = (ModelDoc2)sw.GetFirstDocument();
                if (d == null) break;
                sw.CloseDoc(d.GetTitle());
            }
        }
        catch { }
    }

    static ModelDoc2 NewPart(SldWorks sw)
    {
        object doc = sw.NewDocument(PartTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static Feature EnsureTempAxis(ModelDoc2 model)
    {
        model.ClearSelection2(true);
        model.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        model.Extension.SelectByID2("Top Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
        // Front∩Right or Right∩Top → axis along Z or X; want Z = Front∩Right? 
        // Front=XY, Right=YZ → intersection = Y. Top=XZ ∩ Right=YZ → Z axis. Good.
        model.InsertAxis2(true);
        Feature ax = null, f = (Feature)model.FirstFeature();
        while (f != null)
        {
            if (f.GetTypeName2() == "RefAxis") ax = f;
            f = (Feature)f.GetNextFeature();
        }
        return ax;
    }

    static void BuildMotor(SldWorks sw)
    {
        Log("=== Motor Front-plane rebuild ===");
        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Front plate disk Ø35 × 0.8 along +Z
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, PlateT, false);

        // Ears
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);

        // Mount holes both in one sketch
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, MountHole / 2.0);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, MountHole / 2.0);
        try { CutThru(m); }
        catch (Exception ex)
        {
            Log("holes cutThru: " + ex.Message + " — try blind");
            try { CutBlind(m, PlateT + 0.0005, false); } catch (Exception ex2) { Log("holes: " + ex2.Message); }
        }

        // Can body back along -Z
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, CanLen, true);

        // Rear boss
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.004);
        Extrude(m, CanLen + RearBossH, true);

        // Pilot boss +Z
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BossOd / 2.0);
        Extrude(m, PlateT + BossH, false);

        // Shaft +Z
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PlateT + BossH + ShaftL1, false);

        // Wire block at 45°
        double th = WireThetaDeg * Math.PI / 180.0;
        double ux = Math.Cos(th), uy = Math.Sin(th);
        double vx = -Math.Sin(th), vy = Math.Cos(th);
        double r0 = BodyOd / 2.0 - 0.0005, r1 = BodyOd / 2.0 + WireRadial, hw = WireW / 2.0;
        double[] xs = {
            ux*r0+vx*(-hw), ux*r0+vx*hw, ux*r1+vx*hw, ux*r1+vx*(-hw)
        };
        double[] ys = {
            uy*r0+vy*(-hw), uy*r0+vy*hw, uy*r1+vy*hw, uy*r1+vy*(-hw)
        };
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateLine(xs[0], ys[0], 0, xs[1], ys[1], 0);
        m.SketchManager.CreateLine(xs[1], ys[1], 0, xs[2], ys[2], 0);
        m.SketchManager.CreateLine(xs[2], ys[2], 0, xs[3], ys[3], 0);
        m.SketchManager.CreateLine(xs[3], ys[3], 0, xs[0], ys[0], 0);
        Extrude(m, 0.012, true);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateLine(xs[0], ys[0], 0, xs[1], ys[1], 0);
        m.SketchManager.CreateLine(xs[1], ys[1], 0, xs[2], ys[2], 0);
        m.SketchManager.CreateLine(xs[2], ys[2], 0, xs[3], ys[3], 0);
        m.SketchManager.CreateLine(xs[3], ys[3], 0, xs[0], ys[0], 0);
        Extrude(m, PlateT + 0.002, false);

        SaveDoc(m, Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT"));
        sw.CloseDoc(m.GetTitle());
        Log("Motor OK");
    }

    static void BuildPulley(SldWorks sw)
    {
        double pd = Teeth * Pitch / Math.PI;
        double rootR = (pd - 0.0014) / 2.0; // ~root for GT2 approx
        if (rootR < ShaftDia / 2.0 + 0.0008) rootR = ShaftDia / 2.0 + 0.0008;
        double tipR = PulleyOd / 2.0;
        Log(string.Format("=== Pulley GT2 z={0} PD={1:F2} tipR={2:F2} rootR={3:F2} L={4} ===",
            Teeth, pd * 1000, tipR * 1000, rootR * 1000, PulleyLen * 1000));

        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        // Solid blank Ø8 × 6 with bore Ø3
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, tipR);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen, false);

        // Cut one tooth SPACE (gap between teeth), then circular pattern ×12
        // Gap centered on +X: from tipR down toward root between half-pitches
        double halfGap = Math.PI / Teeth * 0.55; // angular half-width of space
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        // Trapezoid gap: outer wide, inner narrow
        double a1 = -halfGap, a2 = halfGap;
        double ix1 = rootR * Math.Cos(a1 * 0.7), iy1 = rootR * Math.Sin(a1 * 0.7);
        double ix2 = rootR * Math.Cos(a2 * 0.7), iy2 = rootR * Math.Sin(a2 * 0.7);
        double ox1 = (tipR + 0.0003) * Math.Cos(a1), oy1 = (tipR + 0.0003) * Math.Sin(a1);
        double ox2 = (tipR + 0.0003) * Math.Cos(a2), oy2 = (tipR + 0.0003) * Math.Sin(a2);
        m.SketchManager.CreateLine(ix1, iy1, 0, ox1, oy1, 0);
        m.SketchManager.CreateLine(ox1, oy1, 0, ox2, oy2, 0);
        m.SketchManager.CreateLine(ox2, oy2, 0, ix2, iy2, 0);
        m.SketchManager.CreateLine(ix2, iy2, 0, ix1, iy1, 0);
        Feature gap = CutBlind(m, PulleyLen + 0.0001, false);

        Feature ax = EnsureTempAxis(m);
        m.ClearSelection2(true);
        m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
        if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
        Feature pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
            Teeth, 2.0 * Math.PI / Teeth, false, "NULL", false, false, false);
        Log("Tooth gap pattern = " + (pat != null));

        // Side flanges (GT2 style) — thin washers at z=0 and optional at far end via extrude from plane
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, FlangeOd / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, FlangeT, false);

        // Far flange: extrude from offset — sketch on front, extrude to PulleyLen+FlangeT then cut middle? 
        // Simpler: sketch flange at end using boss from plane with start offset via FeatureExtrusion with offset
        try
        {
            m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
            m.SketchManager.InsertSketch(true);
            m.SketchManager.CreateCircleByRadius(0, 0, 0, FlangeOd / 2.0);
            m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
            // Extrude reverse direction past body then... skip second flange if API hard
            Feature f2 = (Feature)m.FeatureManager.FeatureExtrusion2(
                true, false, false,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                FlangeT, 0.0,
                false, false, false, false, 0.0, 0.0,
                false, false, false, false,
                true, true, true,
                (int)swStartConditions_e.swStartOffset, PulleyLen, false);
            Log("Far flange = " + (f2 != null));
        }
        catch (Exception ex) { Log("Far flange: " + ex.Message); }

        SaveDoc(m, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"));
        sw.CloseDoc(m.GetTitle());
        Log("Pulley OK");
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
        ModelDoc2 asmModel = (ModelDoc2)assy;
        sw.ActivateDoc3(asmModel.GetTitle(), false, 0, ref err);
        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); } catch { }
        if (c == null) try { c = assy.AddComponent4(path, "", x, y, z); } catch { }
        if (c == null)
        {
            if (assy.AddComponent(path, x, y, z))
            {
                object[] comps = (object[])assy.GetComponents(false);
                if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
            }
        }
        if (c == null) throw new Exception("AddComponent " + path);
        Log("Added " + c.Name2 + " z=" + (z * 1000).ToString("F2") + "mm");
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
        Log(string.Format("shaftTip={0:F2} pulley={1:F2}..{2:F2}", shaftTip * 1000, pulleyZ * 1000, (pulleyZ + PulleyLen) * 1000));

        model.EditRebuild3();
        SaveDoc(model, Path.Combine(OutDir, "Motor_PM35L_N48_Pulley.SLDASM"));
        sw.CloseDoc(model.GetTitle());
        Log("Assembly OK");
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== rebuild " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw;
        try { sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application"); Log("Attached SW"); }
        catch
        {
            sw = (SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
            sw.Visible = true;
            System.Threading.Thread.Sleep(8000);
            Log("Started SW");
        }
        sw.Visible = true;
        CloseAll(sw);
        BuildMotor(sw);
        BuildPulley(sw);
        BuildAssembly(sw);
        Log("DONE");
    }
}
