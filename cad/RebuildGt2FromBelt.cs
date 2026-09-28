// GT2 12T pulley from belt drawing radii (tip R0.555 + root R0.15).
// R1.00@0.40 cannot G1-blend with R0.555 (2*Rt > Rf) — see GT2_pulley_profile_notes.txt.
// 12T, P=2, PLD=0.254, L=6, no flange, bore Ø3. Front Plane → +Z.
// Also refreshes Motor_PM42L_048_EPAO_Pulley.SLDASM (shaft tip +2 mm past pulley).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Program
{
    static string OutDir = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    static string PartTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Part.PRTDOT";
    static string AsmTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Assembly.ASMDOT";
    static string LogPath = Path.Combine(OutDir, "sw_gt2_belt.log");

    const int Teeth = 12;
    const double PitchMm = 2.0;
    const double PldMm = 0.254;
    const double ToothHMm = 0.75;
    const double TipRMm = 0.555;
    const double RootRMm = 0.15;
    const double LandHalfMm = 0.75;
    const double ClearanceMm = 0.05; // groove larger than belt tooth
    const double ShaftDia = 0.003;
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;

    // PM42L-048-EPAO / file_07_40.gif
    const double PlateT = 0.0008;
    const double BossH = 0.0015;
    const double ShaftL1 = 0.017;

    static void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + m;
        File.AppendAllText(LogPath, line + System.Environment.NewLine);
        Console.WriteLine(line);
    }

    static string FrontPlane(ModelDoc2 model)
    {
        var planes = new List<string>();
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
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok + " e=" + e);
        if (!ok) throw new Exception("SaveAs failed " + path);
    }

    static Feature Extrude(ModelDoc2 model, double depth)
    {
        Feature f = (Feature)model.FeatureManager.FeatureExtrusion2(
            true, false, false,
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

    // Belt tooth local mm: x tangential, y=0 at land, +y toward tip (into pulley).
    // With clearance so tip & root circles intersect (G0 join).
    static List<double[]> BuildGroovePolygonMm()
    {
        double rt = TipRMm + ClearanceMm;
        double rr = RootRMm + ClearanceMm;
        double h = ToothHMm + ClearanceMm;
        double land = LandHalfMm + ClearanceMm * 0.4;
        double ctx = 0.0, cty = h - rt;
        double crx = land, cry = rr;

        // circle-circle intersections tip ∩ root
        double dx = crx - ctx, dy = cry - cty;
        double d = Math.Sqrt(dx * dx + dy * dy);
        double a = (rt * rt - rr * rr + d * d) / (2.0 * d);
        double hgt = Math.Sqrt(Math.Max(0.0, rt * rt - a * a));
        double mx = ctx + a * dx / d, my = cty + a * dy / d;
        double jx1 = mx + hgt * (-dy) / d, jy1 = my + hgt * dx / d;
        double jx2 = mx - hgt * (-dy) / d, jy2 = my - hgt * dx / d;
        // pick upper join (larger y)
        double jx = jy1 >= jy2 ? jx1 : jx2;
        double jy = jy1 >= jy2 ? jy1 : jy2;

        Log(string.Format("Groove clr={0:F3} tipC=(0,{1:F3}) R={2:F3} rootC=({3:F3},{4:F3}) R={5:F3} join=({6:F3},{7:F3})",
            ClearanceMm, cty, rt, crx, cry, rr, jx, jy));

        var raw = new List<double[]>();
        double halfP = 1.0; // half pitch stub outside land
        raw.Add(new[] { halfP, -0.5 });
        raw.Add(new[] { halfP, 0.0 });
        raw.Add(new[] { land, 0.0 });

        int nRoot = 8, nTip = 16;

        // root fillet right: land angle -pi/2 → join (shortest sweep)
        double a0 = -Math.PI / 2.0;
        double a1 = Math.Atan2(jy - cry, jx - crx);
        double daR = a1 - a0;
        while (daR > Math.PI) daR -= 2.0 * Math.PI;
        while (daR < -Math.PI) daR += 2.0 * Math.PI;
        for (int i = 1; i <= nRoot; i++)
        {
            double ang = a0 + daR * ((double)i / nRoot);
            raw.Add(new[] { crx + rr * Math.Cos(ang), cry + rr * Math.Sin(ang) });
        }

        // tip arc right join → tip bottom → left join
        double atR = Math.Atan2(jy - cty, jx - ctx);
        double atTip = Math.PI / 2.0;
        double atL = Math.Atan2(jy - cty, -jx - ctx);
        for (int i = 1; i <= nTip; i++)
        {
            double ang = atR + (atTip - atR) * ((double)i / nTip);
            raw.Add(new[] { ctx + rt * Math.Cos(ang), cty + rt * Math.Sin(ang) });
        }
        for (int i = 1; i <= nTip; i++)
        {
            double ang = atTip + (atL - atTip) * ((double)i / nTip);
            raw.Add(new[] { ctx + rt * Math.Cos(ang), cty + rt * Math.Sin(ang) });
        }

        // root fillet left: join → land
        double clx = -land, cly = rr;
        double aLstart = Math.Atan2(jy - cly, -jx - clx);
        double aLend = -Math.PI / 2.0;
        double daL = aLend - aLstart;
        while (daL > Math.PI) daL -= 2.0 * Math.PI;
        while (daL < -Math.PI) daL += 2.0 * Math.PI;
        for (int i = 1; i <= nRoot; i++)
        {
            double ang = aLstart + daL * ((double)i / nRoot);
            raw.Add(new[] { clx + rr * Math.Cos(ang), cly + rr * Math.Sin(ang) });
        }

        raw.Add(new[] { -land, 0.0 });
        raw.Add(new[] { -halfP, 0.0 });
        raw.Add(new[] { -halfP, -0.5 });
        raw.Add(new[] { halfP, -0.5 });

        // dedupe
        var pts = new List<double[]>();
        foreach (var p in raw)
        {
            if (pts.Count == 0 ||
                Math.Abs(p[0] - pts[pts.Count - 1][0]) > 1e-7 ||
                Math.Abs(p[1] - pts[pts.Count - 1][1]) > 1e-7)
                pts.Add(p);
        }
        return pts;
    }

    static void SketchGrooveCut(ModelDoc2 model, double tipR, List<double[]> beltMm)
    {
        // Map belt (x,y) → Front plane meters: tooth on +X;
        // land y=0 at r=tipR; +y (tip) → toward center (−radial).
        int n = beltMm.Count;
        double[,] pts = new double[n, 2];
        for (int i = 0; i < n; i++)
        {
            double ox = beltMm[i][0] * 0.001;
            double oy = beltMm[i][1] * 0.001;
            pts[i, 0] = tipR - oy;
            pts[i, 1] = ox;
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            model.SketchManager.CreateLine(
                pts[i, 0], pts[i, 1], 0,
                pts[j, 0], pts[j, 1], 0);
        }
    }

    static void BuildPulley(SldWorks sw)
    {
        double pdMm = Teeth * PitchMm / Math.PI;
        double odMm = pdMm - 2.0 * PldMm;
        double tipR = odMm * 0.5 * 0.001;
        var poly = BuildGroovePolygonMm();
        Log(string.Format("GT2 {0}T PD={1:F4} OD={2:F4} tipR={3:F4} L=6 NO flange polyN={4}",
            Teeth, pdMm, odMm, tipR * 1000, poly.Count));

        ModelDoc2 m = (ModelDoc2)sw.NewDocument(PartTpl, 0, 0, 0);
        string front = FrontPlane(m);

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, tipR);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen);
        Log("Blank OK");

        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        SketchGrooveCut(m, tipR, poly);
        Feature gap = CutBlind(m, PulleyLen + 0.0002);
        Log("Groove cut OK: " + gap.Name);

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
        string final = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        try
        {
            if (File.Exists(final))
            {
                File.SetAttributes(final, FileAttributes.Normal);
                File.Delete(final);
            }
            File.Move(tmp, final);
            Log("Pulley saved " + final);
        }
        catch (Exception ex)
        {
            Log("File locked — left as Pulley_GT2_12T_new.SLDPRT: " + ex.Message);
        }
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
        MathTransform t = c.Transform2;
        double[] a = (double[])t.ArrayData;
        a[0] = 1; a[1] = 0; a[2] = 0;
        a[3] = 0; a[4] = 1; a[5] = 0;
        a[6] = 0; a[7] = 0; a[8] = 1;
        a[9] = x; a[10] = y; a[11] = z;
        a[12] = 1;
        t.ArrayData = a;
        c.Transform2 = t;
        Log(string.Format("placed {0} z={1:F2} mm", Path.GetFileName(path), z * 1000));
        return c;
    }

    static void BuildAssembly(SldWorks sw)
    {
        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pulley = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        if (!File.Exists(pulley)) pulley = Path.Combine(OutDir, "Pulley_GT2_12T_new.SLDPRT");
        if (!File.Exists(motor)) throw new Exception("Motor missing");

        object adoc = sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;

        Component2 cm = AddComp(sw, asm, md, motor, 0, 0, 0);
        try
        {
            md.Extension.SelectByID2(cm.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            asm.FixComponent();
        }
        catch { }

        double tip = PlateT + BossH + ShaftL1;
        double pz = tip - TipPast - PulleyLen;
        AddComp(sw, asm, md, pulley, 0, 0, pz);
        Log(string.Format("shaft tip={0:F2} pulley={1:F2}..{2:F2} past=+{3:F2}",
            tip * 1000, pz * 1000, (pz + PulleyLen) * 1000, TipPast * 1000));

        md.EditRebuild3();
        string tmp = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley_new.SLDASM");
        SaveDoc(md, tmp);
        sw.CloseDoc(md.GetTitle());
        string dest = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley.SLDASM");
        try
        {
            if (File.Exists(dest))
            {
                File.SetAttributes(dest, FileAttributes.Normal);
                File.Delete(dest);
            }
            File.Move(tmp, dest);
            Log("Assembly saved");
        }
        catch (Exception ex)
        {
            Log("Asm locked — left as _new: " + ex.Message);
        }
    }

    static void Preview(SldWorks sw)
    {
        string path = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        if (!File.Exists(path)) path = Path.Combine(OutDir, "Pulley_GT2_12T_new.SLDPRT");
        int e = 0, w = 0;
        ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (m == null) return;
        m.ShowNamedView2("*Isometric", -1);
        m.ViewZoomtofit2();
        m.SaveBMP(Path.Combine(OutDir, "_preview_pulley.bmp"), 1400, 1000);
        PartDoc part = (PartDoc)m;
        object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);
        if (bodies != null)
            foreach (object ob in bodies)
            {
                double[] box = (double[])((Body2)ob).GetBodyBox();
                Log(string.Format("BOX X[{0:F2}..{1:F2}] Y[{2:F2}..{3:F2}] Z[{4:F2}..{5:F2}]",
                    box[0] * 1000, box[3] * 1000, box[1] * 1000, box[4] * 1000, box[2] * 1000, box[5] * 1000));
            }
        sw.CloseDoc(m.GetTitle());
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== gt2 from belt " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        try { sw.CloseAllDocuments(true); } catch { }
        System.Threading.Thread.Sleep(700);
        BuildPulley(sw);
        BuildAssembly(sw);
        Preview(sw);
        Log("DONE");
    }
}
