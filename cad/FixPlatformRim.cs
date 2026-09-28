// Rebuild only Platform_RimGear_200 with full tooth pattern. Does NOT touch user motor.
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Program
{
    static string OutDir = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    static string PartTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Part.PRTDOT";
    static string LogPath = Path.Combine(OutDir, "fix_rim.log");

    const double Module = 0.002;
    const int ZRim = 98;
    const double FaceW = 0.008;
    const double DeckT = 0.006;
    static double DaRim { get { return Module * (ZRim + 2); } }
    static double DfRim { get { return Module * (ZRim - 2.5); } }

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

    static void Main()
    {
        File.WriteAllText(LogPath, "=== fix rim " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        sw.CloseAllDocuments(true);
        System.Threading.Thread.Sleep(500);

        sw.NewDocument(PartTpl, 0, 0, 0);
        ModelDoc2 m = (ModelDoc2)sw.ActiveDoc;
        string front = FrontPlane(m);

        double da = DaRim, df = DfRim;
        double rp = Module * ZRim / 2.0;
        double rt = da / 2.0 + 0.0003;
        double rr = df / 2.0 - 0.0003;
        double halfW = rp * Math.PI / ZRim * 0.55;
        int z = ZRim;

        Log(string.Format("Platform tip={0:F0} z={1} halfW={2:F2}", da * 1000, z, halfW * 1000));
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, da / 2.0);
        Extrude(m, FaceW, false);
        Feature ax = MakeZAxis(m);

        // First space
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCornerRectangle(rr, -halfW, 0, rt, halfW, 0);
        Feature gap = CutBlind(m, FaceW + 0.001);
        Log("first space=" + gap.Name);

        m.ClearSelection2(true);
        m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
        if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
        Feature pat = null;
        try { pat = (Feature)m.FeatureManager.FeatureCircularPattern4(z, 2.0 * Math.PI, true, "NULL", false, false, true); } catch { }
        if (pat == null)
        {
            m.ClearSelection2(true);
            m.Extension.SelectByID2(gap.Name, "BODYFEATURE", 0, 0, 0, false, 4, null, 0);
            if (ax != null) m.Extension.SelectByID2(ax.Name, "AXIS", 0, 0, 0, true, 1, null, 0);
            try { pat = (Feature)m.FeatureManager.FeatureCircularPattern4(z, 2.0 * Math.PI / z, false, "NULL", true, false, false); } catch { }
        }
        Log("pattern=" + (pat != null));

        if (pat == null)
        {
            Log("fallback cuts...");
            for (int i = 1; i < z; i++)
            {
                double ang = i * 2.0 * Math.PI / z;
                double c = Math.Cos(ang), s = Math.Sin(ang);
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
                try { CutBlind(m, FaceW + 0.001); }
                catch { try { m.SketchManager.InsertSketch(true); } catch { } }
                if (i % 10 == 0) Log("  " + i + "/" + z);
            }
        }

        // Deck
        double deckR = df / 2.0 - 0.0005;
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, deckR);
        Extrude(m, FaceW + DeckT, false);
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, 0.0045);
        try { CutBlind(m, FaceW + DeckT + 0.002); } catch { try { m.SketchManager.InsertSketch(true); } catch { } }

        string tmp = Path.Combine(OutDir, "Platform_RimGear_200_new.SLDPRT");
        int e = 0, w = 0;
        m.Extension.SaveAs(tmp, 0, 1, null, ref e, ref w);
        sw.CloseDoc(m.GetTitle());
        string dest = Path.Combine(OutDir, "Platform_RimGear_200.SLDPRT");
        if (File.Exists(dest)) { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); }
        File.Move(tmp, dest);
        Log("Saved " + dest + " size=" + new FileInfo(dest).Length);
        Log("DONE");
    }
}
