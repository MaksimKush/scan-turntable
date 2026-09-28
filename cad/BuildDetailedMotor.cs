// Detailed Minebea PM35L-N48 + GT2 pulley + assembly
// Dims from Minebea std / PM35 datasheet (NOT simplified cylinder):
//   body Ø35 × 22.2 MAX, front plate 0.8, ears 2-R3.5, holes 2-Ø3.2, P=42±0.2
//   boss Ø10×1.5, shaft Ø3 × l1=10 from boss, rear boss 1 MAX
//   wire holder W=12.7, radial 5.5 MAX, 15.8 from CL, θ≈45°
// Pulley: GT2 12T, OD 8, L 6, tip sticks out 2 mm past pulley
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
    static string LogPath = Path.Combine(OutDir, "sw_detailed.log");

    const double BodyOd = 0.035;
    const double BodyLen = 0.0222;       // MAX incl. flange face stack; we: plate 0.8 + can 21.4
    const double PlateT = 0.0008;
    const double CanLen = 0.0214;        // BodyLen - PlateT
    const double RearBoss = 0.001;
    const double BossOd = 0.010;
    const double BossH = 0.0015;
    const double ShaftDia = 0.003;
    const double ShaftL1 = 0.010;        // from boss face
    const double MountP = 0.042;
    const double MountHole = 0.0032;
    const double EarR = 0.0035;
    const double WireW = 0.0127;
    const double WireRadial = 0.0055;
    const double WireFromCl = 0.0158;
    const double WireThetaDeg = 45.0;

    const int PulleyTeeth = 12;
    const double Gt2Pitch = 0.002;
    const double PulleyOd = 0.008;
    const double PulleyRoot = 0.0064;    // approx root diameter under teeth
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;

    static void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + m;
        File.AppendAllText(LogPath, line + System.Environment.NewLine);
        Console.WriteLine(line);
    }

    static string TopPlane(ModelDoc2 model)
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
            if (n.Contains("top") || n.Contains("верх") || n == "сверху") return name;
        }
        return planes.Count > 1 ? planes[1] : planes[0];
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
        // FeatureExtrusion2(Sd, Flip, Dir=reverse direction, ...)
        Feature f = (Feature)model.FeatureManager.FeatureExtrusion2(
            true, false, reverse,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            depth, 0.0,
            false, false, false, false, 0.0, 0.0,
            false, false, false, false,
            true, true, true,
            (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (f == null)
        {
            f = (Feature)model.FeatureManager.FeatureExtrusion2(
                true, false, !reverse,
                (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0,
                false, false, false, false, 0.0, 0.0,
                false, false, false, false,
                true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        }
        if (f == null) throw new Exception("Extrude failed d=" + depth + " rev=" + reverse);
        Log("Extrude OK d=" + depth + " rev=" + reverse);
        return f;
    }

    static void MakeThruHole(ModelDoc2 model, double x, double y, double zFace, double dia)
    {
        model.ClearSelection2(true);
        bool ok = model.Extension.SelectByID2("", "FACE", x, y, zFace, false, 0, null, 0);
        if (!ok) ok = model.Extension.SelectByID2("", "FACE", x, y, 0, false, 0, null, 0);
        if (!ok) ok = model.Extension.SelectByRay(x, y, zFace + 0.05, 0, 0, -1, 0.001, 2, false, 0, 0);
        if (!ok) ok = model.Extension.SelectByRay(x, y, -0.05, 0, 0, 1, 0.001, 2, false, 0, 0);
        Log("Hole face @" + x + "," + y + "," + zFace + " = " + ok);
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
                PlateT + 0.001, 0.01, false, false, false, false,
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
        if (doc == null) throw new Exception("NewDocument part null");
        return (ModelDoc2)sw.ActiveDoc;
    }

    static void SketchFlange(ModelDoc2 model)
    {
        // Circle body OD + two ear lobes at ±Y on mount pitch, then holes cut later
        double r = BodyOd / 2.0;
        double hy = MountP / 2.0;
        model.SketchManager.CreateCircleByRadius(0, 0, 0, r);
        // Ear lobes: circles at mount holes that merge with body
        model.SketchManager.CreateCircleByRadius(0, hy, 0, EarR);
        model.SketchManager.CreateCircleByRadius(0, -hy, 0, EarR);
    }

    static void BuildMotor(SldWorks sw)
    {
        Log("=== Detailed Motor_PM35L_N48 ===");
        ModelDoc2 m = NewPart(sw);
        string top = TopPlane(m);

        // 1) Front plate disk Ø35 × 0.8
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, PlateT, false);

        // Mounting ears as merged disks at ±P/2
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, -MountP / 2.0, 0, EarR);
        Extrude(m, PlateT, false);

        // Mount holes: one SimpleHole + circular pattern ×2
        try
        {
            MakeThruHole(m, 0, MountP / 2.0, PlateT, MountHole);
            Feature holeFeat = (Feature)m.FeatureByPositionReverse(0);
            m.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
            m.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
            m.InsertAxis2(true);
            Feature ax = null;
            Feature ff = (Feature)m.FirstFeature();
            while (ff != null)
            {
                if (ff.GetTypeName2() == "RefAxis") ax = ff;
                ff = (Feature)ff.GetNextFeature();
            }
            m.ClearSelection2(true);
            m.Extension.SelectByID2(holeFeat.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
            if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
            Feature pat = (Feature)m.FeatureManager.FeatureCircularPattern4(2, Math.PI, false, "NULL", false, true, false);
            Log("Mount hole pattern " + (pat != null));
        }
        catch (Exception ex) { Log("Mount holes: " + ex.Message); }

        // 2) Can body Ø35 × 21.4 downward (reverse) from plate back = Z=0 plane goes +Z for plate,
        //    body goes -Z. With default extrude +Z for plate, reverse body.
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BodyOd / 2.0);
        Extrude(m, CanLen, true);

        // 3) Rear center boss 1 mm on back face (further reverse)
        // Sketch on plane offset: use top plane + reverse deep enough from Z=0 → CanLen+RearBoss reverse
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.004); // Ø8 approx rear boss
        Extrude(m, CanLen + RearBoss, true);

        // 4) Front pilot boss Ø10 × 1.5 from plate front (+Z)
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, BossOd / 2.0);
        Extrude(m, PlateT + BossH, false);

        // 5) Shaft Ø3: from plate through boss + l1
        double shaftFromPlane = PlateT + BossH + ShaftL1;
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, shaftFromPlane, false);

        // 6) Wire exit block — rectangular boss on rim at θ=45°
        // Block sits on cylinder: sketch on Top plane, rectangle outside radius, extrude PlateT (thin flange height look)
        // Better: extrude radially via boss on Right plane — simplify as box attached to OD
        double th = WireThetaDeg * Math.PI / 180.0;
        // Place block along +X rotated: center of block at angle θ from +X toward +Y
        double cx = WireFromCl * Math.Cos(th);
        double cy = WireFromCl * Math.Sin(th);
        // Rectangle oriented roughly tangential: width 12.7 along tangent, thickness radial ~5.5 beyond OD/2
        // Approximate with circle-sector style: extrude a rectangle from OD/2 to OD/2+5.5
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        // Local frame at θ: radial u, tangent v
        double ux = Math.Cos(th), uy = Math.Sin(th);
        double vx = -Math.Sin(th), vy = Math.Cos(th);
        double r0 = BodyOd / 2.0 - 0.0005;
        double r1 = BodyOd / 2.0 + WireRadial;
        double hw = WireW / 2.0;
        double[] xs = new double[4];
        double[] ys = new double[4];
        xs[0] = ux * r0 + vx * (-hw); ys[0] = uy * r0 + vy * (-hw);
        xs[1] = ux * r0 + vx * (hw);  ys[1] = uy * r0 + vy * (hw);
        xs[2] = ux * r1 + vx * (hw);  ys[2] = uy * r1 + vy * (hw);
        xs[3] = ux * r1 + vx * (-hw); ys[3] = uy * r1 + vy * (-hw);
        m.SketchManager.CreateLine(xs[0], ys[0], 0, xs[1], ys[1], 0);
        m.SketchManager.CreateLine(xs[1], ys[1], 0, xs[2], ys[2], 0);
        m.SketchManager.CreateLine(xs[2], ys[2], 0, xs[3], ys[3], 0);
        m.SketchManager.CreateLine(xs[3], ys[3], 0, xs[0], ys[0], 0);
        // Wire block height ~ half body (from plate back into can) — use Blind PlateT+10mm reverse+forward merge
        Extrude(m, 0.010, true); // into body side
        // Also protrude slightly on front of plate
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateLine(xs[0], ys[0], 0, xs[1], ys[1], 0);
        m.SketchManager.CreateLine(xs[1], ys[1], 0, xs[2], ys[2], 0);
        m.SketchManager.CreateLine(xs[2], ys[2], 0, xs[3], ys[3], 0);
        m.SketchManager.CreateLine(xs[3], ys[3], 0, xs[0], ys[0], 0);
        Extrude(m, PlateT + 0.002, false);

        // Skip risky rear annular cut — body already has rear boss
        Log("Rear recess omitted (keep solid can)");

        string motorPath = Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT");
        SaveDoc(m, motorPath);
        sw.CloseDoc(m.GetTitle());
        Log("Motor detailed saved");
    }

    static void BuildPulley(SldWorks sw)
    {
        double pd = PulleyTeeth * Gt2Pitch / Math.PI;
        Log(string.Format("=== GT2 pulley z={0} PD={1:F2}mm OD={2} L={3} tip+{4} ===",
            PulleyTeeth, pd * 1000, PulleyOd * 1000, PulleyLen * 1000, TipPast * 1000));

        ModelDoc2 m = NewPart(sw);
        string top = TopPlane(m);

        // Hub with bore: outer root + inner shaft hole in one sketch
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, PulleyRoot / 2.0);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen, false);

        // Tooth tips as outer lobes: one trapezoid tooth, circular pattern
        double tipR = PulleyOd / 2.0;
        double rootR = PulleyRoot / 2.0;
        double toothHalfAngle = Math.PI / PulleyTeeth * 0.35;
        m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        // Tooth centered on +X
        double a1 = -toothHalfAngle, a2 = toothHalfAngle;
        double x0 = rootR * Math.Cos(a1), y0 = rootR * Math.Sin(a1);
        double x1 = tipR * Math.Cos(a1 * 0.6), y1 = tipR * Math.Sin(a1 * 0.6);
        double x2 = tipR * Math.Cos(a2 * 0.6), y2 = tipR * Math.Sin(a2 * 0.6);
        double x3 = rootR * Math.Cos(a2), y3 = rootR * Math.Sin(a2);
        m.SketchManager.CreateLine(x0, y0, 0, x1, y1, 0);
        m.SketchManager.CreateLine(x1, y1, 0, x2, y2, 0);
        m.SketchManager.CreateLine(x2, y2, 0, x3, y3, 0);
        m.SketchManager.CreateLine(x3, y3, 0, x0, y0, 0);
        Extrude(m, PulleyLen, false);

        // Circular pattern of tooth feature
        try
        {
            Feature last = (Feature)m.FeatureByPositionReverse(0);
            m.ClearSelection2(true);
            m.Extension.SelectByID2(last.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
            // Axis: temporary — use Z via Right/Front intersection; select Right plane edge hard.
            // Use FeatureCircularPattern4 with axis selection by temporary axis
            Feature axisFeat = null;
            Feature f = (Feature)m.FirstFeature();
            while (f != null)
            {
                if (f.GetTypeName2() == "RefAxis" || f.Name.ToLowerInvariant().Contains("axis"))
                    axisFeat = f;
                f = (Feature)f.GetNextFeature();
            }
            // Create axis from origin along Z using two planes
            m.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
            m.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
            m.InsertAxis2(true);

            m.ClearSelection2(true);
            m.Extension.SelectByID2(last.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
            // Select Axis1
            Feature ax = null;
            f = (Feature)m.FirstFeature();
            while (f != null)
            {
                string tn = f.GetTypeName2();
                if (tn == "RefAxis") ax = f;
                f = (Feature)f.GetNextFeature();
            }
            if (ax != null)
                m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);

            Feature pat = (Feature)m.FeatureManager.FeatureCircularPattern4(
                PulleyTeeth, 2.0 * Math.PI / PulleyTeeth, false, "NULL", false, true, false);
            Log("CircularPattern " + (pat != null));
        }
        catch (Exception ex) { Log("Tooth pattern: " + ex.Message); }

        // Bore already in hub sketch
        Log("Bore in hub sketch");

        // Flanges on both ends slightly larger (GT2 often has side walls) — OD+0.6 mm × 0.5 mm
        try
        {
            m.Extension.SelectByID2(top, "PLANE", 0, 0, 0, false, 0, null, 0);
            m.SketchManager.InsertSketch(true);
            m.SketchManager.CreateCircleByRadius(0, 0, 0, (PulleyOd + 0.0006) / 2.0);
            m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
            Extrude(m, 0.0005, false);
        }
        catch (Exception ex) { Log("Flange tip: " + ex.Message); }

        SaveDoc(m, Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT"));
        sw.CloseDoc(m.GetTitle());
        Log("Pulley saved");
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
        ModelDoc2 asmModel = (ModelDoc2)assy;
        sw.ActivateDoc3(asmModel.GetTitle(), false, 0, ref err);
        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); } catch (Exception ex) { Log("Add5: " + ex.Message); }
        if (c == null) try { c = assy.AddComponent4(path, "", x, y, z); } catch (Exception ex) { Log("Add4: " + ex.Message); }
        if (c == null)
        {
            bool added = assy.AddComponent(path, x, y, z);
            Log("AddComponent bool=" + added);
            if (added)
            {
                object[] comps = (object[])assy.GetComponents(false);
                if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
            }
        }
        if (c == null) throw new Exception("AddComponent failed " + path);
        Log("Added " + c.Name2 + " @ z=" + (z * 1000) + "mm");
        return c;
    }

    static void BuildAssembly(SldWorks sw)
    {
        Log("=== Assembly Motor+Pulley ===");
        string motorPath = Path.Combine(OutDir, "Motor_PM35L_N48.SLDPRT");
        string pulleyPath = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        string asmPath = Path.Combine(OutDir, "Motor_PM35L_N48_Pulley.SLDASM");

        object adoc = sw.NewDocument(AsmTpl, 0, 0, 0);
        if (adoc == null) throw new Exception("New assembly null");
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 model = (ModelDoc2)sw.ActiveDoc;

        Component2 motor = AddComp(sw, asm, motorPath, 0, 0, 0);
        try
        {
            model.Extension.SelectByID2(motor.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            asm.FixComponent();
        }
        catch { }

        // Shaft tip = PlateT + BossH + ShaftL1 from Top plane (+Z)
        double shaftTip = PlateT + BossH + ShaftL1;
        double pulleyZ = shaftTip - TipPast - PulleyLen;
        AddComp(sw, asm, pulleyPath, 0, 0, pulleyZ);
        Log(string.Format("shaftTip={0:F2}mm pulley {1:F2}..{2:F2}mm (tip past={3}mm)",
            shaftTip * 1000, pulleyZ * 1000, (pulleyZ + PulleyLen) * 1000, TipPast * 1000));

        model.EditRebuild3();
        SaveDoc(model, asmPath);
        sw.CloseDoc(model.GetTitle());
        Log("Assembly saved " + asmPath);
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== detailed " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = null;
        try { sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application"); Log("Attached running SW"); }
        catch
        {
            Type t = Type.GetTypeFromProgID("SldWorks.Application");
            sw = (SldWorks)Activator.CreateInstance(t);
            sw.Visible = true;
            Log("Started SW");
            System.Threading.Thread.Sleep(8000);
        }
        sw.Visible = true;
        Log("Rev=" + sw.RevisionNumber());
        CloseAll(sw);
        BuildMotor(sw);
        BuildPulley(sw);
        BuildAssembly(sw);
        Log("DONE");
    }
}
