// Real GT2 2mm tooth profile (droftarts / OpenSCAD GT2_2mm polygon), no flanges.
// 12T, L=6 mm, bore Ø3. OD = PD - 2*PLD, PLD=0.254.
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Program
{
    static string OutDir = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    static string PartTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Part.PRTDOT";
    static string LogPath = Path.Combine(OutDir, "sw_gt2_real.log");

    const int Teeth = 12;
    const double PitchMm = 2.0;
    const double PldMm = 0.254;
    const double ToothWidthMm = 1.494;
    const double AddToothWidthMm = 0.2;
    const double ShaftDia = 0.003;
    const double PulleyLen = 0.006;

    // GT2_2mm() polygon from OpenSCAD (mm): local X = pitch direction, Y = radial (+ toward center)
    static readonly double[,] Gt2Mm = {
        { 0.747183, -0.5 },
        { 0.747183, 0 },
        { 0.647876, 0.037218 },
        { 0.598311, 0.130528 },
        { 0.578556, 0.238423 },
        { 0.547158, 0.343077 },
        { 0.504649, 0.443762 },
        { 0.451556, 0.53975 },
        { 0.358229, 0.636924 },
        { 0.2484, 0.707276 },
        { 0.127259, 0.750044 },
        { 0, 0.76447 },
        { -0.127259, 0.750044 },
        { -0.2484, 0.707276 },
        { -0.358229, 0.636924 },
        { -0.451556, 0.53975 },
        { -0.504797, 0.443762 },
        { -0.547291, 0.343077 },
        { -0.578605, 0.238423 },
        { -0.598311, 0.130528 },
        { -0.648009, 0.037218 },
        { -0.747183, 0 },
        { -0.747183, -0.5 }
    };

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
        if (!ok) throw new Exception("SaveAs failed");
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

    static void SketchGt2ToothCut(ModelDoc2 model, double toothDistM)
    {
        // Map OpenSCAD (x_mm, y_mm) → Front plane meters:
        // tooth on +X side; local +Y (toward center) → −X; local X → Y
        int n = Gt2Mm.GetLength(0);
        double[,] pts = new double[n, 2];
        for (int i = 0; i < n; i++)
        {
            double ox = Gt2Mm[i, 0] * 0.001;
            double oy = Gt2Mm[i, 1] * 0.001;
            pts[i, 0] = toothDistM - oy; // radial
            pts[i, 1] = ox;              // tangential
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            model.SketchManager.CreateLine(pts[i, 0], pts[i, 1], 0, pts[j, 0], pts[j, 1], 0);
        }
    }

    static void Main()
    {
        File.AppendAllText(LogPath, "\r\n=== real GT2 " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        try { sw.CloseAllDocuments(true); } catch { }
        System.Threading.Thread.Sleep(600);

        double pdMm = Teeth * PitchMm / Math.PI;
        double odMm = pdMm - 2.0 * PldMm;
        double halfTw = (ToothWidthMm + AddToothWidthMm) / 2.0;
        double toothDistMm = Math.Sqrt(Math.Pow(odMm / 2.0, 2) - Math.Pow(halfTw, 2));
        double tipR = odMm / 2.0 * 0.001;
        double toothDist = toothDistMm * 0.001;

        Log(string.Format("GT2 {0}T PD={1:F3} OD={2:F3} toothDist={3:F3} L=6 NO flange REAL profile",
            Teeth, pdMm, odMm, toothDistMm));

        ModelDoc2 m = (ModelDoc2)sw.NewDocument(PartTpl, 0, 0, 0);
        string front = FrontPlane(m);

        // Blank + bore, no flange
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, tipR);
        m.SketchManager.CreateCircleByRadius(0, 0, 0, ShaftDia / 2.0);
        Extrude(m, PulleyLen);
        Log("Blank OK");

        // One real GT2 tooth cut
        m.Extension.SelectByID2(front, "PLANE", 0, 0, 0, false, 0, null, 0);
        m.SketchManager.InsertSketch(true);
        SketchGt2ToothCut(m, toothDist);
        Feature gap = CutBlind(m, PulleyLen + 0.0002);
        Log("Tooth cut OK: " + gap.Name);

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
            if (File.Exists(final)) File.Delete(final);
            File.Move(tmp, final);
        }
        catch
        {
            Log("File locked — open Pulley_GT2_12T_new.SLDPRT");
        }

        // Preview
        int e = 0, w = 0;
        string openPath = File.Exists(final) ? final : tmp;
        ModelDoc2 p = (ModelDoc2)sw.OpenDoc6(openPath, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (p != null)
        {
            p.ShowNamedView2("*Isometric", -1);
            p.ViewZoomtofit2();
            p.SaveBMP(Path.Combine(OutDir, "_preview_pulley.bmp"), 1400, 1000);
            PartDoc part = (PartDoc)p;
            object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);
            if (bodies != null)
                foreach (object ob in bodies)
                {
                    double[] box = (double[])((Body2)ob).GetBodyBox();
                    Log(string.Format("BOX X[{0:F2}..{1:F2}] Y[{2:F2}..{3:F2}] Z[{4:F2}..{5:F2}]",
                        box[0]*1000, box[3]*1000, box[1]*1000, box[4]*1000, box[2]*1000, box[5]*1000));
                }
            sw.CloseDoc(p.GetTitle());
        }
        Log("DONE");
    }
}
