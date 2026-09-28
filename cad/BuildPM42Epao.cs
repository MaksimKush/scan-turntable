// Minebea PM42L-048-EPAO (marking EM-182 TB9608E)
// Ø42, L=22.2, mount P=49.5, holes Ø3.5, ears R3.75, boss Ø10×1.5, shaft Ø3
// EPAO: shaft stickout ~17 mm from boss (retail outline)
// + GT2 12T pulley no flange, L=6, tip +2 mm
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
    static string LogPath = Path.Combine(OutDir, "sw_pm42.log");

    const double BodyOd = 0.042;
    const double PlateT = 0.0008;
    const double CanLen = 0.0214;
    const double RearBossH = 0.001;
    const double BossOd = 0.010;
    const double BossH = 0.0015;
    const double ShaftDia = 0.003;
    const double ShaftL1 = 0.017; // EPAO ~17 mm stickout from boss
    const double MountP = 0.0495;
    const double MountHole = 0.0035;
    const double EarR = 0.00375;
    const double WireW = 0.0127;
    const double WireRadial = 0.0055;
    const double WireThetaDeg = 45.0;

    const int Teeth = 12;
    const double Pitch = 0.002;
    const double Pld = 0.000254;
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;
    const double ToothH = 0.00075;

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
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok);
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
        object doc = sw.NewDocument(PartTpl, 0, 0, 0);
        if (doc == null) throw new Exception("NewDocument null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static void ReplaceFile(string tmp, string final)
    {
        if (File.Exists(final))
        {
            try { File.Delete(final); }
            catch { Log("kept " + Path.GetFileName(tmp)); return; }
        }
        File.Move(tmp, final);
    }

    static void BuildMotor(SldWorks sw)
    {
        Log("=== Motor PM42L-048-EPAO ===");
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
        try { CutBlind(m, PlateT + 0.001); Log("Mount holes OK"); }
        catch (Exception ex) { Log("holes: " + ex.Message); try { m.SketchManager.InsertSketch(true); } catch { } }

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

        string tmp = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_new.SLDPRT");
        SaveDoc(m, tmp);
        sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT"));
        // also refresh Motor_PM42L.SLDPRT alias
        try
        {
            File.Copy(Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT"),
                Path.Combine(OutDir, "Motor_PM42L.SLDPRT"), true);
        }
        catch { }
        Log("Motor OK Ø42 P=49.5 shaft l1=17mm EPAO");
    }

    static void BuildPulley(SldWorks sw)
    {
        double pd = Teeth * Pitch / Math.PI;
        double tipR = pd / 2.0 - Pld;
        double grooveR = 0.00055;
        double cx = tipR - grooveR * 0.85;
        Log(string.Format("=== Pulley GT2 {0}T OD={1:F3} L=6 NO flange ===", Teeth, tipR * 2000));

        ModelDoc2 m = NewPart(sw);
        string front = FrontPlane(m);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, tipR);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen, false);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(cx, 0, 0, grooveR);
        Feature gap = CutBlind(m, PulleyLen + 0.0002);
        Log("Groove cut");

        Feature ax = MakeZAxis(m);
        m.ClearSelection2(true);
        m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
        if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
        Feature pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
            Teeth, 2.0 * Math.PI / Teeth, false, "NULL", false, false, false);
        Log("Pattern=" + (pat != null));

        string tmp = Path.Combine(OutDir, "Pulley_GT2_12T_new.SLDPRT");
        SaveDoc(m, tmp);
        sw.CloseDoc(m.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"));
        Log("Pulley OK no flange GT2 grooves");
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
        string motorPath = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        Component2 motor = AddComp(sw, asm, motorPath, 0, 0, 0);
        try
        {
            model.Extension.SelectByID2(motor.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            asm.FixComponent();
        }
        catch { }
        double tip = PlateT + BossH + ShaftL1;
        double pz = tip - TipPast - PulleyLen;
        AddComp(sw, asm, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"), 0, 0, pz);
        Log(string.Format("tip={0:F2} pulley={1:F2}..{2:F2}", tip * 1000, pz * 1000, (pz + PulleyLen) * 1000));
        model.EditRebuild3();
        string tmp = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley_new.SLDASM");
        SaveDoc(model, tmp);
        sw.CloseDoc(model.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley.SLDASM"));
        Log("Assembly OK");
    }

    static void Preview(SldWorks sw, string path, string bmp)
    {
        int e = 0, w = 0;
        ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (m == null) return;
        m.ShowNamedView2("*Isometric", -1);
        m.ViewZoomtofit2();
        m.SaveBMP(bmp, 1200, 900);
        sw.CloseDoc(m.GetTitle());
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== pm42 " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        sw.CloseAllDocuments(true);
        System.Threading.Thread.Sleep(800);
        BuildMotor(sw);
        BuildPulley(sw);
        BuildAssembly(sw);
        Preview(sw, Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT"), Path.Combine(OutDir, "_preview_motor.bmp"));
        Preview(sw, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"), Path.Combine(OutDir, "_preview_pulley.bmp"));
        Log("DONE");
    }
}
